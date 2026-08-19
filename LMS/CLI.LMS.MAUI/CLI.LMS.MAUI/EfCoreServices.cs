using CLI.LMS.Core;
using CLI.LMS.Core.Services;
using CLI.LMS.MAUI.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text;

namespace CLI.LMS.MAUI.Services;

public class EfCoreCourseService : ICourseService
{
    private readonly IDbContextFactory<LMSDbContext> _contextFactory;

    public EfCoreCourseService(IDbContextFactory<LMSDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<Course>> GetCoursesForStudentAsync(string studentId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        // Courses whose roster has this student. Ids are strings here.
        return await db.Courses
            .Where(c => c.Roster.Any(s => s.Id == studentId.ToString()))
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Course?> GetCourseAsync(string courseId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        return await db.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == courseId);
    }

    public async Task<IReadOnlyList<Section>> GetSectionsForCourseAsync(string courseId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        // Course -> Sections is modeled as a collection on Course.
        var course = await db.Courses
            .Include(c => c.Sections)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == courseId.ToString());

        if (course is null) return new List<Section>();

        return course.Sections.ToList();
    }

    public async Task<IReadOnlyList<Module>> GetModulesForSectionAsync(int sectionId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var section = await db.Sections
            .Include(s => s.Modules)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sectionId);

        if (section is null) return new List<Module>();

