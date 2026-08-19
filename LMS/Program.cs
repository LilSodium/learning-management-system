using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Data.Common;
using System.Text.RegularExpressions;

namespace CLI.LMS
{
    public class Program
    {
        // Helpers

        public static string GetInput(string prompt)
        {
            Console.WriteLine(prompt);
            return Console.ReadLine() ?? "";
        }


        public static T SelectFromList<T>(List<T> items, Func<T, string> displayText)
        {
            if (items == null || items.Count == 0)
            {
                Console.WriteLine("(List Empty)");
                return default;
            }

            while (true)
            {
                for (int i = 0; i < items.Count; i++)
                    Console.WriteLine($"{i + 1}. {displayText(items[i])}");
                Console.WriteLine();

                if (int.TryParse(GetInput("Select an item:"), out int choice) &&
                    choice >= 1 && choice <= items.Count)
                    return items[choice - 1];

                Console.WriteLine("Invalid selection. Try again.\n");
            }
        }

        static void CopyCourse(Course original, List<Course> courses)
        {
            string term = GetInput("Enter term for copied course:");
            courses.Add(CloneCourse(original, term));
            Console.WriteLine($"{original.Name} copied");
        }

        // Deep copy of a course, nothing shared with the original.
        // The copied groups point at the copied assignments, not the old ones.
        public static Course CloneCourse(Course original, string newTerm) => new Course
        {
            Id          = Guid.NewGuid().ToString("N")[..5],
            Name        = original.Name + "  (Copy)",
            Code        = original.Code,
            Description = original.Description,
            Term        = newTerm,
            Roster      = new List<Student>(),
            Sections    = original.Sections.Select(CloneSection).ToList()
        };

        private static Section CloneSection(Section s)
        {
            // old assignment -> new one, so the groups can be repointed
            var map = new Dictionary<Assignment, Assignment>();
            var clonedAssignments = new List<Assignment>();
            foreach (Assignment a in s.Assignments)
            {
                var clone = new Assignment
                {
                    Name            = a.Name,
                    Description     = a.Description,
                    DueDate         = a.DueDate,
                    AvailablePoints = a.AvailablePoints,
                    Submissions     = new List<Submission>()
                };
                map[a] = clone;
                clonedAssignments.Add(clone);
            }

            var clonedGroups = s.AssignmentGroups.Select(g => new AssignmentGroup
            {
                Id     = Guid.NewGuid().ToString("N")[..5],
                Name   = g.Name,
                Weight = g.Weight,
                Assignments = g.Assignments
                    .Where(a => map.ContainsKey(a))
                    .Select(a => map[a])
                    .ToList()
            }).ToList();

            var clonedModules = s.Modules.Select(m => new Module
            {
                Id      = m.Id,
                Content = m.Content.Select(CloneContent).OfType<ModuleContent>().ToList()
            }).ToList();

            return new Section
            {
                SectionNumber    = s.SectionNumber,
                Modules          = clonedModules,
                AssignmentGroups = clonedGroups,
                Assignments      = clonedAssignments
            };
        }

        private static ModuleContent? CloneContent(ModuleContent c) => c switch
        {
            Page p       => new Page { Text = p.Text },
            ModuleFile f => new ModuleFile { Name = f.Name },
            _            => null
        };

