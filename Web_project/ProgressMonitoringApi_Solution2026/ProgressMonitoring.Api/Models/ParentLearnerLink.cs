namespace ProgressMonitoring.Api.Models;

// The table that makes role-based access actually work: a parent can only
// see a learner's marks if a row exists here linking them. Handles both
// a parent with multiple children, and (less commonly) a learner with
// more than one linked guardian.
//
// This is checked in LearnersController.GetMarksForLearner — see the
// comment there for how it's enforced.
public class ParentLearnerLink
{
    public Guid Id { get; set; }
    public Guid ParentId { get; set; }
    public Guid LearnerId { get; set; }
}
