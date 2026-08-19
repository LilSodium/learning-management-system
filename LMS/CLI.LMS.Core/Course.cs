namespace CLI.LMS
{
    public class Course
    {
        public string? Id { get; set; }

        public required string  Term {get; set;}

        public required string Code {get; init;}

        public required string Name {get; set;}

        public required string Description {get; set;}

        public List<Student> Roster {get; set;} = new List<Student>();
        
        public List<Section> Sections {get; set;} = new List<Section>();

        // Announcement displayed to students on the course detail screen
        public string? Announcement { get; set; }

        // Empty means use GradeScale.Default().
        public List<GradeRange> GradeRanges { get; set; } = new List<GradeRange>();

    }
}