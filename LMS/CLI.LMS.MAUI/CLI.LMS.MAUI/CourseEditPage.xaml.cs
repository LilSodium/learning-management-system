using CLI.LMS;
using CLI.LMS.Core.Services;
using CLI.LMS.MAUI.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Layouts;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.ApplicationModel;
using System.Linq;

namespace CLI.LMS.MAUI;

[QueryProperty(nameof(CourseId), "courseId")]
[QueryProperty(nameof(StudentId), "studentId")]
public partial class CourseEditPage : ContentPage, IQueryAttributable
{
    private readonly ICourseService _courseService;
    private readonly IGradeService _gradeService;
    private readonly IStudentService _studentService;
    private readonly IEmailService _emailService;

    public string CourseId { get; set; } = "";
    public string StudentId { get; set; }

    private Course Course = null!;

    public CourseEditPage(ICourseService courseService, IGradeService gradeService, IStudentService studentService, IEmailService emailService)
    {
        InitializeComponent();
        _courseService = courseService;
        _gradeService = gradeService;
        _studentService = studentService;
        _emailService = emailService;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        LoadCourseAndBuildUI();
    }

    private void LoadCourseAndBuildUI()
    {
        // Only hit the db if the course is actually saved. In create mode it's
        // memory only, and reloading would wipe whatever was just typed.
        if (!string.IsNullOrWhiteSpace(CourseId))
        {
            using var db = new LMSDbContext();

            Course = db.Courses
                .Include(c => c.Sections)
                    .ThenInclude(s => s.Modules)
                        .ThenInclude(m => m.Content)
                .Include(c => c.Sections)
                    .ThenInclude(s => s.Assignments)
                .FirstOrDefault(c => c.Id == CourseId);
        }

        if (Course is null)
        {
            Course = new Course
            {
                Term = string.Empty,
                Code = string.Empty,
                Name = string.Empty,
                Description = string.Empty,
                Sections = new List<Section>()
            };
        }

        var titleLabel = this.FindByName<Label>("TitleLabel");
        var descLabel = this.FindByName<Label>("DescriptionLabel");
        var titleEntry = this.FindByName<Entry>("TitleEntry");
        var descEditor = this.FindByName<Editor>("DescriptionEditor");
        var saveBtn = this.FindByName<Button>("SaveButton");
        var cancelBtn = this.FindByName<Button>("CancelButton");

        var isCreate = string.IsNullOrWhiteSpace(CourseId) || string.IsNullOrWhiteSpace(Course.Id);
        if (isCreate)
        {
            // Save what's typed before refreshing the controls, otherwise
            // rebuilding the UI clears it.
            if (titleEntry is not null && titleEntry.IsVisible) Course.Name = titleEntry.Text ?? Course.Name;
            if (descEditor is not null && descEditor.IsVisible) Course.Description = descEditor.Text ?? Course.Description;

            if (titleLabel is not null) titleLabel.IsVisible = false;
            if (descLabel is not null) descLabel.IsVisible = false;
            if (titleEntry is not null) { titleEntry.IsVisible = true; titleEntry.Text = Course.Name ?? string.Empty; }
            if (descEditor is not null) { descEditor.IsVisible = true; descEditor.Text = Course.Description ?? string.Empty; }
            if (saveBtn is not null) saveBtn.IsVisible = true;
            if (cancelBtn is not null) cancelBtn.IsVisible = true;

            Course.Sections = Course.Sections ?? new List<Section>();
            Course.Roster = Course.Roster ?? new List<Student>();

            if (!Course.Sections.Any())
            {
                Course.Sections.Add(new Section { Id = 0, SectionNumber = 1, Modules = new List<Module>(), Assignments = new List<Assignment>() });
            }

            var annEditor = this.FindByName<Editor>("AnnouncementEditor");
            if (annEditor is not null)
            {
                if (annEditor.IsVisible) Course.Announcement = annEditor.Text ?? Course.Announcement;
                annEditor.IsVisible = true;
                annEditor.Text = Course.Announcement ?? string.Empty;
            }

            BuildSectionsUI();
            RosterList.ItemsSource = Course.Roster;
            return;
        }

        if (titleLabel is not null) titleLabel.Text = Course.Name ?? string.Empty;
        if (descLabel is not null) descLabel.Text = Course.Description ?? string.Empty;
        var annLabel = this.FindByName<Label>("AnnouncementLabel");
        var annEditor2 = this.FindByName<Editor>("AnnouncementEditor");
        var editAnnBtn = this.FindByName<Button>("EditAnnouncementButton");
        var saveAnnBtn = this.FindByName<Button>("SaveAnnouncementButton");
        if (annLabel is not null) annLabel.Text = Course.Announcement ?? string.Empty;
        if (annEditor2 is not null) { annEditor2.Text = Course.Announcement ?? string.Empty; annEditor2.IsVisible = false; }
        if (saveAnnBtn is not null) saveAnnBtn.IsVisible = false;
        if (editAnnBtn is not null) editAnnBtn.IsVisible = true;

        BuildSectionsUI();
    }

    private async void OnSaveCourseClicked(object sender, EventArgs e)
    {
        var titleEntry = this.FindByName<Entry>("TitleEntry");
        var descEditor = this.FindByName<Editor>("DescriptionEditor");
        var annEditor = this.FindByName<Editor>("AnnouncementEditor");
        if (titleEntry is null) return;

        var newCourse = new Course
        {
            Id = Guid.NewGuid().ToString(),
            Name = titleEntry.Text ?? string.Empty,
            Description = descEditor?.Text ?? string.Empty,
            Announcement = annEditor?.Text ?? string.Empty,
            Code = string.Empty,
            Term = string.Empty,
            Sections = Course.Sections ?? new List<Section>(),
            Roster = Course.Roster ?? new List<Student>()
        };

        try
        {
            var id = await _courseService.CreateCourseAsync(newCourse);
            await DisplayAlert("Course created", "Saved.", "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Save failed", ex.Message, "OK");
        }
    }

    private void OnEditAnnouncementClicked(object sender, EventArgs e)
    {
        var annEditor = this.FindByName<Editor>("AnnouncementEditor");
        var editBtn = this.FindByName<Button>("EditAnnouncementButton");
        var saveBtn = this.FindByName<Button>("SaveAnnouncementButton");
        if (annEditor is null || editBtn is null || saveBtn is null) return;
        annEditor.IsVisible = true;
        saveBtn.IsVisible = true;
        editBtn.IsVisible = false;
    }

