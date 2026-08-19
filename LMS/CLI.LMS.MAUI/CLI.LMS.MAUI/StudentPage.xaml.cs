using CLI.LMS;
using CLI.LMS.Core.Services;

namespace CLI.LMS.MAUI;

public partial class StudentPage : ContentPage
{
    private readonly IStudentService _studentService;

    public StudentPage(IStudentService studentService)
    {
        InitializeComponent();
        _studentService = studentService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var students = await _studentService.GetStudentsAsync();
        StudentsList.ItemsSource = students;
    }

    private async void OnStudentSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not Student selected)
            return;

        ((CollectionView)sender).SelectedItem = null; // allow re-selecting the same student later

        await Shell.Current.GoToAsync($"{nameof(StudentCoursesPage)}?studentId={selected.Id}");
    }
}