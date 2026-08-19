using CLI.LMS;
using CLI.LMS.Core.Services;

namespace CLI.LMS.MAUI;

[QueryProperty(nameof(StudentId), "studentId")]
public partial class StudentCoursesPage : ContentPage, IQueryAttributable
{
    private readonly ICourseService _courseService;
    private readonly IStudentService _studentService;

    public string StudentId { get; set; }

    public StudentCoursesPage(ICourseService courseService, IStudentService studentService)
    {
        InitializeComponent();
        _courseService = courseService;
        _studentService = studentService;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query == null) return;

        if (query.TryGetValue("studentId", out var s) && s is string studentId)
            StudentId = studentId;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var student = await _studentService.GetStudentAsync(StudentId);
        WelcomeLabel.Text = student is not null ? $"{student.Name}'s courses" : "Courses";

        var courses = await _courseService.GetCoursesForStudentAsync(StudentId);
        CoursesList.ItemsSource = courses;
    }

    private async void OnCourseSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not Course selected)
            return;

        ((CollectionView)sender).SelectedItem = null;


        await Shell.Current.GoToAsync(nameof(CourseDetailPage), new Dictionary<string, object>
        {
            ["courseId"] = selected.Id,
            ["studentId"] = StudentId
        });
    }
}