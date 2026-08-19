namespace CLI.LMS;

public static class SampleDataStore
{
    public static List<Student> Students { get; }
    public static List<Course> Courses { get; }

    static SampleDataStore()
    {
        Student testStudent = new Student { Code = "AA", Id = "AA", Name = "Anthony" };
        Students = new List<Student> { testStudent };

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
        testCourse.Roster.Add(testStudent);

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