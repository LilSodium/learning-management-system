using CLI.LMS;
using CLI.LMS.Core.Services;

namespace CLI.LMS.MAUI;

public partial class SemesterPage : ContentPage
{
    private readonly ISemesterService _semesterService;

    // null = adding, otherwise the id being edited
    private int? _editingId;

    public SemesterPage(ISemesterService semesterService)
    {
        InitializeComponent();
        _semesterService = semesterService;
        ResetForm();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        SemestersList.ItemsSource = await _semesterService.GetSemestersAsync();
    }

    private void ResetForm()
    {
        _editingId = null;
        NameEntry.Text = string.Empty;
        StartPicker.Date = DateTime.Today;
        EndPicker.Date = DateTime.Today.AddMonths(4);
        FormHeader.Text = "Add a semester";
        SaveButton.Text = "Add Semester";
        CancelEditButton.IsVisible = false;
    }

    private async void OnSaveSemesterClicked(object sender, EventArgs e)
    {
        var name = NameEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            await DisplayAlert("Semester", "Please enter a name.", "OK");
            return;
        }

        if (EndPicker.Date < StartPicker.Date)
        {
            await DisplayAlert("Semester", "The stop date must be on or after the start date.", "OK");
            return;
        }

        try
        {
            if (_editingId is int id)
            {
                await _semesterService.UpdateSemesterAsync(new Semester
                {
                    Id = id,
                    Name = name,
                    StartDate = StartPicker.Date.GetValueOrDefault(),
                    EndDate = EndPicker.Date.GetValueOrDefault()
                });
            }
            else
            {
                await _semesterService.CreateSemesterAsync(new Semester
                {
                    Name = name,
                    StartDate = StartPicker.Date.GetValueOrDefault(),
                    EndDate = EndPicker.Date.GetValueOrDefault()
                });
            }

            ResetForm();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Save failed", ex.Message, "OK");
        }
    }

    private async void OnEditSemesterClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not int id) return;
        var semester = await _semesterService.GetSemesterAsync(id);
        if (semester is null) return;

        _editingId = semester.Id;
        NameEntry.Text = semester.Name;
        StartPicker.Date = semester.StartDate;
        EndPicker.Date = semester.EndDate;
        FormHeader.Text = $"Edit {semester.Name}";
        SaveButton.Text = "Save Changes";
        CancelEditButton.IsVisible = true;
    }

    private void OnCancelEditClicked(object sender, EventArgs e)
    {
        ResetForm();
    }

    private async void OnDeleteSemesterClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not int id) return;
        var ok = await DisplayAlert("Delete", "Delete this semester?", "Yes", "No");
        if (!ok) return;

        await _semesterService.DeleteSemesterAsync(id);
        if (_editingId == id) ResetForm();
        await RefreshAsync();
    }
}
