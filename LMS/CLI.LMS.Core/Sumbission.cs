namespace CLI.LMS
{
    public class Submission
    {
        public required string Id {get; init;}

        // FK to the assignment this submission belongs to
        public int AssignmentId { get; set; }

        public required string  StudentId {get; init;}

        public required string Content {get; set;}

        public required string SubmissionDate {get; set;}
        
        public int Grade {get; set;}

        public string Comment {get; set;} = "";

        // File attachment. FilePath is a copy in the app data folder.
        public string? FileName {get; set;}

        public string? FilePath {get; set;}
    }
} 