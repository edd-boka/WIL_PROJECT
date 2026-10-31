using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Cryptography;

namespace ProgressMonitoring.Api.Data;

public static class StaffIdGenerator
{
    private const int FirstStaffId = 10000;
    private const int StaffIdCount = 90000;

    public static bool IsValid(string? staffId) =>
        staffId is { Length: 5 } && staffId.All(character => character is >= '0' and <= '9');

    public static async Task<string> GenerateAsync(
        AppDbContext db,
        CancellationToken cancellationToken = default,
        IEnumerable<string>? reservedIds = null)
    {
        var existingStaffIds = (await db.Teachers
                .AsNoTracking()
                .Select(teacher => teacher.StaffId)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        if (reservedIds is not null)
        {
            existingStaffIds.UnionWith(reservedIds);
        }

        if (existingStaffIds.Count >= StaffIdCount)
        {
            throw new InvalidOperationException("All five-digit staff IDs are in use.");
        }

        var start = RandomNumberGenerator.GetInt32(StaffIdCount);
        for (var offset = 0; offset < StaffIdCount; offset++)
        {
            var value = FirstStaffId + (start + offset) % StaffIdCount;
            var staffId = value.ToString("D5", CultureInfo.InvariantCulture);
            if (!existingStaffIds.Contains(staffId))
            {
                return staffId;
            }
        }

        throw new InvalidOperationException("All five-digit staff IDs are in use.");
    }
}
