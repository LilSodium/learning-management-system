using CLI.LMS;
using CLI.LMS.Core.Services;

namespace CLI.LMS.MAUI;

public partial class TeacherStudentsPage : ContentPage
{
    private readonly IStudentService _studentService;

    public TeacherStudentsPage(IStudentService studentService)
    {
        InitializeComponent();
        _studentService = studentService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        StudentsList.ItemsSource = await _studentService.GetStudentsAsync();
    }

    private async void OnAddStudentClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(StudentEditPage));
    }

    private async void OnEditStudentClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is string studentId)
            await Shell.Current.GoToAsync($"{nameof(StudentEditPage)}?studentId={studentId}");
    }

    private async void OnDeleteStudentClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is string studentId)
        {
            var ok = await DisplayAlert("Delete", "Delete this student and remove all their enrollments and submissions?", "Yes", "No");
            if (!ok) return;

            await _studentService.DeleteStudentAsync(studentId);
            StudentsList.ItemsSource = await _studentService.GetStudentsAsync();
        }
    }
}
