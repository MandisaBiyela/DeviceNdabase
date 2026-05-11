using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Infrastructure.Identity;
using DeviceDesk.Modules.Phase0.Services;
using DeviceDesk.Modules.Phase1.Services;
using DeviceDesk.Modules.Phase2.Data;
using DeviceDesk.Modules.Phase2.Models;
using DeviceDesk.Modules.Phase2.Services;
using DeviceDesk.Modules.Phase3.Data;
using DeviceDesk.Modules.Phase3.Services;
using DeviceDesk.Modules.SuperAdmin.Services;
using DeviceDesk.Middleware;
using DeviceDesk.Services;
using DeviceDesk.Infrastructure.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = false;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database contexts
builder.Services.AddDbContext<DeviceDeskDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddDbContext<Phase1DbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddDbContext<ApplicationDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddDbContext<Phase2DbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddDbContext<Phase3DbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Startup diagnostics
builder.Services.AddHostedService<DbHealthStartupLogger>();

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();
// Authorization policy not used in this build

// Configure cookie authentication
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login.html";
    options.LogoutPath = "/api/auth/logout";
    options.AccessDeniedPath = "/login.html";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.ClaimsIssuer = "DeviceDesk";

    // Avoid HTML redirects for API calls; return 401/403 instead
    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        },
        OnRedirectToAccessDenied = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        }
    };
});

// Configure Identity claim types
builder.Services.Configure<IdentityOptions>(options =>
{
    options.ClaimsIdentity.RoleClaimType = ClaimTypes.Role;
    options.ClaimsIdentity.UserIdClaimType = ClaimTypes.NameIdentifier;
    options.ClaimsIdentity.UserNameClaimType = ClaimTypes.Name;
    options.ClaimsIdentity.EmailClaimType = ClaimTypes.Email;
});

// Phase 0 services
builder.Services.AddScoped<CsvImportService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<NewStockBatchService>();
builder.Services.AddScoped<RnrBatchService>();
builder.Services.AddScoped<OrderValidationService>();
builder.Services.AddSingleton<ProcurementOrderExportService>();

// Integration services (bridges between phases)
// builder.Services.AddScoped<OrderIntegrationService>(); // Commented out - uses old Orders table

// Phase 1 services
builder.Services.AddScoped<ReceivingService>();
builder.Services.AddScoped<BlindCopyService>();
builder.Services.AddScoped<ScanningService>();
builder.Services.AddScoped<ReconciliationService>();
builder.Services.AddScoped<InventoryIntegrationService>();
builder.Services.AddScoped<GRVService>();
builder.Services.AddScoped<SpreadsheetParserService>();
builder.Services.AddScoped<NewStockScanningService>();
builder.Services.AddScoped<RnrBlindCopyService>();
builder.Services.AddScoped<RnrGrvService>();
builder.Services.AddScoped<ModelDrivenScanningService>();
builder.Services.AddScoped<ReceivingBatchSyncService>();

// Phase 2 services
builder.Services.AddScoped<ReceiptingService>();
builder.Services.AddScoped<AssessmentService>();
builder.Services.AddScoped<QualityService>();
builder.Services.AddScoped<DisposalService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<DispatchService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<AutoAllocationService>();
builder.Services.AddScoped<AllocationService>();
builder.Services.AddScoped<PickingService>();

// SuperAdmin services
builder.Services.AddScoped<SuperAdminService>();
builder.Services.AddScoped<ExportService>();

// Register email sender (SMTP if configured, else logging)
builder.Services.AddSingleton<IEmailSender>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var section = config.GetSection("Email");
    var host = section?["Host"];

    if (!string.IsNullOrWhiteSpace(host))
    {
        var logger = sp.GetRequiredService<ILogger<SmtpEmailSender>>();
        return new SmtpEmailSender(section!, logger);
    }

    var logLogger = sp.GetRequiredService<ILogger<LoggingEmailSender>>();
    return new LoggingEmailSender(logLogger);
});

// Phase 3 services
builder.Services.AddScoped<Phase3DispatchService>();
builder.Services.AddScoped<DispatchDocumentService>();
builder.Services.AddScoped<DispatchBatchService>();
builder.Services.AddScoped<CollectionSlipPodService>();
// Audit writer not registered in this build
// CORS: allow browser preview and local development
var allowedUi = new[] { 
    "http://127.0.0.1:60153", "http://localhost:60153",  // Current browser preview
    "http://127.0.0.1:53816", "http://localhost:53816",  // Previous browser preview
    "http://127.0.0.1:53685", "http://localhost:53685",  // Previous browser preview
    "http://127.0.0.1:8211", "http://localhost:8211",
    "http://127.0.0.1:5501", "http://localhost:5501",
    "http://127.0.0.1:8000", "http://localhost:8000"
};

