namespace CLI.LMS
{
    // One letter band. Student earns Letter once they hit MinPercent.
    public class GradeRange
    {
        public int Id { get; set; }

        // FK to the course, set by EF.
        public string? CourseId { get; set; }

        public string Letter { get; set; } = string.Empty;

        public double MinPercent { get; set; }

        // Hex like "#2E7D32". Null = default text color.
        public string? Color { get; set; }
    }
}
