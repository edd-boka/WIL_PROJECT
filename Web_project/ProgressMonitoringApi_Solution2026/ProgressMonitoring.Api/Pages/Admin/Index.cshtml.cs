using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProgressMonitoring.Api.Data;
using ProgressMonitoring.Api.Models;

namespace ProgressMonitoring.Api.Pages.Admin;

[Authorize(Policy = "AdminUi")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly SupabaseAuthUserSync _supabaseAuthUserSync;

    public IndexModel(AppDbContext db, SupabaseAuthUserSync supabaseAuthUserSync)
    {
        _db = db;
        _supabaseAuthUserSync = supabaseAuthUserSync;
    }

    public int UserCount { get; private set; }
    public int TeacherCount { get; private set; }
    public int ParentCount { get; private set; }
    public int LearnerCount { get; private set; }
    public int SubjectCount { get; private set; }
    public int AssessmentCount { get; private set; }
    public int MarkCount { get; private set; }

    public List<UserRow> Users { get; private set; } = new();
    public List<TeacherRow> Teachers { get; private set; } = new();
    public List<ParentRow> Parents { get; private set; } = new();
    public List<LearnerRow> Learners { get; private set; } = new();
    public List<SubjectRow> Subjects { get; private set; } = new();
    public List<GuardianEmailRow> GuardianEmails { get; private set; } = new();
    public List<AssessmentRow> RecentAssessments { get; private set; } = new();
    public string? SupabaseSyncError { get; private set; }
    public int ImportedSupabaseUsers { get; private set; }

    public record UserRow(string Email, string Role);
    public record TeacherRow(Guid Id, string StaffId, string Name, string Email, int LearnerCount, int ClassCount);
    public record ParentRow(Guid Id, string Name, string Email, bool EmailVerified, int LearnerCount);
    public record LearnerRow(string Name, string Grade, string TeacherName, int ParentCount, int SubjectCount);
    public record SubjectRow(string Name, string Phase, int TeacherAssignments, int LearnerEnrollments, int Assessments);
    public record GuardianEmailRow(Guid LearnerId, string LearnerName, string? GuardianEmail, string? AlternateGuardianEmail);
    public record AssessmentRow(string Title, string SubjectName, string Grade, string TeacherName, DateOnly Date);

    public async Task OnGetAsync()
    {
        try
        {
            ImportedSupabaseUsers = await _supabaseAuthUserSync.SyncParentUsersAsync(HttpContext.RequestAborted);
        }
        catch (InvalidOperationException exception)
        {
            SupabaseSyncError = exception.Message;
        }

        Users = await _db.Users
            .OrderBy(user => user.Email)
            .Select(user => new UserRow(user.Email, user.Role))
            .ToListAsync();

        var teacherProfiles = await _db.Teachers
            .OrderBy(teacher => teacher.LastName)
            .ThenBy(teacher => teacher.FirstName)
            .Select(teacher => new
            {
                teacher.Id,
                teacher.StaffId,
                teacher.FirstName,
                teacher.LastName,
                teacher.UserId,
                teacher.Email
            })
            .ToListAsync();
        var teacherUserIds = teacherProfiles
            .Where(teacher => teacher.UserId.HasValue)
            .Select(teacher => teacher.UserId!.Value)
            .ToHashSet();
        var teacherEmails = await _db.Users
            .Where(user => teacherUserIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Email);
        Teachers = teacherProfiles
            .Select(teacher => new TeacherRow(
                teacher.Id,
                teacher.StaffId,
                $"{teacher.FirstName} {teacher.LastName}",
                teacher.UserId is Guid userId
                    ? teacherEmails.GetValueOrDefault(userId, teacher.Email ?? "—")
                    : teacher.Email ?? "—",
                0,
                0))
            .ToList();

        Parents = await (
            from parent in _db.Parents
            join user in _db.Users on parent.UserId equals user.Id
            orderby parent.LastName, parent.FirstName
            select new ParentRow(
                parent.Id,
                parent.FirstName + " " + parent.LastName,
                user.Email,
                parent.EmailVerified,
                0))
            .ToListAsync();

        var learners = await _db.Learners
            .OrderBy(learner => learner.Grade)
            .ThenBy(learner => learner.LastName)
            .ThenBy(learner => learner.FirstName)
            .Select(learner => new
            {
                learner.Id,
                learner.FirstName,
                learner.LastName,
                learner.Grade,
                learner.TeacherId
            })
            .ToListAsync();

        var subjects = await _db.Subjects
            .OrderBy(subject => subject.Name)
            .Select(subject => new { subject.Id, subject.Name, subject.Phase })
            .ToListAsync();
        var guardianEmails = await _db.LearnerParentEmails
            .OrderBy(email => email.Email)
            .Select(email => new { email.LearnerId, email.Email })
            .ToListAsync();
        var links = await _db.ParentLearnerLinks
            .Select(link => new { link.ParentId, link.LearnerId })
            .ToListAsync();
        var teacherAssignments = await _db.TeacherSubjects
            .GroupBy(assignment => assignment.TeacherId)
            .Select(group => new { TeacherId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.TeacherId, group => group.Count);
        var teacherSubjectCounts = await _db.TeacherSubjects
            .GroupBy(assignment => assignment.SubjectId)
            .Select(group => new { SubjectId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.SubjectId, group => group.Count);
        var subjectLearnerCounts = (await _db.Marks
                .Select(mark => new { mark.SubjectId, mark.LearnerId })
                .Distinct()
                .ToListAsync())
            .GroupBy(mark => mark.SubjectId)
            .ToDictionary(group => group.Key, group => group.Count());
        var assessmentCounts = await _db.Assessments
            .GroupBy(assessment => assessment.SubjectId)
            .Select(group => new { SubjectId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.SubjectId, group => group.Count);

        var teacherNames = Teachers.ToDictionary(teacher => teacher.Id, teacher => teacher.Name);
        var guardianEmailsByLearner = guardianEmails
            .GroupBy(email => email.LearnerId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(email => email.Email).Distinct(StringComparer.OrdinalIgnoreCase).Take(2).ToList());
        var parentLearnerCounts = links
            .GroupBy(link => link.ParentId)
            .ToDictionary(group => group.Key, group => group.Count());

        Teachers = Teachers
            .Select(teacher => teacher with
            {
                LearnerCount = learners.Count(learner => learner.TeacherId == teacher.Id),
                ClassCount = teacherAssignments.GetValueOrDefault(teacher.Id)
            })
            .ToList();
        Parents = Parents
            .Select(parent => parent with
            {
                LearnerCount = parentLearnerCounts.GetValueOrDefault(parent.Id)
            })
            .ToList();
        Learners = learners
            .Select(learner => new LearnerRow(
                $"{learner.FirstName} {learner.LastName}",
                learner.Grade,
                learner.TeacherId is Guid teacherId
                    ? teacherNames.GetValueOrDefault(teacherId, "Unassigned")
                    : "Unassigned",
                guardianEmailsByLearner.GetValueOrDefault(learner.Id)?.Count ?? 0,
                SchoolRules.IsFoundationGrade(learner.Grade)
                    ? 4
                    : SchoolRules.IntermediateGrades.Contains(learner.Grade) ? 9 : 0))
            .ToList();
        Subjects = subjects
            .Select(subject => new SubjectRow(
                subject.Name,
                subject.Phase,
                teacherSubjectCounts.GetValueOrDefault(subject.Id),
                subjectLearnerCounts.GetValueOrDefault(subject.Id),
                assessmentCounts.GetValueOrDefault(subject.Id)))
            .ToList();
        GuardianEmails = learners
            .Select(learner =>
            {
                var emails = guardianEmailsByLearner.GetValueOrDefault(learner.Id) ?? new List<string>();
                return new GuardianEmailRow(
                    learner.Id,
                    $"{learner.FirstName} {learner.LastName}",
                    emails.ElementAtOrDefault(0),
                    emails.ElementAtOrDefault(1));
            })
            .ToList();

        RecentAssessments = await (
            from assessment in _db.Assessments
            join subject in _db.Subjects on assessment.SubjectId equals subject.Id
            join teacher in _db.Teachers on assessment.TeacherId equals teacher.Id
            orderby assessment.DateOfAssessment descending
            select new AssessmentRow(
                assessment.Title,
                subject.Name,
                assessment.Grade,
                teacher.FirstName + " " + teacher.LastName,
                assessment.DateOfAssessment))
            .Take(10)
            .ToListAsync();

        UserCount = Users.Count;
        TeacherCount = Teachers.Count;
        ParentCount = Parents.Count;
        LearnerCount = learners.Count;
        SubjectCount = Subjects.Count;
        AssessmentCount = await _db.Assessments.CountAsync();
        MarkCount = await _db.Marks.CountAsync();
    }

    public async Task<IActionResult> OnPostUpdateGuardianEmailsAsync(
        Guid learnerId,
        string? guardianEmail,
        string? alternateGuardianEmail)
    {
        var emails = new[] { guardianEmail, alternateGuardianEmail }
            .Select(ParentLinking.Normalize)
            .Where(email => email.Length > 0)
            .ToList();

        if (emails.Any(email => email.Length > 200 ||
            !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email)))
        {
            TempData["AdminError"] = "Enter valid guardian email addresses no longer than 200 characters.";
            return RedirectToPage();
        }

        if (emails.Distinct(StringComparer.OrdinalIgnoreCase).Count() != emails.Count)
        {
            TempData["AdminError"] = "The primary and alternate guardian emails must be different.";
            return RedirectToPage();
        }

        if (!await _db.Learners.AnyAsync(learner => learner.Id == learnerId))
        {
            TempData["AdminError"] = "The learner record could not be found.";
            return RedirectToPage();
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();
        var currentEmails = await _db.LearnerParentEmails
            .Where(email => email.LearnerId == learnerId)
            .ToListAsync();
        var wantedEmails = emails.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var obsoleteEmails = currentEmails
            .Where(email => !wantedEmails.Contains(ParentLinking.Normalize(email.Email)))
            .ToList();
        _db.LearnerParentEmails.RemoveRange(obsoleteEmails);
        var currentEmailValues = currentEmails
            .Select(email => ParentLinking.Normalize(email.Email))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _db.LearnerParentEmails.AddRange(emails
            .Where(email => !currentEmailValues.Contains(email))
            .Select(email => new LearnerParentEmail
        {
            Id = Guid.NewGuid(),
            LearnerId = learnerId,
            Email = email
        }));

        var matchingParents = await (
            from parent in _db.Parents
            join user in _db.Users on parent.UserId equals user.Id
            where parent.EmailVerified && emails.Contains(user.Email.ToLower())
            select parent.Id)
            .ToListAsync();
        var existingLinks = await _db.ParentLearnerLinks
            .Where(link => link.LearnerId == learnerId)
            .ToListAsync();
        var obsoleteLinks = existingLinks.Where(link => !matchingParents.Contains(link.ParentId)).ToList();
        _db.ParentLearnerLinks.RemoveRange(obsoleteLinks);
        var existingParentIds = existingLinks
            .Where(link => matchingParents.Contains(link.ParentId))
            .Select(link => link.ParentId)
            .ToHashSet();
        _db.ParentLearnerLinks.AddRange(matchingParents
            .Where(parentId => !existingParentIds.Contains(parentId))
            .Select(parentId => new ParentLearnerLink
            {
                Id = Guid.NewGuid(),
                ParentId = parentId,
                LearnerId = learnerId
            }));

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["AdminMessage"] = "Guardian emails updated.";
        return RedirectToPage();
    }
}