        static void ManageList<T>(
            List<T> items,
            Func<T, string> displayText,
            string entityName,
            Action<List<T>> onCreate,
            Action<T> onEdit = null,
            (string Label, Action<T> Action)[] extras = null,
            Action<List<T>, T> onDelete = null)
        {
            while (true)
            {
                Console.WriteLine($"\n--- Manage {entityName}s ---");
                Console.WriteLine($"1. Add {entityName}");
                Console.WriteLine($"2. Edit {entityName}");
                Console.WriteLine($"3. Delete {entityName}");
                
                if (extras != null)
                for(int i = 0; i < extras.Length; i++)
                    Console.WriteLine($"{i + 4}.  {extras[i].Label}");

                Console.WriteLine("0. Back");

                if (!int.TryParse(Console.ReadLine(), out int choice))
                {
                    Console.WriteLine("Invalid input.");
                    continue;
                }

                switch (choice)
                {
                    case 0:
                        return;

                    case 1:
                        onCreate(items);
                        break;

                    case 2:
                        if (onEdit == null) { Console.WriteLine("Editing not available here."); break; }
                        T toEdit = SelectFromList(items, displayText);
                        if (toEdit != null) onEdit(toEdit);
                        break;

                    case 3:
                        T toDelete = SelectFromList(items, displayText);
                        if (toDelete != null)
                        {
                            // onDelete can also remove it from a master list
                            if (onDelete != null) onDelete(items, toDelete);
                            else items.Remove(toDelete);
                            Console.WriteLine($"{entityName} deleted.");
                        }
                        break;

                    default:
                        int extraIndex = choice - 4;
                        if (extras != null && extraIndex >= 0 && extraIndex < extras.Length)
                        {
                            T selected = SelectFromList(items, displayText);
                            if (selected != null) extras[extraIndex].Action(selected);
                        }
                        else Console.WriteLine("Invalid option.");
                        break;
                }
            }
        }

        // Generic Field Editor
        // Numbered list of things you can change on an item. Each field is a
        // label plus the action that edits it.

        static void EditFields<T>(T item, params (string Label, Action<T> Edit)[] fields)
        {
            for (int i = 0; i < fields.Length; i++)
                Console.WriteLine($"{i + 1}. {fields[i].Label}");

            if (!int.TryParse(Console.ReadLine(), out int choice) ||
                choice < 1 || choice > fields.Length)
            {
                Console.WriteLine("Invalid choice.");
                return;
            }

            fields[choice - 1].Edit(item);
        }

        // Course Management
        static void ShowTermMenu(List<Course> courses, List<Course> masterList = null)
        {
            List<Course> master = masterList ?? courses;
            while (true) {
            List<string> terms = courses
                .Select(c => c.Term)
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            Console.WriteLine("--- Select a Term ---");

            for (int i = 0; i < terms.Count; i++)
            {
                Console.WriteLine($"\n{i + 1}. {terms[i]}");
                foreach (Course c in courses.Where(c => c.Term == terms[i]))
                    Console.WriteLine($"   - {c.Name}");
            }

            Console.WriteLine("\n0. Back");

            if (!int.TryParse(Console.ReadLine(), out int input) || input == 0) return;
            if (input < 1 || input > terms.Count) { Console.WriteLine("Invalid."); return; }

            string selectedTerm = terms[input - 1];
            List<Course> filtered = courses.Where(c => c.Term == selectedTerm).ToList();

            ShowCourseMenu(filtered, courses);
            }
        }
        static void ShowCourseMenu(List<Course> courses, List<Course> masterList = null)
        {
            Console.WriteLine("Welcome, Teacher.");
            List<Course> master = masterList ?? courses;


            ManageList(
                courses,
                c => c.Name,
                "Course",
                onCreate: list =>
                {   
                    Random rand = new Random();
                    Course newCourse = new Course
                    {
                        Name        = GetInput("Enter course name:"),
                        Code        = GetInput("Enter course code:"),
                        Description = GetInput("Enter course description:"),
                        Term        = GetInput("Enter term:"),
                        Id          = rand.Next().ToString().Substring(0, 5)
                    };
                    master.Add(newCourse); 
                    list.Add(newCourse);   
                    Console.WriteLine("Course added.");
                },
                onEdit: course =>
                {
                    Console.WriteLine($"\n--- Editing: {course.Name} ---");
                    Console.WriteLine("1. Manage Sections");
                    Console.WriteLine("2. Unenroll Student");
                    Console.WriteLine("3. Change Description");
                    Console.WriteLine("0. Back");

                    if (!int.TryParse(Console.ReadLine(), out int choice))
                    {
                        Console.WriteLine("Invalid input.");
                        return;
                    }

                    switch (choice)
                    {
                        case 1: ManageSections(course); break;
                        case 2:
                            // ManageList again, but only Delete applies so onEdit is null
                            ManageList(
                                course.Roster,
                                s => s.Name,
                                "Student",
                                onCreate: _ => Console.WriteLine("Students must be enrolled separately."));
                            break;
                        case 3:
                            course.Description = GetInput("New description:");
                            Console.WriteLine("Description updated.");
                            break;
                        case 0:
                            break;
                        default:
                            Console.WriteLine("Invalid option.");
                            break;
                    }
                },
                extras: new[] {("Copy Course", (Action<Course>)(c => CopyCourse(c, master))) },
                // this list might be filtered by term, so delete from the
                // master list too or it comes back
                onDelete: (list, c) => { master.Remove(c); list.Remove(c); });
        }