        return section.Modules.ToList();
    }
    public async Task<IReadOnlyList<Course>> GetAllCoursesAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Courses
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<string> CreateCourseAsync(Course course)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        // If caller didn't provide an Id, create a new Course instance with an Id.
        Course toAdd;
        if (string.IsNullOrWhiteSpace(course.Id))
        {
            toAdd = new Course
            {
                Id = Guid.NewGuid().ToString(),
                Code = course.Code,
                Name = course.Name,
                Description = course.Description,
                Announcement = course.Announcement,
                Term = course.Term,
                Roster = course.Roster ?? new List<Student>(),
                Sections = course.Sections ?? new List<Section>()
            };

            // reuse students already in the db so the insert doesn't conflict
            var newRoster = new List<Student>();
            foreach (var s in toAdd.Roster)
            {
                var existing = await db.Students.FindAsync(s.Id);
                if (existing is not null)
                    newRoster.Add(existing);
                else
                    newRoster.Add(new Student { Id = s.Id, Name = s.Name, Code = s.Code });
            }
            toAdd.Roster = newRoster;

            // rebuild these as new objects or the keys collide
            var newSections = new List<Section>();
            foreach (var sec in toAdd.Sections)
            {
                var newSec = new Section
                {
                    SectionNumber = sec.SectionNumber,
                    Modules = new List<Module>(),
                    Assignments = new List<Assignment>()
                };

                // Modules
                foreach (var m in sec.Modules ?? Enumerable.Empty<Module>())
                {
                    var moduleId = m.Id ?? Guid.NewGuid().ToString();
                    // ensure uniqueness across existing Modules
                    if (await db.Modules.AnyAsync(x => x.Id == moduleId))
                        moduleId = moduleId + "-" + Guid.NewGuid().ToString("N");

                    var newMod = new Module { Id = moduleId };
                    foreach (var c in m.Content ?? Enumerable.Empty<ModuleContent>())
                    {
                        if (c is Page p)
                            newMod.Content.Add(new Page { Text = p.Text });
                        else if (c is ModuleFile f)
                            newMod.Content.Add(new ModuleFile { Name = f.Name });
                    }

                    newSec.Modules.Add(newMod);
                }

                // Assignments
                foreach (var a in sec.Assignments ?? Enumerable.Empty<Assignment>())
                {
                    var newA = new Assignment
                    {
                        Name = a.Name,
                        Description = a.Description,
                        AvailablePoints = a.AvailablePoints,
                        DueDate = a.DueDate,
                        IsQuiz = a.IsQuiz,
                        Question = a.Question,
                        Choices = a.Choices,
                        CorrectChoiceIndex = a.CorrectChoiceIndex
                    };
                    newSec.Assignments.Add(newA);
                }

                newSections.Add(newSec);
            }
            toAdd.Sections = newSections;

            // Carry over any custom grade ranges as fresh instances.
            toAdd.GradeRanges = (course.GradeRanges ?? new List<GradeRange>())
                .Select(r => new GradeRange { Letter = r.Letter, MinPercent = r.MinPercent })
                .ToList();
        }
        else
        {
            // Use the provided instance (no post-construction Id mutation).
            toAdd = course;
        }

        db.Courses.Add(toAdd);
        await db.SaveChangesAsync();
        return toAdd.Id;
    }
    public async Task UpdateCourseAsync(Course course)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var existing = await db.Courses.FindAsync(course.Id);
        if (existing is null) return;

        // Copy scalar values
        db.Entry(existing).CurrentValues.SetValues(course);

        // Roster/Sections aren't synced here, that needs more work.
        await db.SaveChangesAsync();
    }

    public async Task DeleteCourseAsync(string courseId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        // Sections and GradeRanges hang off the course with a nullable FK, so EF
        // won't cascade them. Load the children and delete them by hand or SQLite
        // rejects the whole thing with "FOREIGN KEY constraint failed".
        var course = await db.Courses
            .Include(c => c.Roster)
            .Include(c => c.GradeRanges)
            .Include(c => c.Sections).ThenInclude(s => s.Modules).ThenInclude(m => m.Content)
            .Include(c => c.Sections).ThenInclude(s => s.Assignments)
            .Include(c => c.Sections).ThenInclude(s => s.AssignmentGroups)
            .FirstOrDefaultAsync(c => c.Id == courseId);

        if (course is null) return;

        // Submissions and comments are keyed by AssignmentId, not by a navigation.
        var assignmentIds = course.Sections.SelectMany(s => s.Assignments).Select(a => a.Id).ToList();
        if (assignmentIds.Count > 0)
        {
            db.Submissions.RemoveRange(db.Submissions.Where(s => assignmentIds.Contains(s.AssignmentId)));
            db.AssignmentComments.RemoveRange(db.AssignmentComments.Where(c => assignmentIds.Contains(c.AssignmentId)));
        }

        foreach (var section in course.Sections)
        {
            foreach (var module in section.Modules)
                db.RemoveRange(module.Content);

            db.Modules.RemoveRange(section.Modules);
            db.Assignments.RemoveRange(section.Assignments);
            db.RemoveRange(section.AssignmentGroups);
        }

        db.Sections.RemoveRange(course.Sections);
        db.GradeRanges.RemoveRange(course.GradeRanges);

        // Just unenroll them, the students themselves stay.
        course.Roster.Clear();

        db.Courses.Remove(course);
        await db.SaveChangesAsync();
    }
    public async Task<IReadOnlyList<Student>> GetRosterAsync(string courseId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var course = await db.Courses
            .Include(c => c.Roster)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == courseId);
        return course?.Roster.ToList() ?? new List<Student>();
    }

    public async Task<string> ExportRosterCsvAsync(string courseId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var roster = await db.Courses
            .Include(c => c.Roster)
            .AsNoTracking()
            .Where(c => c.Id == courseId)
            .SelectMany(c => c.Roster)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Id,Name,Code");
        foreach (var s in roster)
        {
            // escape quotes by doubling them
            var name = (s.Name ?? string.Empty).Replace("\"", "\"\"");
            var code = (s.Code ?? string.Empty).Replace("\"", "\"\"");
            sb.AppendLine($"\"{s.Id}\",\"{name}\",\"{code}\"");
        }

        return sb.ToString();
    }

    public async Task<int> ImportRosterCsvAsync(string courseId, string csvContent)
    {
        if (string.IsNullOrWhiteSpace(csvContent)) return 0;

        var lines = csvContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return 0;

        // skip the header row if there is one
        var start = 0;
        if (lines[0].Trim().StartsWith("Id", StringComparison.OrdinalIgnoreCase)) start = 1;

        await using var db = await _contextFactory.CreateDbContextAsync();
        var course = await db.Courses.Include(c => c.Roster).FirstOrDefaultAsync(c => c.Id == courseId);
        if (course is null) return 0;

        var added = 0;
        for (var i = start; i < lines.Length; i++)
        {
            var parts = ParseCsvLine(lines[i]);
            if (parts.Length == 0) continue;
            var id = parts.Length > 0 ? parts[0] : null;
            var name = parts.Length > 1 ? parts[1] : null;
            var code = parts.Length > 2 ? parts[2] : null;
            if (string.IsNullOrWhiteSpace(id)) continue;

            // reuse the student if they're already in the db, otherwise make one
            var student = await db.Students.FindAsync(id) ?? new Student { Id = id, Name = name ?? string.Empty, Code = code ?? string.Empty };
            if (student.Id == id && student.Name == (name ?? student.Name) && student.Code == (code ?? student.Code))
            {
                // brand new, so it still needs adding
                if (db.Entry(student).State == EntityState.Detached && !await db.Students.AnyAsync(s => s.Id == id))
                {
                    db.Students.Add(student);
                }
            }

            // skip anyone already on the roster
            if (!course.Roster.Any(r => r.Id == id))
            {
                // use the tracked instance if EF already has one
                var tracked = await db.Students.FindAsync(id);
                course.Roster.Add(tracked ?? student);
                added++;
            }
        }

        if (added > 0)
            await db.SaveChangesAsync();

        return added;
    }

    private static string[] ParseCsvLine(string line)
    {
        // Very small CSV parser that handles quoted values and commas inside quotes.
        var values = new List<string>();
        var cur = new StringBuilder();
        var inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    cur.Append('"');
                    i++; // skip escaped quote
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                values.Add(cur.ToString());
                cur.Clear();
            }
            else
            {
                cur.Append(c);
            }
        }
        values.Add(cur.ToString());
        return values.ToArray();
    }

    public async Task<string> ExportAssignmentsAsync(string courseId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var course = await db.Courses
            .Include(c => c.Sections)
                .ThenInclude(s => s.Assignments)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == courseId);

        var assignments = course?.Sections.SelectMany(s => s.Assignments) ?? Enumerable.Empty<Assignment>();
        var transfer = assignments.Select(a => new AssignmentTransfer
        {
            Name = a.Name,
            Description = a.Description,
            AvailablePoints = a.AvailablePoints,
            DueDate = a.DueDate,
            IsQuiz = a.IsQuiz,
            Question = a.Question,
            Choices = a.Choices,
            CorrectChoiceIndex = a.CorrectChoiceIndex
        }).ToList();

        return System.Text.Json.JsonSerializer.Serialize(transfer,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<int> ImportAssignmentsAsync(string courseId, string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return 0;

        List<AssignmentTransfer>? incoming;
        try
        {
            incoming = System.Text.Json.JsonSerializer.Deserialize<List<AssignmentTransfer>>(json);
        }
        catch (System.Text.Json.JsonException)
        {
            return 0;
        }
        if (incoming is null || incoming.Count == 0) return 0;

        await using var db = await _contextFactory.CreateDbContextAsync();
        var course = await db.Courses
            .Include(c => c.Sections)
                .ThenInclude(s => s.Assignments)
            .FirstOrDefaultAsync(c => c.Id == courseId);
        if (course is null) return 0;

        // Import into the course's first section, creating one if needed.
        var section = course.Sections.FirstOrDefault();
        if (section is null)
        {
            section = new Section { SectionNumber = 1 };
            course.Sections.Add(section);
        }

        var added = 0;
        foreach (var a in incoming)
        {
            if (string.IsNullOrWhiteSpace(a.Name)) continue;
            section.Assignments.Add(new Assignment
            {
                Name = a.Name,
                Description = a.Description,
                AvailablePoints = a.AvailablePoints,
                DueDate = a.DueDate ?? string.Empty,
                IsQuiz = a.IsQuiz,
                Question = a.Question,
                Choices = a.Choices,
                CorrectChoiceIndex = a.CorrectChoiceIndex
            });
            added++;
        }

        if (added > 0)
            await db.SaveChangesAsync();

        return added;
    }

    public async Task<string> ExportGradebookCsvAsync(string courseId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var course = await db.Courses
            .Include(c => c.Roster)
            .Include(c => c.GradeRanges)
            .Include(c => c.Sections)
                .ThenInclude(s => s.Assignments)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == courseId);

        if (course is null) return string.Empty;

        var assignments = course.Sections.SelectMany(s => s.Assignments).ToList();
        var assignmentIds = assignments.Select(a => a.Id).ToList();

        var submissions = await db.Submissions
            .Where(s => assignmentIds.Contains(s.AssignmentId))
            .AsNoTracking()
            .ToListAsync();

        var sb = new StringBuilder();

        // Header: Student Id, Student Name, one column per assignment, total %, letter.
        var header = new List<string> { "Student Id", "Student Name" };
        header.AddRange(assignments.Select(a => a.Name));
        header.Add("Total %");
        header.Add("Letter");
        sb.AppendLine(string.Join(",", header.Select(CsvEscape)));

        foreach (var student in course.Roster)
        {
            var row = new List<string> { student.Id ?? string.Empty, student.Name ?? string.Empty };

            var earned = 0;
            var possible = 0;
            foreach (var a in assignments)
            {
                var sub = submissions.FirstOrDefault(s => s.AssignmentId == a.Id && s.StudentId == student.Id);
                if (sub is not null && sub.Grade > 0)
                {
                    row.Add(sub.Grade.ToString());
                    earned += sub.Grade;
                    possible += a.AvailablePoints;
                }
                else
                {
                    row.Add(string.Empty);
                }
            }

            if (possible > 0)
            {
                var percentage = (double)earned / possible * 100;
                row.Add(percentage.ToString("0.#"));
                row.Add(GradeScale.ToLetter(percentage, course.GradeRanges));
            }
            else
            {
                row.Add(string.Empty);
                row.Add(string.Empty);
            }

            sb.AppendLine(string.Join(",", row.Select(CsvEscape)));
        }

        return sb.ToString();
    }

    private static string CsvEscape(string value)
    {
        value ??= string.Empty;
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    public async Task<IReadOnlyList<GradeRange>> GetGradeRangesAsync(string courseId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var course = await db.Courses
            .Include(c => c.GradeRanges)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == courseId);

        var ranges = course?.GradeRanges?.OrderByDescending(r => r.MinPercent).ToList();
        return ranges is { Count: > 0 } ? ranges : GradeScale.Default();
    }

    public async Task SaveGradeRangesAsync(string courseId, IReadOnlyList<GradeRange> ranges)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var course = await db.Courses
            .Include(c => c.GradeRanges)
            .FirstOrDefaultAsync(c => c.Id == courseId);
        if (course is null) return;

        db.GradeRanges.RemoveRange(course.GradeRanges);
        course.GradeRanges.Clear();

        foreach (var r in ranges ?? new List<GradeRange>())
        {
            if (string.IsNullOrWhiteSpace(r.Letter)) continue;
            course.GradeRanges.Add(new GradeRange { Letter = r.Letter, MinPercent = r.MinPercent, Color = r.Color });
        }

        await db.SaveChangesAsync();
    }

    // Add student
    public async Task AddStudentToCourseAsync(string courseId, string studentId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var course = await db.Courses
            .Include(c => c.Roster)
            .FirstOrDefaultAsync(c => c.Id == courseId);
        if (course is null) return;

        var student = await db.Students.FindAsync(studentId);
        if (student is null) return;

        if (!course.Roster.Any(s => s.Id == studentId))
        {
            course.Roster.Add(student);
            await db.SaveChangesAsync();
        }
    }

    // Remove student
    public async Task RemoveStudentFromCourseAsync(string courseId, string studentId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var course = await db.Courses
            .Include(c => c.Roster)
            .FirstOrDefaultAsync(c => c.Id == courseId);
        if (course is null) return;

        var existing = course.Roster.FirstOrDefault(s => s.Id == studentId);
        if (existing is not null)
        {
            course.Roster.Remove(existing);
            await db.SaveChangesAsync();
        }
    }
}

