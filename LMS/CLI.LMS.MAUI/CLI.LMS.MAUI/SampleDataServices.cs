// In-memory versions of the services, backed by SampleDataStore.
// Used for XAML preview and as a fallback. Keep these in sync with the
// EfCore ones. Everything is wrapped in Task.FromResult since the data
// is already in memory.

using CLI.LMS;
using CLI.LMS.Core.Services;
using System.Text;

namespace CLI.LMS.MAUI.Services;

public class SampleDataCourseService : ICourseService
{
    public Task<IReadOnlyList<Course>> GetCoursesForStudentAsync(string studentId)
    {
        // Enrollment lives on Course.Roster.
        var courses = SampleDataStore.Courses
            .Where(c => c.Roster.Any(s => s.Id == studentId.ToString()))
            .ToList();

        return Task.FromResult<IReadOnlyList<Course>>(courses);
    }

    public Task<Course?> GetCourseAsync(string courseId)
    {
        var course = SampleDataStore.Courses
            .FirstOrDefault(c => c.Id == courseId.ToString());

        return Task.FromResult(course);
    }

    public Task<IReadOnlyList<Section>> GetSectionsForCourseAsync(string courseId)
    {
        var course = SampleDataStore.Courses.FirstOrDefault(c => c.Id == courseId.ToString());
        var sections = course is null ? new List<Section>() : course.Sections.ToList();
        return Task.FromResult<IReadOnlyList<Section>>(sections);
    }

    public Task<IReadOnlyList<Module>> GetModulesForSectionAsync(int sectionId)
    {
        var section = SampleDataStore.Courses
            .SelectMany(c => c.Sections)
            .FirstOrDefault(s => s.Id == sectionId);

        var modules = section is null ? new List<Module>() : section.Modules.ToList();
        return Task.FromResult<IReadOnlyList<Module>>(modules);
    }

    // admin/teacher CRUD methods

    public Task<IReadOnlyList<Course>> GetAllCoursesAsync()
    {
        return Task.FromResult<IReadOnlyList<Course>>(SampleDataStore.Courses.ToList());
    }

    public Task<string> CreateCourseAsync(Course course)
    {
        var toAdd = new Course
        {
            Id = string.IsNullOrWhiteSpace(course.Id) ? Guid.NewGuid().ToString() : course.Id,
            Code = course.Code,
            Name = course.Name,
            Description = course.Description,
            Announcement = course.Announcement,
            Term = course.Term ?? "TBD",
            Roster = course.Roster != null ? new List<Student>(course.Roster) : new List<Student>(),
            Sections = course.Sections != null ? new List<Section>(course.Sections) : new List<Section>(),
            GradeRanges = course.GradeRanges != null ? new List<GradeRange>(course.GradeRanges) : new List<GradeRange>()
        };

        SampleDataStore.Courses.Add(toAdd);
        return Task.FromResult(toAdd.Id);
    }

    public Task UpdateCourseAsync(Course course)
    {
        var idx = SampleDataStore.Courses.FindIndex(c => c.Id == course.Id);
        if (idx >= 0)
        {
            // safe to swap in the incoming instance, it's a new reference
            SampleDataStore.Courses[idx] = course;
        }
        return Task.CompletedTask;
    }

    public Task DeleteCourseAsync(string courseId)
    {
        var existing = SampleDataStore.Courses.FirstOrDefault(c => c.Id == courseId);
        if (existing != null)
            SampleDataStore.Courses.Remove(existing);
        return Task.CompletedTask;
    }
    public Task<IReadOnlyList<Student>> GetRosterAsync(string courseId)
    {
        var course = SampleDataStore.Courses.FirstOrDefault(c => c.Id == courseId);
        var roster = course?.Roster?.ToList() ?? new List<Student>();
        return Task.FromResult<IReadOnlyList<Student>>(roster);
    }

    public Task<string> ExportRosterCsvAsync(string courseId)
    {
        var course = SampleDataStore.Courses.FirstOrDefault(c => c.Id == courseId);
        var roster = course?.Roster ?? new List<Student>();

        var sb = new StringBuilder();
        sb.AppendLine("Id,Name,Code");
        foreach (var s in roster)
        {
            var name = (s.Name ?? string.Empty).Replace("\"", "\"\"");
            var code = (s.Code ?? string.Empty).Replace("\"", "\"\"");
            sb.AppendLine($"\"{s.Id}\",\"{name}\",\"{code}\"");
        }

        return Task.FromResult(sb.ToString());
    }

