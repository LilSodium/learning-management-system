using CLI.LMS;


    namespace CLI.LMS
{
    public class Section
    {
        public required int SectionNumber {get; set;}
        public int Id { get; set; }
        public List<Module> Modules {get; set;} = new List<Module>();
        
        public List<Assignment> Assignments {get; set;}  = new List<Assignment>();
        
        public List<AssignmentGroup> AssignmentGroups { get; set; } = new List<AssignmentGroup>();
    }
}