        static void ManageAssignmentsGroup(Section section)
        {
            ManageList(
                section.AssignmentGroups,
                g => g.Name,
                "Assignment Group",
                onCreate: list =>
                {
                    Random rand = new Random();
                    string id = rand.Next().ToString().Substring(0, 5);
                    list.Add(new AssignmentGroup
                    {
                        Name        = GetInput("Enter Assignment Group name:"),
                        Weight      = Convert.ToInt32(GetInput("Enter Group Weight")),
                        Id          =  id
                    });
                    Console.WriteLine("Assignment Group added.");
                },
                onEdit: assignmentGroup => 
                {
                    Console.WriteLine($"\n--- Editing: Assignment Group {assignmentGroup.Name} ---");
                    Console.WriteLine("1. Add Assignment To Group");
                    Console.WriteLine("2. Remove Assignment from Group");
                    Console.WriteLine("3. Change Group Name");
                    Console.WriteLine("0. Back");

                    if (!int.TryParse(Console.ReadLine(), out int choice))
                    {
                        Console.WriteLine("Invalid input.");
                        return;
                    }

                    switch (choice)
                    {
                        case 1:
                            Assignment selected = SelectFromList(section.Assignments, a => a.Name);
                            if(selected != null)
                            {
                                assignmentGroup.Assignments.Add(selected);
                                Console.WriteLine($"{selected.Name} added to {assignmentGroup.Name}.");
                            }
                             break;
                        case 2:
                            Assignment toRemove = SelectFromList(assignmentGroup.Assignments, a => a.Name);
                            if(toRemove != null)
                            {
                                assignmentGroup.Assignments.Remove(toRemove);
                                Console.WriteLine($"{toRemove.Name} removed from {assignmentGroup.Name}.");
                            }
                            break;
                        case 3: 
                            string newName = GetInput("Enter a new group name:");
                            assignmentGroup.Name = newName;
                            Console.WriteLine($"Group name changed to {assignmentGroup.Name}");
                            break;
                        case 0: break;
                    }

                }
                
            );
        }

        static void ManageSections(Course course)
        {
            ManageList(
                course.Sections,
                s =>  s.SectionNumber.ToString(),
                "Section",
                onCreate: list =>
                {
                    int newNumber = list.Count + 1;
                    list.Add(new Section
                    {
                        SectionNumber = newNumber,
                        Modules = new List<Module>(),
                        Assignments = new List<Assignment>()
                    });
                    Console.WriteLine($"New Section: {newNumber} added");               
                },
                onEdit: section =>
                {
                    Console.WriteLine($"\n--- Editing: Section {section.SectionNumber} ---");
                    Console.WriteLine("1. Manage Modules");
                    Console.WriteLine("2. Manage Assignments");
                    Console.WriteLine("3. Manage Assignments Groups");
                    Console.WriteLine("0. Back");

                    if (!int.TryParse(Console.ReadLine(), out int choice))
                    {
                        Console.WriteLine("Invalid input.");
                        return;
                    }
                    
                    switch(choice)
                    {
                        case 1: 
                             ManageModules(section.Modules); break;
                        case 2: 
                            ManageAssignments(section.Assignments); break;
                        case 3:
                            ManageAssignmentsGroup(section); break;
                        case 0:
                            break;
                        default:
                            Console.WriteLine("Invalid Option");
                            break;
                    }
                
                });
        }

        // Module Management
        static void ManageModules(List<Module> modules)
        {
            ManageList(
                modules,
                m => m.Id,
                "Module",
                onCreate: list =>
                {
                    string id = GetInput("Enter module ID:");
                    list.Add(new Module { Id = id, Content = new List<ModuleContent>() });
                    Console.WriteLine($"Module '{id}' added.");
                },
                onEdit: module => ManageContent(module));  // drill into content management
        }

