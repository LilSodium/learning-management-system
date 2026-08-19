namespace CLI.LMS
{
    public class Semester
    {
        public int Id { get; set; }

        public required string Name { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }
    }
}