    public Task<int> ImportRosterCsvAsync(string courseId, string csvContent)
    {
        if (string.IsNullOrWhiteSpace(csvContent)) return Task.FromResult(0);

        var lines = csvContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return Task.FromResult(0);

        var start = 0;
        if (lines[0].Trim().StartsWith("Id", StringComparison.OrdinalIgnoreCase)) start = 1;

        var course = SampleDataStore.Courses.FirstOrDefault(c => c.Id == courseId);
        if (course == null) return Task.FromResult(0);

        var added = 0;
        for (var i = start; i < lines.Length; i++)
        {
            var parts = ParseCsvLine(lines[i]);
            if (parts.Length == 0) continue;
            var id = parts.Length > 0 ? parts[0] : null;
            var name = parts.Length > 1 ? parts[1] : null;
            var code = parts.Length > 2 ? parts[2] : null;
            if (string.IsNullOrWhiteSpace(id)) continue;

            var student = SampleDataStore.Students.FirstOrDefault(s => s.Id == id);
            if (student == null)
            {
                student = new Student { Id = id, Name = name ?? string.Empty, Code = code ?? string.Empty };
                SampleDataStore.Students.Add(student);
            }

            if (!course.Roster.Any(r => r.Id == id))
            {
                course.Roster.Add(student);
                added++;
            }
        }

        return Task.FromResult(added);
    }

