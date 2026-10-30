using System.Net.Http.Json;

namespace ProgressMonitoring.Api.Data;

// Uses Supabase Auth purely as an "email a 6-digit code" service. Supabase emails the code
// and checks it; this app only needs to know "this person controls this email address".
// Config (user-secrets or environment): Supabase:Url and Supabase:AnonKey.
public class SupabaseEmailOtp
{
    private readonly HttpClient _http;
    private readonly ILogger<SupabaseEmailOtp> _log;
    private readonly string _url;
    private readonly string _key;

    public SupabaseEmailOtp(HttpClient http, IConfiguration config, ILogger<SupabaseEmailOtp> log)
    {
        _http = http;
        _log = log;
        _url = (config["Supabase:Url"] ?? string.Empty).Trim().TrimEnd('/');
        _key = (config["Supabase:AnonKey"] ?? string.Empty).Trim();
    }

    public bool IsConfigured => _url.Length > 0 && _key.Length > 0;

    // Asks Supabase to email a 6-digit code to the address.
    public async Task<(bool Ok, string? Error)> SendCodeAsync(string email)
    {
        if (!IsConfigured) return (false, "The email service is not configured.");

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_url}/auth/v1/otp")
        {
            Content = JsonContent.Create(new { email, create_user = true })
        };
        req.Headers.Add("apikey", _key);

        try
        {
            using var res = await _http.SendAsync(req);
            if (res.IsSuccessStatusCode) return (true, null);

            _log.LogWarning("Supabase OTP send failed ({Status}): {Body}",
                (int)res.StatusCode, await res.Content.ReadAsStringAsync());

            return (false, (int)res.StatusCode == 429
                ? "Too many requests. Please wait a minute and try again."
                : "We could not send the verification email. Please try again shortly.");
        }
        catch (HttpRequestException ex)
        {
            _log.LogWarning(ex, "Supabase OTP send failed");
            return (false, "We could not reach the email service. Please try again shortly.");
        }
    }

    // True only if Supabase confirms the code is valid and unexpired for that email.
    public async Task<bool> VerifyCodeAsync(string email, string code)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(code)) return false;

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_url}/auth/v1/verify")
        {
            Content = JsonContent.Create(new { type = "email", email, token = code.Trim() })
        };
        req.Headers.Add("apikey", _key);

        try
        {
            using var res = await _http.SendAsync(req);
            return res.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _log.LogWarning(ex, "Supabase OTP verify failed");
            return false;
        }
    }
}
