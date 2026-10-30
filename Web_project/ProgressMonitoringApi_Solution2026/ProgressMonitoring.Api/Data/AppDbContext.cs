using Microsoft.EntityFrameworkCore;
using ProgressMonitoring.Api.Models;

namespace ProgressMonitoring.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Learner> Learners => Set<Learner>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<Parent> Parents => Set<Parent>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Mark> Marks => Set<Mark>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ParentLearnerLink> ParentLearnerLinks => Set<ParentLearnerLink>();
    public DbSet<LearnerParentEmail> LearnerParentEmails => Set<LearnerParentEmail>();

    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<TeacherSubject> TeacherSubjects => Set<TeacherSubject>();
    public DbSet<LearnerSubject> LearnerSubjects => Set<LearnerSubject>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // snake_case table names — the common Postgres/Supabase convention.
        modelBuilder.Entity<Learner>().ToTable("learners");
        modelBuilder.Entity<Teacher>().ToTable("teachers");
        modelBuilder.Entity<Parent>().ToTable("parents");
        modelBuilder.Entity<Subject>().ToTable("subjects");
        modelBuilder.Entity<Mark>().ToTable("marks");
        modelBuilder.Entity<User>().ToTable("users");
        modelBuilder.Entity<ParentLearnerLink>().ToTable("parent_learner_links");
        modelBuilder.Entity<LearnerParentEmail>().ToTable("learner_parent_emails");
        // Id is now the primary key (by convention), so no HasKey() call needed.
        // Uniqueness is enforced here instead, allowing the same teacher+subject
        // to appear more than once as long as the Grade differs.
        modelBuilder.Entity<TeacherSubject>()
            .HasIndex(ts => new { ts.TeacherId, ts.SubjectId, ts.Grade })
            .IsUnique();
        modelBuilder.Entity<LearnerSubject>().HasKey(x => new { x.LearnerId, x.SubjectId });

        // Emails must be unique — enforced at the database level, not just in code.
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

        // A given parent-learner pairing should only be linked once.
        modelBuilder.Entity<ParentLearnerLink>()
            .HasIndex(l => new { l.ParentId, l.LearnerId })
            .IsUnique();

        // Parent emails entered when a learner is added. Looked up by email when a parent signs in;
        // the same email can appear on many learners (a parent with several children).
        modelBuilder.Entity<LearnerParentEmail>().HasIndex(e => e.Email);
        modelBuilder.Entity<LearnerParentEmail>()
            .HasIndex(e => new { e.LearnerId, e.Email })
            .IsUnique();
    }
}