builder.Services.AddCors(o => o.AddPolicy("ui", p =>
    p.WithOrigins(allowedUi)
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()
));

var app = builder.Build();

// ═══════════════════════════════════════════════════════════════════
// MIDDLEWARE PIPELINE (ORDER IS CRITICAL!)
// ═══════════════════════════════════════════════════════════════════
Console.WriteLine("[Middleware] Configuring request pipeline...");

// 1. Global Error Handling (must be first to catch all errors)
app.UseMiddleware<GlobalErrorHandlingMiddleware>();

// 2. Request Logging (log all requests/responses)
app.UseMiddleware<RequestLoggingMiddleware>();

// 3. Route Analyzer (displays route map at startup)
// app.UseMiddleware<RouteAnalyzerMiddleware>(); // Commented out - compatibility issue

// DB up and seed users
using (var scope = app.Services.CreateScope())
{
    // Migrate Identity first so login can work
    try
    {
        var authDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Console.WriteLine("[DB] Applying ApplicationDbContext migrations...");
        authDb.Database.Migrate();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Identity migrations failed] {ex.InnerException?.Message ?? ex.Message}");
    }

    // Attempt DeviceDesk core migrations; do not block startup if they fail
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<DeviceDeskDbContext>();
        Console.WriteLine("[DB] Applying DeviceDeskDbContext migrations...");
        db.Database.Migrate();
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Devices]') AND name = 'SchoolName') BEGIN ALTER TABLE [dbo].[Devices] ADD [SchoolName] NVARCHAR(256) NULL; END");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DeviceDesk migrations warning] {ex.InnerException?.Message ?? ex.Message}");
    }

    // Phase1 migrations
    try
    {
        var phase1Db = scope.ServiceProvider.GetRequiredService<Phase1DbContext>();
        Console.WriteLine("[DB] Applying Phase1DbContext migrations...");
        phase1Db.Database.Migrate();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Phase1 migrations warning] {ex.InnerException?.Message ?? ex.Message}");
    }

    // Phase2 migrations
    try
    {
        var phase2Db = scope.ServiceProvider.GetRequiredService<Phase2DbContext>();
        Console.WriteLine("[DB] Applying Phase2DbContext migrations...");
        phase2Db.Database.Migrate();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Phase2 migrations warning] {ex.InnerException?.Message ?? ex.Message}");
    }

    // Phase3 migrations
    try
    {
        var phase3Db = scope.ServiceProvider.GetRequiredService<Phase3DbContext>();
        Console.WriteLine("[DB] Applying Phase3DbContext migrations...");
        phase3Db.Database.Migrate();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Phase3 migrations warning] {ex.InnerException?.Message ?? ex.Message}");
    }

    // Seed default users regardless of non-Identity migration failures
    try
    {
        Console.WriteLine("[DB] Seeding users...");
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await UserSeeder.SeedUsersAsync(userManager, roleManager);
        Console.WriteLine("[DB] Identity seeding completed.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Identity seeding failed] {ex.InnerException?.Message ?? ex.Message}");
    }

    // Workflow seed removed per request

    if (app.Environment.IsDevelopment())
    {
        try
        {
            var coreDb = scope.ServiceProvider.GetRequiredService<DeviceDeskDbContext>();
            var csvPath = Path.Combine(app.Environment.ContentRootPath, "Data", "Seeds", "schools_emis.csv");
            await SchoolsSeeder.SeedFromCsvAsync(coreDb, csvPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Schools seeding warning] {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    }

    // Ensure authentication runs before any RBAC checks
    app.UseAuthentication();
    app.UseAuthorization();

// RBAC page access guard (block cross-phase pages with friendly redirects)
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? string.Empty;
    var isApi = path.StartsWith("/api", StringComparison.OrdinalIgnoreCase);

    // Do not interfere with API calls; cookie auth handlers already return 401/403
    if (isApi)
    {
        await next();
        return;
    }

    

    var isPhase0Page = path.StartsWith("/phase0", StringComparison.OrdinalIgnoreCase);
    var isPhase1Page = path.StartsWith("/phase1", StringComparison.OrdinalIgnoreCase);
    var isPhase2Page = path.StartsWith("/phase2", StringComparison.OrdinalIgnoreCase);
    var isDispatchPage = path.StartsWith("/dispatch", StringComparison.OrdinalIgnoreCase);
    var isAdminPage = path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase);
    var isSuperAdminPage = path.StartsWith("/superadmin", StringComparison.OrdinalIgnoreCase);
    // GM pages not served in this build

    // Build original return url with query
    var originalUrl = ctx.Request.Path + ctx.Request.QueryString.ToString();
    var returnUrl = Uri.EscapeDataString(originalUrl);

    bool isAuthenticated = ctx.User?.Identity?.IsAuthenticated == true;
    var principal = ctx.User;
    
    // Debug logging: print all claims when authenticated
    if (principal?.Identity?.IsAuthenticated == true)
    {
        Console.WriteLine("---- AUTHENTICATED USER CLAIMS ----");
        foreach (var claim in principal.Claims)
        {
            Console.WriteLine($"{claim.Type}: {claim.Value}");
        }
        Console.WriteLine("-----------------------------------");
    }
    else
    {
        Console.WriteLine("User is not authenticated in RBAC middleware.");
    }
    
    bool isOrdersClerk = principal?.IsInRole("OrdersClerk") == true;
    // Support both role names in case of seed/name mismatch
    bool isReceiver = (principal?.IsInRole("Receiver") == true) || (principal?.IsInRole("ReceivingClerk") == true);
    bool isAdmin = principal?.IsInRole("Admin") == true;
    bool isIct =
        (principal?.IsInRole("IctClerk") == true) ||
        (principal?.IsInRole("IctInspector") == true) ||
        (principal?.IsInRole("IctTechnician") == true) ||
        (principal?.IsInRole("IctManager") == true) ||
        (principal?.IsInRole("IctAllocator") == true);
    // Check dispatch roles using both IsInRole and direct claim lookup
    bool isDispatchByRole = 
        (principal?.IsInRole("DispatchClerk") == true) ||
        (principal?.IsInRole("DispatchDriver") == true) ||
        (principal?.IsInRole("DispatchQA") == true) ||
        (principal?.IsInRole("DispatchManager") == true);
    
    // Also check claims directly as a fallback
    bool isDispatchByClaim = false;
    if (principal?.Identity?.IsAuthenticated == true)
    {
        var roleClaims = principal.Claims.Where(c => c.Type == ClaimTypes.Role || c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role").Select(c => c.Value).ToList();
        isDispatchByClaim = roleClaims.Contains("DispatchClerk") || 
                           roleClaims.Contains("DispatchDriver") || 
                           roleClaims.Contains("DispatchQA") || 
                           roleClaims.Contains("DispatchManager");
        
        // Debug: Log dispatch role check
        if (isDispatchPage)
        {
            Console.WriteLine($"[RBAC] Checking dispatch access - isDispatchByRole: {isDispatchByRole}, isDispatchByClaim: {isDispatchByClaim}, isAuthenticated: {isAuthenticated}");
            Console.WriteLine($"[RBAC] All role claims found: {string.Join(", ", roleClaims)}");
        }
    }
    
    bool isDispatch = isDispatchByRole || isDispatchByClaim;
    // GM role not used

    // Guard Phase0 pages
    if (isPhase0Page && !(isOrdersClerk || isAdmin))
    {
        if (!isAuthenticated)
        {
            ctx.Response.Redirect($"/login.html?returnUrl={returnUrl}");
            return;
        }

        // Authenticated but wrong role → send to their dashboard
        if (isDispatch)
        {
            ctx.Response.Redirect("/dispatch/index.html");
            return;
        }
        if (isReceiver)
        {
            ctx.Response.Redirect("/phase1/dashboard.html");
            return;
        }
        if (isIct)
        {
            ctx.Response.Redirect("/phase2/index.html");
            return;
        }

        // Fallback to login
        ctx.Response.Redirect($"/login.html?returnUrl={returnUrl}");
        return;
    }

    // Guard Phase1 pages
    if (isPhase1Page && !(isReceiver || isAdmin))
    {
        if (!isAuthenticated)
        {
            ctx.Response.Redirect($"/login.html?returnUrl={returnUrl}");
            return;
        }

        // Authenticated but wrong role → send to their dashboard
        if (isOrdersClerk)
        {
            ctx.Response.Redirect("/phase0/new.html");
            return;
        }

        // Fallback to login
        ctx.Response.Redirect($"/login.html?returnUrl={returnUrl}");
        return;
    }

    // Guard Phase2 pages
    if (isPhase2Page && !(isIct || isAdmin))
    {
        if (!isAuthenticated)
        {
            ctx.Response.Redirect($"/login.html?returnUrl={returnUrl}");
            return;
        }

        if (isOrdersClerk)
        {
            ctx.Response.Redirect("/phase0/new.html");
            return;
        }

        if (isReceiver)
        {
            ctx.Response.Redirect("/phase1/dashboard.html");
            return;
        }

        ctx.Response.Redirect($"/login.html?returnUrl={returnUrl}");
        return;
    }

    // Guard Dispatch pages
    if (isDispatchPage && !(isDispatch || isAdmin))
    {
        Console.WriteLine($"[RBAC] Dispatch page access denied - isDispatch: {isDispatch}, isAdmin: {isAdmin}, isAuthenticated: {isAuthenticated}");
        if (!isAuthenticated)
        {
            ctx.Response.Redirect($"/login.html?returnUrl={returnUrl}");
            return;
        }

        // Authenticated but wrong role → send to their dashboard
        if (isOrdersClerk)
        {
            Console.WriteLine("[RBAC] Redirecting OrdersClerk to phase0");
            ctx.Response.Redirect("/phase0/new.html");
            return;
        }

        if (isReceiver)
        {
            Console.WriteLine("[RBAC] Redirecting Receiver to phase1");
            ctx.Response.Redirect("/phase1/dashboard.html");
            return;
        }

        if (isIct)
        {
            Console.WriteLine("[RBAC] Redirecting ICT user to phase2");
            ctx.Response.Redirect("/phase2/index.html");
            return;
        }

        // If we get here, user is authenticated but has no recognized role
        Console.WriteLine("[RBAC] Authenticated user with unrecognized role, redirecting to login");
        ctx.Response.Redirect($"/login.html?returnUrl={returnUrl}");
        return;
    }

    // Guard Admin pages
    bool isSuperAdmin = principal?.IsInRole("SuperAdmin") == true;
    if (isAdminPage && !(isSuperAdmin || isAdmin))
    {
        if (!isAuthenticated)
        {
            ctx.Response.Redirect($"/login.html?returnUrl={returnUrl}");
            return;
        }

        // Authenticated but wrong role → send to their dashboard
        if (isOrdersClerk)
        {
            ctx.Response.Redirect("/phase0/new.html");
            return;
        }

        if (isReceiver)
        {
            ctx.Response.Redirect("/phase1/dashboard.html");
            return;
        }

        if (isIct)
        {
            ctx.Response.Redirect("/phase2/index.html");
            return;
        }

        if (isDispatch)
        {
            ctx.Response.Redirect("/dispatch/preparation.html");
            return;
        }

        ctx.Response.Redirect($"/login.html?returnUrl={returnUrl}");
        return;
    }

    // Guard SuperAdmin pages
    if (isSuperAdminPage && !isSuperAdmin)
    {
        if (!isAuthenticated)
        {
            ctx.Response.Redirect($"/login.html?returnUrl={returnUrl}");
            return;
        }

        // Authenticated but wrong role → send to their dashboard
        if (isAdmin)
        {
            ctx.Response.Redirect("/admin/index.html");
            return;
        }

        if (isOrdersClerk)
        {
            ctx.Response.Redirect("/phase0/new.html");
            return;
        }

        if (isReceiver)
        {
            ctx.Response.Redirect("/phase1/dashboard.html");
            return;
        }

        if (isIct)
        {
            ctx.Response.Redirect("/phase2/index.html");
            return;
        }

        if (isDispatch)
        {
            ctx.Response.Redirect("/dispatch/preparation.html");
            return;
        }

        ctx.Response.Redirect($"/login.html?returnUrl={returnUrl}");
        return;
    }

    // GM page guard removed

    await next();
});

