namespace CLI.LMS
{
    // logged to an outbox the teacher can read instead.
    public class EmailMessage
    {
        public int Id { get; set; }

        public required string To { get; set; }

        public required string Subject { get; set; }

        public required string Body { get; set; }

        public DateTime SentAt { get; set; }
    }
}
