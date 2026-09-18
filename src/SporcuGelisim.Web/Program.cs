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
builder.Services.AddScoped<ICurrentUserService, ComponentCurrentUserService>();

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
        options.SignIn.RequireConfirmedAccount = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddErrorDescriber<TurkishIdentityErrorDescriber>()
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<AccountEmailSender>();
builder.Services.AddSingleton<IEmailSender<ApplicationUser>>(sp => sp.GetRequiredService<AccountEmailSender>());
builder.Services.AddSingleton<IAccountEmailSender>(sp => sp.GetRequiredService<AccountEmailSender>());

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

app.MapPost("/coach/athletes/add", async (
        HttpRequest request,
        ClaimsPrincipal principal,
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken) =>
    {
        var coachIdValue = userManager.GetUserId(principal);
        if (!Guid.TryParse(coachIdValue, out var coachId) || !principal.IsInRole(RoleNames.Coach))
        {
            return Results.LocalRedirect("/Account/AccessDenied");
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var athleteProfileId = ParseNullableGuid(form["AthleteProfileId"]);
        if (!athleteProfileId.HasValue)
        {
            return Results.LocalRedirect("/athletes?coachMissing=1");
        }

        var athleteExists = await (
            from profile in db.AthleteProfiles.AsNoTracking()
            join user in db.Users.AsNoTracking() on profile.UserId equals user.Id
            where profile.Id == athleteProfileId.Value && !profile.IsDeleted && user.IsActive
            select profile.Id)
            .AnyAsync(cancellationToken);
        if (!athleteExists)
        {
            return Results.LocalRedirect("/athletes?coachMissing=1");
        }

        var relation = await db.AthleteRelations.FirstOrDefaultAsync(x =>
            x.AthleteProfileId == athleteProfileId.Value &&
            x.RelatedUserId == coachId &&
            x.RelationType == AthleteRelationType.Coach,
            cancellationToken);

        if (relation is null)
        {
            db.AthleteRelations.Add(new AthleteRelation
            {
                AthleteProfileId = athleteProfileId.Value,
                RelatedUserId = coachId,
                RelationType = AthleteRelationType.Coach,
                CreatedByUserId = coachId
            });
        }
        else
        {
            relation.IsActive = true;
            relation.EndDate = null;
            relation.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.LocalRedirect($"/athletes/{athleteProfileId.Value}?coachAdded=1");
    })
    .RequireAuthorization(policy => policy.RequireRole(RoleNames.Coach));

app.MapPost("/coach/athletes/remove", async (
        HttpRequest request,
        ClaimsPrincipal principal,
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken) =>
    {
        var coachIdValue = userManager.GetUserId(principal);
        if (!Guid.TryParse(coachIdValue, out var coachId) || !principal.IsInRole(RoleNames.Coach))
        {
            return Results.LocalRedirect("/Account/AccessDenied");
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var athleteProfileId = ParseNullableGuid(form["AthleteProfileId"]);
        if (!athleteProfileId.HasValue)
        {
            return Results.LocalRedirect("/coach/athletes?coachMissing=1");
        }

        var relation = await db.AthleteRelations.FirstOrDefaultAsync(x =>
            x.AthleteProfileId == athleteProfileId.Value &&
            x.RelatedUserId == coachId &&
            x.RelationType == AthleteRelationType.Coach &&
            x.IsActive,
            cancellationToken);
        if (relation is null)
        {
            return Results.LocalRedirect("/coach/athletes?coachMissing=1");
        }

        relation.IsActive = false;
        relation.EndDate = DateTimeOffset.UtcNow;
        relation.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.LocalRedirect("/coach/athletes?coachRemoved=1");
    })
    .RequireAuthorization(policy => policy.RequireRole(RoleNames.Coach));

app.MapPost("/coach/athletes/assign-parent", async (
        HttpRequest request,
        ClaimsPrincipal principal,
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken) =>
    {
        var coachIdValue = userManager.GetUserId(principal);
        if (!Guid.TryParse(coachIdValue, out var coachId) || !principal.IsInRole(RoleNames.Coach))
        {
            return Results.LocalRedirect("/Account/AccessDenied");
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var athleteProfileId = ParseNullableGuid(form["AthleteProfileId"]);
        var parentUserId = ParseNullableGuid(form["ParentUserId"]);
        if (!athleteProfileId.HasValue || !parentUserId.HasValue)
        {
            return Results.LocalRedirect("/coach/athletes?parentMissing=1");
        }

        var ownsAthlete = await db.AthleteRelations.AnyAsync(x =>
            x.AthleteProfileId == athleteProfileId.Value &&
            x.RelatedUserId == coachId &&
            x.RelationType == AthleteRelationType.Coach &&
            x.IsActive,
            cancellationToken);
        if (!ownsAthlete)
        {
            return Results.LocalRedirect("/Account/AccessDenied");
        }

        var parentHasRole = await (
            from userRole in db.UserRoles.AsNoTracking()
            join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            join user in db.Users.AsNoTracking() on userRole.UserId equals user.Id
            where userRole.UserId == parentUserId.Value && role.Name == RoleNames.Parent && user.IsActive
            select userRole.UserId)
            .AnyAsync(cancellationToken);
        if (!parentHasRole)
        {
            return Results.LocalRedirect($"/coach/athletes/{athleteProfileId.Value}?parentInvalid=1");
        }

        var relation = await db.AthleteRelations.FirstOrDefaultAsync(x =>
            x.AthleteProfileId == athleteProfileId.Value &&
            x.RelatedUserId == parentUserId.Value &&
            x.RelationType == AthleteRelationType.Parent,
            cancellationToken);

        if (relation is null)
        {
            db.AthleteRelations.Add(new AthleteRelation
            {
                AthleteProfileId = athleteProfileId.Value,
                RelatedUserId = parentUserId.Value,
                RelationType = AthleteRelationType.Parent,
                CreatedByUserId = coachId
            });
        }
        else
        {
            relation.IsActive = true;
            relation.EndDate = null;
            relation.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.LocalRedirect($"/coach/athletes/{athleteProfileId.Value}?parentAssigned=1");
    })
    .RequireAuthorization(policy => policy.RequireRole(RoleNames.Coach));

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
            join user in db.Users.AsNoTracking() on userRole.UserId equals user.Id
            where userRole.UserId == relatedUserId.Value && role.Name == expectedRole && user.IsActive
            select userRole.UserId)
            .AnyAsync(cancellationToken);
        if (!relatedUserHasRole)
        {
            return Results.LocalRedirect("/admin/relations?invalid=1");
        }

        var athleteExists = await (
            from profile in db.AthleteProfiles.AsNoTracking()
            join user in db.Users.AsNoTracking() on profile.UserId equals user.Id
            where profile.Id == athleteProfileId.Value && !profile.IsDeleted && user.IsActive
            select profile.Id)
            .AnyAsync(cancellationToken);
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

app.MapPost("/feedback/send", async (
        HttpRequest request,
        ClaimsPrincipal principal,
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IFeedbackService feedbackService,
        CancellationToken cancellationToken) =>
    {
        var userIdValue = userManager.GetUserId(principal);
        if (!Guid.TryParse(userIdValue, out var userId) || !principal.IsInRole(RoleNames.Athlete))
        {
            return Results.LocalRedirect("/Account/AccessDenied");
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var coachUserId = ParseNullableGuid(form["CoachUserId"]);
        var comment = Clean(form["Comment"]);
        if (!coachUserId.HasValue)
        {
            return Results.LocalRedirect("/feedback?error=Antren%C3%B6r%20se%C3%A7imi%20zorunludur.");
        }

        if (string.IsNullOrWhiteSpace(comment))
        {
            return Results.LocalRedirect("/feedback?error=Mesaj%20zorunludur.");
        }

        if (comment.Length > 2000)
        {
            return Results.LocalRedirect("/feedback?error=Mesaj%20en%20fazla%202000%20karakter%20olabilir.");
        }

        var athleteProfileId = await db.AthleteProfiles.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (athleteProfileId == Guid.Empty)
        {
            return Results.LocalRedirect("/feedback?error=Sporcu%20profili%20bulunamad%C4%B1.");
        }

        try
        {
            await feedbackService.CreateAsync(
                new CreateFeedbackRequest(athleteProfileId, null, coachUserId, DateTimeOffset.UtcNow, comment),
                cancellationToken);
            return Results.LocalRedirect("/feedback?sent=1");
        }
        catch (ValidationFailedException ex)
        {
            var message = string.Join(" ", ex.Errors.SelectMany(x => x.Value));
            return Results.LocalRedirect($"/feedback?error={Uri.EscapeDataString(message)}");
        }
        catch (ForbiddenException ex)
        {
            return Results.LocalRedirect($"/feedback?error={Uri.EscapeDataString(ex.Message)}");
        }
    })
    .RequireAuthorization(policy => policy.RequireRole(RoleNames.Athlete));

app.MapPost("/feedback/send-related", async (
        HttpRequest request,
        ClaimsPrincipal principal,
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken) =>
    {
        var userIdValue = userManager.GetUserId(principal);
        if (!Guid.TryParse(userIdValue, out var userId) ||
            (!principal.IsInRole(RoleNames.Coach) && !principal.IsInRole(RoleNames.Parent)))
        {
            return Results.LocalRedirect("/Account/AccessDenied");
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var target = ParseFeedbackTarget(form["Target"]);
        var comment = Clean(form["Comment"]);
        if (target is null)
        {
            return Results.LocalRedirect("/feedback?error=Al%C4%B1c%C4%B1%20se%C3%A7imi%20zorunludur.");
        }

        if (string.IsNullOrWhiteSpace(comment))
        {
            return Results.LocalRedirect("/feedback?error=Mesaj%20zorunludur.");
        }

        if (comment.Length > 2000)
        {
            return Results.LocalRedirect("/feedback?error=Mesaj%20en%20fazla%202000%20karakter%20olabilir.");
        }

        var currentRelationType = principal.IsInRole(RoleNames.Coach)
            ? AthleteRelationType.Coach
            : AthleteRelationType.Parent;
        var targetRelationType = currentRelationType == AthleteRelationType.Coach
            ? AthleteRelationType.Parent
            : AthleteRelationType.Coach;
        var targetRole = currentRelationType == AthleteRelationType.Coach ? RoleNames.Parent : RoleNames.Coach;

        var hasCurrentRelation = await db.AthleteRelations.AnyAsync(x =>
            x.AthleteProfileId == target.Value.AthleteProfileId &&
            x.RelatedUserId == userId &&
            x.RelationType == currentRelationType &&
            x.IsActive,
            cancellationToken);
        if (!hasCurrentRelation)
        {
            return Results.LocalRedirect("/Account/AccessDenied");
        }

        var hasTargetRelation = await (
            from relation in db.AthleteRelations.AsNoTracking()
            join userRole in db.UserRoles.AsNoTracking() on relation.RelatedUserId equals userRole.UserId
            join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where relation.AthleteProfileId == target.Value.AthleteProfileId &&
                  relation.RelatedUserId == target.Value.RecipientUserId &&
                  relation.RelationType == targetRelationType &&
                  relation.IsActive &&
                  role.Name == targetRole
            select relation.Id)
            .AnyAsync(cancellationToken);
        if (!hasTargetRelation)
        {
            return Results.LocalRedirect("/feedback?error=Se%C3%A7ilen%20al%C4%B1c%C4%B1%20bu%20sporcu%20ile%20e%C5%9Fle%C5%9Fmi%C5%9F%20de%C4%9Fil.");
        }

        db.Feedbacks.Add(new Feedback
        {
            AthleteProfileId = target.Value.AthleteProfileId,
            AuthorUserId = userId,
            RecipientUserId = target.Value.RecipientUserId,
            FeedbackDate = DateTimeOffset.UtcNow,
            Comment = comment
        });
        await db.SaveChangesAsync(cancellationToken);
        return Results.LocalRedirect("/feedback?sent=1");
    })
    .RequireAuthorization(policy => policy.RequireRole(RoleNames.Coach, RoleNames.Parent));

app.MapPost("/coach/profile/update", async (
        HttpRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        IFileStorageService fileStorage,
        CancellationToken cancellationToken) =>
    {
        var userIdValue = userManager.GetUserId(principal);
        if (!Guid.TryParse(userIdValue, out var userId) || !principal.IsInRole(RoleNames.Coach))
        {
            return Results.LocalRedirect("/Account/AccessDenied");
        }

        var returnUrl = "/coach/profile";
        try
        {
            var form = await request.ReadFormAsync(cancellationToken);
            returnUrl = SafeReturnUrl(form["ReturnUrl"], "/coach/profile");
            var phoneNumber = NormalizePhoneNumber(Clean(form["PhoneNumber"]), "Telefon no");
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                return Results.LocalRedirect("/Account/Login");
            }

            user.PhoneNumber = phoneNumber;
            var photo = form.Files.GetFile("ProfilePhoto");
            if (photo is not null && photo.Length > 0)
            {
                await using var stream = photo.OpenReadStream();
                var file = await fileStorage.SaveProfilePhotoAsync(
                    new UploadProfilePhotoRequest(userId, photo.FileName, photo.ContentType, photo.Length, stream),
                    cancellationToken);
                user.ProfilePhotoId = file.Id;
            }

            var result = await userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var message = string.Join(" ", result.Errors.Select(x => x.Description));
                return Results.LocalRedirect($"{returnUrl}?error={Uri.EscapeDataString(message)}");
            }

            return Results.LocalRedirect($"{returnUrl}?saved=1");
        }
        catch (ValidationFailedException ex)
        {
            var message = string.Join(" ", ex.Errors.SelectMany(x => x.Value));
            return Results.LocalRedirect($"{returnUrl}?error={Uri.EscapeDataString(message)}");
        }
        catch
        {
            return Results.LocalRedirect($"{returnUrl}?error=1");
        }
    })
    .RequireAuthorization(policy => policy.RequireRole(RoleNames.Coach));

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

        var returnUrl = "/athlete/profile";
        try
        {
            var form = await request.ReadFormAsync(cancellationToken);
            returnUrl = SafeReturnUrl(form["ReturnUrl"], "/athlete/profile");
            var primaryBranchId = ParseNullableGuid(form["PrimaryBranchId"]);
            var birthDate = ParseNullableDate(form["BirthDate"]);
            var nationalIdentityNumber = Clean(form["NationalIdentityNumber"]);
            var phoneNumber = Clean(form["PhoneNumber"]);
            var secondaryPhoneNumber = Clean(form["SecondaryPhoneNumber"]);
            var address = Clean(form["Address"]);
            var biography = Clean(form["Biography"]);

            phoneNumber = NormalizePhoneNumber(phoneNumber, "Telefon no");
            secondaryPhoneNumber = NormalizePhoneNumber(secondaryPhoneNumber, "2. telefon numarası (ebeveyn no)");
            ValidateAthleteProfile(nationalIdentityNumber, phoneNumber, secondaryPhoneNumber, primaryBranchId, birthDate, address, biography);
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
            profile.ParentPhoneNumber = null;
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

            return Results.LocalRedirect($"{returnUrl}?saved=1");
        }
        catch (ValidationFailedException ex)
        {
            var message = string.Join(" ", ex.Errors.SelectMany(x => x.Value));
            return Results.LocalRedirect($"{returnUrl}?error={Uri.EscapeDataString(message)}");
        }
        catch
        {
            return Results.LocalRedirect($"{returnUrl}?error=1");
        }
    })
    .RequireAuthorization(policy => policy.RequireRole(RoleNames.Athlete));

// Database availability must not prevent Kestrel from binding its HTTP/HTTPS ports.
// LocalDB can take time to start or be temporarily unavailable on development machines,
// so migration and seed work begins only after the web server has started.
app.Lifetime.ApplicationStarted.Register(() =>
{
    _ = Task.Run(async () =>
    {
        await using var scope = app.Services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            await db.Database.MigrateAsync(app.Lifetime.ApplicationStopping);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database migration skipped. Run the runbook commands when LocalDB is available.");
        }

        try
        {
            await scope.ServiceProvider.GetRequiredService<DbSeeder>()
                .SeedAsync(app.Lifetime.ApplicationStopping);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database seed skipped. Run the runbook commands when LocalDB is available.");
        }
    });
});

app.Run();

static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

static string SafeReturnUrl(string? value, string fallback)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return fallback;
    }

    var trimmed = value.Trim();
    return trimmed.StartsWith("/", StringComparison.Ordinal) &&
        !trimmed.StartsWith("//", StringComparison.Ordinal) &&
        !trimmed.Contains("://", StringComparison.Ordinal)
        ? trimmed
        : fallback;
}

