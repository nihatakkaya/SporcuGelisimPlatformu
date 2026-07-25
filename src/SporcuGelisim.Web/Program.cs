using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Serilog;
using SporcuGelisim.Application.Validation;
using SporcuGelisim.Domain.Common;
using SporcuGelisim.Infrastructure;
using SporcuGelisim.Infrastructure.Data;
using SporcuGelisim.Infrastructure.Identity;
using SporcuGelisim.Web.Components;
using SporcuGelisim.Web.Components.Account;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/sporcu-gelisim-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateBranchRequestValidator>();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies(options =>
    {
        options.ApplicationCookie!.Configure(cookie =>
        {
            cookie.LoginPath = "/Account/Login";
            cookie.AccessDeniedPath = "/Account/AccessDenied";
            cookie.SlidingExpiration = true;
        });
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicyNames.AdminOnly, policy => policy.RequireRole(RoleNames.Admin));
    options.AddPolicy(AuthorizationPolicyNames.AthleteOnly, policy => policy.RequireRole(RoleNames.Athlete));
    options.AddPolicy(AuthorizationPolicyNames.CoachOnly, policy => policy.RequireRole(RoleNames.Coach));
    options.AddPolicy(AuthorizationPolicyNames.ParentOnly, policy => policy.RequireRole(RoleNames.Parent));
    options.AddPolicy(AuthorizationPolicyNames.AthleteOrAdmin, policy => policy.RequireRole(RoleNames.Athlete, RoleNames.Admin));
    options.AddPolicy(AuthorizationPolicyNames.CoachOrAdmin, policy => policy.RequireRole(RoleNames.Coach, RoleNames.Admin));
    options.AddPolicy(AuthorizationPolicyNames.CanAccessAthlete, policy => policy.RequireAuthenticatedUser());
    options.AddPolicy(AuthorizationPolicyNames.CanManageSession, policy => policy.RequireAuthenticatedUser());
    options.AddPolicy(AuthorizationPolicyNames.CanCreateFeedback, policy => policy.RequireAuthenticatedUser());
    options.AddPolicy(AuthorizationPolicyNames.CanManageWord, policy => policy.RequireRole(RoleNames.Admin, RoleNames.Coach));
});

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddErrorDescriber<TurkishIdentityErrorDescriber>()
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAdditionalIdentityEndpoints();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    try
    {
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Database migration skipped. Run the runbook commands when LocalDB is available.");
    }

    try
    {
        await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync(CancellationToken.None);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Database seed skipped. Run the runbook commands when LocalDB is available.");
    }
}

app.Run();

public partial class Program;