    private async void OnSaveAnnouncementClicked(object sender, EventArgs e)
    {
        var annEditor = this.FindByName<Editor>("AnnouncementEditor");
        var annLabel = this.FindByName<Label>("AnnouncementLabel");
        var editBtn = this.FindByName<Button>("EditAnnouncementButton");
        var saveBtn = this.FindByName<Button>("SaveAnnouncementButton");
        if (annEditor is null || annLabel is null || editBtn is null || saveBtn is null) return;

        Course.Announcement = annEditor.Text ?? string.Empty;
        try
        {
            await _courseService.UpdateCourseAsync(Course);
            annLabel.Text = Course.Announcement ?? string.Empty;
            annEditor.IsVisible = false;
            saveBtn.IsVisible = false;
            editBtn.IsVisible = true;
            await DisplayAlert("Announcement", "Saved.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Save failed", ex.Message, "OK");
        }
        }

    private async void OnCancelCreateClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private void BuildSectionsUI()
    {
        var container = this.FindByName<VerticalStackLayout>("SectionsContainer");
        if (container is null) return;
        container.Children.Clear();

        foreach (var section in Course.Sections.OrderBy(s => s.SectionNumber))
        {
            var header = new HorizontalStackLayout { Spacing = 12 };
            header.Children.Add(new Label { Text = $"Section {section.SectionNumber}", FontAttributes = FontAttributes.Bold, FontSize = 18 });
            var addModuleBtn = new Button { Text = "Add Module", HorizontalOptions = LayoutOptions.End };
            addModuleBtn.Clicked += async (_, __) =>
            {
                var title = await DisplayPromptAsync("New Module", "Enter module title:");
                if (string.IsNullOrWhiteSpace(title)) return;
                await AddModuleToSectionAsync(section.Id, title);
            };
            header.Children.Add(addModuleBtn);
            var deleteSectionBtn = new Button { Text = "Delete Section", FontSize = 12, BackgroundColor = Colors.LightCoral, HorizontalOptions = LayoutOptions.End };
            deleteSectionBtn.Clicked += async (_, __) =>
            {
                var ok = await DisplayAlert("Delete Section", $"Delete Section {section.SectionNumber} and all its modules and assignments?", "Yes", "No");
                if (!ok) return;
                await DeleteSectionAsync(section);
            };
            header.Children.Add(deleteSectionBtn);
            container.Children.Add(header);

            // Modules
            foreach (var module in section.Modules)
            {
                var moduleFrame = new Frame { Padding = 12, BorderColor = Colors.LightGray, CornerRadius = 8 };
                var v = new VerticalStackLayout();
                var mh = new HorizontalStackLayout { Spacing = 8 };
                mh.Children.Add(new Label { Text = module.Id, FontAttributes = FontAttributes.Bold, FontSize = 16, VerticalOptions = LayoutOptions.Center });

                var editModuleBtn = new Button { Text = "Edit", FontSize = 12 };
                editModuleBtn.Clicked += async (_, __) =>
                {
                    var newTitle = await DisplayPromptAsync("Edit Module", "New module title:", initialValue: module.Id);
                    if (string.IsNullOrWhiteSpace(newTitle)) return;
                    await RenameModuleAsync(section.Id, module.Id, newTitle);
                };
                mh.Children.Add(editModuleBtn);

                var deleteModuleBtn = new Button { Text = "Delete", FontSize = 12, BackgroundColor = Colors.LightCoral };
                deleteModuleBtn.Clicked += async (_, __) =>
                {
                    var ok = await DisplayAlert("Delete Module", $"Remove module '{module.Id}'?", "Yes", "No");
                    if (!ok) return;
                    await DeleteModuleAsync(section.Id, module.Id);
                };
                mh.Children.Add(deleteModuleBtn);

                var addContentBtn = new Button { Text = "Add Content", FontSize = 12 };
                addContentBtn.Clicked += async (_, __) =>
                {
                    var contentTitle = await DisplayPromptAsync("Add Content", "Content title:");
                    if (string.IsNullOrWhiteSpace(contentTitle)) return;
                    var contentText = await DisplayPromptAsync("Add Content", "Content text:");
                    await AddPageToModuleAsync(section.Id, module.Id, contentTitle, contentText ?? string.Empty);
                };
                mh.Children.Add(addContentBtn);

                // Inline add assignment (creates assignment for section)
                var addAssignmentBtnInline = new Button { Text = "Add Assignment", FontSize = 12 };
                addAssignmentBtnInline.Clicked += async (_, __) =>
                {
                    var name = await DisplayPromptAsync("New Assignment", "Assignment name:");
                    if (string.IsNullOrWhiteSpace(name)) return;
                    var due = await DisplayPromptAsync("New Assignment", "Due date:");
                    var pointsStr = await DisplayPromptAsync("New Assignment", "Available points:", initialValue: "100");
                    int.TryParse(pointsStr, out var points);
                    var desc = await DisplayPromptAsync("New Assignment", "Description:");
                    await AddAssignmentToSectionAsync(section.Id, name, desc ?? string.Empty, points, due ?? string.Empty);
                };
                mh.Children.Add(addAssignmentBtnInline);

                v.Children.Add(mh);

                // Content framed
                var contentStack = new VerticalStackLayout { Spacing = 6 };
                foreach (var content in module.Content)
                {
                    var ch = new HorizontalStackLayout { Spacing = 8 };
                    ch.Children.Add(new Label { Text = content.DisplayName, VerticalOptions = LayoutOptions.Center });
                    var editContentBtn = new Button { Text = "Edit", FontSize = 12 };
                    editContentBtn.Clicked += async (_, __) =>
                    {
                        var newText = await DisplayPromptAsync("Edit Content", "Update text:", initialValue: content is Page p ? p.Text : content.DisplayName);
                        if (newText is null) return;
                        await UpdateModuleContentAsync(section.Id, module.Id, content.Id, newText);
                    };
                    ch.Children.Add(editContentBtn);
                    var deleteContentBtn = new Button { Text = "Delete", FontSize = 12, BackgroundColor = Colors.LightCoral };
                    deleteContentBtn.Clicked += async (_, __) =>
                    {
                        var ok = await DisplayAlert("Delete Content", $"Remove content '{content.DisplayName}'?", "Yes", "No");
                        if (!ok) return;
                        await DeleteModuleContentAsync(section.Id, module.Id, content.Id);
                    };
                    ch.Children.Add(deleteContentBtn);
                    contentStack.Children.Add(ch);
                }
                var contentFrameInner = new Frame { Padding = 6, CornerRadius = 6, BorderColor = Colors.LightGray, Content = contentStack };
                v.Children.Add(contentFrameInner);

                moduleFrame.Content = v;
                container.Children.Add(moduleFrame);
            }

            // Assignments framed for section
            var assignmentsFrame = new Frame { Padding = 10, CornerRadius = 8, BorderColor = Colors.LightGray };
            var assignStack = new VerticalStackLayout { Spacing = 8 };
            var assignHeader = new HorizontalStackLayout { Spacing = 8 };
            assignHeader.Children.Add(new Label { Text = "Assignments", FontAttributes = FontAttributes.Bold, FontSize = 16 });
            var addAssignBtn = new Button { Text = "Add Assignment", FontSize = 12, HorizontalOptions = LayoutOptions.End };
            addAssignBtn.Clicked += async (_, __) =>
            {
                var name = await DisplayPromptAsync("New Assignment", "Assignment name:");
                if (string.IsNullOrWhiteSpace(name)) return;
                var due = await DisplayPromptAsync("New Assignment", "Due date:");
                var pointsStr = await DisplayPromptAsync("New Assignment", "Available points:", initialValue: "100");
                int.TryParse(pointsStr, out var points);
                var desc = await DisplayPromptAsync("New Assignment", "Description:");
                await AddAssignmentToSectionAsync(section.Id, name, desc ?? string.Empty, points, due ?? string.Empty);
            };
            assignHeader.Children.Add(addAssignBtn);

            var addQuizBtn = new Button { Text = "Add Quiz", FontSize = 12, HorizontalOptions = LayoutOptions.End };
            addQuizBtn.Clicked += async (_, __) =>
            {
                var name = await DisplayPromptAsync("New Quiz", "Quiz name:");
                if (string.IsNullOrWhiteSpace(name)) return;
                var question = await DisplayPromptAsync("New Quiz", "Question for students:");
                if (string.IsNullOrWhiteSpace(question)) return;

                // Comma separated options. Blank means free response.
                var choicesRaw = await DisplayPromptAsync("New Quiz", "Multiple-choice options separated by commas (leave blank for a free-response quiz):");
                string? choices = null;
                int? correctIndex = null;
                if (!string.IsNullOrWhiteSpace(choicesRaw))
                {
                    var options = choicesRaw.Split(',').Select(o => o.Trim()).Where(o => o.Length > 0).ToList();
                    if (options.Count >= 2)
                    {
                        choices = string.Join("\n", options);
                        var correctStr = await DisplayPromptAsync("New Quiz", $"Which option number is the correct answer? (1-{options.Count})", initialValue: "1", keyboard: Keyboard.Numeric);
                        if (int.TryParse(correctStr, out var oneBased) && oneBased >= 1 && oneBased <= options.Count)
                            correctIndex = oneBased - 1;
                    }
                    else
                    {
                        await DisplayAlert("New Quiz", "Enter at least two comma-separated options for a multiple-choice quiz. Creating a free-response quiz instead.", "OK");
                    }
                }

                var due = await DisplayPromptAsync("New Quiz", "Due date:");
                var pointsStr = await DisplayPromptAsync("New Quiz", "Available points:", initialValue: "100");
                int.TryParse(pointsStr, out var points);
                var desc = await DisplayPromptAsync("New Quiz", "Description (optional):");
                await AddAssignmentToSectionAsync(section.Id, name, desc ?? string.Empty, points, due ?? string.Empty, question, choices, correctIndex);
            };
            assignHeader.Children.Add(addQuizBtn);
            assignStack.Children.Add(assignHeader);

            foreach (var assignment in section.Assignments)
            {
                var ah = new HorizontalStackLayout { Spacing = 8 };
                ah.Children.Add(new Label { Text = assignment.Name + " - Due: " + assignment.DueDate, VerticalOptions = LayoutOptions.Center });
                if (assignment.IsQuiz)
                {
                    ah.Children.Add(new Label { Text = "Quiz", FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Colors.MediumPurple, VerticalOptions = LayoutOptions.Center });
                }
                var editAssignBtn = new Button { Text = "Edit", FontSize = 12 };
                editAssignBtn.Clicked += async (_, __) =>
                {
                    var newName = await DisplayPromptAsync("Edit Assignment", "Name:", initialValue: assignment.Name);
                    if (newName is null) return;
                    var newDue = await DisplayPromptAsync("Edit Assignment", "Due date:", initialValue: assignment.DueDate);
                    var newPointsStr = await DisplayPromptAsync("Edit Assignment", "Available points:", initialValue: assignment.AvailablePoints.ToString());
                    int.TryParse(newPointsStr, out var newPoints);
                    var newDesc = await DisplayPromptAsync("Edit Assignment", "Description:", initialValue: assignment.Description);
                    await UpdateAssignmentAsync(section.Id, assignment.Id, newName, newDesc ?? string.Empty, newPoints, newDue ?? string.Empty);
                };
                ah.Children.Add(editAssignBtn);
                var deleteAssignBtn = new Button { Text = "Delete", FontSize = 12, BackgroundColor = Colors.LightCoral };
                deleteAssignBtn.Clicked += async (_, __) =>
                {
                    var ok = await DisplayAlert("Delete Assignment", $"Remove assignment '{assignment.Name}' and all its submissions?", "Yes", "No");
                    if (!ok) return;
                    await DeleteAssignmentAsync(section.Id, assignment.Id);
                };
                ah.Children.Add(deleteAssignBtn);
                var copyAssignBtn = new Button { Text = "Copy", FontSize = 12 };
                copyAssignBtn.Clicked += async (_, __) =>
                {
                    // pick which course to copy into
                    using var db = new LMSDbContext();
                    var courses = db.Courses.Include(c => c.Sections).ToList();
                    var candidates = courses.Where(c => c.Id != CourseId).ToList();
                    if (!candidates.Any())
                    {
                        await DisplayAlert("Copy Assignment", "No other courses available to copy into.", "OK");
                        return;
                    }

                    var display = candidates.Select(c => c.Name).ToArray();
                    var choice = await DisplayActionSheet("Copy assignment to...", "Cancel", null, display);
                    if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel") return;

                    var index = Array.IndexOf(display, choice);
                    if (index < 0) return;

                    var destCourse = db.Courses.Include(c => c.Sections).ThenInclude(s => s.Assignments).FirstOrDefault(c => c.Id == candidates[index].Id);
                    if (destCourse is null)
                    {
                        await DisplayAlert("Copy Assignment", "Destination course could not be found.", "OK");
                        return;
                    }

                    // Ensure destination has at least one section
                    if (destCourse.Sections == null || !destCourse.Sections.Any())
                    {
                        var newSection = new Section { SectionNumber = 1 };
                        destCourse.Sections.Add(newSection);
                    }

                    // Copy assignment metadata only (do not copy submissions)
                    var targetSection = destCourse.Sections.First();
                    var newAssignment = new Assignment
                    {
                        Name = assignment.Name,
                        Description = assignment.Description,
                        AvailablePoints = assignment.AvailablePoints,
                        DueDate = assignment.DueDate,
                        IsQuiz = assignment.IsQuiz,
                        Question = assignment.Question,
                        Choices = assignment.Choices,
                        CorrectChoiceIndex = assignment.CorrectChoiceIndex
                    };

                    // Add to destination section and save
                    targetSection.Assignments = targetSection.Assignments ?? new List<Assignment>();
                    targetSection.Assignments.Add(newAssignment);
                    await db.SaveChangesAsync();

                    await DisplayAlert("Copy Assignment", "Assignment copied (submissions were not copied).", "OK");
                    LoadCourseAndBuildUI();
                };
                ah.Children.Add(copyAssignBtn);
                assignStack.Children.Add(ah);
            }

            assignmentsFrame.Content = assignStack;
            container.Children.Add(assignmentsFrame);
        }
    }

