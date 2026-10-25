using CLI.LMS;
using CLI.LMS.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CLI.LMS.Api.Controllers;

// Course CRUD plus roster membership.
[ApiController]
[Route("api/[controller]")]
public class CoursesController : ControllerBase
{
    private readonly ApiDbContext _db;

    public CoursesController(ApiDbContext db)
    {
        _db = db;
    }

    // GET /api/courses
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CourseDto>>> GetAll()
    {
        var courses = await _db.Courses.Include(c => c.Roster).AsNoTracking().ToListAsync();
        return Ok(courses.Select(CourseDto.From));
    }

    // GET /api/courses/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<CourseDto>> Get(string id)
    {
        var course = await _db.Courses.Include(c => c.Roster).AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
        if (course is null) return NotFound();
        return Ok(CourseDto.From(course));
    }

    // POST /api/courses
    [HttpPost]
    public async Task<ActionResult<CourseDto>> Create(CourseDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("Name is required.");

        var id = string.IsNullOrWhiteSpace(dto.Id) ? Guid.NewGuid().ToString() : dto.Id;
        if (await _db.Courses.AnyAsync(c => c.Id == id))
            return Conflict($"A course with id '{id}' already exists.");

        var course = new Course
        {
            Id = id,
            Code = dto.Code,
            Name = dto.Name,
            Description = dto.Description,
            Term = dto.Term,
            Announcement = dto.Announcement
        };
        _db.Courses.Add(course);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = course.Id }, CourseDto.From(course));
    }

    // PUT /api/courses/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, CourseDto dto)
    {
        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == id);
        if (course is null) return NotFound();

        // Code and Id are init-only so they can't be updated.
        course.Name = dto.Name;
        course.Description = dto.Description;
        course.Term = dto.Term;
        course.Announcement = dto.Announcement;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // DELETE /api/courses/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        // Same cleanup as the desktop app. Sections and GradeRanges use a
        // nullable FK, so EF won't cascade them and SQLite rejects the delete
        // with "FOREIGN KEY constraint failed" unless they go first.
        var course = await _db.Courses
            .Include(c => c.Roster)
            .Include(c => c.GradeRanges)
            .Include(c => c.Sections).ThenInclude(s => s.Modules).ThenInclude(m => m.Content)
            .Include(c => c.Sections).ThenInclude(s => s.Assignments)
            .Include(c => c.Sections).ThenInclude(s => s.AssignmentGroups)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (course is null) return NotFound();

        // Submissions are keyed by AssignmentId, not by a navigation.
        var assignmentIds = course.Sections.SelectMany(s => s.Assignments).Select(a => a.Id).ToList();
        if (assignmentIds.Count > 0)
            _db.Submissions.RemoveRange(_db.Submissions.Where(s => assignmentIds.Contains(s.AssignmentId)));

        foreach (var section in course.Sections)
        {
            foreach (var module in section.Modules)
                _db.RemoveRange(module.Content);

            _db.Modules.RemoveRange(section.Modules);
            _db.Assignments.RemoveRange(section.Assignments);
            _db.RemoveRange(section.AssignmentGroups);
        }

        _db.Sections.RemoveRange(course.Sections);
        _db.GradeRanges.RemoveRange(course.GradeRanges);

        // Just unenroll them, the students themselves stay.
        course.Roster.Clear();

        _db.Courses.Remove(course);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/courses/{id}/roster
    [HttpGet("{id}/roster")]
    public async Task<ActionResult<IEnumerable<StudentDto>>> GetRoster(string id)
    {
        var course = await _db.Courses.Include(c => c.Roster).AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
        if (course is null) return NotFound();
        return Ok(course.Roster.Select(StudentDto.From));
    }

    // POST /api/courses/{id}/roster/{studentId}
    [HttpPost("{id}/roster/{studentId}")]
    public async Task<IActionResult> AddToRoster(string id, string studentId)
    {
        var course = await _db.Courses.Include(c => c.Roster).FirstOrDefaultAsync(c => c.Id == id);
        if (course is null) return NotFound($"Course '{id}' not found.");

        var student = await _db.Students.FindAsync(studentId);
        if (student is null) return NotFound($"Student '{studentId}' not found.");

        if (course.Roster.Any(s => s.Id == studentId))
            return NoContent();

        course.Roster.Add(student);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/courses/{id}/roster/{studentId}
    [HttpDelete("{id}/roster/{studentId}")]
    public async Task<IActionResult> RemoveFromRoster(string id, string studentId)
    {
        var course = await _db.Courses.Include(c => c.Roster).FirstOrDefaultAsync(c => c.Id == id);
        if (course is null) return NotFound();

        var existing = course.Roster.FirstOrDefault(s => s.Id == studentId);
        if (existing is null) return NotFound($"Student '{studentId}' is not in this course.");

        course.Roster.Remove(existing);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
