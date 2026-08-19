using CLI.LMS;
using CLI.LMS.Core.Services;
using CLI.LMS.MAUI.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System;
using Microsoft.Maui.Layouts;
using Microsoft.Maui.Graphics;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace CLI.LMS.MAUI;

// Read-only course view for students. Editing happens on CourseEditPage.
// Content plus an Open button per assignment, no edit controls.
[QueryProperty(nameof(CourseId), "courseId")]
[QueryProperty(nameof(StudentId), "studentId")]
public partial class CourseDetailPage : ContentPage, IQueryAttributable
{
    private readonly ICourseService _courseService;
    private readonly IGradeService _gradeService;
    public string CourseId { get; set; } = "";
    public string StudentId { get; set; }

    private Course Course = null!;
    public CourseDetailPage(ICourseService courseService, IGradeService gradeService)
    {
        InitializeComponent();
        _courseService = courseService ?? throw new ArgumentNullException(nameof(courseService));
        _gradeService = gradeService ?? throw new ArgumentNullException(nameof(gradeService));
    }

    // Parameterless ctor for XAML/previewer or when DI isn't used to construct the page.
    public CourseDetailPage()
    {
        InitializeComponent();

        var provider = Application.Current?.Handler?.MauiContext?.Services;
        if (provider is null)
            throw new InvalidOperationException("IServiceProvider is not available to resolve page services.");

        _courseService = provider.GetRequiredService<ICourseService>();
        _gradeService = provider.GetRequiredService<IGradeService>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Both ids come off the Shell query as strings.
        var course = await _courseService.GetCourseAsync(CourseId);
        BindingContext = course;

        var letterGrade = await _gradeService.GetLetterGradeAsync(CourseId, StudentId);
        var gradeLabel = this.FindByName<Label>("GradeLabel");
        if (gradeLabel is not null)
        {
            gradeLabel.Text = letterGrade ?? "Not yet graded";

            // Color the letter if the teacher set one for that band.
            if (!string.IsNullOrWhiteSpace(letterGrade))
            {
                var ranges = await _courseService.GetGradeRangesAsync(CourseId);
                var hex = ranges.FirstOrDefault(r => r.Letter == letterGrade)?.Color;
                if (!string.IsNullOrWhiteSpace(hex))
                {
                    try { gradeLabel.TextColor = Color.FromArgb(hex); } catch { /* ignore bad hex */ }
                }
            }
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query == null) return;

        if (query.TryGetValue("courseId", out var c) && c is string courseId)
            CourseId = courseId;

        if (query.TryGetValue("studentId", out var s) && s is string studentId)
            StudentId = studentId;
    }


    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        LoadCourseAndBuildUI();
    }

    private void LoadCourseAndBuildUI()
    {
        using var db = new LMSDbContext();

        Course = db.Courses
            .Include(c => c.Sections)
                .ThenInclude(s => s.Modules)
                    .ThenInclude(m => m.Content)
            .Include(c => c.Sections)
                .ThenInclude(s => s.Assignments)
            .FirstOrDefault(c => c.Id == CourseId) ?? null!;

        TitleLabel.Text = Course.Name;
        DescriptionLabel.Text = Course.Description;
        var annLabel = this.FindByName<Label>("AnnouncementLabel");
        var annFrame = this.FindByName<Border>("AnnouncementFrame");
        if (annLabel is not null)
        {
            annLabel.Text = Course.Announcement ?? string.Empty;
            if (annFrame is not null)
                annFrame.IsVisible = !string.IsNullOrWhiteSpace(Course.Announcement);
        }

        BuildSectionsUI();
    }

    private void BuildSectionsUI()
    {
        var container = this.FindByName<VerticalStackLayout>("SectionsContainer");
        if (container is null) return;

        container.Children.Clear();

        foreach (var section in Course.Sections.OrderBy(s => s.SectionNumber))
        {
            // Section header (read-only)
            container.Children.Add(new Label
            {
                Text = $"Section {section.SectionNumber}",
                FontAttributes = FontAttributes.Bold,
                FontSize = 18
            });

            // Modules and their content (read-only)
            foreach (var module in section.Modules)
            {
                var moduleFrame = new Frame { Padding = 12, BorderColor = Colors.LightGray, CornerRadius = 8 };
                var v = new VerticalStackLayout { Spacing = 4 };
                v.Children.Add(new Label { Text = module.Id, FontAttributes = FontAttributes.Bold, FontSize = 16 });

                foreach (var content in module.Content)
                {
                    v.Children.Add(new Label { Text = content.DisplayName });
                }

                moduleFrame.Content = v;
                container.Children.Add(moduleFrame);
            }

            // Assignments, with an Open button to go submit.
            container.Children.Add(new Label
            {
                Text = "Assignments",
                FontAttributes = FontAttributes.Bold,
                FontSize = 18
            });

            if (!section.Assignments.Any())
            {
                container.Children.Add(new Label { Text = "No assignments yet.", TextColor = Colors.Gray, FontSize = 13 });
            }

            foreach (var assignment in section.Assignments)
            {
                var ah = new HorizontalStackLayout { Spacing = 8 };
                ah.Children.Add(new Label { Text = assignment.Name + " - Due: " + assignment.DueDate, VerticalOptions = LayoutOptions.Center });
                if (assignment.IsQuiz)
                {
                    ah.Children.Add(new Label { Text = "Quiz", FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Colors.MediumPurple, VerticalOptions = LayoutOptions.Center });
                }

                var openAssignBtn = new Button { Text = "Open", FontSize = 12 };
                openAssignBtn.Clicked += async (_, __) =>
                {
                    await Shell.Current.GoToAsync(nameof(AssignmentDetailPage), new Dictionary<string, object>
                    {
                        ["assignmentId"] = assignment.Id,
                        ["studentId"] = StudentId ?? string.Empty
                    });
                };
                ah.Children.Add(openAssignBtn);
                container.Children.Add(ah);
            }
        }
    }
}