    private async Task AddAssignmentToSectionAsync(int sectionId, string name, string description, int points, string dueDate, string? question = null, string? choices = null, int? correctIndex = null)
    {
        var isQuiz = !string.IsNullOrWhiteSpace(question) || !string.IsNullOrWhiteSpace(choices);
        var isCreate = string.IsNullOrWhiteSpace(CourseId) || string.IsNullOrWhiteSpace(Course?.Id);
        if (isCreate)
        {
            var section = Course.Sections.FirstOrDefault(s => s.Id == sectionId) ?? Course.Sections.FirstOrDefault();
            if (section is null) return;
            section.Assignments = section.Assignments ?? new List<Assignment>();
            section.Assignments.Add(new Assignment { Name = name, Description = description, AvailablePoints = points, DueDate = dueDate, IsQuiz = isQuiz, Question = isQuiz ? question : null, Choices = choices, CorrectChoiceIndex = correctIndex });
            LoadCourseAndBuildUI();
            return;
        }

        using var db = new LMSDbContext();
        var sectionDb = db.Sections.Include(s => s.Assignments).FirstOrDefault(s => s.Id == sectionId);
        if (sectionDb is null) return;
        var assignment = new Assignment { Name = name, Description = description, AvailablePoints = points, DueDate = dueDate, IsQuiz = isQuiz, Question = isQuiz ? question : null, Choices = choices, CorrectChoiceIndex = correctIndex };
        sectionDb.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        LoadCourseAndBuildUI();

        // Let the students know something new was posted.
        var notified = await _emailService.NotifyNewAssignmentAsync(CourseId, name);
        if (notified > 0)
            await DisplayAlert("Assignment posted", $"Emailed {notified} enrolled student(s) about \"{name}\". See the Email Log on the teacher menu.", "OK");
    }

