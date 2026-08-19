namespace CLI.LMS;

public class AssignmentGroup
{
    public string Id {get; set;}
    public string Name {get; set;}
    public double Weight { get; set; }
    public List<Assignment> Assignments { get; set; } = new List<Assignment>();
}