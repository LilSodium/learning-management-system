using CLI.LMS.Core.Services;
using CLI.LMS.MAUI.Data;
using CLI.LMS.MAUI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CLI.LMS.MAUI
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "lms.db");

            builder.Services.AddDbContextFactory<LMSDbContext>(options =>
                options.UseSqlite($"Filename={dbPath}"));

            // EF services are the live ones. Swap for the SampleData versions
            // if the db is giving trouble.
            builder.Services.AddSingleton<ICourseService, EfCoreCourseService>();
            builder.Services.AddSingleton<IAssignmentService, EfCoreAssignmentService>();
            builder.Services.AddSingleton<ISubmissionService, EfCoreSubmissionService>();
            builder.Services.AddSingleton<IStudentService, EfCoreStudentService>();
            builder.Services.AddSingleton<IGradeService, EfCoreGradeService>();
            builder.Services.AddSingleton<ISemesterService, EfCoreSemesterService>();
            builder.Services.AddSingleton<ICommentService, EfCoreCommentService>();
            builder.Services.AddSingleton<IEmailService, EfCoreEmailService>();

            builder.Services.AddTransient<StudentCoursesPage>();
            builder.Services.AddTransient<CourseDetailPage>();
            builder.Services.AddTransient<AssignmentDetailPage>();
            builder.Services.AddTransient<StudentPage>();
            builder.Services.AddTransient<TeacherCoursesPage>();
            builder.Services.AddTransient<CourseEditPage>();
            builder.Services.AddTransient<TeacherStudentsPage>();
            builder.Services.AddTransient<StudentEditPage>();
            builder.Services.AddTransient<SubmissionsPage>();
            builder.Services.AddTransient<SemesterPage>();
            builder.Services.AddTransient<OutboxPage>();


            // Keep the db between launches. It used to get deleted on startup
            // which threw away anything you made. There are no EF migrations,
            // so if an old db is missing a newer table just rebuild it.
            using (var db = new LMSDbContext())
            {
                db.Database.EnsureCreated();

                try
                {
                    // Poke the newest tables and columns. An old db throws
                    // "no such table"/"no such column" here and gets rebuilt.
                    // Add to this list when the model changes.
                    _ = db.Semesters.Any();
                    _ = db.AssignmentComments.Any();
                    _ = db.Emails.Any();
                    _ = db.Students.Select(s => s.Email).FirstOrDefault();
                    _ = db.GradeRanges.Select(r => r.Color).FirstOrDefault();
                    _ = db.Assignments.Select(a => a.Choices).FirstOrDefault();
                    // roster is now a many-to-many; an old one-to-many db has no join table
                    _ = db.Students.Include(s => s.Courses).FirstOrDefault();
                }
                catch
                {
                    db.Database.EnsureDeleted();
                    db.Database.EnsureCreated();
                }

                if (!db.Students.Any())
                {
                    db.Students.AddRange(SampleDataStore.Students);
                }

                if (!db.Courses.Any())
                {
                    db.Courses.AddRange(SampleDataStore.Courses);
                }

                if (!db.Semesters.Any())
                {
                    // Drop the ids and let SQLite assign them.
                    db.Semesters.AddRange(SampleDataStore.Semesters
                        .Select(s => new Semester { Name = s.Name, StartDate = s.StartDate, EndDate = s.EndDate }));
                }

                db.SaveChanges();
            }

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}