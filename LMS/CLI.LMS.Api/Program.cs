using CLI.LMS;
using CLI.LMS.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Own SQLite file so this runs without the desktop app.
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "lms-api.db");
builder.Services.AddDbContext<ApiDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Make the db and seed it the first time so the endpoints return something.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
    db.Database.EnsureCreated();

    if (!db.Students.Any())
        db.Students.AddRange(SampleDataStore.Students);

    if (!db.Courses.Any())
        db.Courses.AddRange(SampleDataStore.Courses);

    db.SaveChanges();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // Swagger UI at /swagger for testing endpoints in the browser.
    // Dev only, this should never be live in production.
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "CLI.LMS API v1"));
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
