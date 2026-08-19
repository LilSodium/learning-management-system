using CLI.LMS;
using CLI.LMS.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CLI.LMS.Api.Controllers;

// Student CRUD.
[ApiController]
[Route("api/[controller]")]
public class StudentsController : ControllerBase
{
    private readonly ApiDbContext _db;

    public StudentsController(ApiDbContext db)
    {
        _db = db;
    }

    // GET /api/students
    [HttpGet]
    public async Task<ActionResult<IEnumerable<StudentDto>>> GetAll()
    {
        var students = await _db.Students.AsNoTracking().ToListAsync();
        return Ok(students.Select(StudentDto.From));
    }

    // GET /api/students/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<StudentDto>> Get(string id)
    {
        var student = await _db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (student is null) return NotFound();
        return Ok(StudentDto.From(student));
    }

    // POST /api/students
    [HttpPost]
    public async Task<ActionResult<StudentDto>> Create(StudentDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("Name is required.");

        var id = string.IsNullOrWhiteSpace(dto.Id) ? Guid.NewGuid().ToString() : dto.Id;
        if (await _db.Students.AnyAsync(s => s.Id == id))
            return Conflict($"A student with id '{id}' already exists.");

        var student = new Student
        {
            Id = id,
            Name = dto.Name,
            Code = dto.Code,
            Classification = dto.Classification
        };
        _db.Students.Add(student);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = student.Id }, StudentDto.From(student));
    }

    // PUT /api/students/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, StudentDto dto)
    {
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == id);
        if (student is null) return NotFound();

        student.Name = dto.Name;
        student.Code = dto.Code;
        student.Classification = dto.Classification;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // DELETE /api/students/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == id);
        if (student is null) return NotFound();

        // Same cleanup the desktop app does: pull them off rosters and
        // delete their submissions.
        var subs = _db.Submissions.Where(s => s.StudentId == id);
        _db.Submissions.RemoveRange(subs);

        var courses = await _db.Courses.Include(c => c.Roster)
            .Where(c => c.Roster.Any(r => r.Id == id))
            .ToListAsync();
        foreach (var course in courses)
        {
            var inRoster = course.Roster.FirstOrDefault(r => r.Id == id);
            if (inRoster is not null) course.Roster.Remove(inRoster);
        }

        _db.Students.Remove(student);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
