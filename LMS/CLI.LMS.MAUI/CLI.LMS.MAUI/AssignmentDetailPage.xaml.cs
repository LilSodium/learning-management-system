using CLI.LMS;
using CLI.LMS.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace CLI.LMS.MAUI;

[QueryProperty(nameof(AssignmentId), "assignmentId")]
[QueryProperty(nameof(StudentId), "studentId")]
public partial class AssignmentDetailPage : ContentPage, IQueryAttributable
{
    public Assignment Assignment { get; set; }
    public Student Student { get; set; }

    // Query properties
    public int AssignmentId { get; set; }
    public string StudentId { get; set; }

    private readonly IAssignmentService _assignmentService;
    private readonly IStudentService _studentService;
    private readonly ISubmissionService _submissionService;
    private readonly ICommentService _commentService;

    // File picked but not submitted yet.
    private string? _attachmentName;
    private string? _attachmentPath;

    // Empty unless this is a multiple choice quiz.
    private readonly List<RadioButton> _choiceButtons = new();

    // DI ctor
    public AssignmentDetailPage(IAssignmentService assignmentService, IStudentService studentService, ISubmissionService submissionService, ICommentService commentService)
    {
        InitializeComponent();

        _assignmentService = assignmentService ?? throw new ArgumentNullException(nameof(assignmentService));
        _studentService = studentService ?? throw new ArgumentNullException(nameof(studentService));
        _submissionService = submissionService ?? throw new ArgumentNullException(nameof(submissionService));
        _commentService = commentService ?? throw new ArgumentNullException(nameof(commentService));

        SubmitButton.IsEnabled = true;
        BindingContext = this;
    }

    // Parameterless ctor for XAML/previewer
    public AssignmentDetailPage()
    {
        InitializeComponent();

        var provider = Application.Current?.Handler?.MauiContext?.Services;
        if (provider is null)
            throw new InvalidOperationException("IServiceProvider is not available to resolve page services.");

        _assignmentService = provider.GetRequiredService<IAssignmentService>();
        _studentService = provider.GetRequiredService<IStudentService>();
        _submissionService = provider.GetRequiredService<ISubmissionService>();
        _commentService = provider.GetRequiredService<ICommentService>();

        SubmitButton.IsEnabled = true;
        BindingContext = this;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query == null) return;

        if (query.TryGetValue("assignmentId", out var a) && a is int aid)
            AssignmentId = aid;

        if (query.TryGetValue("studentId", out var s) && s is string sid)
            StudentId = sid;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        // Load assignment and student using services
        Assignment = await _assignmentService.GetAssignmentAsync(AssignmentId);
        Student = await _studentService.GetStudentAsync(StudentId);

        if (Assignment == null)
        {
            StatusLabel.Text = "Not yet submitted";
            return;
        }

        NameLabel.Text = Assignment.Name;
        DueDateLabel.Text = $"Due: {Assignment.DueDate}";
        PointsLabel.Text = $"{Assignment.AvailablePoints} points";
        DescriptionLabel.Text = Assignment.Description;

        // Quiz assignments show the teacher's question above the response box.
        if (Assignment.IsQuiz && !string.IsNullOrWhiteSpace(Assignment.Question))
        {
            QuestionLabel.Text = Assignment.Question;
            QuestionCard.IsVisible = true;
            SubmissionEditor.Placeholder = "Type your response here...";
        }

