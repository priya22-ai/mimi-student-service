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

// Helper: convert postgresql:// URI to Npgsql key-value string (NpgsqlConnectionStringBuilder does not accept URI)
string NormalizeConnectionString(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw)) return raw!;
    if (!raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) &&
        !raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        return raw;

    try
    {
        var uri = new Uri(raw);
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "";
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var host = uri.Host;
        var port = uri.Port != -1 ? uri.Port : 5432;
        var database = uri.AbsolutePath.TrimStart('/');

        // Parse query string manually (avoid extra deps)
        var queryParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(uri.Query))
        {
            var q = uri.Query.TrimStart('?');
            foreach (var part in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=', 2);
                var key = Uri.UnescapeDataString(kv[0]);
                var val = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";
                queryParams[key] = val;
            }
        }

        var builder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = database,
            Username = username,
            Password = password,
        };

        // Map common URI query params to Npgsql builder
        if (queryParams.TryGetValue("sslmode", out var sslmode))
        {
            if (Enum.TryParse<Npgsql.SslMode>(sslmode, true, out var mode))
                builder.SslMode = mode;
            else if (sslmode.Equals("require", StringComparison.OrdinalIgnoreCase))
                builder.SslMode = Npgsql.SslMode.Require;
            else if (sslmode.Equals("prefer", StringComparison.OrdinalIgnoreCase))
                builder.SslMode = Npgsql.SslMode.Prefer;
            else if (sslmode.Equals("disable", StringComparison.OrdinalIgnoreCase))
                builder.SslMode = Npgsql.SslMode.Disable;
        }

        // channel_binding — Npgsql 8+ uses ChannelBinding property
        if (queryParams.TryGetValue("channel_binding", out var cb))
        {
            try { builder["Channel Binding"] = cb; } catch { /* ignore if not supported */ }
        }

        // Pass through any other params (e.g., pooling, timeout) via builder indexer
        foreach (var kv in queryParams)
        {
            if (kv.Key.Equals("sslmode", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("channel_binding", StringComparison.OrdinalIgnoreCase))
                continue;
            try { builder[kv.Key] = kv.Value; } catch { /* unknown key, ignore */ }
        }

        // Neon requires SSL; ensure TrustServerCertificate handling is not needed (Neon uses valid cert)
        return builder.ConnectionString;
    }
    catch
    {
        // Fallback: return raw (EF will throw clear error)
        return raw;
    }
}

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
        if (conn.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
            conn.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
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
    var normalizedForLog = NormalizeConnectionString(rawConn);
    // Validate Npgsql can parse normalized string
    try
    {
        var csb = new Npgsql.NpgsqlConnectionStringBuilder(normalizedForLog);
        tmpLogger.LogInformation("Npgsql parsed OK — Host:{Host} Database:{Database} Username:{Username} SslMode:{SslMode} Port:{Port}", csb.Host, csb.Database, csb.Username, csb.SslMode, csb.Port);
    }
    catch (Exception ex)
    {
        tmpLogger.LogWarning(ex, "NpgsqlConnectionStringBuilder parse warning for: {Sanitized}", Sanitize(rawConn));
    }
}

// Use normalized Npgsql-compatible connection string
var connectionString = NormalizeConnectionString(rawConn);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
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
        // Use MigrateAsync for proper migrations support (EnsureCreated is incompatible with migrations)
        // For fresh Neon DB (ep-late-art) this will apply InitialCreate
        await context.Database.MigrateAsync();
        await SeedData.InitializeAsync(services);
        var startupLogger = services.GetRequiredService<ILogger<Program>>();
        startupLogger.LogInformation("Database Migrate + SeedData completed");
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
