using Microsoft.EntityFrameworkCore;
using CLI.LMS;

namespace CLI.LMS.MAUI.Data;

public class LMSDbContext : DbContext
{
    public DbSet<Student> Students { get; set; }
    public DbSet<Course> Courses { get; set; }
    public DbSet<Assignment> Assignments { get; set; }
    public DbSet<Submission> Submissions { get; set; }
    public DbSet<Module> Modules { get; set; }
    public DbSet<Section> Sections { get; set; }
    public DbSet<Page> Pages { get; set; }
    public DbSet<ModuleFile> ModuleFiles { get; set; }
    public DbSet<GradeRange> GradeRanges { get; set; }
    public DbSet<Semester> Semesters { get; set; }
    public DbSet<AssignmentComment> AssignmentComments { get; set; }
    public DbSet<EmailMessage> Emails { get; set; }

    public string DbPath { get; }

    public LMSDbContext()
    {
        var folder = FileSystem.AppDataDirectory;

        DbPath = Path.Combine(folder, "lms.db");
    }


    protected override void OnConfiguring(
        DbContextOptionsBuilder options)
    {
        options.UseSqlite($"Filename={DbPath}");
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Page>();
        modelBuilder.Entity<ModuleFile>();
        // roster is many-to-many, so a student can be in more than one course
        modelBuilder.Entity<Course>()
            .HasMany(c => c.Roster)
            .WithMany(s => s.Courses);

        base.OnModelCreating(modelBuilder);
    }
}