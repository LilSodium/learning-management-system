namespace CLI.LMS
{
    public class Assignment 
    {
        public int Id {get; init;}

        public required string Name {get; set;}

        public string? Description {get; set;}

        public int AvailablePoints {get; set;}

        public required string DueDate {get; set;}

        // Quizzes show Question on the assignment page. Unset for normal work.
        public bool IsQuiz {get; set;}

        public string? Question {get; set;}

        // Options separated by newlines. If set, the quiz shows radio buttons
        // instead of a text box. CorrectChoiceIndex is 0-based.
        public string? Choices {get; set;}

        public int? CorrectChoiceIndex {get; set;}

        public List<Submission> Submissions {get; set;} = new List<Submission>();
    }
}