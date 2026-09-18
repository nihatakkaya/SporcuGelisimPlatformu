using System.Text.RegularExpressions;
using SporcuGelisim.Web.Components.Account;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SporcuGelisim.Domain.Common;
using SporcuGelisim.Domain.Entities;
using SporcuGelisim.Domain.Enums;
using SporcuGelisim.Infrastructure.Data;
using SporcuGelisim.Infrastructure.Identity;

namespace SporcuGelisim.IntegrationTests;

public sealed class SessionPageTests
{
    [Fact]
    public async Task Coach_pages_render_assignment_picker_and_private_session_notes()
    {
        await using var factory = new SessionFactory();
        using var client = factory.CreateClient();
        await factory.SeedAsync();
        client.DefaultRequestHeaders.Add("Test-Role", RoleNames.Coach);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/coach/athletes/{SessionFactory.AthleteId}/sessions"));
        Assert.Contains("COACH-PRIVATE-ONLY", html);
        Assert.Contains("SHARED-MEETING-NOTE", html);
        Assert.Contains("Önceki Oturumdan Gelen Kelimeler", html);
        Assert.Contains("Yeni Oturum", html);
        Assert.Contains("Düzenle", html);
        Assert.Contains("Oturumu Sil", html);
        Assert.DoesNotContain("id=\"private-note\"", html);
        Assert.DoesNotContain("emin misiniz?", html);
        var words = WebUtility.HtmlDecode(await client.GetStringAsync($"/coach/athletes/{SessionFactory.AthleteId}/words"));
        Assert.Contains("Seçilen Kelimeleri Ata", words);
        Assert.Contains("Disiplin", words);
        Assert.DoesNotContain("Hata:", words);
    }

    [Fact]
    public async Task Athlete_page_renders_only_own_shared_notes_without_private_data_in_html()
    {
        await using var factory = new SessionFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        await factory.SeedAsync();
        client.DefaultRequestHeaders.Add("Test-Role", RoleNames.Athlete);
        var response = await client.GetAsync("/athlete/sessions");
        response.EnsureSuccessStatusCode();
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("SHARED-MEETING-NOTE", html);
        Assert.DoesNotContain("COACH-PRIVATE-ONLY", html);
        Assert.DoesNotContain("OTHER-ATHLETE-NOTE", html);
        Assert.DoesNotContain("private-note", html);
        Assert.DoesNotContain("Hata:", html);
        var forbidden = await client.GetAsync($"/coach/athletes/{SessionFactory.AthleteId}/sessions");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Coach_cannot_render_unrelated_athlete_details_by_changing_url()
    {
        await using var factory = new SessionFactory();
        using var client = factory.CreateClient();
        await factory.SeedAsync();
        client.DefaultRequestHeaders.Add("Test-Role", RoleNames.Coach);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/coach/athletes/{SessionFactory.OtherAthleteId}/sessions"));
        Assert.DoesNotContain("OTHER-ATHLETE-NOTE", html);
        Assert.Contains("Hata:", html);
    }

    [Theory]
    [InlineData("", "Sporcu kaydı oluşturmak için branş seçmelisiniz.")]
    [InlineData("00000000-0000-0000-0000-000000000000", "Sporcu kaydı oluşturmak için branş seçmelisiniz.")]
    [InlineData("10000000-0000-0000-0000-000000000099", "Seçilen branş bulunamadı veya aktif değil.")]
    [InlineData("inactive", "Seçilen branş bulunamadı veya aktif değil.")]
    public async Task Athlete_registration_rejects_missing_or_unavailable_branch_without_creating_account(string branch, string error)
    {
        await using var factory = new SessionFactory();
        using var client = factory.CreateClient();
        await factory.SeedAsync();
        client.DefaultRequestHeaders.Add("Test-Role", RoleNames.Coach);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (branch == "inactive")
        {
            var inactive = new SportBranch { Name = "Pasif test", Slug = "pasif-test", IsActive = false };
            db.SportBranches.Add(inactive);
            await db.SaveChangesAsync();
            branch = inactive.Id.ToString();
        }
        var initialUsers = await db.Users.CountAsync();
        var html = await RegisterAsync(client, RoleNames.Athlete, branch);
        Assert.Contains(error, html);
        Assert.Equal(initialUsers, await db.Users.CountAsync());
        Assert.False(await db.Users.AnyAsync(x => x.Email == "registration-test@example.test"));
    }

    [Fact]
    public async Task Athlete_registration_saves_selected_branch_and_coach_relationship()
    {
        await using var factory = new SessionFactory();
        using var client = factory.CreateClient();
        await factory.SeedAsync();
        client.DefaultRequestHeaders.Add("Test-Role", RoleNames.Coach);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch = new SportBranch { Name = "Kayıt test branşı", Slug = "kayit-test" };
        db.SportBranches.Add(branch);
        await db.SaveChangesAsync();
        var html = await RegisterAsync(client, RoleNames.Athlete, branch.Id.ToString());
        Assert.True(html.Contains("Başarılı:"), Regex.Replace(html, "<[^>]*>", " "));
        var user = await db.Users.SingleAsync(x => x.Email == "registration-test@example.test");
        var profile = await db.AthleteProfiles.SingleAsync(x => x.UserId == user.Id);
        Assert.Equal(branch.Id, profile.PrimaryBranchId);
        Assert.True(await db.AthleteRelations.AnyAsync(x => x.AthleteProfileId == profile.Id && x.RelatedUserId == SessionFactory.CoachUserId));
    }

    [Theory]
    [InlineData(RoleNames.Admin, RoleNames.Coach)]
    [InlineData(RoleNames.Coach, RoleNames.Parent)]
    public async Task Non_athlete_registration_does_not_require_branch(string creatorRole, string role)
    {
        await using var factory = new SessionFactory();
        using var client = factory.CreateClient();
        await factory.SeedAsync();
        client.DefaultRequestHeaders.Add("Test-Role", creatorRole);
        var html = await RegisterAsync(client, role, "");
        Assert.True(html.Contains("Başarılı:"), Regex.Replace(html, "<[^>]*>", " "));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(x => x.Email == "registration-test@example.test");
        Assert.False(await db.AthleteProfiles.AnyAsync(x => x.UserId == user.Id));
    }

    [Fact]
    public async Task Coach_cannot_forge_admin_role_in_registration_post()
    {
        await using var factory = new SessionFactory();
        using var client = factory.CreateClient();
        await factory.SeedAsync();
        client.DefaultRequestHeaders.Add("Test-Role", RoleNames.Coach);
        var html = await RegisterAsync(client, RoleNames.Admin, "");
        Assert.True(html.Contains("Bu hesap türünü oluşturma yetkiniz yok."), Regex.Replace(html, "<[^>]*>", " "));
        using var scope = factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.AnyAsync(x => x.Email == "registration-test@example.test"));
    }

