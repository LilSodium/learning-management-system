using CLI.LMS;
using CLI.LMS.Core.Services;

namespace CLI.LMS.MAUI;

[QueryProperty(nameof(StudentId), "studentId")]
public partial class StudentEditPage : ContentPage, IQueryAttributable
{
    private readonly IStudentService _studentService;
    public string StudentId { get; set; } = string.Empty;

    private Student? _editing;

    public StudentEditPage(IStudentService studentService)
    {
        InitializeComponent();
        _studentService = studentService;

        // populate classification picker
        ClassificationPicker.ItemsSource = Enum.GetNames(typeof(Classification));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (string.IsNullOrWhiteSpace(StudentId))
        {
            TitleLabel.Text = "Add Student";
            _editing = null;
            NameEntry.Text = string.Empty;
            CodeEntry.Text = string.Empty;
            ClassificationPicker.SelectedIndex = 0;
            return;
        }

        TitleLabel.Text = "Edit Student";
        _editing = await _studentService.GetStudentAsync(StudentId);
        if (_editing != null)
        {
            NameEntry.Text = _editing.Name;
            CodeEntry.Text = _editing.Code;
            ClassificationPicker.SelectedIndex = (int)_editing.Classification;
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query == null) return;
        if (query.TryGetValue("studentId", out var s) && s is string studentId)
            StudentId = studentId;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var name = NameEntry.Text?.Trim() ?? string.Empty;
        var code = CodeEntry.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
        {
            await DisplayAlert("Validation", "Name and code are required.", "OK");
            return;
        }

        var classification = ClassificationPicker.SelectedIndex >= 0
            ? (Classification)ClassificationPicker.SelectedIndex
            : Classification.Freshman;

        if (_editing is null)
        {
            var student = new Student
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Code = code,
                Classification = classification
            };

            await _studentService.CreateStudentAsync(student);
        }
        else
        {
            _editing.Name = name;
            _editing.Code = code;
            _editing.Classification = classification;
            await _studentService.UpdateStudentAsync(_editing);
        }

        await Shell.Current.GoToAsync("..");
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