        static void ManageContent(Module module)
        {
            ManageList(
                module.Content,
                c => c.DisplayName,
                "Content",
                onCreate: list =>
                {
                    Console.WriteLine("Enter content lines. Empty line to finish:");
                    while (true)
                    {
                        string line = Console.ReadLine() ?? "";
                        if (line == "") break;
                        list.Add(new Page { Text = line });
                    }
                    Console.WriteLine("Content added.");
                },
                onEdit: content =>
                {
                    if (content is Page page)
                    {
                        page.Text = GetInput("New text:");
                        Console.WriteLine($"Updated: {page.Text}");
                    }
                    else if (content is ModuleFile file)
                    {
                        file.Name = GetInput("New file name:");
                        Console.WriteLine($"Updated: {file.Name}");
                    }
                });
        }

        // Assignment Management
        static void ManageAssignments(List<Assignment> assignments)
        {
            ManageList(
                assignments,
                a => a.Name,
                "Assignment",
                onCreate: list =>
                {
                    string name    = GetInput("Enter assignment name:");
                    string dueDate = GetInput("Enter due date:");
                    int points;
                    while (!int.TryParse(GetInput("Enter available points:"), out points))
                        Console.WriteLine("Invalid. Please enter a number.");

                    list.Add(new Assignment { Name = name, DueDate = dueDate, AvailablePoints = points });
                    Console.WriteLine($"Assignment '{name}' added.");
                },
                onEdit: a => EditFields(a,       
                    ("Change name",        x => { x.Name        = GetInput("New name:");        Console.WriteLine("Updated."); }),
                    ("Change description", x => { x.Description = GetInput("New description:"); Console.WriteLine("Updated."); }),
                    ("Change due date",    x => { x.DueDate     = GetInput("New due date:");    Console.WriteLine("Updated."); }),
                    ("Change points",      x =>
                    {
                        if (int.TryParse(GetInput("New points:"), out int p)) { x.AvailablePoints = p; Console.WriteLine("Updated."); }
                        else Console.WriteLine("Invalid number.");
                    }),
                    ("Grade a submission", x => GradeSubmission(x))));
        }

        static void GradeSubmission(Assignment assignment)
        {
            Submission selectedSubmission = SelectFromList(assignment.Submissions, s => s.StudentId);
            if (selectedSubmission == null) return;

            Console.WriteLine($"\n--- {selectedSubmission.StudentId}'s Submission ---");
            Console.WriteLine(selectedSubmission.Content);
            Console.WriteLine();

            Console.WriteLine("Grade by: 1. Total Points  2. Percentage");

            if (!int.TryParse(Console.ReadLine(), out int mode)) { Console.WriteLine("Invalid."); return; }

            if (mode == 1)
            {
                if (!int.TryParse(GetInput($"Enter points (out of {assignment.AvailablePoints}):"), out int points)
                    || points < 0 || points > assignment.AvailablePoints)
                {
                    Console.WriteLine("Invalid points.");
                    return;
                }
                selectedSubmission.Grade = points;
            }
            else if (mode == 2)
            {
                if (!int.TryParse(GetInput("Enter percentage (0-100):"), out int percent)
                    || percent < 0 || percent > 100)
                {
                    Console.WriteLine("Invalid percentage.");
                    return;
                }
                // Convert percentage to points
                selectedSubmission.Grade = (int)(percent / 100.0 * assignment.AvailablePoints);
            }
            else { Console.WriteLine("Invalid option."); return; }

            Console.WriteLine($"Grade set to {selectedSubmission.Grade}/{assignment.AvailablePoints}.");

            selectedSubmission.Comment = GetInput("Enter a comment:");
            Console.WriteLine("Comment saved.");
        }

        // Weighted Grade Calculation
        // One assignment group's contribution to a student's grade.
        public record GroupGrade(string Name, double Earned, double Possible)
        {
            public double Percent => Possible == 0 ? 0 : Earned / Possible * 100;
        }

