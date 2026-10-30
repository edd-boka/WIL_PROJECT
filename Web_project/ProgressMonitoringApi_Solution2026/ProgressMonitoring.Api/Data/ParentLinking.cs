using Microsoft.EntityFrameworkCore;
using ProgressMonitoring.Api.Models;

namespace ProgressMonitoring.Api.Data;

// Links parents to children by email, so a parent who signs in is immediately attached
// to every child whose record lists their email address.
public static class ParentLinking
{
    public static string Normalize(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    // Called when a teacher adds a learner. Stores the parent emails on the learner and, if a
    // parent with that email already has an account, links them straight away.
    // Does NOT save - the caller saves it together with the learner.
    public static async Task AddParentEmailsAsync(this AppDbContext db, Guid learnerId, IEnumerable<string?> emails)
    {
        foreach (var email in emails.Select(Normalize).Where(e => e.Length > 0).Distinct())
        {
            db.LearnerParentEmails.Add(new LearnerParentEmail
            {
                Id = Guid.NewGuid(),
                LearnerId = learnerId,
                Email = email
            });

            var parentId = await (
                from p in db.Parents
                join u in db.Users on p.UserId equals u.Id
                where u.Email.ToLower() == email && p.EmailVerified   // only verified parents are linked
                select (Guid?)p.Id).FirstOrDefaultAsync();

            if (parentId is Guid pid)
            {
                db.ParentLearnerLinks.Add(new ParentLearnerLink
                {
                    Id = Guid.NewGuid(),
                    ParentId = pid,
                    LearnerId = learnerId
                });
            }
        }
    }

    // Called when a parent signs up or signs in. Links the parent to every learner that lists
    // their email and returns how many NEW links were made. Safe to call repeatedly.
    public static async Task<int> LinkChildrenByEmailAsync(this AppDbContext db, Parent parent, string email)
    {
        // Nobody is linked to a child until they have proved they own the email address.
        if (!parent.EmailVerified) return 0;

        var normalized = Normalize(email);
        if (normalized.Length == 0) return 0;

        var learnerIds = await db.LearnerParentEmails
            .Where(x => x.Email == normalized)
            .Select(x => x.LearnerId)
            .Distinct()
            .ToListAsync();
        if (learnerIds.Count == 0) return 0;

        var alreadyLinked = await db.ParentLearnerLinks
            .Where(l => l.ParentId == parent.Id && learnerIds.Contains(l.LearnerId))
            .Select(l => l.LearnerId)
            .ToListAsync();

        var missing = learnerIds.Except(alreadyLinked).ToList();
        foreach (var learnerId in missing)
        {
            db.ParentLearnerLinks.Add(new ParentLearnerLink
            {
                Id = Guid.NewGuid(),
                ParentId = parent.Id,
                LearnerId = learnerId
            });
        }

        if (missing.Count > 0) await db.SaveChangesAsync();
        return missing.Count;
    }
}
