namespace CLI.LMS;

public static class SampleDataStore
{

    public static List<Student> Students { get; } = new List<Student>();
    public static List<Course> Courses { get; }
    public static List<AssignmentComment> Comments { get; } = new List<AssignmentComment>();
    public static List<EmailMessage> Emails { get; } = new List<EmailMessage>();
    public static List<Semester> Semesters { get; } = new List<Semester>
    {
        new Semester { Id = 1, Name = "Spring 2026", StartDate = new DateTime(2026, 1, 12), EndDate = new DateTime(2026, 5, 8) },
        new Semester { Id = 2, Name = "Fall 2026", StartDate = new DateTime(2026, 8, 24), EndDate = new DateTime(2026, 12, 18) },
    };

    static SampleDataStore()
    {
        var s1 = new Student { Code = "BB", Id = "BB", Name = "Brittany", Classification = Classification.Freshman, Email = "brittany@students.cli-lms.edu" };
        var s2 = new Student { Code = "CC", Id = "CC", Name = "Carlos", Classification = Classification.Sophomore, Email = "carlos@students.cli-lms.edu" };
        var s3 = new Student { Code = "DD", Id = "DD", Name = "Dana", Classification = Classification.Junior, Email = "dana@students.cli-lms.edu" };

        Students.AddRange(new[] { s1, s2, s3 });

        Assignment testAssignment = new Assignment
        {
            Name = "Homework 1",
            DueDate = "6/21/2026",
            AvailablePoints = 100
        };

        Submission testSubmission = new Submission
        {
            Id = "Test!!",
            StudentId = "123",
            Content = "1+1=2",
            SubmissionDate = "02/22/12"
        };
        testAssignment.Submissions.Add(testSubmission);

        Section testSection = new Section
        {
            SectionNumber = 1,
            Modules = new List<Module>(),
            Assignments = new List<Assignment> { testAssignment }
        };

        Course testCourse = new Course
        {
            Name = "Math",
            Id = "22",
            Code = "MTH",
            Description = "Learn math",
            Term = "Spring"
        };
        testCourse.Sections.Add(testSection);
        testCourse.Roster.Add(s1);
        testCourse.Roster.Add(s2);

        Course testCourse2 = new Course
        {
            Name = "English",
            Id = "23", // used to be "22", duplicate id
            Code = "222w2",
            Description = "Learn math",
            Term = "Fall"
        };

        Courses = new List<Course> { testCourse, testCourse2 };
    }
}