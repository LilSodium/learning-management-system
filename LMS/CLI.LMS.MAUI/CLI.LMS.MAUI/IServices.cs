// Interfaces the pages talk to. Pages don't touch SampleDataStore or
// LMSDbContext directly, so the implementation can be swapped out.

using CLI.LMS;

namespace CLI.LMS.Core.Services;

public interface ICourseService
{
    Task<IReadOnlyList<Course>> GetCoursesForStudentAsync(string studentId);
    Task<Course?> GetCourseAsync(string courseId);
    Task<IReadOnlyList<Section>> GetSectionsForCourseAsync(string courseId);
    Task<IReadOnlyList<Module>> GetModulesForSectionAsync(int sectionId);

    // admin / teacher management
    Task<IReadOnlyList<Course>> GetAllCoursesAsync();
    Task<string> CreateCourseAsync(Course course);
    Task UpdateCourseAsync(Course course);
    Task DeleteCourseAsync(string courseId);

    // roster management
    Task<IReadOnlyList<Student>> GetRosterAsync(string courseId);
    Task AddStudentToCourseAsync(string courseId, string studentId);
    Task RemoveStudentFromCourseAsync(string courseId, string studentId);
    // CSV columns are Id,Name,Code. Import returns how many were added
    // and skips students already on the roster.
    Task<string> ExportRosterCsvAsync(string courseId);
    Task<int> ImportRosterCsvAsync(string courseId, string csvContent);

    // Assignments as a JSON array. Import returns how many were added.
    Task<string> ExportAssignmentsAsync(string courseId);
    Task<int> ImportAssignmentsAsync(string courseId, string json);

    // One row per student, one column per assignment, plus total % and letter.
    Task<string> ExportGradebookCsvAsync(string courseId);

    Task<IReadOnlyList<GradeRange>> GetGradeRangesAsync(string courseId);
    Task SaveGradeRangesAsync(string courseId, IReadOnlyList<GradeRange> ranges);
}

public interface IAssignmentService
{
    Task<IReadOnlyList<Assignment>> GetAssignmentsForCourseAsync(string courseId);
    Task<Assignment?> GetAssignmentAsync(int assignmentId);
}

public interface ISubmissionService
{
    Task<Submission?> GetSubmissionAsync(int assignmentId, string studentId);
    Task<IReadOnlyList<Submission>> GetSubmissionsForAssignmentAsync(int assignmentId);
    Task SaveSubmissionAsync(Submission submission);
}

// Backs the student picker page.
public interface IStudentService
{
    Task<IReadOnlyList<Student>> GetStudentsAsync();
    Task<Student?> GetStudentAsync(string studentId);
    // admin / teacher management
    Task CreateStudentAsync(Student student);
    Task UpdateStudentAsync(Student student);
    Task DeleteStudentAsync(string studentId);
}

// Works out a student's grade in a course from their graded submissions.
public interface IGradeService
{
    // Letter grade like "A-", or null if nothing is graded yet.
    Task<string?> GetLetterGradeAsync(string courseId, string studentId);
}

public interface IEmailService
{
    // Emails everyone on the roster. Returns how many were sent.
    Task<int> NotifyNewAssignmentAsync(string courseId, string assignmentName);
    Task<IReadOnlyList<EmailMessage>> GetOutboxAsync();
}

// Comment threads, keyed by assignment + student.
public interface ICommentService
{
    Task<IReadOnlyList<AssignmentComment>> GetCommentsAsync(int assignmentId, string studentId);
    Task AddCommentAsync(AssignmentComment comment);
}

public interface ISemesterService
{
    Task<IReadOnlyList<Semester>> GetSemestersAsync();
    Task<Semester?> GetSemesterAsync(int id);
    Task<int> CreateSemesterAsync(Semester semester);
    Task UpdateSemesterAsync(Semester semester);
    Task DeleteSemesterAsync(int id);
}
