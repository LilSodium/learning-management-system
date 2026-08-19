namespace CLI.LMS
{
    // One message in an assignment comment thread.
    // A thread is keyed by AssignmentId + StudentId, like Submission.
    public class AssignmentComment
    {
        public int Id { get; set; }

        public int AssignmentId { get; set; }

        public required string StudentId { get; set; }

        // "Instructor" or the student's name.
        public required string Author { get; set; }

        public bool IsInstructor { get; set; }

        public required string Text { get; set; }

        public DateTime PostedAt { get; set; }
    }
}