// Redirect /phase0 to the Upload NEW page so URL updates visibly
app.Use(async (ctx, next) =>
{
    var p = ctx.Request.Path.Value ?? string.Empty;
    if (string.Equals(p, "/phase0", StringComparison.OrdinalIgnoreCase))
    {
        ctx.Response.Redirect("/phase0/new.html");
        return;
    }
    await next();
});

// Serve default wwwroot (if used elsewhere)
app.UseStaticFiles();

// IMPORTANT: CORS must run before auth/endpoints
app.UseCors("ui");

// Authentication & Authorization moved earlier to ensure ctx.User is populated

// Serve Phase0 UI from /Modules/Phase0/Phase0/UI at /phase0
var phase0UiPath = Path.Combine(app.Environment.ContentRootPath, "Modules", "Phase0", "Phase0", "UI");

// Default files (index.html) under /phase0
app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = new PhysicalFileProvider(phase0UiPath),
    RequestPath = "/phase0",
    DefaultFileNames = ["new.html"]
});

// Static files (css/js/html) under /phase0
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(phase0UiPath),
    RequestPath = "/phase0"
});

// Serve Phase1 UI from /Modules/Phase1/UI at /phase1
var phase1UiPath = Path.Combine(app.Environment.ContentRootPath, "Modules", "Phase1", "UI");

