namespace CLI.LMS
{
    // JSON shape for import/export between courses.
    // No Ids or submissions, so imports come in clean.
    public class AssignmentTransfer
    {
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int AvailablePoints { get; set; }

        public string DueDate { get; set; } = string.Empty;

        public bool IsQuiz { get; set; }

        public string? Question { get; set; }

        // Keeps multiple-choice quizzes intact through import/export.
        public string? Choices { get; set; }

        public int? CorrectChoiceIndex { get; set; }
    }
}
