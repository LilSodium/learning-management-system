using CLI.LMS;

namespace CLI.LMS.Api;

// Flat DTOs. Sending the EF objects straight out hits reference loops.

public class StudentDto
{
    public string? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public Classification Classification { get; set; }

    public static StudentDto From(Student s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Code = s.Code,
        Classification = s.Classification
    };
}

public class CourseDto
{
    public string? Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Term { get; set; } = string.Empty;
    public string? Announcement { get; set; }
    public int RosterCount { get; set; }

    public static CourseDto From(Course c) => new()
    {
        Id = c.Id,
        Code = c.Code,
        Name = c.Name,
        Description = c.Description,
        Term = c.Term,
        Announcement = c.Announcement,
        RosterCount = c.Roster?.Count ?? 0
    };
}

// Read-only view of a course's modules and assignments for the web client.
public class CourseworkDto
{
    public List<ModuleDto> Modules { get; set; } = new();
    public List<AssignmentDto> Assignments { get; set; } = new();
}

public class ModuleDto
{
    public string Id { get; set; } = string.Empty;
    public List<ModuleItemDto> Items { get; set; } = new();

    public static ModuleDto From(Module m) => new()
    {
        Id = m.Id,
        Items = m.Content.OrderBy(c => c.Id).Select(ModuleItemDto.From).ToList()
    };
}

public class ModuleItemDto
{
    // "page" or "file"
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public static ModuleItemDto From(ModuleContent c) => c switch
    {
        ModuleFile f => new() { Type = "file", Name = f.Name },
        Page p => new() { Type = "page", Name = p.Text },
        _ => new() { Type = "page", Name = string.Empty }
    };
}

public class AssignmentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DueDate { get; set; } = string.Empty;
    public int AvailablePoints { get; set; }
    public bool IsQuiz { get; set; }
    public int SubmissionCount { get; set; }
    public int GradedCount { get; set; }
    // Mean of graded submissions, null until something is graded.
    public double? AverageGrade { get; set; }

    public static AssignmentDto From(Assignment a)
    {
        // A Grade of 0 means not graded yet, same rule as the desktop app.
        var graded = a.Submissions.Where(s => s.Grade > 0).ToList();
        return new()
        {
            Id = a.Id,
            Name = a.Name,
            Description = a.Description,
            DueDate = a.DueDate,
            AvailablePoints = a.AvailablePoints,
            IsQuiz = a.IsQuiz,
            SubmissionCount = a.Submissions.Count,
            GradedCount = graded.Count,
            AverageGrade = graded.Count > 0 ? Math.Round(graded.Average(s => s.Grade), 1) : null
        };
    }
}
