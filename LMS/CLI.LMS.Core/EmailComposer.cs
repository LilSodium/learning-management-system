namespace CLI.LMS
{
    // Builds the new-assignment email. Shared so both email services match.
    public static class EmailComposer
    {
        public static EmailMessage NewAssignment(Course course, Student student, string assignmentName, DateTime sentAt)
        {
            var to = string.IsNullOrWhiteSpace(student.Email)
                ? $"{(student.Code ?? student.Id ?? "student").Trim()}@students.cli-lms.edu"
                : student.Email!;

            return new EmailMessage
            {
                To = to,
                Subject = $"New assignment posted in {course.Name}",
                Body =
                    $"Hi {student.Name},\n\n" +
                    $"A new assignment \"{assignmentName}\" has been posted in " +
                    $"{course.Name} ({course.Code}). Open the course page to view it.\n\n" +
                    $"- {course.Name} staff",
                SentAt = sentAt
            };
        }
    }
}
