using System.Threading.RateLimiting;
using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Serilog;
using SporcuGelisim.Application.Common;
using SporcuGelisim.Application.DTOs;
using SporcuGelisim.Application.Services;
using SporcuGelisim.Application.Validation;
using SporcuGelisim.Domain.Common;
using SporcuGelisim.Domain.Entities;
using SporcuGelisim.Domain.Enums;
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
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 10 * 1024 * 1024;
});

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
app.MapPost("/admin/relations/assign", async (
        HttpRequest request,
        ClaimsPrincipal principal,
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken) =>
    {
        var adminIdValue = userManager.GetUserId(principal);
        if (!Guid.TryParse(adminIdValue, out var adminId) || !principal.IsInRole(RoleNames.Admin))
        {
            return Results.LocalRedirect("/Account/AccessDenied");
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var athleteProfileId = ParseNullableGuid(form["AthleteProfileId"]);
        var relationType = ParseRelationType(form["RelationType"]);
        var relatedUserId = relationType == AthleteRelationType.Parent
            ? ParseNullableGuid(form["RelatedParentUserId"])
            : ParseNullableGuid(form["RelatedCoachUserId"]);

        if (!athleteProfileId.HasValue || !relatedUserId.HasValue)
        {
            return Results.LocalRedirect("/admin/relations?missing=1");
        }

        var expectedRole = relationType == AthleteRelationType.Parent ? RoleNames.Parent : RoleNames.Coach;
        var relatedUserHasRole = await (
            from userRole in db.UserRoles.AsNoTracking()
            join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userRole.UserId == relatedUserId.Value && role.Name == expectedRole
            select userRole.UserId)
            .AnyAsync(cancellationToken);
        if (!relatedUserHasRole)
        {
            return Results.LocalRedirect("/admin/relations?invalid=1");
        }

        var athleteExists = await db.AthleteProfiles.AnyAsync(x => x.Id == athleteProfileId.Value, cancellationToken);
        if (!athleteExists)
        {
            return Results.LocalRedirect("/admin/relations?missing=1");
        }

        var exists = await db.AthleteRelations.AnyAsync(x =>
            x.AthleteProfileId == athleteProfileId.Value &&
            x.RelatedUserId == relatedUserId.Value &&
            x.RelationType == relationType &&
            x.IsActive,
            cancellationToken);
        if (exists)
        {
            return Results.LocalRedirect("/admin/relations?duplicate=1");
        }

        db.AthleteRelations.Add(new AthleteRelation
        {
            AthleteProfileId = athleteProfileId.Value,
            RelatedUserId = relatedUserId.Value,
            RelationType = relationType,
            CreatedByUserId = adminId
        });
        await db.SaveChangesAsync(cancellationToken);
        return Results.LocalRedirect("/admin/relations?assigned=1");
    })
    .RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin));

