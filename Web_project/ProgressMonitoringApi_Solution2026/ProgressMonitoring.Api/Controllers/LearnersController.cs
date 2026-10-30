using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ProgressMonitoring.Api.Data;
using ProgressMonitoring.Api.Models;

namespace ProgressMonitoring.Api.Controllers;

public record CreateLearnerRequest(string FirstName, string LastName, string Grade, string? ParentEmail = null, string? ParentEmail2 = null);

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LearnersController : ControllerBase
{
    private readonly AppDbContext _db;

    public LearnersController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/learners
    // A teacher gets only the learners they added; an admin gets everyone.
    [Authorize(Roles = "teacher,admin")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Learner>>> GetLearners()
    {
        var scope = await _db.GetScopeAsync(User);
        var learners = await scope.VisibleLearners(_db).ToListAsync();
        return Ok(learners);
    }

    // GET /api/learners/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<Learner>> GetLearner(Guid id)
    {
        if (User.IsInRole("parent"))
        {
            if (!await IsLinkedParentAsync(id)) return NotFound();
            var child = await _db.Learners.FindAsync(id);
            return child is null ? NotFound() : Ok(child);
        }

        if (!User.IsInRole("teacher") && !User.IsInRole("admin")) return Forbid();

        // Another teacher's learner is reported as not found.
        var scope = await _db.GetScopeAsync(User);
        var learner = await scope.VisibleLearners(_db).FirstOrDefaultAsync(l => l.Id == id);
        if (learner is null) return NotFound();
        return Ok(learner);
    }

    // POST /api/learners
    // The new learner belongs to the teacher who creates them.
    [Authorize(Roles = "teacher,admin")]
    [HttpPost]
    public async Task<ActionResult<Learner>> CreateLearner(CreateLearnerRequest req)
    {
        var scope = await _db.GetScopeAsync(User);
        if (!scope.IsAdmin && scope.TeacherId is null)
            return StatusCode(403, "This account has no teacher profile.");

        var first = (req.FirstName ?? string.Empty).Trim();
        var last = (req.LastName ?? string.Empty).Trim();
        var grade = (req.Grade ?? string.Empty).Trim();

        if (first.Length == 0 || last.Length == 0) return BadRequest("First and last name are required.");
        if (!SchoolRules.Grades.Contains(grade)) return BadRequest("Unknown grade.");
        if (!scope.TeachesGrade(grade)) return StatusCode(403, "You do not teach that grade.");

        var emailCheck = new System.ComponentModel.DataAnnotations.EmailAddressAttribute();
        foreach (var email in new[] { req.ParentEmail, req.ParentEmail2 })
        {
            if (!string.IsNullOrWhiteSpace(email) && !emailCheck.IsValid(email.Trim()))
                return BadRequest($"'{email}' is not a valid email address.");
        }

        var ownerId = scope.TeacherId;
        var duplicate = await _db.Learners.AnyAsync(l =>
            l.TeacherId == ownerId &&
            l.FirstName.ToLower() == first.ToLower() &&
            l.LastName.ToLower() == last.ToLower() &&
            l.Grade == grade);
        if (duplicate) return Conflict($"{first} {last} (Grade {grade}) already exists.");

        var learner = new Learner
        {
            Id = Guid.NewGuid(),
            FirstName = first,
            LastName = last,
            Grade = grade,
            TeacherId = ownerId
        };
        _db.Learners.Add(learner);
        await _db.AddParentEmailsAsync(learner.Id, new[] { req.ParentEmail, req.ParentEmail2 });
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetLearner), new { id = learner.Id }, learner);
    }

    // GET /api/learners/{id}/marks
    // Parent: only learners linked to them (FR7 in the SRS).
    // Teacher: only learners they added, and only marks from their own assessments.
    // Admin: everything.
    [HttpGet("{id}/marks")]
    public async Task<ActionResult<IEnumerable<Mark>>> GetMarksForLearner(Guid id)
    {
        var query = _db.Marks.Where(m => m.LearnerId == id);

        if (User.IsInRole("parent"))
        {
            if (!await IsLinkedParentAsync(id)) return Forbid();
        }
        else if (User.IsInRole("teacher") || User.IsInRole("admin"))
        {
            var scope = await _db.GetScopeAsync(User);
            if (!await scope.VisibleLearners(_db).AnyAsync(l => l.Id == id)) return NotFound();

            if (!scope.IsAdmin)
            {
                var teacherId = scope.TeacherId ?? Guid.Empty;
                query = query.Where(m => m.TeacherId == teacherId);
            }
        }
        else
        {
            return Forbid();
        }

        var marks = await query.OrderByDescending(m => m.DateRecorded).ToListAsync();
        return Ok(marks);
    }

    private async Task<bool> IsLinkedParentAsync(Guid learnerId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId)) return false;

        var parent = await _db.Parents.FirstOrDefaultAsync(p => p.UserId == userId);
        if (parent is null) return false;

        return await _db.ParentLearnerLinks
            .AnyAsync(l => l.ParentId == parent.Id && l.LearnerId == learnerId);
    }
}