    private async Task UpdateAssignmentAsync(int sectionId, int assignmentId, string name, string description, int points, string dueDate)
    {
        var isCreate = string.IsNullOrWhiteSpace(CourseId) || string.IsNullOrWhiteSpace(Course?.Id);
        if (isCreate)
        {
            var section = Course.Sections.FirstOrDefault(s => s.Id == sectionId) ?? Course.Sections.FirstOrDefault();
            if (section is null) return;
            var a = section.Assignments.FirstOrDefault(x => x.Id == assignmentId);
            if (a is null) return;
            a.Name = name;
            a.Description = description;
            a.AvailablePoints = points;
            a.DueDate = dueDate;
            LoadCourseAndBuildUI();
            return;
        }

        using var db = new LMSDbContext();
        var assignment = db.Assignments.FirstOrDefault(a => a.Id == assignmentId);
        if (assignment is null) return;
        assignment.Name = name;
        assignment.Description = description;
        assignment.AvailablePoints = points;
        assignment.DueDate = dueDate;
        db.Assignments.Update(assignment);
        await db.SaveChangesAsync();

        LoadCourseAndBuildUI();
    }

    private async Task DeleteAssignmentAsync(int sectionId, int assignmentId)
    {
        var isCreate = string.IsNullOrWhiteSpace(CourseId) || string.IsNullOrWhiteSpace(Course?.Id);
        if (isCreate)
        {
            var section = Course.Sections.FirstOrDefault(s => s.Id == sectionId) ?? Course.Sections.FirstOrDefault();
            if (section is null) return;
            var a = section.Assignments.FirstOrDefault(x => x.Id == assignmentId);
            if (a is null) return;
            section.Assignments.Remove(a);
            LoadCourseAndBuildUI();
            return;
        }

        using var db = new LMSDbContext();
        var assignment = db.Assignments.Include(a => a.Submissions).FirstOrDefault(a => a.Id == assignmentId);
        if (assignment is null) return;

        if (assignment.Submissions is not null)
        {
            db.Submissions.RemoveRange(assignment.Submissions);
        }
        db.Assignments.Remove(assignment);
        await db.SaveChangesAsync();

        LoadCourseAndBuildUI();
    }

    private async Task AddModuleToSectionAsync(int sectionId, string title)
    {
        var isCreate = string.IsNullOrWhiteSpace(CourseId) || string.IsNullOrWhiteSpace(Course.Id);
        if (isCreate)
        {
            var section = Course.Sections.FirstOrDefault(s => s.Id == sectionId) ?? Course.Sections.FirstOrDefault();
            if (section is null) return;
            section.Modules = section.Modules ?? new List<Module>();
            section.Modules.Add(new Module { Id = title });
            LoadCourseAndBuildUI();
            return;
        }

        using var db = new LMSDbContext();
        var dbSection = db.Sections.Include(s => s.Modules).FirstOrDefault(s => s.Id == sectionId);
        if (dbSection is null) return;
        var dbModule = new Module { Id = title };
        dbSection.Modules.Add(dbModule);
        await db.SaveChangesAsync();

        LoadCourseAndBuildUI();
    }