public class EfCoreAssignmentService : IAssignmentService
{
    private readonly IDbContextFactory<LMSDbContext> _contextFactory;

    public EfCoreAssignmentService(IDbContextFactory<LMSDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<Assignment>> GetAssignmentsForCourseAsync(string courseId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var course = await db.Courses
            .Include(c => c.Sections)
                .ThenInclude(s => s.Assignments)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == courseId.ToString());

        if (course is null) return new List<Assignment>();

        return course.Sections.SelectMany(s => s.Assignments).ToList();
    }

    public async Task<Assignment?> GetAssignmentAsync(int assignmentId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        return await db.Assignments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == assignmentId);
    }
}

public class EfCoreSubmissionService : ISubmissionService
{
    private readonly IDbContextFactory<LMSDbContext> _contextFactory;

    public EfCoreSubmissionService(IDbContextFactory<LMSDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Submission?> GetSubmissionAsync(int assignmentId, string studentId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        return await db.Submissions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.AssignmentId == assignmentId
                                    && s.StudentId == studentId);
    }

    public async Task<IReadOnlyList<Submission>> GetSubmissionsForAssignmentAsync(int assignmentId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        return await db.Submissions
            .Where(s => s.AssignmentId == assignmentId)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task SaveSubmissionAsync(Submission submission)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var existing = await db.Submissions.FirstOrDefaultAsync(s =>
            s.AssignmentId == submission.AssignmentId &&
            s.StudentId == submission.StudentId);

        if (existing is null)
        {
            db.Submissions.Add(submission);
        }
        else
        {
            // copy values onto the tracked entity so EF only updates changes
            db.Entry(existing).CurrentValues.SetValues(submission);
        }

        await db.SaveChangesAsync();
    }
}