    private static string[] ParseCsvLine(string line)
    {
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
                    i++;
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

    public Task<string> ExportAssignmentsAsync(string courseId)
    {
        var course = SampleDataStore.Courses.FirstOrDefault(c => c.Id == courseId);
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

        return Task.FromResult(System.Text.Json.JsonSerializer.Serialize(transfer,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    public Task<int> ImportAssignmentsAsync(string courseId, string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Task.FromResult(0);

        List<AssignmentTransfer>? incoming;
        try
        {
            incoming = System.Text.Json.JsonSerializer.Deserialize<List<AssignmentTransfer>>(json);
        }
        catch (System.Text.Json.JsonException)
        {
            return Task.FromResult(0);
        }
        if (incoming is null || incoming.Count == 0) return Task.FromResult(0);

        var course = SampleDataStore.Courses.FirstOrDefault(c => c.Id == courseId);
        if (course is null) return Task.FromResult(0);

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

        return Task.FromResult(added);
    }

    public Task<string> ExportGradebookCsvAsync(string courseId)
    {
        var course = SampleDataStore.Courses.FirstOrDefault(c => c.Id == courseId);
        if (course is null) return Task.FromResult(string.Empty);

        var assignments = course.Sections.SelectMany(s => s.Assignments).ToList();

        var sb = new StringBuilder();
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
                var sub = a.Submissions.FirstOrDefault(s => s.StudentId == student.Id);
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

        return Task.FromResult(sb.ToString());
    }

    private static string CsvEscape(string value)
    {
        value ??= string.Empty;
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    public Task<IReadOnlyList<GradeRange>> GetGradeRangesAsync(string courseId)
    {
        var course = SampleDataStore.Courses.FirstOrDefault(c => c.Id == courseId);
        var ranges = course?.GradeRanges?.OrderByDescending(r => r.MinPercent).ToList();
        return Task.FromResult<IReadOnlyList<GradeRange>>(ranges is { Count: > 0 } ? ranges : GradeScale.Default());
    }

    public Task SaveGradeRangesAsync(string courseId, IReadOnlyList<GradeRange> ranges)
    {
        var course = SampleDataStore.Courses.FirstOrDefault(c => c.Id == courseId);
        if (course is null) return Task.CompletedTask;

        course.GradeRanges = (ranges ?? new List<GradeRange>())
            .Where(r => !string.IsNullOrWhiteSpace(r.Letter))
            .Select(r => new GradeRange { Letter = r.Letter, MinPercent = r.MinPercent, Color = r.Color })
            .ToList();
        return Task.CompletedTask;
    }

    public Task AddStudentToCourseAsync(string courseId, string studentId)
    {
        var course = SampleDataStore.Courses.FirstOrDefault(c => c.Id == courseId);
        var student = SampleDataStore.Students.FirstOrDefault(s => s.Id == studentId);
        if (course != null && student != null && !course.Roster.Any(s => s.Id == studentId))
        {
            course.Roster.Add(student);
        }
        return Task.CompletedTask;
    }

    public Task RemoveStudentFromCourseAsync(string courseId, string studentId)
    {
        var course = SampleDataStore.Courses.FirstOrDefault(c => c.Id == courseId);
        if (course == null) return Task.CompletedTask;
        var existing = course.Roster.FirstOrDefault(s => s.Id == studentId);
        if (existing != null) course.Roster.Remove(existing);
        return Task.CompletedTask;
    }
}

public class SampleDataAssignmentService : IAssignmentService
{
    public Task<IReadOnlyList<Assignment>> GetAssignmentsForCourseAsync(string courseId)
    {
        var course = SampleDataStore.Courses.FirstOrDefault(c => c.Id == courseId.ToString());
        var assignments = course is null ? new List<Assignment>() : course.Sections.SelectMany(s => s.Assignments).ToList();
        return Task.FromResult<IReadOnlyList<Assignment>>(assignments);
    }

    public Task<Assignment?> GetAssignmentAsync(int assignmentId)
    {
        var assignment = SampleDataStore.Courses
            .SelectMany(c => c.Sections)
            .SelectMany(s => s.Assignments)
            .FirstOrDefault(a => a.Id == assignmentId);

        return Task.FromResult(assignment);
    }
}

public class SampleDataSubmissionService : ISubmissionService
{
    public Task<Submission?> GetSubmissionAsync(int assignmentId, string studentId)
    {
        var submission = SampleDataStore.Courses
            .SelectMany(c => c.Sections)
            .SelectMany(s => s.Assignments)
            .SelectMany(a => a.Submissions)
            .FirstOrDefault(s => s.AssignmentId == assignmentId && s.StudentId == studentId.ToString());

        return Task.FromResult(submission);
    }

    public Task<IReadOnlyList<Submission>> GetSubmissionsForAssignmentAsync(int assignmentId)
    {
        var submissions = SampleDataStore.Courses
            .SelectMany(c => c.Sections)
            .SelectMany(s => s.Assignments)
            .Where(a => a.Id == assignmentId)
            .SelectMany(a => a.Submissions)
            .ToList();

        return Task.FromResult<IReadOnlyList<Submission>>(submissions);
    }

    public Task SaveSubmissionAsync(Submission submission)
    {
        // Find the assignment and replace or add the submission in its list.
        var assignment = SampleDataStore.Courses
            .SelectMany(c => c.Sections)
            .SelectMany(s => s.Assignments)
            .FirstOrDefault(a => a.Id == submission.AssignmentId);

        if (assignment is null)
            return Task.CompletedTask;

        var existing = assignment.Submissions.FirstOrDefault(s => s.StudentId == submission.StudentId);
        if (existing is not null)
            assignment.Submissions.Remove(existing);

        assignment.Submissions.Add(submission);

        return Task.CompletedTask;
    }
}

public class SampleDataStudentService : IStudentService
{
    public Task<IReadOnlyList<Student>> GetStudentsAsync()
    {
        return Task.FromResult<IReadOnlyList<Student>>(SampleDataStore.Students.ToList());
    }

    public Task<Student?> GetStudentAsync(string studentId)
    {
        var student = SampleDataStore.Students.FirstOrDefault(s => s.Id == studentId);
        return Task.FromResult(student);
    }

    public Task CreateStudentAsync(Student student)
    {
        var toAdd = new Student
        {
            Id = string.IsNullOrWhiteSpace(student.Id) ? Guid.NewGuid().ToString() : student.Id,
            Code = student.Code,
            Name = student.Name,
            Classification = student.Classification
        };

        SampleDataStore.Students.Add(toAdd);
        return Task.CompletedTask;
    }

    public Task UpdateStudentAsync(Student student)
    {
        var idx = SampleDataStore.Students.FindIndex(s => s.Id == student.Id);
        if (idx >= 0)
        {
            SampleDataStore.Students[idx] = student;
        }
        return Task.CompletedTask;
    }

    public Task DeleteStudentAsync(string studentId)
    {
        // Remove from global student list
        var existing = SampleDataStore.Students.FirstOrDefault(s => s.Id == studentId);
        if (existing != null)
            SampleDataStore.Students.Remove(existing);

        // Remove from any course rosters
        foreach (var course in SampleDataStore.Courses)
        {
            var inRoster = course.Roster.FirstOrDefault(r => r.Id == studentId);
            if (inRoster != null)
                course.Roster.Remove(inRoster);
        }

        // Remove any submissions by this student
        foreach (var assignment in SampleDataStore.Courses.SelectMany(c => c.Sections).SelectMany(s => s.Assignments))
        {
            var toRemove = assignment.Submissions.Where(sub => sub.StudentId == studentId).ToList();
            foreach (var sub in toRemove)
                assignment.Submissions.Remove(sub);
        }

        return Task.CompletedTask;
    }
}

public class SampleDataEmailService : IEmailService
{
    public Task<int> NotifyNewAssignmentAsync(string courseId, string assignmentName)
    {
        var course = SampleDataStore.Courses.FirstOrDefault(c => c.Id == courseId);
        if (course is null) return Task.FromResult(0);

        var now = DateTime.Now;
        var count = 0;
        foreach (var s in course.Roster)
        {
            var msg = EmailComposer.NewAssignment(course, s, assignmentName, now);
            msg.Id = SampleDataStore.Emails.Any() ? SampleDataStore.Emails.Max(e => e.Id) + 1 : 1;
            SampleDataStore.Emails.Add(msg);
            count++;
        }
        return Task.FromResult(count);
    }

    public Task<IReadOnlyList<EmailMessage>> GetOutboxAsync()
    {
        var outbox = SampleDataStore.Emails.OrderByDescending(e => e.SentAt).ToList();
        return Task.FromResult<IReadOnlyList<EmailMessage>>(outbox);
    }
}

public class SampleDataCommentService : ICommentService
{
    public Task<IReadOnlyList<AssignmentComment>> GetCommentsAsync(int assignmentId, string studentId)
    {
        var comments = SampleDataStore.Comments
            .Where(c => c.AssignmentId == assignmentId && c.StudentId == studentId)
            .OrderBy(c => c.PostedAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<AssignmentComment>>(comments);
    }

    public Task AddCommentAsync(AssignmentComment comment)
    {
        var nextId = SampleDataStore.Comments.Any() ? SampleDataStore.Comments.Max(c => c.Id) + 1 : 1;
        SampleDataStore.Comments.Add(new AssignmentComment
        {
            Id = nextId,
            AssignmentId = comment.AssignmentId,
            StudentId = comment.StudentId,
            Author = comment.Author,
            IsInstructor = comment.IsInstructor,
            Text = comment.Text,
            PostedAt = comment.PostedAt == default ? DateTime.Now : comment.PostedAt
        });
        return Task.CompletedTask;
    }
}

public class SampleDataSemesterService : ISemesterService
{
    public Task<IReadOnlyList<Semester>> GetSemestersAsync()
    {
        var semesters = SampleDataStore.Semesters.OrderBy(s => s.StartDate).ToList();
        return Task.FromResult<IReadOnlyList<Semester>>(semesters);
    }

    public Task<Semester?> GetSemesterAsync(int id)
    {
        var semester = SampleDataStore.Semesters.FirstOrDefault(s => s.Id == id);
        return Task.FromResult(semester);
    }

    public Task<int> CreateSemesterAsync(Semester semester)
    {
        var nextId = SampleDataStore.Semesters.Any() ? SampleDataStore.Semesters.Max(s => s.Id) + 1 : 1;
        var toAdd = new Semester
        {
            Id = nextId,
            Name = semester.Name,
            StartDate = semester.StartDate,
            EndDate = semester.EndDate
        };
        SampleDataStore.Semesters.Add(toAdd);
        return Task.FromResult(toAdd.Id);
    }

    public Task UpdateSemesterAsync(Semester semester)
    {
        var existing = SampleDataStore.Semesters.FirstOrDefault(s => s.Id == semester.Id);
        if (existing is not null)
        {
            existing.Name = semester.Name;
            existing.StartDate = semester.StartDate;
            existing.EndDate = semester.EndDate;
        }
        return Task.CompletedTask;
    }

    public Task DeleteSemesterAsync(int id)
    {
        var existing = SampleDataStore.Semesters.FirstOrDefault(s => s.Id == id);
        if (existing is not null) SampleDataStore.Semesters.Remove(existing);
        return Task.CompletedTask;
    }
}

public class SampleDataGradeService : IGradeService
{
    public Task<string?> GetLetterGradeAsync(string courseId, string studentId)
    {
        var course = SampleDataStore.Courses.FirstOrDefault(c => c.Id == courseId);
        var assignments = course?.Sections.SelectMany(s => s.Assignments).ToList();

        if (assignments is null || assignments.Count == 0)
            return Task.FromResult<string?>(null);

        var graded = assignments
            .SelectMany(a => a.Submissions.Where(s => s.StudentId == studentId && s.Grade > 0)
                .Select(s => new { Score = s.Grade, PointsPossible = a.AvailablePoints }))
            .ToList();

        if (graded.Count == 0)
            return Task.FromResult<string?>(null);

        var earned = graded.Sum(g => g.Score);
        var possible = graded.Sum(g => g.PointsPossible);

        if (possible == 0)
            return Task.FromResult<string?>(null);

        var percentage = (double)earned / possible * 100;
        return Task.FromResult<string?>(GradeScale.ToLetter(percentage, course!.GradeRanges));
    }
}