        // Multiple choice, so show the options and hide the text box.
        _choiceButtons.Clear();
        ChoicesContainer.Children.Clear();
        if (Assignment.IsQuiz && !string.IsNullOrWhiteSpace(Assignment.Choices))
        {
            var options = Assignment.Choices.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var opt in options)
            {
                var rb = new RadioButton { Content = opt, Value = opt, GroupName = "quizChoices" };
                _choiceButtons.Add(rb);
                ChoicesContainer.Children.Add(rb);
            }
            ChoicesContainer.IsVisible = true;
            SubmissionEditor.IsVisible = false;
        }
        else
        {
            ChoicesContainer.IsVisible = false;
            SubmissionEditor.IsVisible = true;
        }

        if (Student == null)
        {
            StatusLabel.Text = "Not yet submitted";
            return;
        }

        var existing = await _submissionService.GetSubmissionAsync(AssignmentId, Student.Id);
        if (existing != null)
        {
            SubmissionEditor.Text = existing.Content;
            // Restore the previously selected multiple-choice answer.
            var chosen = _choiceButtons.FirstOrDefault(r => (r.Value?.ToString() ?? r.Content?.ToString()) == existing.Content);
            if (chosen is not null) chosen.IsChecked = true;
            _attachmentName = existing.FileName;
            _attachmentPath = existing.FilePath;
            UpdateAttachmentLabel();
            StatusLabel.Text = existing.Grade > 0
                ? $"Graded: {existing.Grade}/{Assignment.AvailablePoints}" +
                    (string.IsNullOrWhiteSpace(existing.Comment) ? "" : $" - {existing.Comment}")
                : "Submitted, awaiting grade";

            // Once graded, the student can't change or resubmit their work.
            if (existing.Grade > 0)
                LockSubmissionControls();
        }
        else
        {
            StatusLabel.Text = "Not yet submitted";
        }

        await RefreshCommentsAsync();
    }

    // Comments
    private async Task RefreshCommentsAsync()
    {
        CommentsContainer.Children.Clear();

        if (Student is null || string.IsNullOrEmpty(Student.Id))
        {
            NoCommentsLabel.IsVisible = true;
            PostCommentButton.IsEnabled = false;
            return;
        }

        PostCommentButton.IsEnabled = true;
        var comments = await _commentService.GetCommentsAsync(AssignmentId, Student.Id);
        NoCommentsLabel.IsVisible = comments.Count == 0;

        foreach (var c in comments)
            CommentsContainer.Children.Add(CommentViews.Build(c));
    }

    private async void OnPostCommentClicked(object sender, EventArgs e)
    {
        if (Student is null || string.IsNullOrEmpty(Student.Id))
        {
            await DisplayAlert("Comments", "No student is associated with this page.", "OK");
            return;
        }

        var text = CommentEditor.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        await _commentService.AddCommentAsync(new AssignmentComment
        {
            AssignmentId = AssignmentId,
            StudentId = Student.Id,
            Author = string.IsNullOrWhiteSpace(Student.Name) ? "Student" : Student.Name,
            IsInstructor = false,
            Text = text,
            PostedAt = DateTime.Now
        });

        CommentEditor.Text = string.Empty;
        await RefreshCommentsAsync();
    }

    private void UpdateAttachmentLabel()
    {
        AttachmentLabel.Text = string.IsNullOrWhiteSpace(_attachmentName)
            ? "No file attached"
            : $"Attached: {_attachmentName}";
    }

    // Once a grade is in, freeze the inputs so the student can't resubmit.
    private void LockSubmissionControls()
    {
        SubmissionEditor.IsEnabled = false;
        AttachButton.IsEnabled = false;
        foreach (var rb in _choiceButtons) rb.IsEnabled = false;
        SubmitButton.IsEnabled = false;
        SubmitButton.Text = "Graded, cannot resubmit";
    }

    private async void OnAttachFileClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select a file to attach"
            });
            if (result is null) return;

            // Copy it into app data in case the original gets moved.
            var uploadsDir = Path.Combine(FileSystem.AppDataDirectory, "uploads");
            Directory.CreateDirectory(uploadsDir);
            var destName = $"{Guid.NewGuid():N}_{result.FileName}";
            var destPath = Path.Combine(uploadsDir, destName);

            using (var src = await result.OpenReadAsync())
            using (var dst = File.Create(destPath))
            {
                await src.CopyToAsync(dst);
            }

            _attachmentName = result.FileName;
            _attachmentPath = destPath;
            UpdateAttachmentLabel();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Attach failed", ex.Message, "OK");
        }
    }

    private async void OnSubmitClicked(object sender, EventArgs e)
    {
        if (Student == null || string.IsNullOrEmpty(Student.Id))
        {
            await DisplayAlert("Error", "No student associated with this page.", "OK");
            return;
        }

        // Answer is the picked option on a quiz, otherwise the typed text.
        string content;
        if (ChoicesContainer.IsVisible && _choiceButtons.Count > 0)
        {
            var selected = _choiceButtons.FirstOrDefault(r => r.IsChecked);
            if (selected is null)
            {
                await DisplayAlert("Quiz", "Please select an answer before submitting.", "OK");
                return;
            }
            content = selected.Value?.ToString() ?? selected.Content?.ToString() ?? string.Empty;
        }
        else
        {
            content = SubmissionEditor.Text ?? string.Empty;
        }

        // Go through the service so this actually lands in the db and the
        // teacher can see it.
        var existing = await _submissionService.GetSubmissionAsync(AssignmentId, Student.Id);
        if (existing is not null && existing.Grade > 0)
        {
            await DisplayAlert("Already graded", "This submission has been graded and can no longer be changed.", "OK");
            return;
        }

        var submission = new Submission
        {
            Id = existing?.Id ?? Guid.NewGuid().ToString("N").Substring(0, 8),
            AssignmentId = AssignmentId,
            StudentId = Student.Id,
            Content = content,
            SubmissionDate = DateTime.Today.ToShortDateString(),
            // don't wipe a grade the teacher already gave
            Grade = existing?.Grade ?? 0,
            Comment = existing?.Comment ?? string.Empty,
            FileName = _attachmentName,
            FilePath = _attachmentPath
        };

        await _submissionService.SaveSubmissionAsync(submission);

        StatusLabel.Text = submission.Grade > 0
            ? $"Graded: {submission.Grade}/{Assignment.AvailablePoints}"
            : "Submitted, awaiting grade";
        await DisplayAlert("Success", "Your submission was saved.", "OK");
    }
}