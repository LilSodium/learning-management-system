namespace CLI.LMS
{
    public class Student : User
    {
        public Classification Classification {get; set;}

        // If blank the email service builds one from the student code.
        public string? Email { get; set; }

        // many-to-many, so a student can be on more than one roster
        public List<Course> Courses { get; set; } = new List<Course>();
    }
}