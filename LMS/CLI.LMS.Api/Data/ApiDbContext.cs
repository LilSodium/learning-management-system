using CLI.LMS;
using Microsoft.EntityFrameworkCore;

namespace CLI.LMS.Api.Data;

// Same entities as the MAUI LMSDbContext but its own SQLite file, so the
// API can run without the desktop app.
public class ApiDbContext : DbContext
{
    public DbSet<Student> Students { get; set; } = null!;
    public DbSet<Course> Courses { get; set; } = null!;
    public DbSet<Assignment> Assignments { get; set; } = null!;
    public DbSet<Submission> Submissions { get; set; } = null!;
    public DbSet<Module> Modules { get; set; } = null!;
    public DbSet<Section> Sections { get; set; } = null!;
    public DbSet<Page> Pages { get; set; } = null!;
    public DbSet<ModuleFile> ModuleFiles { get; set; } = null!;
    public DbSet<GradeRange> GradeRanges { get; set; } = null!;
    public DbSet<Semester> Semesters { get; set; } = null!;

    public ApiDbContext(DbContextOptions<ApiDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Page / ModuleFile inherit ModuleContent, same as the MAUI context.
        modelBuilder.Entity<Page>();
        modelBuilder.Entity<ModuleFile>();

        base.OnModelCreating(modelBuilder);
    }
}
