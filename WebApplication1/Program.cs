using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = Directory.GetCurrentDirectory()
});

// Fix for Render inotify limit (128 instances) — disable reloadOnChange for JSON files
// See: https://render.com/docs/troubleshooting-deploys#inotify-instances
// appsettings.json is .gitignored and provided via env vars on Render, so must be optional:true
builder.Configuration.Sources.Clear();
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Normalize connection string: supports both postgresql:// URL and Host= key-value formats
string? rawConn = builder.Configuration.GetConnectionString("DefaultConnection");
// Fallback: direct env var check (covers DATABASE_URL on some platforms)
if (string.IsNullOrWhiteSpace(rawConn))
{
    rawConn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
           ?? Environment.GetEnvironmentVariable("DATABASE_URL");
}
// Final fallback: default Neon DB as requested by user (used when appsettings.json is .gitignored and env var not set)
if (string.IsNullOrWhiteSpace(rawConn))
{
    rawConn = "postgresql://neondb_owner:npg_oih65YHxzRrU@ep-late-art-aepe1nc0-pooler.c-2.us-east-2.aws.neon.tech/neondb?sslmode=require&channel_binding=require";
}

string Sanitize(string? conn)
{
    if (string.IsNullOrEmpty(conn)) return "(null)";
    try
    {
        // Hide password in logs
        if (conn.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(conn);
            var userInfo = uri.UserInfo; // user:password
            var atIdx = userInfo.IndexOf(':');
            var user = atIdx >= 0 ? userInfo.Substring(0, atIdx) : userInfo;
            return $"postgresql://{user}:***@{uri.Host}:{uri.Port}{uri.AbsolutePath}?{uri.Query.TrimStart('?')}";
        }
        else
        {
            // Host=...;Username=...;Password=...
            var parts = conn.Split(';', StringSplitOptions.RemoveEmptyEntries);
            var sanitizedParts = parts.Select(p =>
            {
                var kv = p.Split('=', 2);
                if (kv.Length == 2 && kv[0].Trim().Equals("Password", StringComparison.OrdinalIgnoreCase))
                    return $"{kv[0]}=***";
                return p;
            });
            return string.Join(";", sanitizedParts);
        }
    }
    catch { return "(sanitize-failed)"; }
}

// Early startup log (without leaking password) to diagnose Render 500s
var tmpLogger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger("Startup");
if (string.IsNullOrWhiteSpace(rawConn))
{
    tmpLogger.LogError("DefaultConnection missing! Set ConnectionStrings__DefaultConnection env var. Raw: {Sanitized}", Sanitize(rawConn));
}
else
{
    tmpLogger.LogInformation("Using DefaultConnection: {Sanitized}", Sanitize(rawConn));
    // Validate Npgsql can parse it
    try
    {
        var csb = new Npgsql.NpgsqlConnectionStringBuilder(rawConn);
        tmpLogger.LogInformation("Npgsql parsed OK — Host:{Host} Database:{Database} Username:{Username} SslMode:{SslMode}", csb.Host, csb.Database, csb.Username, csb.SslMode);
    }
    catch (Exception ex)
    {
        // postgresql:// URI may still be valid for Npgsql even if builder fails — log warning only
        tmpLogger.LogWarning(ex, "NpgsqlConnectionStringBuilder parse warning for: {Sanitized}", Sanitize(rawConn));
    }
}

// Use normalized string (fallback to raw if null to let EF throw clear error)
var connectionString = rawConn;

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Lightweight health endpoint that does NOT require DB — use to distinguish boot vs DB failure
app.MapGet("/health", () => Results.Ok(new { status = "ok", env = app.Environment.EnvironmentName, time = DateTime.UtcNow }));
app.MapGet("/health/db", async (ApplicationDbContext db) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync();
        return Results.Ok(new { db = canConnect ? "ok" : "cannot_connect", time = DateTime.UtcNow });
    }
    catch (Exception ex)
    {
        return Results.Problem($"DB check failed: {ex.Message}");
    }
});

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureCreatedAsync();
        await SeedData.InitializeAsync(services);
        var startupLogger = services.GetRequiredService<ILogger<Program>>();
        startupLogger.LogInformation("Database EnsureCreated + SeedData completed");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while initializing the database. Connection: {Sanitized}", Sanitize(rawConn));
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
