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