static Guid? ParseNullableGuid(string? value) => Guid.TryParse(value, out var id) ? id : null;

static (Guid AthleteProfileId, Guid RecipientUserId)? ParseFeedbackTarget(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return null;
    }

    var parts = value.Split(':', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    return parts.Length == 2 &&
        Guid.TryParse(parts[0], out var athleteProfileId) &&
        Guid.TryParse(parts[1], out var recipientUserId)
        ? (athleteProfileId, recipientUserId)
        : null;
}

static AthleteRelationType ParseRelationType(string? value) =>
    Enum.TryParse<AthleteRelationType>(value, out var relationType) ? relationType : AthleteRelationType.Coach;

static DateOnly? ParseNullableDate(string? value) =>
    DateOnly.TryParse(value, out var date) ? date : null;

static void ValidateAthleteProfile(
    string? nationalIdentityNumber,
    string? phoneNumber,
    string? secondaryPhoneNumber,
    Guid? primaryBranchId,
    DateOnly? birthDate,
    string? address,
    string? biography)
{
    ValidateAthleteProfileRequired(nationalIdentityNumber, phoneNumber, secondaryPhoneNumber, primaryBranchId, birthDate, address, biography);

    var errors = new Dictionary<string, string[]>();
    if (!string.IsNullOrWhiteSpace(nationalIdentityNumber) && (nationalIdentityNumber.Length != 11 || nationalIdentityNumber.Any(x => !char.IsDigit(x))))
    {
        errors["NationalIdentityNumber"] = ["TC kimlik no 11 rakam olmalıdır."];
    }

    AddLengthError(errors, nameof(phoneNumber), phoneNumber, 30, "Telefon no en fazla 30 karakter olabilir.");
    AddLengthError(errors, nameof(secondaryPhoneNumber), secondaryPhoneNumber, 30, "2. telefon numarası en fazla 30 karakter olabilir.");
    AddLengthError(errors, nameof(address), address, 500, "Adres en fazla 500 karakter olabilir.");
    AddLengthError(errors, nameof(biography), biography, 2000, "Sporcu notu en fazla 2000 karakter olabilir.");

    if (errors.Count > 0)
    {
        throw new ValidationFailedException(errors);
    }
}

