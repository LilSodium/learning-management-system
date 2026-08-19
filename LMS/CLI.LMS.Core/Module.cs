namespace CLI.LMS
{
    public class Module
    {
        public required string Id {get; init;}

        public List<ModuleContent> Content {get; set;} = new List<ModuleContent>();
    }
}