    private async Task RenameModuleAsync(int sectionId, string oldId, string newId)
    {
        // Not in the db yet, so rename in memory. Module.Id is init-only so
        // swap in a new instance at the same spot, keeping the content.
        var isCreate = string.IsNullOrWhiteSpace(CourseId) || string.IsNullOrWhiteSpace(Course.Id);
        if (isCreate)
        {
            var inMemorySection = Course.Sections.FirstOrDefault(s => s.Id == sectionId) ?? Course.Sections.FirstOrDefault();
            if (inMemorySection is null) return;
            var target = inMemorySection.Modules.FirstOrDefault(m => m.Id == oldId);
            if (target is null) return;

            var replacement = new Module { Id = newId };
            foreach (var c in target.Content)
            {
                if (c is Page p) replacement.Content.Add(new Page { Text = p.Text });
                else if (c is ModuleFile f) replacement.Content.Add(new ModuleFile { Name = f.Name });
            }

            var idx = inMemorySection.Modules.IndexOf(target);
            inMemorySection.Modules.RemoveAt(idx);
            inMemorySection.Modules.Insert(idx, replacement);
            LoadCourseAndBuildUI();
            return;
        }

        using var db = new LMSDbContext();
        var section = db.Sections.Include(s => s.Modules).ThenInclude(m => m.Content).FirstOrDefault(s => s.Id == sectionId);
        if (section is null) return;
        var oldModule = section.Modules.FirstOrDefault(m => m.Id == oldId);
        if (oldModule is null) return;
        var newModule = new Module { Id = newId };
        foreach (var c in oldModule.Content)
        {
            if (c is Page p)
                newModule.Content.Add(new Page { Text = p.Text });
            else if (c is ModuleFile f)
                newModule.Content.Add(new ModuleFile { Name = f.Name });
        }
        section.Modules.Add(newModule);
        section.Modules.Remove(oldModule);
        db.Modules.Remove(oldModule);
        await db.SaveChangesAsync();
        LoadCourseAndBuildUI();
    }

    private async Task DeleteModuleAsync(int sectionId, string moduleId)
    {
        var isCreate = string.IsNullOrWhiteSpace(CourseId) || string.IsNullOrWhiteSpace(Course.Id);
        if (isCreate)
        {
            var inMemorySection = Course.Sections.FirstOrDefault(s => s.Id == sectionId) ?? Course.Sections.FirstOrDefault();
            if (inMemorySection is null) return;
            var inMemoryModule = inMemorySection.Modules.FirstOrDefault(m => m.Id == moduleId);
            if (inMemoryModule is null) return;
            inMemorySection.Modules.Remove(inMemoryModule);
            LoadCourseAndBuildUI();
            return;
        }

        using var db = new LMSDbContext();
        var dbSection = db.Sections.Include(s => s.Modules).FirstOrDefault(s => s.Id == sectionId);
        if (dbSection is null) return;
        var dbModule = dbSection.Modules.FirstOrDefault(m => m.Id == moduleId);
        if (dbModule is null) return;
        dbSection.Modules.Remove(dbModule);
        db.Modules.Remove(dbModule);
        await db.SaveChangesAsync();
        LoadCourseAndBuildUI();
    }

    private async Task AddPageToModuleAsync(int sectionId, string moduleId, string title, string text)
    {
        var isCreate = string.IsNullOrWhiteSpace(CourseId) || string.IsNullOrWhiteSpace(Course.Id);
        if (isCreate)
        {
            var section = Course.Sections.FirstOrDefault(s => s.Id == sectionId) ?? Course.Sections.FirstOrDefault();
            if (section is null) return;
            var module = section.Modules.FirstOrDefault(m => m.Id == moduleId);
            if (module is null) return;
            module.Content.Add(new Page { Text = text });
            LoadCourseAndBuildUI();
            return;
        }

        using var db = new LMSDbContext();
        var moduleDb = db.Modules.Include(m => m.Content).FirstOrDefault(m => m.Id == moduleId);
        if (moduleDb is null) return;
        var page = new Page { Text = text };
        moduleDb.Content.Add(page);
        await db.SaveChangesAsync();
        LoadCourseAndBuildUI();
    }

    private async Task UpdateModuleContentAsync(int sectionId, string moduleId, int contentId, string newText)
    {
        var isCreate = string.IsNullOrWhiteSpace(CourseId) || string.IsNullOrWhiteSpace(Course.Id);
        if (isCreate)
        {
            var section = Course.Sections.FirstOrDefault(s => s.Id == sectionId) ?? Course.Sections.FirstOrDefault();
            if (section is null) return;
            var module = section.Modules.FirstOrDefault(m => m.Id == moduleId);
            if (module is null) return;
            var contentItem = module.Content.FirstOrDefault(c => c.Id == contentId);
            if (contentItem is Page p) p.Text = newText;
            LoadCourseAndBuildUI();
            return;
        }

        using var db = new LMSDbContext();
        var contentDb = db.Pages.FirstOrDefault(p => p.Id == contentId);
        if (contentDb is null) return;
        contentDb.Text = newText;
        db.Pages.Update(contentDb);
        await db.SaveChangesAsync();
        LoadCourseAndBuildUI();
    }

    private async Task DeleteModuleContentAsync(int sectionId, string moduleId, int contentId)
    {
        var isCreate = string.IsNullOrWhiteSpace(CourseId) || string.IsNullOrWhiteSpace(Course.Id);
        if (isCreate)
        {
            var section = Course.Sections.FirstOrDefault(s => s.Id == sectionId) ?? Course.Sections.FirstOrDefault();
            if (section is null) return;
            var module = section.Modules.FirstOrDefault(m => m.Id == moduleId);
            if (module is null) return;
            var contentItem = module.Content.FirstOrDefault(c => c.Id == contentId);
            if (contentItem is null) return;
            module.Content.Remove(contentItem);
            LoadCourseAndBuildUI();
            return;
        }

        using var db = new LMSDbContext();
        var content = db.Pages.FirstOrDefault(p => p.Id == contentId);
        if (content is null) return;
        db.Pages.Remove(content);
        await db.SaveChangesAsync();
        LoadCourseAndBuildUI();
    }

