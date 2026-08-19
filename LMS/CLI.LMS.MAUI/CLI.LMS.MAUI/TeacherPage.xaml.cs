using CLI.LMS.Core.Services;

namespace CLI.LMS.MAUI;

public partial class TeacherCoursesPage : ContentPage
{
    private readonly ICourseService _courseService;

    public TeacherCoursesPage(ICourseService courseService)
    {
        InitializeComponent();
        _courseService = courseService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        CoursesList.ItemsSource = await _courseService.GetAllCoursesAsync();
    }

    private async void OnAddCourseClicked(object sender, EventArgs e)
    {
        // navigate to CourseEditPage without an id
        await Shell.Current.GoToAsync(nameof(CourseEditPage));
    }

    private async void OnEditCourseClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is string courseId)
            await Shell.Current.GoToAsync($"{nameof(CourseEditPage)}?courseId={courseId}");
    }

    private async void OnManageStudentsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(TeacherStudentsPage));
    }

    private async void OnManageSemestersClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(SemesterPage));
    }

    private async void OnEmailLogClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(OutboxPage));
    }

    private async void OnGradeCourseClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is string courseId)
            await Shell.Current.GoToAsync($"{nameof(SubmissionsPage)}?courseId={courseId}");
    }

    private async void OnDeleteCourseClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is string courseId)
        {
            var ok = await DisplayAlert("Delete", "Delete this course?", "Yes", "No");
            if (!ok) return;

            // async void, so an unhandled error here takes the whole app down
            try
            {
                await _courseService.DeleteCourseAsync(courseId);
                CoursesList.ItemsSource = await _courseService.GetAllCoursesAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Delete failed", ex.Message, "OK");
            }
        }
    }
}