using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ProgressMonitoring.Api.Models;

namespace ProgressMonitoring.Api.Data;

public sealed class SupabaseAuthUserSync
{
    private const int PageSize = 100;
    private readonly HttpClient _http;
    private readonly AppDbContext _db;
    private readonly ILogger<SupabaseAuthUserSync> _log;
    private readonly string _url;
    private readonly string _serviceRoleKey;

    public SupabaseAuthUserSync(
        HttpClient http,
        AppDbContext db,
        IConfiguration config,
        ILogger<SupabaseAuthUserSync> log)
    {
        _http = http;
        _db = db;
        _log = log;
        _url = (config["Supabase:Url"] ?? string.Empty).Trim().TrimEnd('/');
        _serviceRoleKey = (config["Supabase:ServiceRoleKey"] ?? string.Empty).Trim();
    }

    public async Task<int> SyncParentUsersAsync(CancellationToken cancellationToken = default)
    {
        if (_url.Length == 0 || _serviceRoleKey.Length == 0)
        {
            throw new InvalidOperationException(
                "Supabase Auth user sync is not configured. Set Supabase:Url and Supabase:ServiceRoleKey.");
        }

        var authUsers = await FetchUsersAsync(cancellationToken);
        var localUsers = await _db.Users.ToListAsync(cancellationToken);
        var usersByEmail = localUsers
            .Select(user => (User: user, Email: ParentLinking.Normalize(user.Email)))
            .Where(item => item.Email.Length > 0)
            .GroupBy(item => item.Email, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().User, StringComparer.OrdinalIgnoreCase);
        var parentsByUserId = (await _db.Parents.ToListAsync(cancellationToken))
            .GroupBy(parent => parent.UserId)
            .ToDictionary(group => group.Key, group => group.First());
        var verifiedParentsToLink = new List<(Parent Parent, string Email)>();
        var importedCount = 0;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        foreach (var authUser in authUsers)
        {
            var email = ParentLinking.Normalize(authUser.Email);
            if (email.Length == 0) continue;

            if (!usersByEmail.TryGetValue(email, out var user))
            {
                var userId = localUsers.Any(existing => existing.Id == authUser.Id)
                    ? Guid.NewGuid()
                    : authUser.Id;
                var temporaryPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
                user = new User
                {
                    Id = userId,
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword),
                    Role = "parent"
                };
                _db.Users.Add(user);
                localUsers.Add(user);
                usersByEmail.Add(email, user);
                importedCount++;
            }
            else if (!string.Equals(user.Role, "parent", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!parentsByUserId.TryGetValue(user.Id, out var parent))
            {
                var (firstName, lastName) = GetNames(authUser.UserMetadata, email);
                parent = new Parent
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    FirstName = firstName,
                    LastName = lastName,
                    EmailVerified = authUser.EmailConfirmedAt is not null
                };
                _db.Parents.Add(parent);
                parentsByUserId.Add(user.Id, parent);
            }
            else if (authUser.EmailConfirmedAt is not null)
            {
                parent.EmailVerified = true;
            }

            if (parent.EmailVerified)
            {
                verifiedParentsToLink.Add((parent, email));
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        foreach (var (parent, email) in verifiedParentsToLink.DistinctBy(item => item.Parent.Id))
        {
            await _db.LinkChildrenByEmailAsync(parent, email);
        }

        return importedCount;
    }

    private async Task<List<AuthUser>> FetchUsersAsync(CancellationToken cancellationToken)
    {
        var users = new List<AuthUser>();
        for (var page = 1; ; page++)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{_url}/auth/v1/admin/users?page={page}&per_page={PageSize}");
            request.Headers.Add("apikey", _serviceRoleKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceRoleKey);

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException exception)
            {
                _log.LogWarning(exception, "Could not reach Supabase Auth while syncing admin users.");
                throw new InvalidOperationException("Could not reach Supabase Auth. User accounts were not synced.", exception);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    _log.LogWarning("Supabase Auth user sync returned HTTP {StatusCode}.", (int)response.StatusCode);
                    throw new InvalidOperationException(
                        $"Supabase Auth user sync failed (HTTP {(int)response.StatusCode}). Check the Auth admin key configuration.");
                }

                AuthUsersResponse? result;
                try
                {
                    result = await response.Content.ReadFromJsonAsync<AuthUsersResponse>(cancellationToken);
                }
                catch (System.Text.Json.JsonException exception)
                {
                    _log.LogWarning(exception, "Supabase Auth returned an invalid user list response.");
                    throw new InvalidOperationException("Supabase Auth returned an invalid user list response.", exception);
                }

                if (result?.Users is null)
                {
                    throw new InvalidOperationException("Supabase Auth returned an unexpected user list response.");
                }

                users.AddRange(result.Users);
                if (result.Users.Count < PageSize) break;
            }
        }

        return users;
    }

    private static (string FirstName, string LastName) GetNames(
        System.Text.Json.JsonElement metadata,
        string email)
    {
        var first = ReadMetadata(metadata, "first_name") ?? ReadMetadata(metadata, "firstName");
        var last = ReadMetadata(metadata, "last_name") ?? ReadMetadata(metadata, "lastName");
        var fullName = ReadMetadata(metadata, "full_name") ?? ReadMetadata(metadata, "name");

        if (fullName is not null && first is null)
        {
            var parts = fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            first = parts.FirstOrDefault();
            last ??= parts.Skip(1).FirstOrDefault();
        }

        return (first ?? email.Split('@')[0], last ?? string.Empty);
    }

    private static string? ReadMetadata(System.Text.Json.JsonElement metadata, string propertyName)
    {
        if (metadata.ValueKind != System.Text.Json.JsonValueKind.Object ||
            !metadata.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != System.Text.Json.JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString()?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private sealed record AuthUsersResponse(
        [property: JsonPropertyName("users")] List<AuthUser>? Users);

    private sealed record AuthUser(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("email_confirmed_at")] string? EmailConfirmedAt,
        [property: JsonPropertyName("user_metadata")] System.Text.Json.JsonElement UserMetadata);
}