app.MapPost("/athlete/profile/update", async (
        HttpRequest request,
        ClaimsPrincipal principal,
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IFileStorageService fileStorage,
        CancellationToken cancellationToken) =>
    {
        var userIdValue = userManager.GetUserId(principal);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Results.LocalRedirect("/Account/Login");
        }

        try
        {
            var form = await request.ReadFormAsync(cancellationToken);
            var primaryBranchId = ParseNullableGuid(form["PrimaryBranchId"]);
            var birthDate = ParseNullableDate(form["BirthDate"]);
            var nationalIdentityNumber = Clean(form["NationalIdentityNumber"]);
            var phoneNumber = Clean(form["PhoneNumber"]);
            var secondaryPhoneNumber = Clean(form["SecondaryPhoneNumber"]);
            var parentPhoneNumber = Clean(form["ParentPhoneNumber"]);
            var address = Clean(form["Address"]);
            var biography = Clean(form["Biography"]);

            ValidateAthleteProfile(nationalIdentityNumber, phoneNumber, secondaryPhoneNumber, parentPhoneNumber, address, biography);
            if (primaryBranchId.HasValue)
            {
                var branchActive = await db.SportBranches.AnyAsync(x => x.Id == primaryBranchId.Value && x.IsActive, cancellationToken);
                if (!branchActive)
                {
                    throw new ValidationFailedException(new Dictionary<string, string[]> { ["PrimaryBranchId"] = ["Seçilen branş aktif değil."] });
                }
            }

            var profile = await db.AthleteProfiles.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
            if (profile is null)
            {
                profile = new AthleteProfile { UserId = userId };
                db.AthleteProfiles.Add(profile);
            }

            profile.PrimaryBranchId = primaryBranchId;
            profile.NationalIdentityNumber = nationalIdentityNumber;
            profile.PhoneNumber = phoneNumber;
            profile.SecondaryPhoneNumber = secondaryPhoneNumber;
            profile.ParentPhoneNumber = parentPhoneNumber;
            profile.Address = address;
            profile.Biography = biography;
            profile.BirthDate = birthDate;
            profile.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            var photo = form.Files.GetFile("ProfilePhoto");
            if (photo is not null && photo.Length > 0)
            {
                await using var stream = photo.OpenReadStream();
                var file = await fileStorage.SaveProfilePhotoAsync(
                    new UploadProfilePhotoRequest(userId, photo.FileName, photo.ContentType, photo.Length, stream),
                    cancellationToken);

                var user = await userManager.FindByIdAsync(userId.ToString());
                if (user is not null)
                {
                    user.ProfilePhotoId = file.Id;
                    var result = await userManager.UpdateAsync(user);
                    if (!result.Succeeded)
                    {
                        throw new ValidationFailedException(new Dictionary<string, string[]> { ["ProfilePhoto"] = result.Errors.Select(x => x.Description).ToArray() });
                    }
                }
            }

            return Results.LocalRedirect("/athlete/profile?saved=1");
        }
        catch (ValidationFailedException ex)
        {
            var message = string.Join(" ", ex.Errors.SelectMany(x => x.Value));
            return Results.LocalRedirect($"/athlete/profile?error={Uri.EscapeDataString(message)}");
        }
        catch
        {
            return Results.LocalRedirect("/athlete/profile?error=1");
        }
    })
    .RequireAuthorization(policy => policy.RequireRole(RoleNames.Athlete));

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

static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

static Guid? ParseNullableGuid(string? value) => Guid.TryParse(value, out var id) ? id : null;

static AthleteRelationType ParseRelationType(string? value) =>
    Enum.TryParse<AthleteRelationType>(value, out var relationType) ? relationType : AthleteRelationType.Coach;

static DateOnly? ParseNullableDate(string? value) =>
    DateOnly.TryParse(value, out var date) ? date : null;

static void ValidateAthleteProfile(
    string? nationalIdentityNumber,
    string? phoneNumber,
    string? secondaryPhoneNumber,
    string? parentPhoneNumber,
    string? address,
    string? biography)
{
    var errors = new Dictionary<string, string[]>();
    if (!string.IsNullOrWhiteSpace(nationalIdentityNumber) && (nationalIdentityNumber.Length != 11 || nationalIdentityNumber.Any(x => !char.IsDigit(x))))
    {
        errors["NationalIdentityNumber"] = ["TC kimlik no 11 rakam olmalıdır."];
    }

    AddLengthError(errors, nameof(phoneNumber), phoneNumber, 30, "Telefon no en fazla 30 karakter olabilir.");
    AddLengthError(errors, nameof(secondaryPhoneNumber), secondaryPhoneNumber, 30, "2. telefon numarası en fazla 30 karakter olabilir.");
    AddLengthError(errors, nameof(parentPhoneNumber), parentPhoneNumber, 30, "Ebeveyn no en fazla 30 karakter olabilir.");
    AddLengthError(errors, nameof(address), address, 500, "Adres en fazla 500 karakter olabilir.");
    AddLengthError(errors, nameof(biography), biography, 2000, "Sporcu notu en fazla 2000 karakter olabilir.");

    if (errors.Count > 0)
    {
        throw new ValidationFailedException(errors);
    }
}

static void AddLengthError(Dictionary<string, string[]> errors, string key, string? value, int maxLength, string message)
{
    if (!string.IsNullOrWhiteSpace(value) && value.Length > maxLength)
    {
        errors[key] = [message];
    }
}

public partial class Program;
