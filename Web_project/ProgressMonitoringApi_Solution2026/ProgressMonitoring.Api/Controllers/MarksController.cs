using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProgressMonitoring.Api.Data;
using ProgressMonitoring.Api.Models;

namespace ProgressMonitoring.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "teacher,admin")]
public class MarksController : ControllerBase
{
    private readonly AppDbContext _db;

    public MarksController(AppDbContext db)
    {
        _db = db;
    }

    // POST /api/marks
    // Handles both capture (new mark) and update (existing mark). If the
    // request includes a known Id, it updates that row; otherwise creates
    // a new one. A teacher can only record marks for learners they added,
    // and can only change marks they captured themselves.
    [HttpPost]
    public async Task<ActionResult<Mark>> SaveMark(Mark mark)
    {
        var scope = await _db.GetScopeAsync(User);
        if (!scope.IsAdmin && scope.TeacherId is null)
            return StatusCode(403, "This account has no teacher profile.");

        if (mark.Id == Guid.Empty)
        {
            var learner = await scope.VisibleLearners(_db).FirstOrDefaultAsync(l => l.Id == mark.LearnerId);
            if (learner is null) return NotFound("Learner not found.");
            if (!scope.Teaches(mark.SubjectId, learner.Grade))
                return StatusCode(403, "You do not teach that subject in that grade.");

            if (mark.AssessmentId is Guid assessmentId)
            {
                var assessment = await _db.Assessments.FindAsync(assessmentId);
                if (assessment is null || !scope.CanAccessAssessment(assessment))
                    return NotFound("Assessment not found.");
            }

            mark.Id = Guid.NewGuid();
            mark.DateRecorded = DateTime.UtcNow;
            if (!scope.IsAdmin) mark.TeacherId = scope.TeacherId!.Value; // never trust a client-supplied teacher
            _db.Marks.Add(mark);

            await _db.SaveChangesAsync();
            return Ok(mark);
        }

        var existing = await _db.Marks.FindAsync(mark.Id);
        if (existing is null) return NotFound();

        var owner = await scope.VisibleLearners(_db).FirstOrDefaultAsync(l => l.Id == existing.LearnerId);
        if (owner is null) return NotFound();
        if (!scope.IsAdmin && existing.TeacherId != scope.TeacherId) return NotFound();
        if (!scope.Teaches(mark.SubjectId, owner.Grade))
            return StatusCode(403, "You do not teach that subject in that grade.");

        existing.SubjectId = mark.SubjectId;
        existing.AssessmentType = mark.AssessmentType;
        existing.Score = mark.Score;
        existing.Total = mark.Total;

        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    // DELETE /api/marks/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMark(Guid id)
    {
        var scope = await _db.GetScopeAsync(User);
        var mark = await _db.Marks.FindAsync(id);
        if (mark is null) return NotFound();

        var visible = await scope.VisibleLearners(_db).AnyAsync(l => l.Id == mark.LearnerId);
        if (!visible) return NotFound();
        if (!scope.IsAdmin && mark.TeacherId != scope.TeacherId) return NotFound();

        _db.Marks.Remove(mark);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
