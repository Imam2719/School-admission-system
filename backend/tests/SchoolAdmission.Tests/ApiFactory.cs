using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SchoolAdmission.Tests;

/// Starts the real API in-process against a REAL PostgreSQL database
/// (row locks cannot be tested with an in-memory fake).
/// Set TEST_DB_CONNECTION to point to your own Postgres; the database is created automatically.
public class ApiFactory : WebApplicationFactory<Program>
{
    public ApiFactory()
    {
        Environment.SetEnvironmentVariable("DB_CONNECTION_STRING",
            Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=school_admission_test;Username=postgres;Password=postgres");
        Environment.SetEnvironmentVariable("HOLD_DURATION_SECONDS", "600");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing");
}
