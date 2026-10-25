using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace CLI.LMS.Api.Tests;

// Starts the real API in memory with its own throwaway SQLite file, so every
// test begins from the sample data and can't affect any other test.
public class LmsApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"lms-test-{Guid.NewGuid()}.db");
    private readonly bool _swaggerEnabled;

    public LmsApiFactory(bool swaggerEnabled = false)
    {
        _swaggerEnabled = swaggerEnabled;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Path", _dbPath);
        builder.UseSetting("Swagger:Enabled", _swaggerEnabled.ToString());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        // SQLite keeps the file open in its connection pool until it's cleared.
        SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
    }
}
