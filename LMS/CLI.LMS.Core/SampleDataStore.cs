namespace CLI.LMS;

// Seed data for a fresh database and for the in-memory services. Shared by
// the MAUI client, the API and the console app. People here are made up.
public static class SampleDataStore
{
    public static List<Student> Students { get; } = new List<Student>();
    public static List<Course> Courses { get; } = new List<Course>();
    public static List<AssignmentComment> Comments { get; } = new List<AssignmentComment>();
    public static List<EmailMessage> Emails { get; } = new List<EmailMessage>();
    public static List<Semester> Semesters { get; } = new List<Semester>
    {
        new Semester { Id = 1, Name = "Spring 2026", StartDate = new DateTime(2026, 1, 12), EndDate = new DateTime(2026, 5, 8) },
        new Semester { Id = 2, Name = "Fall 2026", StartDate = new DateTime(2026, 8, 24), EndDate = new DateTime(2026, 12, 18) },
    };

    static SampleDataStore()
    {
        var maya = NewStudent("S1001", "Maya Chen", Classification.Freshman);
        var jordan = NewStudent("S1002", "Jordan Brooks", Classification.Sophomore);
        var priya = NewStudent("S1003", "Priya Patel", Classification.Sophomore);
        var marcus = NewStudent("S1004", "Marcus Rivera", Classification.Junior);
        var elena = NewStudent("S1005", "Elena Novak", Classification.Junior);
        var tyler = NewStudent("S1006", "Tyler Grant", Classification.Senior);
        var aisha = NewStudent("S1007", "Aisha Johnson", Classification.Senior);
        var sam = NewStudent("S1008", "Sam Okafor", Classification.Freshman);
        Students.AddRange(new[] { maya, jordan, priya, marcus, elena, tyler, aisha, sam });

        // Object-Oriented Programming: graded and ungraded work plus a quiz.
        var oopHw1 = new Assignment
        {
            Name = "Homework 1: Classes and Objects",
            Description = "Write a BankAccount class with deposit, withdraw and a running balance. Reject withdrawals that would overdraw the account.",
            DueDate = "9/11/2026",
            AvailablePoints = 100
        };
        oopHw1.Submissions.Add(NewSubmission("oop-hw1-s1001", maya, "Submitted BankAccount.cs with unit tests for overdraw.", "9/10/2026", 94, "Clean design. Tests cover the edge cases."));
        oopHw1.Submissions.Add(NewSubmission("oop-hw1-s1002", jordan, "BankAccount class attached.", "9/11/2026", 81, "Withdraw allows a negative balance; see the spec."));
        oopHw1.Submissions.Add(NewSubmission("oop-hw1-s1008", sam, "Here is my BankAccount implementation.", "9/11/2026", 88, ""));

        var oopHw2 = new Assignment
        {
            Name = "Homework 2: Inheritance",
            Description = "Extend BankAccount into SavingsAccount and CheckingAccount. Override the monthly fee calculation in each.",
            DueDate = "10/2/2026",
            AvailablePoints = 100
        };
        oopHw2.Submissions.Add(NewSubmission("oop-hw2-s1001", maya, "SavingsAccount and CheckingAccount attached.", "10/1/2026"));

        var oopQuiz = new Assignment
        {
            Name = "Quiz 1: Encapsulation",
            DueDate = "9/18/2026",
            AvailablePoints = 10,
            IsQuiz = true,
            Question = "Which access modifier limits a member to its own class?",
            Choices = "public\nprivate\nprotected\ninternal",
            CorrectChoiceIndex = 1
        };

        var oop = new Course
        {
            Id = "COP3330",
            Code = "COP 3330",
            Name = "Object-Oriented Programming",
            Description = "Classes, inheritance, polymorphism and interfaces, with weekly programming assignments.",
            Term = "Fall 2026",
            Announcement = "Homework 2 is posted. Office hours move to Thursday 2-4 pm this week."
        };
        oop.Sections.Add(new Section
        {
            SectionNumber = 1,
            Modules = new List<Module> { NewModule("cop3330-week1", "Week 1: Course overview and syllabus", "Lecture1-Classes.pdf") },
            Assignments = new List<Assignment> { oopHw1, oopQuiz, oopHw2 }
        });
        oop.Roster.AddRange(new[] { maya, jordan, sam, priya });

        // Data Structures.
        var dsProject = new Assignment
        {
            Name = "Project 1: Linked List",
            Description = "Implement a doubly linked list template with insert, remove and reverse iterators.",
            DueDate = "9/25/2026",
            AvailablePoints = 150
        };
        dsProject.Submissions.Add(NewSubmission("cop4530-p1-s1004", marcus, "LinkedList.h and driver attached.", "9/24/2026", 142, "Reverse iterator works. Watch memory on remove."));
        dsProject.Submissions.Add(NewSubmission("cop4530-p1-s1005", elena, "Submission attached.", "9/25/2026", 135, ""));

        var dsHw = new Assignment
        {
            Name = "Homework 3: Big-O Analysis",
            Description = "Give the worst-case running time of each function in the handout and justify each answer.",
            DueDate = "10/16/2026",
            AvailablePoints = 50
        };

        var ds = new Course
        {
            Id = "COP4530",
            Code = "COP 4530",
            Name = "Data Structures, Algorithms, and Generic Programming",
            Description = "Lists, trees, hash tables and graphs, with analysis of the algorithms that use them.",
            Term = "Fall 2026"
        };
        ds.Sections.Add(new Section
        {
            SectionNumber = 1,
            Modules = new List<Module> { NewModule("cop4530-week1", "Week 1: Review of templates and pointers", "Project1-Spec.pdf") },
            Assignments = new List<Assignment> { dsProject, dsHw }
        });
        ds.Roster.AddRange(new[] { marcus, elena, priya, jordan });

        // Software Engineering.
        var seReq = new Assignment
        {
            Name = "Requirements Document",
            Description = "As a team, write the requirements document for your semester project: user stories, acceptance criteria and a use case diagram.",
            DueDate = "9/30/2026",
            AvailablePoints = 100
        };
        seReq.Submissions.Add(NewSubmission("cen4020-req-s1006", tyler, "Team 3 requirements document attached.", "9/29/2026", 90, "Good user stories. Acceptance criteria need to be testable."));
        seReq.Submissions.Add(NewSubmission("cen4020-req-s1007", aisha, "Team 1 requirements document attached.", "9/30/2026", 96, ""));

        var seSprint = new Assignment
        {
            Name = "Sprint 1 Demo",
            Description = "Demo your first working feature to the class and turn in the sprint retrospective.",
            DueDate = "10/23/2026",
            AvailablePoints = 100
        };

        var se = new Course
        {
            Id = "CEN4020",
            Code = "CEN 4020",
            Name = "Software Engineering",
            Description = "Team-based development of a semester project: requirements, design, testing and sprints.",
            Term = "Fall 2026",
            Announcement = "Sprint 1 demos are in class on October 23. Each team gets 10 minutes."
        };
        se.Sections.Add(new Section
        {
            SectionNumber = 1,
            Modules = new List<Module> { NewModule("cen4020-week1", "Week 1: Team formation and project pitches", "Project-Rubric.pdf") },
            Assignments = new List<Assignment> { seReq, seSprint }
        });
        se.Roster.AddRange(new[] { tyler, aisha, marcus, elena });

        // Last term's course, so the semester filter has something to show.
        var discrete = new Course
        {
            Id = "COT3100",
            Code = "COT 3100",
            Name = "Introduction to Discrete Structures",
            Description = "Logic, sets, proofs, counting and graphs.",
            Term = "Spring 2026"
        };
        discrete.Roster.AddRange(new[] { tyler, aisha });

        Courses.AddRange(new[] { oop, ds, se, discrete });
    }

    private static Student NewStudent(string code, string name, Classification classification) => new Student
    {
        Id = code,
        Code = code,
        Name = name,
        Classification = classification,
        Email = $"{name.Split(' ')[0].ToLowerInvariant()}.{name.Split(' ')[1].ToLowerInvariant()}@students.cli-lms.edu"
    };

    // A Grade of 0 means not graded yet.
    private static Submission NewSubmission(string id, Student student, string content, string date, int grade = 0, string comment = "") => new Submission
    {
        Id = id,
        StudentId = student.Id,
        Content = content,
        SubmissionDate = date,
        Grade = grade,
        Comment = comment
    };

    private static Module NewModule(string id, string pageText, string fileName) => new Module
    {
        Id = id,
        Content = new List<ModuleContent>
        {
            new Page { Text = pageText },
            new ModuleFile { Name = fileName }
        }
    };
}
