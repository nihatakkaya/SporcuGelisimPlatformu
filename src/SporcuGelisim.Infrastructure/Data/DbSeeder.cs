using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SporcuGelisim.Domain.Common;
using SporcuGelisim.Domain.Entities;
using SporcuGelisim.Infrastructure.Identity;

namespace SporcuGelisim.Infrastructure.Data;

public sealed class DbSeeder(
    RoleManager<IdentityRole<Guid>> roleManager,
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db,
    IConfiguration configuration,
    ILogger<DbSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        foreach (var role in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        await SeedAdminAsync();
        await SeedBranchesAsync(cancellationToken);
        await SeedWordsAsync(cancellationToken);
    }

    private async Task SeedAdminAsync()
    {
        var email = configuration["SeedAdmin:Email"];
        var password = configuration["SeedAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("SeedAdmin configuration missing. Admin user seed skipped.");
            return;
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = "System",
                LastName = "Admin"
            };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                logger.LogWarning("Admin seed failed: {Errors}", string.Join(", ", result.Errors.Select(e => e.Code)));
                return;
            }
        }

        if (!await userManager.IsInRoleAsync(user, RoleNames.Admin))
        {
            await userManager.AddToRoleAsync(user, RoleNames.Admin);
        }
    }

    private async Task SeedBranchesAsync(CancellationToken cancellationToken)
    {
        if (await db.SportBranches.AnyAsync(cancellationToken))
        {
            return;
        }

        var sport = Branch("Spor", "spor", 1);
        var team = Branch("Takim Sporlari", "takim-sporlari", 1, sport.Id);
        var individual = Branch("Bireysel Sporlar", "bireysel-sporlar", 2, sport.Id);
        var combat = Branch("Dovus Sporlari", "dovus-sporlari", 3, sport.Id);
        var athletics = Branch("Atletizm", "atletizm", 1, individual.Id);

        db.SportBranches.AddRange(
            sport, team, individual, combat,
            Branch("Futbol", "futbol", 1, team.Id),
            Branch("Basketbol", "basketbol", 2, team.Id),
            Branch("Voleybol", "voleybol", 3, team.Id),
            athletics,
            Branch("Sprint", "sprint", 1, athletics.Id),
            Branch("Uzun Mesafe", "uzun-mesafe", 2, athletics.Id),
            Branch("Yuzme", "yuzme", 2, individual.Id),
            Branch("Okculuk", "okculuk", 3, individual.Id),
            Branch("Gures", "gures", 1, combat.Id),
            Branch("Karate", "karate", 2, combat.Id),
            Branch("Tekvando", "tekvando", 3, combat.Id));

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedWordsAsync(CancellationToken cancellationToken)
    {
        if (await db.MotivationWords.AnyAsync(cancellationToken))
        {
            return;
        }

        var mental = Word("Mental Guc", "mental-guc", true, null);
        var focus = Word("Odaklanma", "odaklanma", true, mental.Id);
        var confidence = Word("Ozguven", "ozguven", true, mental.Id);
        var resilience = Word("Dayaniklilik", "dayaniklilik", true, mental.Id);

        db.MotivationWords.AddRange(
            mental, focus, confidence, resilience,
            Word("Dikkat", "dikkat", true, focus.Id),
            Word("Konsantrasyon", "konsantrasyon", true, focus.Id),
            Word("Cesaret", "cesaret", true, confidence.Id),
            Word("Kendine Inanc", "kendine-inanc", true, confidence.Id),
            Word("Sabir", "sabir", true, resilience.Id),
            Word("Mucadele", "mucadele", true, resilience.Id));

        await db.SaveChangesAsync(cancellationToken);
    }

    private static SportBranch Branch(string name, string slug, int displayOrder, Guid? parentId = null) =>
        new() { Name = name, Slug = slug, DisplayOrder = displayOrder, ParentBranchId = parentId };

    private static MotivationWord Word(string text, string normalized, bool isGlobal, Guid? parentId) =>
        new() { Text = text, NormalizedText = normalized, IsGlobal = isGlobal, ParentWordId = parentId, CreatedByRole = RoleNames.Admin };
}
