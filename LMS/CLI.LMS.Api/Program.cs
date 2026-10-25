using System.Threading.RateLimiting;
using CLI.LMS;
using CLI.LMS.Api.Data;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Own SQLite file so this runs without the desktop app.
// Database:Path overrides the location (the container sets it to /app/data).
var dbPath = builder.Configuration["Database:Path"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "lms-api.db");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dbPath))!);
builder.Services.AddDbContext<ApiDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Hosts like Render terminate TLS at a proxy and pass the real scheme and
// client IP in X-Forwarded-* headers. The proxy's address isn't fixed, so
// trust the headers from any source.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// The public demo is writable, so cap each client at 60 requests a minute.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));
});

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

app.UseForwardedHeaders();

// Swagger UI at /swagger for testing endpoints in the browser. On in
// Development; the hosted demo turns it on with Swagger__Enabled=true.
var swaggerEnabled = app.Environment.IsDevelopment()
    || app.Configuration.GetValue<bool>("Swagger:Enabled");
if (swaggerEnabled)
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "CLI.LMS API v1"));
}

// The web client in wwwroot is served at "/". It sits ahead of the rate
// limiter so loading the page doesn't use up a visitor's API requests.
app.UseDefaultFiles();
app.UseStaticFiles();

// The hosting proxy already redirects to HTTPS, so only do it locally.
if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).ExcludeFromDescription();

app.MapControllers();

app.Run();

// Lets the test project start the API in memory (WebApplicationFactory<Program>).
public partial class Program { }
