namespace CLI.LMS
{
    // Letter grade logic, shared by the grade services and gradebook export
    // so they all agree.
    public static class GradeScale
    {
        // Standard +/- scale, used when a course has no custom ranges.
        public static List<GradeRange> Default() => new List<GradeRange>
        {
            new GradeRange { Letter = "A+", MinPercent = 97 },
            new GradeRange { Letter = "A",  MinPercent = 93 },
            new GradeRange { Letter = "A-", MinPercent = 90 },
            new GradeRange { Letter = "B+", MinPercent = 87 },
            new GradeRange { Letter = "B",  MinPercent = 83 },
            new GradeRange { Letter = "B-", MinPercent = 80 },
            new GradeRange { Letter = "C+", MinPercent = 77 },
            new GradeRange { Letter = "C",  MinPercent = 73 },
            new GradeRange { Letter = "C-", MinPercent = 70 },
            new GradeRange { Letter = "D+", MinPercent = 67 },
            new GradeRange { Letter = "D",  MinPercent = 63 },
            new GradeRange { Letter = "D-", MinPercent = 60 },
            new GradeRange { Letter = "F",  MinPercent = 0 },
        };

        // Percent to letter. Takes the highest band the percent reaches.
        public static string ToLetter(double percentage, IEnumerable<GradeRange>? ranges)
        {
            var scale = (ranges is not null && ranges.Any()) ? ranges : Default();

            var match = scale
                .Where(r => percentage >= r.MinPercent)
                .OrderByDescending(r => r.MinPercent)
                .FirstOrDefault();

            return match?.Letter ?? "F";
        }
    }
}
