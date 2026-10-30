using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProgressMonitoring.Api.Data;
using ProgressMonitoring.Api.Models;

namespace ProgressMonitoring.Api.Controllers;

[ApiController]
[Route("api/parent-learner-links")]
public class ParentLearnerLinksController : ControllerBase
{
    private readonly AppDbContext _db;

    public ParentLearnerLinksController(AppDbContext db)
    {
        _db = db;
    }

    public record CreateLinkRequest(Guid ParentId, Guid LearnerId);

    // POST /api/parent-learner-links
    // This is the endpoint that actually grants a parent access to a
    // learner's marks — restricted to admins, since it's the core of the
    // access-control model. In a real rollout this would likely be
    // confirmed against school records rather than freely creatable.
    [Authorize(Roles = "admin")]
    [HttpPost]
    public async Task<ActionResult<ParentLearnerLink>> CreateLink(CreateLinkRequest request)
    {
        var alreadyLinked = await _db.ParentLearnerLinks.AnyAsync(
            l => l.ParentId == request.ParentId && l.LearnerId == request.LearnerId);
        if (alreadyLinked)
        {
            return Conflict(new { message = "This parent is already linked to this learner." });
        }

        var link = new ParentLearnerLink
        {
            Id = Guid.NewGuid(),
            ParentId = request.ParentId,
            LearnerId = request.LearnerId,
        };
        _db.ParentLearnerLinks.Add(link);
        await _db.SaveChangesAsync();

        return Ok(link);
    }

    // GET /api/parent-learner-links/my-children
    // Lets a logged-in parent see which learners they're linked to —
    // needed by Flutter before it can even ask for a specific learner's marks.
    [Authorize(Roles = "parent")]
    [HttpGet("my-children")]
    public async Task<ActionResult<IEnumerable<Learner>>> GetMyChildren()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Forbid();
        }

        var parent = await _db.Parents.FirstOrDefaultAsync(p => p.UserId == userId);
        if (parent is null) return Ok(Array.Empty<Learner>());

        // Pick up any child a teacher added after the parent last signed in.
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (email is not null) await _db.LinkChildrenByEmailAsync(parent, email);

        var learnerIds = await _db.ParentLearnerLinks
            .Where(l => l.ParentId == parent.Id)
            .Select(l => l.LearnerId)
            .ToListAsync();

        var learners = await _db.Learners
            .Where(l => learnerIds.Contains(l.Id))
            .ToListAsync();

        return Ok(learners);
    }
}
