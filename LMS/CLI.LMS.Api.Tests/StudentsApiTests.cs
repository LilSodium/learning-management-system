using System.Net;
using System.Net.Http.Json;

namespace CLI.LMS.Api.Tests;

public class StudentsApiTests
{
    [Fact]
    public async Task GetAll_ReturnsSeededStudents()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        var students = await client.GetFromJsonAsync<List<StudentDto>>("/api/students");

        Assert.Equal(8, students?.Count);
    }

    [Fact]
    public async Task Get_KnownId_ReturnsTheStudent()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        var student = await client.GetFromJsonAsync<StudentDto>("/api/students/S1001");

        Assert.Equal("Maya Chen", student?.Name);
        Assert.Equal(Classification.Freshman, student?.Classification);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/students/NOPE");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_ThenGet_ReturnsTheNewStudent()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();
        var dto = new StudentDto { Id = "S2001", Code = "S2001", Name = "Riley Adams", Classification = Classification.Junior };

        var response = await client.PostAsJsonAsync("/api/students", dto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var fetched = await client.GetFromJsonAsync<StudentDto>("/api/students/S2001");
        Assert.Equal("Riley Adams", fetched?.Name);
        Assert.Equal(Classification.Junior, fetched?.Classification);
    }

    [Fact]
    public async Task Create_WithoutName_Returns400()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/students", new StudentDto { Id = "S2002", Code = "S2002", Name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateId_Returns409()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/students", new StudentDto { Id = "S1001", Code = "S1001", Name = "Someone Else" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_ChangesNameAndClassification()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();
        var update = new StudentDto { Name = "Maya Chen-Lopez", Code = "S1001", Classification = Classification.Sophomore };

        var response = await client.PutAsJsonAsync("/api/students/S1001", update);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var fetched = await client.GetFromJsonAsync<StudentDto>("/api/students/S1001");
        Assert.Equal("Maya Chen-Lopez", fetched?.Name);
        Assert.Equal(Classification.Sophomore, fetched?.Classification);
    }

    [Fact]
    public async Task Update_UnknownId_Returns404()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/students/NOPE", new StudentDto { Name = "x" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesStudentFromEveryRoster()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        // S1003 (Priya) is on both COP 3330 and COP 4530.
        var response = await client.DeleteAsync("/api/students/S1003");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/students/S1003")).StatusCode);
        foreach (var courseId in new[] { "COP3330", "COP4530" })
        {
            var roster = await client.GetFromJsonAsync<List<StudentDto>>($"/api/courses/{courseId}/roster");
            Assert.DoesNotContain(roster!, s => s.Id == "S1003");
        }
    }

    [Fact]
    public async Task Delete_StudentWithSubmissions_Succeeds()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        // S1001 (Maya) has graded and ungraded submissions in COP 3330.
        var response = await client.DeleteAsync("/api/students/S1001");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_UnknownId_Returns404()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/students/NOPE");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