public class EfCoreStudentService : IStudentService
{
    private readonly IDbContextFactory<LMSDbContext> _contextFactory;

    public EfCoreStudentService(IDbContextFactory<LMSDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<Student>> GetStudentsAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        return await db.Students
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Student?> GetStudentAsync(string studentId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        return await db.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId);
    }

    public async Task CreateStudentAsync(Student student)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var toAdd = new Student
        {
            Id = string.IsNullOrWhiteSpace(student.Id) ? Guid.NewGuid().ToString() : student.Id,
            Name = student.Name,
            Code = student.Code,
            Classification = student.Classification
        };

        db.Students.Add(toAdd);
        await db.SaveChangesAsync();
    }

    public async Task UpdateStudentAsync(Student student)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var existing = await db.Students.FindAsync(student.Id);
        if (existing is null) return;

        db.Entry(existing).CurrentValues.SetValues(student);
        await db.SaveChangesAsync();
    }

    public async Task DeleteStudentAsync(string studentId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        // Remove submissions by this student
        var subs = db.Submissions.Where(s => s.StudentId == studentId);
        db.Submissions.RemoveRange(subs);

        // Remove student from any course rosters
        var courses = db.Courses.Include(c => c.Roster).Where(c => c.Roster.Any(r => r.Id == studentId));
        foreach (var course in courses)
        {
            var existing = course.Roster.FirstOrDefault(r => r.Id == studentId);
            if (existing != null)
                course.Roster.Remove(existing);
        }

        // Remove the student entity
        var student = await db.Students.FindAsync(studentId);
        if (student != null)
            db.Students.Remove(student);

        await db.SaveChangesAsync();
    }
}

