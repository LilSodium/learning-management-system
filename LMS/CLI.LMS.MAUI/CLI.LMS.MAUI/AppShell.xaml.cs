namespace CLI.LMS.MAUI;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(TeacherCoursesPage), typeof(TeacherCoursesPage));
        Routing.RegisterRoute(nameof(TeacherStudentsPage), typeof(TeacherStudentsPage));
        Routing.RegisterRoute(nameof(CourseEditPage), typeof(CourseEditPage));
        Routing.RegisterRoute(nameof(StudentPage), typeof(StudentPage));
        Routing.RegisterRoute(nameof(StudentCoursesPage), typeof(StudentCoursesPage));
        Routing.RegisterRoute(nameof(CourseDetailPage), typeof(CourseDetailPage));
        Routing.RegisterRoute(nameof(AssignmentDetailPage), typeof(AssignmentDetailPage));
        Routing.RegisterRoute(nameof(StudentEditPage), typeof(StudentEditPage));
        Routing.RegisterRoute(nameof(SubmissionsPage), typeof(SubmissionsPage));
        Routing.RegisterRoute(nameof(SemesterPage), typeof(SemesterPage));
        Routing.RegisterRoute(nameof(OutboxPage), typeof(OutboxPage));
    }
}