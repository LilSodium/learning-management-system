using CLI.LMS;
using CLI.LMS.Core.Services;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls.Shapes;

namespace CLI.LMS.MAUI;

// Grading screen, opened from the Grade button on Manage Courses.
// Pick an assignment then score each student. Scores feed the gradebook
// and letter grades.
[QueryProperty(nameof(CourseId), "courseId")]
public partial class SubmissionsPage : ContentPage, IQueryAttributable
{
    private readonly ICourseService _courseService;
    private readonly IAssignmentService _assignmentService;
    private readonly ISubmissionService _submissionService;
    private readonly ICommentService _commentService;

    public string CourseId { get; set; } = "";

    private List<Assignment> _assignments = new();
    private IReadOnlyList<Student> _roster = new List<Student>();

    public SubmissionsPage(ICourseService courseService, IAssignmentService assignmentService, ISubmissionService submissionService, ICommentService commentService)
    {
        InitializeComponent();
        _courseService = courseService;
        _assignmentService = assignmentService;
        _submissionService = submissionService;
        _commentService = commentService;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query == null) return;
        if (query.TryGetValue("courseId", out var c) && c is string id)
            CourseId = id;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var course = await _courseService.GetCourseAsync(CourseId);
        CourseTitleLabel.Text = course is not null ? $"Grade: {course.Name}" : "Grade Submissions";

        _roster = await _courseService.GetRosterAsync(CourseId);
        _assignments = (await _assignmentService.GetAssignmentsForCourseAsync(CourseId)).ToList();

        AssignmentPicker.ItemsSource = _assignments
            .Select(a => a.IsQuiz ? $"{a.Name} (Quiz)" : a.Name)
            .ToList();

        SubmissionsContainer.Children.Clear();
        if (_assignments.Count == 0)
        {
            SubmissionsContainer.Children.Add(new Label { Text = "This course has no assignments yet." });
            return;
        }

        // this fires OnAssignmentPickerChanged, which builds the rows
        AssignmentPicker.SelectedIndex = 0;
    }

    private async void OnAssignmentPickerChanged(object sender, EventArgs e)
    {
        var idx = AssignmentPicker.SelectedIndex;
        if (idx < 0 || idx >= _assignments.Count) return;
        await BuildSubmissionsAsync(_assignments[idx]);
    }

    private async Task BuildSubmissionsAsync(Assignment assignment)
    {
        SubmissionsContainer.Children.Clear();

        if (_roster.Count == 0)
        {
            SubmissionsContainer.Children.Add(new Label { Text = "No students are enrolled in this course." });
            return;
        }

        foreach (var student in _roster)
        {
            var sub = await _submissionService.GetSubmissionAsync(assignment.Id, student.Id);

            var card = new Border
            {
                Stroke = Colors.LightGray,
                StrokeThickness = 1,
                Padding = 12,
                StrokeShape = new RoundRectangle { CornerRadius = 8 }
            };
            var v = new VerticalStackLayout { Spacing = 6 };
            v.Children.Add(new Label { Text = student.Name, FontAttributes = FontAttributes.Bold, FontSize = 16 });

            if (sub is null)
            {
                v.Children.Add(new Label { Text = "Not submitted", TextColor = Colors.Gray });
            }
            else
            {
                v.Children.Add(new Label { Text = string.IsNullOrWhiteSpace(sub.Content) ? "(no text response)" : sub.Content });

                if (!string.IsNullOrWhiteSpace(sub.FileName))
                {
                    var fileRow = new HorizontalStackLayout { Spacing = 8 };
                    fileRow.Children.Add(new Label { Text = $"Attached: {sub.FileName}", VerticalOptions = LayoutOptions.Center });
                    if (!string.IsNullOrWhiteSpace(sub.FilePath))
                    {
                        var openBtn = new Button { Text = "Open", FontSize = 12 };
                        var path = sub.FilePath;
                        openBtn.Clicked += async (_, __) =>
                        {
                            try
                            {
                                await Launcher.Default.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(path) });
                            }
                            catch (Exception ex)
                            {
                                await DisplayAlert("Open failed", ex.Message, "OK");
                            }
                        };
                        fileRow.Children.Add(openBtn);
                    }
                    v.Children.Add(fileRow);
                }

                var gradeRow = new HorizontalStackLayout { Spacing = 8 };
                gradeRow.Children.Add(new Label { Text = "Score:", VerticalOptions = LayoutOptions.Center });
                var scoreEntry = new Entry { Keyboard = Keyboard.Numeric, WidthRequest = 90, Text = sub.Grade > 0 ? sub.Grade.ToString() : string.Empty };
                gradeRow.Children.Add(scoreEntry);
                gradeRow.Children.Add(new Label { Text = $"/ {assignment.AvailablePoints}", VerticalOptions = LayoutOptions.Center });
                v.Children.Add(gradeRow);

                var commentEntry = new Entry { Placeholder = "Comment (optional)", Text = sub.Comment ?? string.Empty };
                v.Children.Add(commentEntry);

                var saveBtn = new Button { Text = "Save Grade", FontSize = 13, HorizontalOptions = LayoutOptions.Start };
                var studentId = student.Id;
                var studentName = student.Name;
                saveBtn.Clicked += async (_, __) =>
                {
                    int.TryParse(scoreEntry.Text, out var score);
                    var toSave = await _submissionService.GetSubmissionAsync(assignment.Id, studentId);
                    if (toSave is null)
                    {
                        await DisplayAlert("Grade", "That submission could not be found.", "OK");
                        return;
                    }
                    toSave.Grade = score;
                    toSave.Comment = commentEntry.Text ?? string.Empty;
                    await _submissionService.SaveSubmissionAsync(toSave);
                    await DisplayAlert("Grade", $"Saved {score}/{assignment.AvailablePoints} for {studentName}.", "OK");
                };
                v.Children.Add(saveBtn);
            }

            // Comments show for everyone, even before they submit.
            await AddCommentsSectionAsync(v, assignment.Id, student);

            card.Content = v;
            SubmissionsContainer.Children.Add(card);
        }
    }

    private async Task AddCommentsSectionAsync(VerticalStackLayout parent, int assignmentId, Student student)
    {
        parent.Children.Add(new Label { Text = "Comments", FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 8, 0, 0) });

        var commentsContainer = new VerticalStackLayout { Spacing = 6 };
        parent.Children.Add(commentsContainer);

        var studentId = student.Id;

        async Task ReloadAsync()
        {
            commentsContainer.Children.Clear();
            var comments = await _commentService.GetCommentsAsync(assignmentId, studentId);
            if (comments.Count == 0)
            {
                commentsContainer.Children.Add(new Label { Text = "No comments yet.", TextColor = Colors.Gray, FontSize = 12 });
            }
            else
            {
                foreach (var c in comments)
                    commentsContainer.Children.Add(CommentViews.Build(c));
            }
        }

        await ReloadAsync();

        var entry = new Entry { Placeholder = "Reply to the student..." };
        parent.Children.Add(entry);

        var postBtn = new Button { Text = "Post Comment", FontSize = 13, HorizontalOptions = LayoutOptions.Start };
        postBtn.Clicked += async (_, __) =>
        {
            var text = entry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text)) return;
            await _commentService.AddCommentAsync(new AssignmentComment
            {
                AssignmentId = assignmentId,
                StudentId = studentId,
                Author = "Instructor",
                IsInstructor = true,
                Text = text,
                PostedAt = DateTime.Now
            });
            entry.Text = string.Empty;
            await ReloadAsync();
        };
        parent.Children.Add(postBtn);
    }
}