public class EfCoreEmailService : IEmailService
{
    private readonly IDbContextFactory<LMSDbContext> _contextFactory;

    public EfCoreEmailService(IDbContextFactory<LMSDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<int> NotifyNewAssignmentAsync(string courseId, string assignmentName)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var course = await db.Courses
            .Include(c => c.Roster)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == courseId);
        if (course is null) return 0;

        var now = DateTime.Now;
        var count = 0;
        foreach (var s in course.Roster)
        {
            db.Emails.Add(EmailComposer.NewAssignment(course, s, assignmentName, now));
            count++;
        }

        if (count > 0) await db.SaveChangesAsync();
        return count;
    }

    public async Task<IReadOnlyList<EmailMessage>> GetOutboxAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Emails
            .OrderByDescending(e => e.SentAt)
            .AsNoTracking()
            .ToListAsync();
    }
}

public class EfCoreCommentService : ICommentService
{
    private readonly IDbContextFactory<LMSDbContext> _contextFactory;

    public EfCoreCommentService(IDbContextFactory<LMSDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<AssignmentComment>> GetCommentsAsync(int assignmentId, string studentId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.AssignmentComments
            .Where(c => c.AssignmentId == assignmentId && c.StudentId == studentId)
            .OrderBy(c => c.PostedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task AddCommentAsync(AssignmentComment comment)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        db.AssignmentComments.Add(new AssignmentComment
        {
            AssignmentId = comment.AssignmentId,
            StudentId = comment.StudentId,
            Author = comment.Author,
            IsInstructor = comment.IsInstructor,
            Text = comment.Text,
            PostedAt = comment.PostedAt == default ? DateTime.Now : comment.PostedAt
        });
        await db.SaveChangesAsync();
    }
}

public class EfCoreSemesterService : ISemesterService
{
    private readonly IDbContextFactory<LMSDbContext> _contextFactory;

    public EfCoreSemesterService(IDbContextFactory<LMSDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<Semester>> GetSemestersAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Semesters
            .OrderBy(s => s.StartDate)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Semester?> GetSemesterAsync(int id)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Semesters
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<int> CreateSemesterAsync(Semester semester)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var toAdd = new Semester
        {
            Name = semester.Name,
            StartDate = semester.StartDate,
            EndDate = semester.EndDate
        };
        db.Semesters.Add(toAdd);
        await db.SaveChangesAsync();
        return toAdd.Id;
    }

    public async Task UpdateSemesterAsync(Semester semester)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var existing = await db.Semesters.FindAsync(semester.Id);
        if (existing is null) return;
        existing.Name = semester.Name;
        existing.StartDate = semester.StartDate;
        existing.EndDate = semester.EndDate;
        await db.SaveChangesAsync();
    }

    public async Task DeleteSemesterAsync(int id)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var existing = await db.Semesters.FindAsync(id);
        if (existing is null) return;
        db.Semesters.Remove(existing);
        await db.SaveChangesAsync();
    }
}