    private static async Task<string> RegisterAsync(HttpClient client, string role, string branch)
    {
        var html = await client.GetStringAsync("/Account/Register");
        var form = Regex.Matches(html, @"<form\b[^>]*>.*?</form>", RegexOptions.Singleline)
            .Cast<Match>().Single(x => x.Value.Contains("Input.FirstName")).Value;
        var fields = new Dictionary<string, string>();
        foreach (Match input in Regex.Matches(form, @"<input\b[^>]*>"))
        {
            var name = Regex.Match(input.Value, "name=\"([^\"]+)\"");
            var value = Regex.Match(input.Value, "value=\"([^\"]*)\"");
            if (name.Success) fields[WebUtility.HtmlDecode(name.Groups[1].Value)] = WebUtility.HtmlDecode(value.Groups[1].Value);
        }
        fields["Input.FirstName"] = "Kayıt";
        fields["Input.LastName"] = "Test";
        fields["Input.Email"] = "registration-test@example.test";
        fields["Input.Role"] = role;
        fields["Input.PrimaryBranchId"] = branch;
        fields["Input.Password"] = "Test-Only!42a";
        fields["Input.ConfirmPassword"] = "Test-Only!42a";
        var response = await client.PostAsync("/Account/Register", new FormUrlEncodedContent(fields));
        response.EnsureSuccessStatusCode();
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private sealed class TestAccountEmailSender : IAccountEmailSender
    {
        public Task SendEmailConfirmationCodeAsync(ApplicationUser user, string email, string code) => Task.CompletedTask;
        public Task SendEmailChangeCodeAsync(ApplicationUser user, string email, string code) => Task.CompletedTask;
        public Task SendTemporaryPasswordAsync(ApplicationUser user, string email, string temporaryPassword) => Task.CompletedTask;
    }
    private sealed class SessionFactory : WebApplicationFactory<Program>
    {
        internal static readonly Guid CoachUserId = Guid.NewGuid();
        internal static readonly Guid AthleteUserId = Guid.NewGuid();
        internal static readonly Guid AthleteId = Guid.NewGuid();
        internal static readonly Guid OtherAthleteId = Guid.NewGuid();
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            connection.Open();
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAccountEmailSender>();
                services.AddSingleton<IAccountEmailSender, TestAccountEmailSender>();
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(o => o.UseSqlite(connection), ServiceLifetime.Scoped);
                using (var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options)) db.Database.EnsureCreated();
                services.AddAuthentication(o => { o.DefaultAuthenticateScheme = "Test"; o.DefaultChallengeScheme = "Test"; o.DefaultForbidScheme = "Test"; })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            });
        }
        internal async Task SeedAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var otherUserId = Guid.NewGuid();
            db.Users.AddRange(new ApplicationUser { Id = CoachUserId, UserName = "coach@local", FirstName = "Antrenör" },
                new ApplicationUser { Id = AthleteUserId, UserName = "athlete@local", FirstName = "Büşra" },
                new ApplicationUser { Id = otherUserId, UserName = "other@local" });
            db.AthleteProfiles.AddRange(new AthleteProfile { Id = AthleteId, UserId = AthleteUserId }, new AthleteProfile { Id = OtherAthleteId, UserId = otherUserId });
            db.AthleteRelations.Add(new AthleteRelation { AthleteProfileId = AthleteId, RelatedUserId = CoachUserId, RelationType = AthleteRelationType.Coach });
            db.AthleteSessions.AddRange(new AthleteSession { AthleteProfileId = AthleteId, SessionNumber = 1, CreatedByUserId = CoachUserId, HasWordHistory = true, PrivateCoachNote = "COACH-PRIVATE-ONLY", SharedNote = "SHARED-MEETING-NOTE" },
                new AthleteSession { AthleteProfileId = OtherAthleteId, SessionNumber = 1, SharedNote = "OTHER-ATHLETE-NOTE" });
            db.MotivationWords.Add(new MotivationWord { Text = "Disiplin", NormalizedText = "TEST-DISIPLIN", IsGlobal = true });
            await db.SaveChangesAsync();
        }
        public override async ValueTask DisposeAsync() { await base.DisposeAsync(); await connection.DisposeAsync(); }
    }

    private sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["Test-Role"].ToString();
            if (string.IsNullOrEmpty(role)) return Task.FromResult(AuthenticateResult.NoResult());
            var id = role == RoleNames.Coach ? SessionFactory.CoachUserId : SessionFactory.AthleteUserId;
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, id.ToString()), new(ClaimTypes.Role, role)], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
