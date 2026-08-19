using System.Formats.Tar;
using CLI.LMS;

namespace CLI.LMS
{
    public abstract class ModuleContent
    {
        public int Id { get; set; }
        public abstract string DisplayName { get; }
    }

    public class Page : ModuleContent
    {
        public override string DisplayName => Text;
        public string Text { get; set; } = string.Empty;
    }

    public class ModuleFile : ModuleContent
    {
        public override string DisplayName => Name;
        public string Name { get; set; } = string.Empty;
        public void Open() { }
    }
}