app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = new PhysicalFileProvider(phase1UiPath),
    RequestPath = "/phase1"
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(phase1UiPath),
    RequestPath = "/phase1"
});

// Serve Phase2 UI from /Modules/Phase2/UI at /phase2
var phase2UiPath = Path.Combine(app.Environment.ContentRootPath, "Modules", "Phase2", "UI");

app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = new PhysicalFileProvider(phase2UiPath),
    RequestPath = "/phase2"
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(phase2UiPath),
    RequestPath = "/phase2"
});

// Serve Phase3 Dispatch UI from /Modules/Phase3/UI at /dispatch
var phase3UiPath = Path.Combine(app.Environment.ContentRootPath, "Modules", "Phase3", "UI");

// Only register if the folder actually exists to avoid crash
if (Directory.Exists(phase3UiPath))
{
    Console.WriteLine($"[STATIC] Phase 3 Dispatch UI root: {phase3UiPath}");

app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = new PhysicalFileProvider(phase3UiPath),
    RequestPath = "/dispatch",
    DefaultFileNames = ["index.html"]
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(phase3UiPath),
    RequestPath = "/dispatch"
});
}
else
{
    Console.WriteLine($"[STATIC] Phase 3 Dispatch UI folder NOT FOUND: {phase3UiPath}");
    Console.WriteLine($"[STATIC] App will continue but /dispatch routes will not serve static files.");
}