static void ValidateAthleteProfileRequired(
    string? nationalIdentityNumber,
    string? phoneNumber,
    string? secondaryPhoneNumber,
    Guid? primaryBranchId,
    DateOnly? birthDate,
    string? address,
    string? biography)
{
    var errors = new Dictionary<string, string[]>();
    if (string.IsNullOrWhiteSpace(nationalIdentityNumber))
    {
        errors["NationalIdentityNumber"] = ["TC kimlik no zorunludur."];
    }
    else if (nationalIdentityNumber.Length != 11 || nationalIdentityNumber.Any(x => !char.IsDigit(x)))
    {
        errors["NationalIdentityNumber"] = ["TC kimlik no 11 rakam olmalıdır."];
    }

    if (!primaryBranchId.HasValue)
    {
        errors["PrimaryBranchId"] = ["Spor branşı zorunludur."];
    }

    if (!birthDate.HasValue)
    {
        errors["BirthDate"] = ["Doğum tarihi zorunludur."];
    }

    if (string.IsNullOrWhiteSpace(phoneNumber))
    {
        errors["PhoneNumber"] = ["Telefon no zorunludur."];
    }

    if (string.IsNullOrWhiteSpace(secondaryPhoneNumber))
    {
        errors["SecondaryPhoneNumber"] = ["2. telefon numarası (ebeveyn no) zorunludur."];
    }

    if (string.IsNullOrWhiteSpace(address))
    {
        errors["Address"] = ["Adres zorunludur."];
    }

    AddLengthError(errors, nameof(phoneNumber), phoneNumber, 30, "Telefon no en fazla 30 karakter olabilir.");
    AddLengthError(errors, nameof(secondaryPhoneNumber), secondaryPhoneNumber, 30, "2. telefon numarası en fazla 30 karakter olabilir.");
    AddLengthError(errors, nameof(address), address, 500, "Adres en fazla 500 karakter olabilir.");
    AddLengthError(errors, nameof(biography), biography, 2000, "Sporcu notu en fazla 2000 karakter olabilir.");

    if (errors.Count > 0)
    {
        throw new ValidationFailedException(errors);
    }
}

static string? NormalizePhoneNumber(string? value, string label)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return null;
    }

    var digits = new string(value.Where(char.IsDigit).ToArray());
    if (digits.Length != 11 || !digits.StartsWith("05", StringComparison.Ordinal))
    {
        throw new ValidationFailedException(new Dictionary<string, string[]>
        {
            [label] = [$"{label} 05 ile başlamalı ve 11 hane olmalıdır."]
        });
    }

    return $"{digits[..4]} {digits.Substring(4, 3)} {digits.Substring(7, 2)} {digits.Substring(9, 2)}";
}

static void AddLengthError(Dictionary<string, string[]> errors, string key, string? value, int maxLength, string message)
{
    if (!string.IsNullOrWhiteSpace(value) && value.Length > maxLength)
    {
        errors[key] = [message];
    }
}

public partial class Program;