    private void OnRefreshModulesClicked(object sender, EventArgs e)
    {
        LoadCourseAndBuildUI();
    }

    private async void OnAddSectionClicked(object sender, EventArgs e)
    {
        // Everything hangs off a section and some seeded courses have none
        // (English), so there has to be a way to add one.
        var isCreate = string.IsNullOrWhiteSpace(CourseId) || string.IsNullOrWhiteSpace(Course?.Id);
        if (isCreate)
        {
            Course.Sections ??= new List<Section>();
            var nextNum = Course.Sections.Any() ? Course.Sections.Max(s => s.SectionNumber) + 1 : 1;
            Course.Sections.Add(new Section { SectionNumber = nextNum, Modules = new List<Module>(), Assignments = new List<Assignment>() });
            LoadCourseAndBuildUI();
            return;
        }

        using var db = new LMSDbContext();
        var course = db.Courses.Include(c => c.Sections).FirstOrDefault(c => c.Id == CourseId);
        if (course is null) return;
        var next = course.Sections.Any() ? course.Sections.Max(s => s.SectionNumber) + 1 : 1;
        course.Sections.Add(new Section { SectionNumber = next });
        await db.SaveChangesAsync();
        LoadCourseAndBuildUI();
    }

    private async Task DeleteSectionAsync(Section section)
    {
        // Unsaved sections all have Id 0, so remove the instance not the id.
        var isCreate = string.IsNullOrWhiteSpace(CourseId) || string.IsNullOrWhiteSpace(Course?.Id);
        if (isCreate)
        {
            Course.Sections.Remove(section);
            LoadCourseAndBuildUI();
            return;
        }

        using var db = new LMSDbContext();
        var dbSection = db.Sections
            .Include(s => s.Modules).ThenInclude(m => m.Content)
            .Include(s => s.Assignments).ThenInclude(a => a.Submissions)
            .FirstOrDefault(s => s.Id == section.Id);
        if (dbSection is null) return;

        // Delete the children too so nothing gets orphaned.
        foreach (var m in dbSection.Modules.ToList())
        {
            db.Modules.Remove(m);
        }
        foreach (var a in dbSection.Assignments.ToList())
        {
            if (a.Submissions is not null)
                db.Submissions.RemoveRange(a.Submissions);
            db.Assignments.Remove(a);
        }
        db.Sections.Remove(dbSection);
        await db.SaveChangesAsync();
        LoadCourseAndBuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Load into the page's Course field so the helpers see the same one.
        Course = await _courseService.GetCourseAsync(CourseId) ?? Course;
        BindingContext = Course;
        var letterGrade = await _gradeService.GetLetterGradeAsync(CourseId, StudentId);
        var gradeLabel = this.FindByName<Label>("GradeLabel");
        if (gradeLabel is not null) gradeLabel.Text = letterGrade ?? "Not yet graded";
        await RefreshRosterAsync();
    }

    // Keep the whole roster so the search box can filter without re-querying.
    private List<Student> _rosterAll = new List<Student>();

    private async Task RefreshRosterAsync()
    {
        var isCreate = string.IsNullOrWhiteSpace(CourseId) || string.IsNullOrWhiteSpace(Course?.Id);
        if (isCreate)
        {
            _rosterAll = Course?.Roster?.ToList() ?? new List<Student>();
        }
        else
        {
            _rosterAll = (await _courseService.GetRosterAsync(CourseId)).ToList();
        }

        ApplyRosterFilter();
    }

    private void ApplyRosterFilter()
    {
        var query = RosterSearchBar?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            RosterList.ItemsSource = _rosterAll;
            return;
        }

        RosterList.ItemsSource = _rosterAll
            .Where(s => (s.Name ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase)
                     || (s.Code ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase)
                     || (s.Id ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private void OnRosterSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyRosterFilter();
    }

    private async void OnAddStudentClicked(object sender, EventArgs e)
    {
        var all = (await _studentService.GetStudentsAsync()).ToList();
        var isCreate = string.IsNullOrWhiteSpace(CourseId) || string.IsNullOrWhiteSpace(Course?.Id);
        List<Student> current;
        if (isCreate)
        {
            current = Course.Roster?.ToList() ?? new List<Student>();
        }
        else
        {
            current = (await _courseService.GetRosterAsync(CourseId)).ToList();
        }
        var candidates = all.Where(s => !current.Any(r => r.Id == s.Id)).ToList();
        if (candidates.Count == 0)
        {
            await DisplayAlert("Add Student", "No available students to add.", "OK");
            return;
        }

        var display = candidates.Select(s => $"{s.Name} ({s.Code})").ToArray();
        var choice = await DisplayActionSheet("Add student", "Cancel", null, display);
        if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel") return;

        var index = Array.IndexOf(display, choice);
        if (index < 0) return;

        var selected = candidates[index];
        if (isCreate)
        {
            // Persist the new course first so roster changes survive navigation
            var newId = await _courseService.CreateCourseAsync(Course);
            CourseId = newId;
            Course.Id = newId;
            // Now add the selected student to the persisted course
            await _courseService.AddStudentToCourseAsync(CourseId, selected.Id);
            await RefreshRosterAsync();
        }
        else
        {
            await _courseService.AddStudentToCourseAsync(CourseId, selected.Id);
            await RefreshRosterAsync();
        }
    }

    private async void OnRemoveStudentClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not string studentId) return;
        var isCreate = string.IsNullOrWhiteSpace(CourseId) || string.IsNullOrWhiteSpace(Course?.Id);
        var ok = await DisplayAlert("Remove", isCreate ? "Remove this student from the new course?" : "Remove this student from course? This will also remove the student from all courses and delete their submissions and grades.", "Yes", "No");
        if (!ok) return;