// Serve Admin UI from /Modules/Admin/UI at /admin (only if folder exists)
var adminUiPath = Path.Combine(app.Environment.ContentRootPath, "Modules", "Admin", "UI");
if (Directory.Exists(adminUiPath))
{
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = new PhysicalFileProvider(adminUiPath),
        RequestPath = "/admin",
        DefaultFileNames = ["index.html"]
    });

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(adminUiPath),
        RequestPath = "/admin"
    });
}

// Serve SuperAdmin UI from /Modules/SuperAdmin/SuperAdmin/UI at /superadmin
var superAdminUiPath = Path.Combine(app.Environment.ContentRootPath, "Modules", "SuperAdmin", "SuperAdmin", "UI");

app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = new PhysicalFileProvider(superAdminUiPath),
    RequestPath = "/superadmin",
    DefaultFileNames = ["dashboard.html"]
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(superAdminUiPath),
    RequestPath = "/superadmin"
});

// Serve React app from frontend/dist at /app when available
var reactUiPath = Path.Combine(app.Environment.ContentRootPath, "frontend", "dist");
if (Directory.Exists(reactUiPath))
{
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = new PhysicalFileProvider(reactUiPath),
        RequestPath = "/app",
        DefaultFileNames = ["index.html"]
    });

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(reactUiPath),
        RequestPath = "/app"
    });
}

// GM UI not served in this build

// Root resolves to MVC default route (Home/Index)

// Redirect /phase1 to dashboard
app.MapGet("/phase1", () => Results.Redirect("/phase1/dashboard.html"));

// Redirect /dispatch to its hub
app.MapGet("/dispatch", () => Results.Redirect("/dispatch/index.html"));

// Redirect /admin to its hub
app.MapGet("/admin", () => Results.Redirect("/admin/index.html"));

// Redirect /superadmin to its hub
app.MapGet("/superadmin", () => Results.Redirect("/superadmin/dashboard.html"));

// Swagger (dev-only) at /dev/swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(c => c.RouteTemplate = "dev/swagger/{documentName}/swagger.json");
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/dev/swagger/v1/swagger.json", "DeviceDesk API v1");
        c.RoutePrefix = "dev/swagger";
    });
}

// API + fallback to Phase0 hub
app.MapControllers();
if (Directory.Exists(reactUiPath))
{
    app.MapFallbackToFile("/app/{*path:nonfile}", "index.html", new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(reactUiPath)
    });
}

