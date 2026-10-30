using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProgressMonitoring.Api.Data;
using ProgressMonitoring.Api.Models;

namespace ProgressMonitoring.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubjectsController : ControllerBase
{
    private readonly AppDbContext _db;

    public SubjectsController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/subjects — feeds the subject dropdown on the teacher screen.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Subject>>> GetSubjects()
    {
        var subjects = await _db.Subjects.OrderBy(s => s.Name).ToListAsync();
        return Ok(subjects);
    }

    // POST /api/subjects — admin sets up the subject list once.
    [Authorize(Roles = "admin")]
    [HttpPost]
    public async Task<ActionResult<Subject>> CreateSubject(Subject subject)
    {
        subject.Id = Guid.NewGuid();
        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync();
        return Ok(subject);
    }
}