        // Weighted grade like Canvas does it. Each group scores earned/possible,
        // then average those by weight. Only groups with graded work count, so
        // the weights don't have to add up to 100 and empty groups don't hurt.
        public static (double FinalPercent, List<GroupGrade> Groups) ComputeWeightedGrade(Course course, Student student)
        {
            var groups = new List<GroupGrade>();
            double weightedSum = 0;
            double totalWeight = 0;

            foreach (AssignmentGroup group in course.Sections.SelectMany(s => s.AssignmentGroups))
            {
                double earned = 0;
                double possible = 0;
                foreach (Assignment assignment in group.Assignments)
                {
                    Submission sub = assignment.Submissions.FirstOrDefault(s => s.StudentId == student.Id);
                    if (sub == null) continue;
                    earned   += sub.Grade;
                    possible += assignment.AvailablePoints;
                }

                if (possible == 0) continue;   // nothing graded in this group yet

                groups.Add(new GroupGrade(group.Name, earned, possible));
                weightedSum += (earned / possible) * group.Weight;
                totalWeight += group.Weight;
            }

            double finalPercent = totalWeight == 0 ? 0 : weightedSum / totalWeight * 100;
            return (finalPercent, groups);
        }

        // Student Menu
        static void ShowStudentMenu(List<Course> courses, Student student)
        {   
            Console.WriteLine("--- Enrolled Courses ---");
            Course selectedCourse = SelectFromList(courses, c => c.Name);
            if (selectedCourse == null) return;

            Console.WriteLine($"\n--- {selectedCourse.Name} ---");
            Console.WriteLine("1. Unenroll from course");
            Console.WriteLine("2. Submit an assignment");
            Console.WriteLine("3. View Grades");

            if (!int.TryParse(Console.ReadLine(), out int input))
            {
                Console.WriteLine("Invalid input.");
                return;
            }

            switch (input)
            {
                case 1:
                    selectedCourse.Roster.Remove(student);
                    Console.WriteLine($"{student.Name} unenrolled from {selectedCourse.Name}.");
                    break;

                case 2:
                    Section  selectedSection = SelectFromList(selectedCourse.Sections, s => s.SectionNumber.ToString());
                    Assignment selected = SelectFromList(selectedSection.Assignments, a => a.Name);
                    if (selected == null) return;

                    string content = GetInput($"Enter your submission for '{selected.Name}':");
                    Random rand = new Random();
                    string id = rand.Next().ToString().Substring(0, 5);
                    selected.Submissions.Add(new Submission
                    {
                        Id             = id,
                        Content        = content,
                        StudentId      = student.Id,
                        SubmissionDate = DateTime.Today.ToShortDateString()
                    });
                    Console.WriteLine("Submission uploaded.");
                    break;
                case 3:
                    var (finalPercent, groupGrades) = ComputeWeightedGrade(selectedCourse, student);
                    if (groupGrades.Count == 0)
                    {
                        Console.WriteLine("You have no graded Assignments");
                        return;
                    }

                    Console.WriteLine($"--- {selectedCourse.Code} Grades ---");
                    foreach (GroupGrade g in groupGrades)
                        Console.WriteLine($"-- {g.Name}: {g.Earned}/{g.Possible} ({g.Percent:0.#}%) --");

                    Console.WriteLine();
                    Console.WriteLine($"__ Weighted Final Grade: {finalPercent:0.#}% __");
                break;

                default:
                    Console.WriteLine("Invalid option.");
                    break;
            }
        }

        // Entry Point
        static void Main(string[] args)
        {
            List<Course> courses = SampleDataStore.Courses;
            List<Student> students = SampleDataStore.Students;

            while (true)
            {
                Console.WriteLine("\n1. Student  2. Teacher  0. Exit");

                if (!int.TryParse(Console.ReadLine(), out int input)) continue;

                switch (input)
                {
                    case 0: return;
                    case 1:
                        Console.WriteLine("Select who you are");
                        Student s = SelectFromList(students, st => st.Name);
                        List<Course> enrolledCourses = new List<Course>();
                        foreach (Course course in courses)
                        {
                            if(course.Roster.Contains(s))
                            {
                                enrolledCourses.Add(course);
                            }
                        }
                        if (s != null && enrolledCourses != null) ShowStudentMenu(enrolledCourses, s);
                        break;
                    case 2:
                        ShowTermMenu(courses);
                        break;
                    default:
                        Console.WriteLine("Invalid option.");
                        break;
                }
            }
        }
    }
}