// Role-based landing page
app.MapGet("/", (HttpContext ctx) =>
{
    var user = ctx.User;
    var isAuthenticated = user?.Identity?.IsAuthenticated == true;
    
    // Debug logging for root route
    if (isAuthenticated && user != null)
    {
        var roleClaims = user.Claims.Where(c => c.Type == ClaimTypes.Role || c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role").Select(c => c.Value).ToList();
        Console.WriteLine($"[ROOT] Authenticated user - Role claims: {string.Join(", ", roleClaims)}");
        Console.WriteLine($"[ROOT] IsInRole checks - SuperAdmin: {user.IsInRole("SuperAdmin")}, Admin: {user.IsInRole("Admin")}, DispatchClerk: {user.IsInRole("DispatchClerk")}");
    }
    else
    {
        Console.WriteLine("[ROOT] User not authenticated, redirecting to login");
    }
    
    // Check roles using both IsInRole and direct claim lookup
    // Priority order: SuperAdmin > DispatchClerk > ReceivingClerk > ICT > OrdersClerk
    if (isAuthenticated && user != null)
    {
        var roleClaims = user.Claims.Where(c => c.Type == ClaimTypes.Role || c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role").Select(c => c.Value).ToList();
        
        if (user.IsInRole("SuperAdmin") || roleClaims.Contains("SuperAdmin"))
            return Results.Redirect("/superadmin/dashboard.html");
        if (user.IsInRole("Admin") || roleClaims.Contains("Admin"))
            return Results.Redirect("/admin/index.html");
        // Dispatch group must come BEFORE OrdersClerk to ensure priority
        if (user.IsInRole("DispatchClerk") || user.IsInRole("DispatchDriver") || user.IsInRole("DispatchQA") || user.IsInRole("DispatchManager") ||
            roleClaims.Contains("DispatchClerk") || roleClaims.Contains("DispatchDriver") || roleClaims.Contains("DispatchQA") || roleClaims.Contains("DispatchManager"))
        {
            Console.WriteLine("[ROOT] Redirecting Dispatch user to /dispatch/index.html");
            return Results.Redirect("/dispatch/index.html");
        }
        if (user.IsInRole("Receiver") || user.IsInRole("ReceivingClerk") || roleClaims.Contains("Receiver") || roleClaims.Contains("ReceivingClerk"))
            return Results.Redirect("/phase1/dashboard.html");
        if (user.IsInRole("IctAllocator") || roleClaims.Contains("IctAllocator"))
            return Results.Redirect("/phase2/index.html");
        if (user.IsInRole("IctClerk") || user.IsInRole("IctInspector") || user.IsInRole("IctTechnician") || user.IsInRole("IctManager") ||
            roleClaims.Contains("IctClerk") || roleClaims.Contains("IctInspector") || roleClaims.Contains("IctTechnician") || roleClaims.Contains("IctManager"))
            return Results.Redirect("/phase2/index.html");
        if (user.IsInRole("OrdersClerk") || roleClaims.Contains("OrdersClerk"))
            return Results.Redirect("/phase0/new.html");
    }
    
    return Results.Redirect("/login.html");
});
app.MapFallback(async context =>
{
    var user = context.User;

    if (user?.Identity?.IsAuthenticated == true)
    {
        if (user.IsInRole("SuperAdmin"))
        {
            context.Response.Redirect("/superadmin/dashboard.html");
            return;
        }

        if (user.IsInRole("DispatchClerk") || user.IsInRole("DispatchDriver") || user.IsInRole("DispatchQA") || user.IsInRole("DispatchManager"))
        {
            context.Response.Redirect("/dispatch/index.html");
            return;
        }

        if (user.IsInRole("ReceivingClerk") || user.IsInRole("Receiver"))
        {
            context.Response.Redirect("/phase1/dashboard.html");
            return;
        }

        if (user.IsInRole("IctClerk") || user.IsInRole("IctInspector") ||
            user.IsInRole("IctTechnician") || user.IsInRole("IctManager") ||
            user.IsInRole("IctAllocator"))
        {
            context.Response.Redirect("/phase2/index.html");
            return;
        }

        if (user.IsInRole("OrdersClerk"))
{
    context.Response.Redirect("/phase0/new.html");
            return;
        }
    }

    // Not authenticated or unknown role → login page
    context.Response.Redirect("/login.html");
    await Task.CompletedTask;
});

app.Run();
