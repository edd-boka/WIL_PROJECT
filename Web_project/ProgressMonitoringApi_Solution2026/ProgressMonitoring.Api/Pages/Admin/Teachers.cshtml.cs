using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProgressMonitoring.Api.Data;
using ProgressMonitoring.Api.Models;

namespace ProgressMonitoring.Api.Pages.Admin;

[Authorize(Policy = "AdminUi")]
public class TeachersModel : PageModel
{
    private const int MaxImportRows = 200;
    private const long MaxImportBytes = 5 * 1024 * 1024;
    private readonly AppDbContext _db;
    private readonly ILogger<TeachersModel> _logger;

    public TeachersModel(AppDbContext db, ILogger<TeachersModel> logger)
    {
        _db = db;
        _logger = logger;
    }

    [BindProperty]
    public string FirstName { get; set; } = string.Empty;

    [BindProperty]
    public string LastName { get; set; } = string.Empty;

    [BindProperty]
    public string? StaffId { get; set; }

    [BindProperty]
    public string? Email { get; set; }

    [BindProperty]
    public IFormFile? ImportFile { get; set; }

    public List<TeacherRow> Teachers { get; private set; } = new();
    public string? ErrorMessage { get; private set; }
    public int ImportedCount { get; private set; }
    public int GeneratedCount { get; private set; }

    public record TeacherRow(Guid Id, string Name, string StaffId, string Email, List<string> Classes, bool IsSignedUp);

    public async Task OnGetAsync()
    {
        await LoadTeachersAsync();
    }

    public async Task<IActionResult> OnPostAddTeacherAsync()
    {
        FirstName = FirstName.Trim();
        LastName = LastName.Trim();
        StaffId = string.IsNullOrWhiteSpace(StaffId) ? null : StaffId.Trim();
        Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();

        if (FirstName.Length == 0 || LastName.Length == 0)
        {
            ErrorMessage = "Enter the teacher's first and last name.";
            await LoadTeachersAsync();
            return Page();
        }

        if (FirstName.Length > 100 || LastName.Length > 100)
        {
            ErrorMessage = "Teacher names can contain at most 100 characters.";
            await LoadTeachersAsync();
            return Page();
        }

        if (StaffId is not null && !StaffIdGenerator.IsValid(StaffId))
        {
            ErrorMessage = "Staff IDs must be exactly five digits.";
            await LoadTeachersAsync();
            return Page();
        }

        if (Email is { Length: > 200 })
        {
            ErrorMessage = "Email addresses can contain at most 200 characters.";
            await LoadTeachersAsync();
            return Page();
        }

        if (Email is not null &&
            (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(Email) ||
             await _db.Users.AnyAsync(user => user.Email.ToLower() == Email.ToLower()) ||
             await _db.Teachers.AnyAsync(teacher => teacher.Email != null && teacher.Email.ToLower() == Email.ToLower())))
        {
            ErrorMessage = "Enter an unused, valid email address, or leave the email blank.";
            await LoadTeachersAsync();
            return Page();
        }

        if (StaffId is not null && await _db.Teachers.AnyAsync(teacher => teacher.StaffId.ToLower() == StaffId.ToLower()))
        {
            ErrorMessage = "That staff ID is already in use.";
            await LoadTeachersAsync();
            return Page();
        }

        var teacher = new Teacher
        {
            Id = Guid.NewGuid(),
            FirstName = FirstName,
            LastName = LastName,
            StaffId = StaffId ?? await StaffIdGenerator.GenerateAsync(_db),
            Email = Email
        };
        _db.Teachers.Add(teacher);
        await _db.SaveChangesAsync();

        TempData["TeacherMessage"] = $"Added {teacher.FirstName} {teacher.LastName}. Staff ID: {teacher.StaffId}.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostGenerateMissingStaffIdsAsync()
    {
        var teachersWithoutIds = await _db.Teachers
            .Where(teacher => teacher.StaffId == null || teacher.StaffId == string.Empty)
            .ToListAsync();

        foreach (var teacher in teachersWithoutIds)
        {
            teacher.StaffId = await StaffIdGenerator.GenerateAsync(_db);
        }

        await _db.SaveChangesAsync();
        TempData["TeacherMessage"] = teachersWithoutIds.Count == 0
            ? "All teachers already have staff IDs."
            : $"Generated staff IDs for {teachersWithoutIds.Count} teacher(s).";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetDownloadTemplateAsync()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Teachers");
        sheet.Cell(1, 1).Value = "First Name";
        sheet.Cell(1, 2).Value = "Last Name";
        sheet.Cell(1, 3).Value = "Staff ID";
        sheet.Cell(1, 4).Value = "Email";
        sheet.Range(1, 1, 1, 4).Style.Font.Bold = true;
        sheet.Columns().AdjustToContents();

        await using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "EduTracker-teachers-template.xlsx");
    }