public class EfCoreGradeService : IGradeService
{
    private readonly IDbContextFactory<LMSDbContext> _contextFactory;

    public EfCoreGradeService(IDbContextFactory<LMSDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<string?> GetLetterGradeAsync(string courseId, string studentId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        // Load the course + assignments, then grade off the graded submissions.
        var course = await db.Courses
            .Include(c => c.Sections)
                .ThenInclude(s => s.Assignments)
            .Include(c => c.GradeRanges)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == courseId);

        if (course is null) return null;

        var assignmentIds = course.Sections
            .SelectMany(s => s.Assignments)
            .Select(a => a.Id)
            .ToList();

        if (assignmentIds.Count == 0) return null;

        var graded = await db.Submissions
            .Where(s => s.StudentId == studentId
                     && assignmentIds.Contains(s.AssignmentId)
                     && s.Grade > 0)
            .AsNoTracking()
            .ToListAsync();

        if (graded.Count == 0) return null;

        // Only count assignments that have been graded. Counting all of them
        // would make an early 90/100 show up as 18% in a 5 assignment course.
        var pointsByAssignment = course.Sections
            .SelectMany(s => s.Assignments)
            .GroupBy(a => a.Id)
            .ToDictionary(g => g.Key, g => g.First().AvailablePoints);

        var earned = graded.Sum(s => s.Grade);
        var possible = graded
            .Where(s => pointsByAssignment.ContainsKey(s.AssignmentId))
            .Sum(s => pointsByAssignment[s.AssignmentId]);

        if (possible == 0) return null;

        var percentage = (double)earned / possible * 100;
        return GradeScale.ToLetter(percentage, course.GradeRanges);
    }
}