        if (isCreate)
        {
            var existing = Course.Roster.FirstOrDefault(s => s.Id == studentId);
            if (existing is not null)
            {
                Course.Roster.Remove(existing);
            }
            await RefreshRosterAsync();
        }
        else
        {
            await _courseService.RemoveStudentFromCourseAsync(CourseId, studentId);
            await RefreshRosterAsync();
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

    private async void OnExportRosterClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CourseId))
        {
            await DisplayAlert("Export", "Course not loaded.", "OK");
            return;
        }

        try
        {
            var csv = await _courseService.ExportRosterCsvAsync(CourseId);
            if (string.IsNullOrWhiteSpace(csv))
            {
                await DisplayAlert("Export", "No students to export.", "OK");
                return;
            }

            await Clipboard.SetTextAsync(csv);
            await DisplayAlert("Export", "Roster CSV copied to clipboard. You can paste it into a text editor and save as .csv.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Export failed", ex.Message, "OK");
        }
    }

    private async void OnImportRosterClicked(object sender, EventArgs e)
    {
        var isCreate = string.IsNullOrWhiteSpace(CourseId) || string.IsNullOrWhiteSpace(Course?.Id);
        try
        {
            // Try reading CSV from clipboard first
            var clipboardText = await Clipboard.GetTextAsync();
            string csv = clipboardText;

            if (string.IsNullOrWhiteSpace(csv))
            {
                // Ask user to paste CSV manually
                csv = await DisplayPromptAsync("Import roster", "Paste CSV content here (Id,Name,Code).", maxLength: 20000);
                if (string.IsNullOrWhiteSpace(csv))
                {
                    await DisplayAlert("Import", "No CSV provided.", "OK");
                    return;
                }
            }

            if (isCreate)
            {
                // Persist the new course so imports are associated with a real course Id
                var newId = await _courseService.CreateCourseAsync(Course);
                CourseId = newId;
                Course.Id = newId;
            }

            var added = await _courseService.ImportRosterCsvAsync(CourseId, csv);
            await DisplayAlert("Import", $"Imported {added} new roster association(s).", "OK");
            await RefreshRosterAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Import failed", ex.Message, "OK");
        }
    }

    // Grade ranges
    private List<GradeRange> _editRanges = new List<GradeRange>();

    private async void OnToggleGradeRangesClicked(object sender, EventArgs e)
    {
        var panel = this.FindByName<Border>("GradeRangesPanel");
        if (panel is null) return;

        if (!panel.IsVisible)
        {
            // saved course -> service, new course -> memory, else default
            var isCreate = string.IsNullOrWhiteSpace(CourseId) || string.IsNullOrWhiteSpace(Course?.Id);
            if (isCreate)
            {
                _editRanges = (Course?.GradeRanges is { Count: > 0 } inMemory)
                    ? inMemory.Select(x => new GradeRange { Letter = x.Letter, MinPercent = x.MinPercent, Color = x.Color }).ToList()
                    : GradeScale.Default();
            }
            else
            {
                var ranges = await _courseService.GetGradeRangesAsync(CourseId);
                _editRanges = ranges.Select(x => new GradeRange { Letter = x.Letter, MinPercent = x.MinPercent, Color = x.Color }).ToList();
            }
            BuildGradeRangesUI();
        }

        panel.IsVisible = !panel.IsVisible;

        // Scroll to the top so the highest band shows first.
        if (panel.IsVisible)
        {
            var scroll = this.FindByName<ScrollView>("GradeRangesScroll");
            if (scroll is not null)
            {
                await Dispatcher.DispatchAsync(async () => await scroll.ScrollToAsync(0, 0, false));
            }
        }
    }

    private void BuildGradeRangesUI()
    {
        var container = this.FindByName<VerticalStackLayout>("GradeRangesContainer");
        if (container is null) return;
        container.Children.Clear();

        foreach (var range in _editRanges.OrderByDescending(r => r.MinPercent).ToList())
        {
            var row = new HorizontalStackLayout { Spacing = 8 };

            // swatch + tinted letter so you can see the color
            var letterColor = TryParseColor(range.Color);
            var swatch = new Border
            {
                WidthRequest = 18,
                HeightRequest = 18,
                BackgroundColor = letterColor ?? Colors.Transparent,
                Stroke = Colors.Gray,
                StrokeThickness = 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
                VerticalOptions = LayoutOptions.Center
            };
            row.Children.Add(swatch);

            var letterLabel = new Label { Text = range.Letter, FontAttributes = FontAttributes.Bold, WidthRequest = 44, VerticalOptions = LayoutOptions.Center };
            if (letterColor is not null) letterLabel.TextColor = letterColor;
            row.Children.Add(letterLabel);
            row.Children.Add(new Label { Text = $"{range.MinPercent:0.#}% and up", WidthRequest = 90, VerticalOptions = LayoutOptions.Center });

            var editBtn = new Button { Text = "Edit", FontSize = 12 };
            editBtn.Clicked += async (_, __) =>
            {
                var letter = await DisplayPromptAsync("Edit Range", "Letter:", initialValue: range.Letter);
                if (string.IsNullOrWhiteSpace(letter)) return;
                var minStr = await DisplayPromptAsync("Edit Range", "Minimum percent:", initialValue: range.MinPercent.ToString("0.#"), keyboard: Keyboard.Numeric);
                if (!double.TryParse(minStr, out var min)) return;
                var colorStr = await DisplayPromptAsync("Edit Range", "Hex color (e.g. #2E7D32, blank for none):", initialValue: range.Color ?? string.Empty);
                range.Letter = letter.Trim();
                range.MinPercent = min;
                range.Color = NormalizeColor(colorStr);
                BuildGradeRangesUI();
            };
            row.Children.Add(editBtn);

            var deleteBtn = new Button { Text = "Delete", FontSize = 12, BackgroundColor = Colors.LightCoral };
            deleteBtn.Clicked += (_, __) =>
            {
                _editRanges.Remove(range);
                BuildGradeRangesUI();
            };
            row.Children.Add(deleteBtn);

            container.Children.Add(row);
        }
    }

    private async void OnAddGradeRangeClicked(object sender, EventArgs e)
    {
        var letter = await DisplayPromptAsync("Add Range", "Letter (e.g. A, B+, Pass):");
        if (string.IsNullOrWhiteSpace(letter)) return;
        var minStr = await DisplayPromptAsync("Add Range", "Minimum percent:", initialValue: "0", keyboard: Keyboard.Numeric);
        if (!double.TryParse(minStr, out var min)) return;
        var colorStr = await DisplayPromptAsync("Add Range", "Hex color (e.g. #2E7D32, blank for none):");
        _editRanges.Add(new GradeRange { Letter = letter.Trim(), MinPercent = min, Color = NormalizeColor(colorStr) });
        BuildGradeRangesUI();
    }

    // Hex string to Color, null if unset or bad.
    private static Color? TryParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        try { return Color.FromArgb(hex.Trim()); }
        catch { return null; }
    }

    // Tidies typed input into "#RRGGBB", null if it won't parse.
    private static string? NormalizeColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        var t = hex.Trim();
        if (!t.StartsWith("#")) t = "#" + t;
        return TryParseColor(t) is null ? null : t;
    }

    private void OnResetGradeRangesClicked(object sender, EventArgs e)
    {
        _editRanges = GradeScale.Default();
        BuildGradeRangesUI();
    }

    private async void OnSaveGradeRangesClicked(object sender, EventArgs e)
    {
        var toSave = _editRanges.Where(r => !string.IsNullOrWhiteSpace(r.Letter)).ToList();
        var isCreate = string.IsNullOrWhiteSpace(CourseId) || string.IsNullOrWhiteSpace(Course?.Id);
        if (isCreate)
        {
            Course.GradeRanges = toSave.Select(r => new GradeRange { Letter = r.Letter, MinPercent = r.MinPercent, Color = r.Color }).ToList();
            await DisplayAlert("Grade ranges", "Saved to the new course (persisted when the course is saved).", "OK");
            return;
        }

        try
        {
            await _courseService.SaveGradeRangesAsync(CourseId, toSave);
            await DisplayAlert("Grade ranges", "Saved.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Save failed", ex.Message, "OK");
        }
    }

    // Assignment import / export
    private async void OnExportAssignmentsClicked(object sender, EventArgs e)
    {
        try
        {
            var isCreate = string.IsNullOrWhiteSpace(CourseId) || string.IsNullOrWhiteSpace(Course?.Id);
            string json;
            if (isCreate)
            {
                var transfer = (Course?.Sections ?? new List<Section>())
                    .SelectMany(s => s.Assignments)
                    .Select(a => new AssignmentTransfer { Name = a.Name, Description = a.Description, AvailablePoints = a.AvailablePoints, DueDate = a.DueDate, IsQuiz = a.IsQuiz, Question = a.Question, Choices = a.Choices, CorrectChoiceIndex = a.CorrectChoiceIndex })
                    .ToList();
                json = System.Text.Json.JsonSerializer.Serialize(transfer, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                json = await _courseService.ExportAssignmentsAsync(CourseId);
            }

            if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]")
            {
                await DisplayAlert("Export", "No assignments to export.", "OK");
                return;
            }

            await Clipboard.SetTextAsync(json);
            await DisplayAlert("Export", "Assignments JSON copied to clipboard. Paste it into a text editor to save a backup. To copy assignments into another course, use that course's Import Assignments.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Export failed", ex.Message, "OK");
        }
    }

    private async void OnImportAssignmentsClicked(object sender, EventArgs e)
    {
        try
        {
            // need a saved course to import into
            var isCreate = string.IsNullOrWhiteSpace(CourseId) || string.IsNullOrWhiteSpace(Course?.Id);
            if (isCreate)
            {
                var newId = await _courseService.CreateCourseAsync(Course);
                CourseId = newId;
                Course.Id = newId;
            }

            using var db = new LMSDbContext();

            // 1) pick the source course
            var sources = db.Courses
                .Include(c => c.Sections)
                    .ThenInclude(s => s.Assignments)
                .Where(c => c.Id != CourseId)
                .ToList();
            if (!sources.Any())
            {
                await DisplayAlert("Import Assignments", "No other courses to import from.", "OK");
                return;
            }

            var sourceNames = sources.Select(c => c.Name).ToArray();
            var sourceChoice = await DisplayActionSheet("Import assignments from which course?", "Cancel", null, sourceNames);
            if (string.IsNullOrWhiteSpace(sourceChoice) || sourceChoice == "Cancel") return;
            var source = sources[Array.IndexOf(sourceNames, sourceChoice)];

            var sourceAssignments = source.Sections.SelectMany(s => s.Assignments).ToList();
            if (!sourceAssignments.Any())
            {
                await DisplayAlert("Import Assignments", $"'{source.Name}' has no assignments.", "OK");
                return;
            }

            // 2) all of them or just one
            var allLabel = $"All assignments ({sourceAssignments.Count})";
            var options = new List<string> { allLabel };
            options.AddRange(sourceAssignments.Select(a => a.Name));
            var pick = await DisplayActionSheet("Import which assignments?", "Cancel", null, options.ToArray());
            if (string.IsNullOrWhiteSpace(pick) || pick == "Cancel") return;

            var toImport = pick == allLabel
                ? sourceAssignments
                : sourceAssignments.Where(a => a.Name == pick).ToList();

            // 3) copy them into the first section, without submissions
            var dest = db.Courses
                .Include(c => c.Sections)
                    .ThenInclude(s => s.Assignments)
                .FirstOrDefault(c => c.Id == CourseId);
            if (dest is null)
            {
                await DisplayAlert("Import Assignments", "Destination course not found.", "OK");
                return;
            }

            var section = dest.Sections.FirstOrDefault();
            if (section is null)
            {
                section = new Section { SectionNumber = 1 };
                dest.Sections.Add(section);
            }

            var added = 0;
            foreach (var a in toImport)
            {
                section.Assignments.Add(new Assignment
                {
                    Name = a.Name,
                    Description = a.Description,
                    AvailablePoints = a.AvailablePoints,
                    DueDate = a.DueDate,
                    IsQuiz = a.IsQuiz,
                    Question = a.Question,
                    Choices = a.Choices,
                    CorrectChoiceIndex = a.CorrectChoiceIndex
                });
                added++;
            }
            await db.SaveChangesAsync();

            await DisplayAlert("Import Assignments", $"Imported {added} assignment(s) from '{source.Name}'.", "OK");
            LoadCourseAndBuildUI();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Import failed", ex.Message, "OK");
        }
    }

    // Gradebook export
    private async void OnExportGradebookClicked(object sender, EventArgs e)
    {
        var isCreate = string.IsNullOrWhiteSpace(CourseId) || string.IsNullOrWhiteSpace(Course?.Id);
        if (isCreate)
        {
            await DisplayAlert("Gradebook", "Save the course first (add a student), then export the gradebook.", "OK");
            return;
        }

        try
        {
            var csv = await _courseService.ExportGradebookCsvAsync(CourseId);
            if (string.IsNullOrWhiteSpace(csv))
            {
                await DisplayAlert("Gradebook", "Nothing to export.", "OK");
                return;
            }

            await Clipboard.SetTextAsync(csv);
            await DisplayAlert("Gradebook", "Gradebook CSV copied to clipboard. Paste it into a spreadsheet and save as .csv.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Export failed", ex.Message, "OK");
        }
    }
}