    public async Task<IActionResult> OnPostImportAsync()
    {
        if (ImportFile is null || ImportFile.Length == 0)
        {
            ErrorMessage = "Choose an Excel workbook to import.";
            await LoadTeachersAsync();
            return Page();
        }

        if (!Path.GetExtension(ImportFile.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
            ImportFile.Length > MaxImportBytes)
        {
            ErrorMessage = "Choose an .xlsx workbook no larger than 5 MB.";
            await LoadTeachersAsync();
            return Page();
        }

        List<ImportedTeacher> rows;
        try
        {
            await using var stream = ImportFile.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var sheet = workbook.Worksheets.FirstOrDefault();
            if (sheet is null)
            {
                ErrorMessage = "The workbook does not contain a worksheet.";
                await LoadTeachersAsync();
                return Page();
            }

            var headers = sheet.Row(1).CellsUsed()
                .ToDictionary(
                    cell => cell.GetString().Trim(),
                    cell => cell.Address.ColumnNumber,
                    StringComparer.OrdinalIgnoreCase);
            if (!headers.TryGetValue("First Name", out var firstNameColumn) ||
                !headers.TryGetValue("Last Name", out var lastNameColumn))
            {
                ErrorMessage = "The first row must contain “First Name” and “Last Name” column headers.";
                await LoadTeachersAsync();
                return Page();
            }

            headers.TryGetValue("Staff ID", out var staffIdColumn);
            headers.TryGetValue("Email", out var emailColumn);

            var usedRows = sheet.RowsUsed().Skip(1).ToList();
            if (usedRows.Count > MaxImportRows)
            {
                ErrorMessage = $"A workbook can contain at most {MaxImportRows} teacher rows.";
                await LoadTeachersAsync();
                return Page();
            }

            rows = usedRows
                .Select(row => new ImportedTeacher(
                    row.Cell(firstNameColumn).GetString().Trim(),
                    row.Cell(lastNameColumn).GetString().Trim(),
                    staffIdColumn == 0 ? null : row.Cell(staffIdColumn).GetString().Trim(),
                    emailColumn == 0 ? null : row.Cell(emailColumn).GetString().Trim()))
                .Where(row => row.FirstName.Length > 0 || row.LastName.Length > 0 ||
                              !string.IsNullOrWhiteSpace(row.StaffId) || !string.IsNullOrWhiteSpace(row.Email))
                .ToList();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to read the uploaded teacher workbook.");
            ErrorMessage = "The uploaded file could not be read as a valid .xlsx workbook.";
            await LoadTeachersAsync();
            return Page();
        }

        if (rows.Count == 0)
        {
            ErrorMessage = "The workbook contains no teacher rows.";
            await LoadTeachersAsync();
            return Page();
        }

        var existingStaffIds = (await _db.Teachers.Select(teacher => teacher.StaffId).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingEmails = (await _db.Teachers
                .Where(teacher => teacher.Email != null)
                .Select(teacher => teacher.Email!)
                .ToListAsync())
            .Concat(await _db.Users.Select(user => user.Email).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seenStaffIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        var teachersToAdd = new List<Teacher>();

        foreach (var (row, index) in rows.Select((row, index) => (row, index)))
        {
            var rowNumber = index + 2;
            var staffId = string.IsNullOrWhiteSpace(row.StaffId)
                ? await StaffIdGenerator.GenerateAsync(_db, reservedIds: seenStaffIds)
                : row.StaffId.Trim();
            var email = string.IsNullOrWhiteSpace(row.Email) ? null : row.Email.Trim();

            if (row.FirstName.Length == 0 || row.LastName.Length == 0)
            {
                errors.Add($"Row {rowNumber}: first and last name are required.");
                continue;
            }
            if (row.FirstName.Length > 100 || row.LastName.Length > 100)
            {
                errors.Add($"Row {rowNumber}: names can contain at most 100 characters.");
                continue;
            }
            if (!StaffIdGenerator.IsValid(staffId) || !seenStaffIds.Add(staffId) || existingStaffIds.Contains(staffId))
            {
                errors.Add($"Row {rowNumber}: staff ID “{staffId}” must be five digits and not already in use.");
                continue;
            }
            if (email is not null &&
                (email.Length > 200 ||
                 !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email) ||
                 !seenEmails.Add(email) || existingEmails.Contains(email)))
            {
                errors.Add($"Row {rowNumber}: email “{email}” is invalid or already in use.");
                continue;
            }

            teachersToAdd.Add(new Teacher
            {
                Id = Guid.NewGuid(),
                FirstName = row.FirstName,
                LastName = row.LastName,
                StaffId = staffId,
                Email = email
            });
        }

        if (errors.Count > 0)
        {
            ErrorMessage = "Nothing was imported. " + string.Join(" ", errors.Take(5)) +
                           (errors.Count > 5 ? $" And {errors.Count - 5} more row error(s)." : string.Empty);
            await LoadTeachersAsync();
            return Page();
        }

        _db.Teachers.AddRange(teachersToAdd);
        await _db.SaveChangesAsync();
        TempData["TeacherMessage"] = $"Imported {teachersToAdd.Count} teacher(s).";
        return RedirectToPage();
    }

    private async Task LoadTeachersAsync()
    {
        var teachers = await _db.Teachers
            .OrderBy(teacher => teacher.LastName)
            .ThenBy(teacher => teacher.FirstName)
            .Select(teacher => new
            {
                teacher.Id,
                teacher.FirstName,
                teacher.LastName,
                teacher.StaffId,
                teacher.UserId,
                teacher.Email
            })
            .ToListAsync();
        var linkedUserIds = teachers
            .Where(teacher => teacher.UserId.HasValue)
            .Select(teacher => teacher.UserId!.Value)
            .ToHashSet();
        var emailsByUser = await _db.Users
            .Where(user => linkedUserIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Email);
        var classesByTeacher = await _db.TeacherSubjects
            .Select(assignment => new { assignment.TeacherId, assignment.Grade })
            .Distinct()
            .ToListAsync();
        var classLists = classesByTeacher
            .GroupBy(assignment => assignment.TeacherId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(assignment => $"Grade {assignment.Grade}").OrderBy(name => name).ToList());

        Teachers = teachers.Select(teacher => new TeacherRow(
                teacher.Id,
                $"{teacher.FirstName} {teacher.LastName}",
                teacher.StaffId,
                teacher.UserId is Guid userId
                    ? emailsByUser.GetValueOrDefault(userId, teacher.Email ?? "—")
                    : teacher.Email ?? "—",
                classLists.GetValueOrDefault(teacher.Id) ?? new List<string>(),
                teacher.UserId is Guid linkedUserId && emailsByUser.ContainsKey(linkedUserId)))
            .ToList();
    }

    private sealed record ImportedTeacher(string FirstName, string LastName, string? StaffId, string? Email);
}
