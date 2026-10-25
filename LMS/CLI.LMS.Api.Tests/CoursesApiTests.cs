using System.Net;
using System.Net.Http.Json;

namespace CLI.LMS.Api.Tests;

// Each test gets its own LmsApiFactory, so it starts from the sample data
// in SampleDataStore: COP3330 has 4 students and a section with assignments.
public class CoursesApiTests
{
    [Fact]
    public async Task GetAll_ReturnsSeededCourses()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        var courses = await client.GetFromJsonAsync<List<CourseDto>>("/api/courses");

        Assert.NotNull(courses);
        Assert.Equal(4, courses.Count);
        var oop = Assert.Single(courses, c => c.Id == "COP3330");
        Assert.Equal("Object-Oriented Programming", oop.Name);
        Assert.Equal(4, oop.RosterCount);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/courses/NOPE");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_ThenGet_ReturnsTheNewCourse()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();
        var dto = new CourseDto { Id = "CIS4385", Code = "CIS 4385", Name = "Cybercrime Detection and Forensics", Description = "Digital forensics", Term = "Fall 2026" };

        var response = await client.PostAsJsonAsync("/api/courses", dto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        // The route uses [controller], so the Location header says "Courses".
        Assert.EndsWith("/api/courses/CIS4385", response.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
        var fetched = await client.GetFromJsonAsync<CourseDto>("/api/courses/CIS4385");
        Assert.Equal("Cybercrime Detection and Forensics", fetched?.Name);
    }

    [Fact]
    public async Task Create_WithoutId_GeneratesOne()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/courses", new CourseDto { Code = "TMP 1000", Name = "Temp", Term = "Fall 2026" });
        var created = await response.Content.ReadFromJsonAsync<CourseDto>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(created?.Id));
    }

    [Fact]
    public async Task Create_WithoutName_Returns400()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/courses", new CourseDto { Id = "X1", Code = "X 1", Name = " " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateId_Returns409()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/courses", new CourseDto { Id = "COP3330", Code = "COP 3330", Name = "Copy" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_ChangesNameAndAnnouncement()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();
        var update = new CourseDto { Name = "OOP in C#", Description = "Updated", Term = "Fall 2026", Announcement = "Exam moved to Friday." };

        var response = await client.PutAsJsonAsync("/api/courses/COP3330", update);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var fetched = await client.GetFromJsonAsync<CourseDto>("/api/courses/COP3330");
        Assert.Equal("OOP in C#", fetched?.Name);
        Assert.Equal("Exam moved to Friday.", fetched?.Announcement);
        // Code is init-only, so an update leaves it alone.
        Assert.Equal("COP 3330", fetched?.Code);
    }

    [Fact]
    public async Task Update_UnknownId_Returns404()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/courses/NOPE", new CourseDto { Name = "x" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_CourseWithSectionsAndAssignments_RemovesIt()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/courses/COP3330");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/courses/COP3330")).StatusCode);
        // Students on the roster are not deleted with the course.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/students/S1001")).StatusCode);
    }

    [Fact]
    public async Task Delete_UnknownId_Returns404()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/courses/NOPE");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetRoster_ReturnsEnrolledStudents()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        var roster = await client.GetFromJsonAsync<List<StudentDto>>("/api/courses/COP3330/roster");

        Assert.NotNull(roster);
        Assert.Equal(new[] { "S1001", "S1002", "S1003", "S1008" }, roster.Select(s => s.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task GetRoster_UnknownCourse_Returns404()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/courses/NOPE/roster");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddToRoster_EnrollsTheStudent()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/courses/COP3330/roster/S1006", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var roster = await client.GetFromJsonAsync<List<StudentDto>>("/api/courses/COP3330/roster");
        Assert.Contains(roster!, s => s.Id == "S1006");
        Assert.Equal(5, roster!.Count);
    }

    [Fact]
    public async Task AddToRoster_AlreadyEnrolled_DoesNotDuplicate()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/courses/COP3330/roster/S1001", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var roster = await client.GetFromJsonAsync<List<StudentDto>>("/api/courses/COP3330/roster");
        Assert.Equal(4, roster!.Count);
    }

    [Theory]
    [InlineData("/api/courses/NOPE/roster/S1001")]
    [InlineData("/api/courses/COP3330/roster/NOPE")]
    public async Task AddToRoster_UnknownCourseOrStudent_Returns404(string url)
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync(url, null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RemoveFromRoster_UnenrollsTheStudent()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/courses/COP3330/roster/S1002");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var roster = await client.GetFromJsonAsync<List<StudentDto>>("/api/courses/COP3330/roster");
        Assert.DoesNotContain(roster!, s => s.Id == "S1002");
    }

    [Fact]
    public async Task RemoveFromRoster_StudentNotEnrolled_Returns404()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/courses/COP3330/roster/S1006");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Coursework_ReturnsModulesAndAssignmentsWithGradeStats()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        var work = await client.GetFromJsonAsync<CourseworkDto>("/api/courses/COP3330/coursework");

        Assert.NotNull(work);
        var module = Assert.Single(work.Modules);
        Assert.Contains(module.Items, i => i.Type == "file" && i.Name == "Lecture1-Classes.pdf");
        Assert.Equal(3, work.Assignments.Count);

        // Homework 1 has three submissions, all graded: 94, 81, 88.
        var hw1 = Assert.Single(work.Assignments, a => a.Name.StartsWith("Homework 1"));
        Assert.Equal(3, hw1.SubmissionCount);
        Assert.Equal(3, hw1.GradedCount);
        Assert.Equal(87.7, hw1.AverageGrade);

        // Homework 2 has one ungraded submission.
        var hw2 = Assert.Single(work.Assignments, a => a.Name.StartsWith("Homework 2"));
        Assert.Equal(1, hw2.SubmissionCount);
        Assert.Equal(0, hw2.GradedCount);
        Assert.Null(hw2.AverageGrade);

        Assert.Contains(work.Assignments, a => a.IsQuiz);
    }

    [Fact]
    public async Task Coursework_UnknownCourse_Returns404()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/courses/NOPE/coursework");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
