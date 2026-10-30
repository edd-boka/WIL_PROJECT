using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProgressMonitoring.Api.Data;

namespace ProgressMonitoring.Api.Pages.Reports;

// Statistics and reports for the logged-in teacher. Only the teacher's own assessments and
// learners are counted (admins see everything), so one teacher never sees another's results.
[Authorize(Roles = "teacher,admin")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public string? Grade { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? SubjectId { get; set; }

    public record SubjectOption(Guid Id, string Name);

    public class ResultRow
    {
        public Guid AssessmentId { get; set; }
        public string AssessmentTitle { get; set; } = string.Empty;
        public string AssessmentType { get; set; } = string.Empty;
        public DateOnly AssessmentDate { get; set; }
        public Guid SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public string Grade { get; set; } = string.Empty;
        public Guid LearnerId { get; set; }
        public string LearnerName { get; set; } = string.Empty;
        public double Score { get; set; }
        public double Total { get; set; }
        public DateTime DateRecorded { get; set; }

        public double Percent => SchoolRules.Percent(Score, Total);
        public bool Passed => SchoolRules.IsPass(Score, Total);
    }

    public class AssessmentRow
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Grade { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateOnly Date { get; set; }
        public int LearnerCount { get; set; }
        public int Captured { get; set; }
        public double? Average { get; set; }
        public double? PassRate { get; set; }
    }

    public record LearnerStat(string Name, string Grade, double Average, int Tests);

    public List<string> GradeOptions { get; set; } = new();
    public List<SubjectOption> SubjectOptions { get; set; } = new();

    public int LearnerCount { get; set; }
    public int AssessmentCount { get; set; }
    public int MarksCaptured { get; set; }
    public double? AveragePercent { get; set; }
    public double? PassRate { get; set; }

    public List<AssessmentRow> Assessments { get; set; } = new();
    public List<LearnerStat> NeedSupport { get; set; } = new();
    public List<LearnerStat> TopPerformers { get; set; } = new();

    // Serialised to JSON for the charts.
    public object Charts { get; set; } = new { };

    public async Task OnGetAsync()
    {
        var scope = await _db.GetScopeAsync(User);
        PrepareFilters(scope);

        var rows = await QueryRowsAsync(scope);
        var assessments = await QueryAssessmentsAsync(scope);

        var grade = Grade;
        LearnerCount = await scope.VisibleLearners(_db)
            .Where(l => grade == null || l.Grade == grade)
            .CountAsync();
        AssessmentCount = assessments.Count;
        MarksCaptured = rows.Count;
        AveragePercent = rows.Count == 0 ? (double?)null : Math.Round(rows.Average(r => r.Percent), 1);
        PassRate = rows.Count == 0 ? (double?)null : Math.Round(100.0 * rows.Count(r => r.Passed) / rows.Count, 1);

        // Per-assessment table: how many marks are in, the average and the pass rate.
        var byAssessment = rows.GroupBy(r => r.AssessmentId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var a in assessments)
        {
            if (!byAssessment.TryGetValue(a.Id, out var list)) continue;
            a.Captured = list.Count;
            a.Average = Math.Round(list.Average(r => r.Percent), 1);
            a.PassRate = Math.Round(100.0 * list.Count(r => r.Passed) / list.Count, 1);
        }
        Assessments = assessments
            .OrderByDescending(a => a.Date)
            .ThenBy(a => a.Title)
            .ToList();

        // Learners: lowest averages need support, highest are top performers.
        var learners = rows
            .GroupBy(r => r.LearnerId)
            .Select(g => new LearnerStat(
                g.First().LearnerName,
                g.First().Grade,
                Math.Round(g.Average(r => r.Percent), 1),
                g.Count()))
            .ToList();
        NeedSupport = learners.Where(l => l.Average < SchoolRules.PassPercent)
            .OrderBy(l => l.Average).Take(8).ToList();
        TopPerformers = learners.OrderByDescending(l => l.Average).Take(5).ToList();

        Charts = BuildCharts(rows);
    }

    // Downloads every result in the current filter as a spreadsheet-friendly CSV.
    public async Task<IActionResult> OnGetExportAsync()
    {
        var scope = await _db.GetScopeAsync(User);
        PrepareFilters(scope);
        var rows = await QueryRowsAsync(scope);

        var sb = new StringBuilder();
        sb.AppendLine("Learner,Grade,Subject,Assessment,Type,Assessment date,Score,Total,Percent,Result,Date captured");
        foreach (var r in rows
                     .OrderBy(r => SchoolRules.GradeOrder(r.Grade)).ThenBy(r => r.SubjectName)
                     .ThenBy(r => r.LearnerName).ThenBy(r => r.AssessmentDate))
        {
            sb.AppendLine(string.Join(",",
                Csv(r.LearnerName), Csv(r.Grade), Csv(r.SubjectName), Csv(r.AssessmentTitle), Csv(r.AssessmentType),
                r.AssessmentDate.ToString("yyyy-MM-dd"), r.Score.ToString("0.##"), r.Total.ToString("0.##"),
                Math.Round(r.Percent, 1).ToString("0.#"), r.Passed ? "Pass" : "Fail",
                r.DateRecorded.ToString("yyyy-MM-dd")));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"EduTracker-report-{DateTime.Today:yyyyMMdd}.csv");
    }

    // Grade and subject choices come from what this teacher teaches; anything else is ignored.
    private void PrepareFilters(TeacherScope scope)
    {
        GradeOptions = scope.VisibleGrades;
        if (!string.IsNullOrEmpty(Grade) && !GradeOptions.Contains(Grade)) Grade = null;
        if (string.IsNullOrEmpty(Grade)) Grade = null;

        var gradeChosen = Grade is not null;
        var subjects = _db.Subjects.OrderBy(s => s.Name).ToList();
        var allowedIds = scope.IsAdmin
            ? subjects.Select(s => s.Id).ToHashSet()
            : scope.Assignments.Where(x => !gradeChosen || x.Grade == Grade).Select(x => x.SubjectId).ToHashSet();

        SubjectOptions = subjects.Where(s => allowedIds.Contains(s.Id))
            .Select(s => new SubjectOption(s.Id, s.Name)).ToList();
        if (SubjectId is not null && !SubjectOptions.Any(s => s.Id == SubjectId)) SubjectId = null;
    }

    private async Task<List<ResultRow>> QueryRowsAsync(TeacherScope scope)
    {
        var isAdmin = scope.IsAdmin;
        var teacherId = scope.TeacherId ?? Guid.Empty;
        var grade = Grade;
        var subjectId = SubjectId;

        return await (
            from m in _db.Marks
            join a in _db.Assessments on m.AssessmentId equals a.Id
            join s in _db.Subjects on a.SubjectId equals s.Id
            join l in _db.Learners on m.LearnerId equals l.Id
            where (isAdmin || (a.TeacherId == teacherId && l.TeacherId == teacherId))
                  && (grade == null || a.Grade == grade)
                  && (subjectId == null || a.SubjectId == subjectId)
            select new ResultRow
            {
                AssessmentId = a.Id,
                AssessmentTitle = a.Title,
                AssessmentType = a.AssessmentType,
                AssessmentDate = a.DateOfAssessment,
                SubjectId = s.Id,
                SubjectName = s.Name,
                Grade = a.Grade,
                LearnerId = l.Id,
                LearnerName = l.FirstName + " " + l.LastName,
                Score = m.Score,
                Total = a.Total,
                DateRecorded = m.DateRecorded
            }).ToListAsync();
    }

    private async Task<List<AssessmentRow>> QueryAssessmentsAsync(TeacherScope scope)
    {
        var isAdmin = scope.IsAdmin;
        var teacherId = scope.TeacherId ?? Guid.Empty;
        var grade = Grade;
        var subjectId = SubjectId;

        return await (
            from a in _db.Assessments
            join s in _db.Subjects on a.SubjectId equals s.Id
            where (isAdmin || a.TeacherId == teacherId)
                  && (grade == null || a.Grade == grade)
                  && (subjectId == null || a.SubjectId == subjectId)
            select new AssessmentRow
            {
                Id = a.Id,
                Title = a.Title,
                Subject = s.Name,
                Grade = a.Grade,
                Type = a.AssessmentType,
                Date = a.DateOfAssessment,
                LearnerCount = _db.Learners.Count(l => l.Grade == a.Grade && l.TeacherId == a.TeacherId)
            }).ToListAsync();
    }

    private static object BuildCharts(List<ResultRow> rows)
    {
        // 1 + 2: by subject - average % and pass/fail counts
        var bySubject = rows.GroupBy(r => r.SubjectName).OrderBy(g => g.Key).ToList();

        // 3: progress over time - average % per assessment, oldest to newest (latest 12)
        var trend = rows
            .GroupBy(r => new { r.AssessmentId, r.AssessmentTitle, r.AssessmentDate })
            .OrderBy(g => g.Key.AssessmentDate).ThenBy(g => g.Key.AssessmentTitle)
            .TakeLast(12)
            .ToList();

        // 4: how marks are spread across percentage bands
        var bandLabels = new[] { "0-29", "30-39", "40-49", "50-59", "60-69", "70-79", "80-100" };
        var bandCounts = new int[7];
        foreach (var r in rows)
        {
            var p = r.Percent;
            var i = p < 30 ? 0 : p < 40 ? 1 : p < 50 ? 2 : p < 60 ? 3 : p < 70 ? 4 : p < 80 ? 5 : 6;
            bandCounts[i]++;
        }

        return new
        {
            passMark = SchoolRules.PassPercent,
            subjects = new
            {
                labels = bySubject.Select(g => g.Key).ToArray(),
                averages = bySubject.Select(g => Math.Round(g.Average(r => r.Percent), 1)).ToArray(),
                passes = bySubject.Select(g => g.Count(r => r.Passed)).ToArray(),
                fails = bySubject.Select(g => g.Count(r => !r.Passed)).ToArray()
            },
            trend = new
            {
                labels = trend.Select(g => $"{g.Key.AssessmentTitle} ({g.Key.AssessmentDate:dd MMM})").ToArray(),
                averages = trend.Select(g => Math.Round(g.Average(r => r.Percent), 1)).ToArray()
            },
            bands = new { labels = bandLabels, counts = bandCounts }
        };
    }

    // Quote a CSV field, and stop spreadsheet programs treating text as a formula.
    private static string Csv(string? value)
    {
        var v = value ?? string.Empty;
        if (v.Length > 0 && "=+-@".Contains(v[0])) v = "'" + v;
        return "\"" + v.Replace("\"", "\"\"") + "\"";
    }
}
