using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SporcuGelisim.Application.Common;
using SporcuGelisim.Application.DTOs;
using SporcuGelisim.Application.Services;
using SporcuGelisim.Domain.Common;
using SporcuGelisim.Domain.Entities;
using SporcuGelisim.Domain.Enums;
using SporcuGelisim.Infrastructure.Data;
using SporcuGelisim.Infrastructure.Identity;
using SporcuGelisim.Infrastructure.Services;

namespace SporcuGelisim.UnitTests;

public sealed class AuthorizationBusinessRuleTests
{
    [Fact]
    public async Task Athlete_cannot_access_another_athlete_profile()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Athlete, TestFixture.AthleteUser1Id);
        Assert.False(await fixture.Access.CanAccessAthleteAsync(TestFixture.AthleteProfile2Id, CancellationToken.None));
    }

    [Fact]
    public async Task Coach_cannot_access_unrelated_athlete_profile()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Coach, TestFixture.CoachUserId);
        Assert.False(await fixture.Access.CanAccessAthleteAsync(TestFixture.AthleteProfile2Id, CancellationToken.None));
    }

    [Fact]
    public async Task Coach_lists_only_related_athletes_with_branch()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Coach, TestFixture.CoachUserId);
        var service = new AthleteRelationService(fixture.Db, fixture.CurrentUser, fixture.Audit);
        var athletes = await service.GetRelatedAthletesAsync(CancellationToken.None);
        var athlete = Assert.Single(athletes);
        Assert.Equal(TestFixture.AthleteProfile1Id, athlete.Id);
        Assert.Equal("Futbol", athlete.BranchName);
    }

    [Fact]
    public async Task Athlete_lists_exclude_inactive_athlete_users()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Admin, TestFixture.AdminUserId);
        var inactiveAthleteUser = await fixture.Db.Users.FirstAsync(x => x.Id == TestFixture.AthleteUser2Id);
        inactiveAthleteUser.IsActive = false;
        await fixture.Db.SaveChangesAsync();

        var service = new AthleteRelationService(fixture.Db, fixture.CurrentUser, fixture.Audit);
        var athletes = await service.GetRelatedAthletesAsync(CancellationToken.None);

        Assert.Contains(athletes, x => x.Id == TestFixture.AthleteProfile1Id);
        Assert.DoesNotContain(athletes, x => x.Id == TestFixture.AthleteProfile2Id);
    }

    [Fact]
    public async Task Parent_cannot_access_unrelated_athlete_profile()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Parent, TestFixture.ParentUserId);
        Assert.False(await fixture.Access.CanAccessAthleteAsync(TestFixture.AthleteProfile2Id, CancellationToken.None));
    }

    [Fact]
    public async Task Coach_can_create_feedback_for_related_athlete()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Coach, TestFixture.CoachUserId);
        var service = new FeedbackService(fixture.Db, fixture.CurrentUser, fixture.Access, fixture.Audit);
        var result = await service.CreateAsync(new CreateFeedbackRequest(TestFixture.AthleteProfile1Id, null, null, DateTimeOffset.UtcNow, "İyi ilerleme"), CancellationToken.None);
        Assert.Equal(TestFixture.CoachUserId, result.AuthorUserId);
    }

    [Fact]
    public async Task Parent_can_create_feedback_for_related_athlete()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Parent, TestFixture.ParentUserId);
        var service = new FeedbackService(fixture.Db, fixture.CurrentUser, fixture.Access, fixture.Audit);
        var result = await service.CreateAsync(new CreateFeedbackRequest(TestFixture.AthleteProfile1Id, null, null, DateTimeOffset.UtcNow, "Destek mesajı"), CancellationToken.None);
        Assert.Equal(TestFixture.ParentUserId, result.AuthorUserId);
    }

    [Fact]
    public async Task Athlete_can_send_feedback_only_to_responsible_coach()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Athlete, TestFixture.AthleteUser1Id);
        var service = new FeedbackService(fixture.Db, fixture.CurrentUser, fixture.Access, fixture.Audit);
        var result = await service.CreateAsync(new CreateFeedbackRequest(TestFixture.AthleteProfile1Id, null, TestFixture.CoachUserId, DateTimeOffset.UtcNow, "Bugünkü çalışma hakkında not"), CancellationToken.None);
        Assert.Equal(TestFixture.CoachUserId, result.RecipientUserId);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(new CreateFeedbackRequest(TestFixture.AthleteProfile1Id, null, TestFixture.ParentUserId, DateTimeOffset.UtcNow, "Yanlış hedef"), CancellationToken.None));
    }

    [Fact]
    public async Task Parent_cannot_create_motivation_word()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Parent, TestFixture.ParentUserId);
        var service = new MotivationWordService(fixture.Db, fixture.CurrentUser, fixture.Access, fixture.Audit);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(new CreateMotivationWordRequest("Sabır", null, null, false, []), CancellationToken.None));
    }

    [Fact]
    public async Task Coach_can_create_motivation_word()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Coach, TestFixture.CoachUserId);
        var service = new MotivationWordService(fixture.Db, fixture.CurrentUser, fixture.Access, fixture.Audit);
        var result = await service.CreateAsync(new CreateMotivationWordRequest("Disiplin", null, null, false, []), CancellationToken.None);
        Assert.False(result.IsGlobal);
    }

    [Fact]
    public async Task Coach_can_assign_word_to_related_athlete()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Coach, TestFixture.CoachUserId);
        var service = new MotivationWordService(fixture.Db, fixture.CurrentUser, fixture.Access, fixture.Audit);
        var word = await service.CreateAsync(new CreateMotivationWordRequest("Mücadele", null, null, false, []), CancellationToken.None);
        await service.AssignAthletesAsync(new AssignWordToAthletesRequest(word.Id, [TestFixture.AthleteProfile1Id]), CancellationToken.None);
        Assert.True(await fixture.Db.AthleteWordAssignments.AnyAsync(x => x.MotivationWordId == word.Id && x.AthleteProfileId == TestFixture.AthleteProfile1Id));
    }

    [Fact]
    public async Task Coach_cannot_assign_word_to_unrelated_athlete()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Coach, TestFixture.CoachUserId);
        var service = new MotivationWordService(fixture.Db, fixture.CurrentUser, fixture.Access, fixture.Audit);
        var word = await service.CreateAsync(new CreateMotivationWordRequest("Kararlılık", null, null, false, []), CancellationToken.None);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.AssignAthletesAsync(new AssignWordToAthletesRequest(word.Id, [TestFixture.AthleteProfile2Id]), CancellationToken.None));
    }

    [Fact]
    public async Task Coach_approval_adds_athlete_word_request_to_pool()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Athlete, TestFixture.AthleteUser1Id);
        var athleteService = new MotivationWordService(fixture.Db, fixture.CurrentUser, fixture.Access, fixture.Audit);
        var request = await athleteService.SubmitWordRequestAsync(
            new SubmitWordRequest(TestFixture.AthleteProfile1Id, TestFixture.CoachUserId, "Cesaret", "Antrenmanlarda kullanmak istiyorum."),
            CancellationToken.None);

        var coachUser = new TestCurrentUser(TestFixture.CoachUserId, new HashSet<string>([RoleNames.Coach], StringComparer.OrdinalIgnoreCase));
        var coachAccess = new AthleteAccessService(fixture.Db, coachUser);
        var coachService = new MotivationWordService(fixture.Db, coachUser, coachAccess, fixture.Audit);
        var reviewed = await coachService.ReviewWordRequestAsync(new ReviewWordRequest(request.Id, true, null), CancellationToken.None);

        Assert.Equal(WordRequestStatus.Approved, reviewed.Status);
        Assert.True(await fixture.Db.AthleteWordAssignments.AnyAsync(x => x.AthleteProfileId == TestFixture.AthleteProfile1Id));
    }

    [Fact]
    public async Task Coach_cannot_create_global_word()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Coach, TestFixture.CoachUserId);
        var service = new MotivationWordService(fixture.Db, fixture.CurrentUser, fixture.Access, fixture.Audit);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(new CreateMotivationWordRequest("Global", null, null, true, []), CancellationToken.None));
    }

    [Fact]
    public async Task Coach_can_update_only_own_word()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Coach, TestFixture.CoachUserId);
        var service = new MotivationWordService(fixture.Db, fixture.CurrentUser, fixture.Access, fixture.Audit);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdateAsync(new UpdateMotivationWordRequest(TestFixture.AdminWordId, "Yeni", null, null, false, true, []), CancellationToken.None));
    }

    [Fact]
    public async Task Admin_can_manage_all_words()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Admin, TestFixture.AdminUserId);
        var service = new MotivationWordService(fixture.Db, fixture.CurrentUser, fixture.Access, fixture.Audit);
        var updated = await service.UpdateAsync(new UpdateMotivationWordRequest(TestFixture.AdminWordId, "Admin Güncel", null, null, true, true, []), CancellationToken.None);
        Assert.True(updated.IsGlobal);
    }

    [Fact]
    public async Task Admin_can_create_dynamic_branch()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Admin, TestFixture.AdminUserId);
        var service = new BranchService(fixture.Db, fixture.CurrentUser, fixture.Audit);
        var branch = await service.CreateAsync(new CreateBranchRequest("Tenis", null, null, 1), CancellationToken.None);
        Assert.Equal("Tenis", branch.Name);
    }

    [Fact]
    public async Task Branch_cycle_is_blocked()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Admin, TestFixture.AdminUserId);
        var service = new BranchService(fixture.Db, fixture.CurrentUser, fixture.Audit);
        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAsync(new UpdateBranchRequest(TestFixture.RootBranchId, "Spor", null, TestFixture.ChildBranchId, 1, true), CancellationToken.None));
    }

    [Fact]
    public async Task Word_cycle_is_blocked()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Admin, TestFixture.AdminUserId);
        var service = new MotivationWordService(fixture.Db, fixture.CurrentUser, fixture.Access, fixture.Audit);
        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAsync(new UpdateMotivationWordRequest(TestFixture.AdminWordId, "Güç", null, TestFixture.ChildWordId, true, true, []), CancellationToken.None));
    }

    [Fact]
    public async Task Duplicate_word_in_session_is_not_added_twice()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Athlete, TestFixture.AthleteUser1Id);
        var service = new SessionWordService(fixture.Db, fixture.CurrentUser, fixture.Access, fixture.Audit);
        await service.AddWordsAsync(new AddSessionWordsRequest(TestFixture.Session1Id, [TestFixture.AdminWordId]), CancellationToken.None);
        var second = await service.AddWordsAsync(new AddSessionWordsRequest(TestFixture.Session1Id, [TestFixture.AdminWordId]), CancellationToken.None);
        Assert.Empty(second);
    }

    [Fact]
    public async Task Copy_previous_session_words_works()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Athlete, TestFixture.AthleteUser1Id);
        var service = new SessionWordService(fixture.Db, fixture.CurrentUser, fixture.Access, fixture.Audit);
        await service.AddWordsAsync(new AddSessionWordsRequest(TestFixture.Session1Id, [TestFixture.AdminWordId]), CancellationToken.None);
        var copied = await service.CopyWordsAsync(new CopySessionWordsRequest(TestFixture.Session1Id, TestFixture.Session2Id, null), CancellationToken.None);
        Assert.Single(copied);
    }

    [Fact]
    public async Task Cannot_copy_words_from_another_athletes_session()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Athlete, TestFixture.AthleteUser1Id);
        var service = new SessionWordService(fixture.Db, fixture.CurrentUser, fixture.Access, fixture.Audit);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.CopyWordsAsync(new CopySessionWordsRequest(TestFixture.Session3Id, TestFixture.Session2Id, null), CancellationToken.None));
    }

    [Fact]
    public async Task Duplicate_session_number_is_rejected_by_database()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Admin, TestFixture.AdminUserId);
        fixture.Db.AthleteSessions.Add(new AthleteSession { AthleteProfileId = TestFixture.AthleteProfile1Id, SessionNumber = 1, Title = "Tekrar" });
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => fixture.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Passive_branch_cannot_be_assigned_to_athlete()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Athlete, TestFixture.AthleteUser1Id);
        var service = new AthleteProfileService(fixture.Db, fixture.Access);
        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAsync(
            new UpdateAthleteProfileRequest(TestFixture.AthleteProfile1Id, TestFixture.PassiveBranchId, null, null, null, null, null, null, null),
            CancellationToken.None));
    }

    [Fact]
    public async Task Passive_word_cannot_be_added_to_new_session()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Athlete, TestFixture.AthleteUser1Id);
        var service = new SessionWordService(fixture.Db, fixture.CurrentUser, fixture.Access, fixture.Audit);
        var added = await service.AddWordsAsync(new AddSessionWordsRequest(TestFixture.Session1Id, [TestFixture.PassiveWordId]), CancellationToken.None);
        Assert.Empty(added);
    }

    [Fact]
    public async Task Session_word_snapshot_is_preserved_after_word_update()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Athlete, TestFixture.AthleteUser1Id);
        var service = new SessionWordService(fixture.Db, fixture.CurrentUser, fixture.Access, fixture.Audit);
        var added = await service.AddWordsAsync(new AddSessionWordsRequest(TestFixture.Session1Id, [TestFixture.AdminWordId]), CancellationToken.None);
        var word = await fixture.Db.MotivationWords.FirstAsync(x => x.Id == TestFixture.AdminWordId);
        word.Text = "Değişti";
        await fixture.Db.SaveChangesAsync();
        Assert.Equal("Güç", added.Single().WordTextSnapshot);
    }

    [Fact]
    public async Task Invalid_profile_photo_type_is_rejected()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Athlete, TestFixture.AthleteUser1Id);
        var storage = new TestFileStorageService(fixture.Db);
        await Assert.ThrowsAsync<ValidationFailedException>(() => storage.SaveProfilePhotoAsync(new UploadProfilePhotoRequest(TestFixture.AthleteUser1Id, "a.txt", "text/plain", 4, new MemoryStream([1, 2, 3, 4])), CancellationToken.None));
    }

    [Fact]
    public async Task Profile_photo_over_five_mb_is_rejected()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Athlete, TestFixture.AthleteUser1Id);
        var storage = new TestFileStorageService(fixture.Db);
        await Assert.ThrowsAsync<ValidationFailedException>(() => storage.SaveProfilePhotoAsync(new UploadProfilePhotoRequest(TestFixture.AthleteUser1Id, "a.png", "image/png", 6 * 1024 * 1024, new MemoryStream([0x89, 0x50, 0x4E, 0x47])), CancellationToken.None));
    }

    [Fact]
    public async Task Inactive_user_login_rule_is_detectable()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Athlete, TestFixture.AthleteUser1Id);
        var user = await fixture.Db.Users.FirstAsync(x => x.Id == TestFixture.AthleteUser1Id);
        user.IsActive = false;
        await fixture.Db.SaveChangesAsync();
        Assert.False(user.IsActive);
    }

    [Fact]
    public async Task Admin_cannot_assign_relation_to_inactive_accounts()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Admin, TestFixture.AdminUserId);
        var service = new AthleteRelationService(fixture.Db, fixture.CurrentUser, fixture.Audit);

        var coach = await fixture.Db.Users.FirstAsync(x => x.Id == TestFixture.CoachUserId);
        coach.IsActive = false;
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => service.AssignAsync(
            new AssignAthleteRelationRequest(TestFixture.AthleteProfile2Id, TestFixture.CoachUserId, AthleteRelationType.Coach),
            CancellationToken.None));

        coach.IsActive = true;
        var athlete = await fixture.Db.Users.FirstAsync(x => x.Id == TestFixture.AthleteUser2Id);
        athlete.IsActive = false;
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => service.AssignAsync(
            new AssignAthleteRelationRequest(TestFixture.AthleteProfile2Id, TestFixture.CoachUserId, AthleteRelationType.Coach),
            CancellationToken.None));
    }

    [Fact]
    public async Task Anonymous_user_cannot_access_athlete()
    {
        await using var fixture = await TestFixture.CreateAsync(null, null);
        Assert.False(await fixture.Access.CanAccessAthleteAsync(TestFixture.AthleteProfile1Id, CancellationToken.None));
    }

    [Fact]
    public async Task Non_admin_cannot_manage_admin_branch_screen_rule()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Coach, TestFixture.CoachUserId);
        var service = new BranchService(fixture.Db, fixture.CurrentUser, fixture.Audit);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(new CreateBranchRequest("Yeni", null, null, 1), CancellationToken.None));
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        public static readonly Guid AdminUserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        public static readonly Guid AthleteUser1Id = Guid.Parse("10000000-0000-0000-0000-000000000002");
        public static readonly Guid AthleteUser2Id = Guid.Parse("10000000-0000-0000-0000-000000000003");
        public static readonly Guid CoachUserId = Guid.Parse("10000000-0000-0000-0000-000000000004");
        public static readonly Guid ParentUserId = Guid.Parse("10000000-0000-0000-0000-000000000005");
        public static readonly Guid AthleteProfile1Id = Guid.Parse("20000000-0000-0000-0000-000000000001");
        public static readonly Guid AthleteProfile2Id = Guid.Parse("20000000-0000-0000-0000-000000000002");
        public static readonly Guid RootBranchId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        public static readonly Guid ChildBranchId = Guid.Parse("30000000-0000-0000-0000-000000000002");
        public static readonly Guid PassiveBranchId = Guid.Parse("30000000-0000-0000-0000-000000000003");
        public static readonly Guid AdminWordId = Guid.Parse("40000000-0000-0000-0000-000000000001");
        public static readonly Guid ChildWordId = Guid.Parse("40000000-0000-0000-0000-000000000002");
        public static readonly Guid PassiveWordId = Guid.Parse("40000000-0000-0000-0000-000000000003");
        public static readonly Guid Session1Id = Guid.Parse("50000000-0000-0000-0000-000000000001");
        public static readonly Guid Session2Id = Guid.Parse("50000000-0000-0000-0000-000000000002");
        public static readonly Guid Session3Id = Guid.Parse("50000000-0000-0000-0000-000000000003");

        private readonly SqliteConnection connection;

        private TestFixture(SqliteConnection connection, ApplicationDbContext db, TestCurrentUser currentUser)
        {
            this.connection = connection;
            Db = db;
            CurrentUser = currentUser;
            Audit = new NullAuditService();
            Access = new AthleteAccessService(db, currentUser);
        }

        public ApplicationDbContext Db { get; }
        public TestCurrentUser CurrentUser { get; }
        public IAuditService Audit { get; }
        public AthleteAccessService Access { get; }

        public static async Task<TestFixture> CreateAsync(string? role, Guid? userId)
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
            var db = new ApplicationDbContext(options);
            await db.Database.EnsureCreatedAsync();

            db.Users.AddRange(
                User(AdminUserId, "Admin", "User"),
                User(AthleteUser1Id, "Sporcu", "Bir"),
                User(AthleteUser2Id, "Sporcu", "İki"),
                User(CoachUserId, "Koç", "Bir"),
                User(ParentUserId, "Ebeveyn", "Bir"));
            var adminRoleId = Guid.Parse("60000000-0000-0000-0000-000000000001");
            var athleteRoleId = Guid.Parse("60000000-0000-0000-0000-000000000002");
            var coachRoleId = Guid.Parse("60000000-0000-0000-0000-000000000003");
            var parentRoleId = Guid.Parse("60000000-0000-0000-0000-000000000004");
            db.Roles.AddRange(
                Role(adminRoleId, RoleNames.Admin),
                Role(athleteRoleId, RoleNames.Athlete),
                Role(coachRoleId, RoleNames.Coach),
                Role(parentRoleId, RoleNames.Parent));
            db.UserRoles.AddRange(
                new IdentityUserRole<Guid> { UserId = AdminUserId, RoleId = adminRoleId },
                new IdentityUserRole<Guid> { UserId = AthleteUser1Id, RoleId = athleteRoleId },
                new IdentityUserRole<Guid> { UserId = AthleteUser2Id, RoleId = athleteRoleId },
                new IdentityUserRole<Guid> { UserId = CoachUserId, RoleId = coachRoleId },
                new IdentityUserRole<Guid> { UserId = ParentUserId, RoleId = parentRoleId });
            db.AthleteProfiles.AddRange(
                new AthleteProfile { Id = AthleteProfile1Id, UserId = AthleteUser1Id, PrimaryBranchId = ChildBranchId },
                new AthleteProfile { Id = AthleteProfile2Id, UserId = AthleteUser2Id });
            db.SportBranches.AddRange(
                new SportBranch { Id = RootBranchId, Name = "Spor", Slug = "spor" },
                new SportBranch { Id = ChildBranchId, Name = "Futbol", Slug = "futbol", ParentBranchId = RootBranchId },
                new SportBranch { Id = PassiveBranchId, Name = "Pasif", Slug = "pasif", IsActive = false });
            db.MotivationWords.AddRange(
                new MotivationWord { Id = AdminWordId, Text = "Güç", NormalizedText = "GUC", IsGlobal = true, IsActive = true, CreatedByRole = RoleNames.Admin },
                new MotivationWord { Id = ChildWordId, Text = "Odak", NormalizedText = "ODAK", ParentWordId = AdminWordId, IsGlobal = true, IsActive = true, CreatedByRole = RoleNames.Admin },
                new MotivationWord { Id = PassiveWordId, Text = "Pasif", NormalizedText = "PASIF", IsGlobal = true, IsActive = false, CreatedByRole = RoleNames.Admin });
            db.AthleteRelations.AddRange(
                new AthleteRelation { AthleteProfileId = AthleteProfile1Id, RelatedUserId = CoachUserId, RelationType = AthleteRelationType.Coach },
                new AthleteRelation { AthleteProfileId = AthleteProfile1Id, RelatedUserId = ParentUserId, RelationType = AthleteRelationType.Parent });
            db.AthleteSessions.AddRange(
                new AthleteSession { Id = Session1Id, AthleteProfileId = AthleteProfile1Id, SessionNumber = 1, Title = "Oturum 1" },
                new AthleteSession { Id = Session2Id, AthleteProfileId = AthleteProfile1Id, SessionNumber = 2, Title = "Oturum 2" },
                new AthleteSession { Id = Session3Id, AthleteProfileId = AthleteProfile2Id, SessionNumber = 1, Title = "Diğer" });
            await db.SaveChangesAsync();

            var roles = role is null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>([role], StringComparer.OrdinalIgnoreCase);
            return new TestFixture(connection, db, new TestCurrentUser(userId, roles));
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }

        private static ApplicationUser User(Guid id, string firstName, string lastName) =>
            new() { Id = id, UserName = $"{id}@test.local", Email = $"{id}@test.local", FirstName = firstName, LastName = lastName, IsActive = true };

        private static IdentityRole<Guid> Role(Guid id, string name) =>
            new() { Id = id, Name = name, NormalizedName = name.ToUpperInvariant() };
    }

    private sealed class TestCurrentUser(Guid? userId, IReadOnlySet<string> roles) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
        public IReadOnlySet<string> Roles { get; } = roles;
    }

    private sealed class NullAuditService : IAuditService
    {
        public Task WriteAsync(string action, string entityName, string entityId, object? oldValues, object? newValues, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TestFileStorageService(ApplicationDbContext db)
    {
        public Task<FileAssetDto> SaveProfilePhotoAsync(UploadProfilePhotoRequest request, CancellationToken cancellationToken)
        {
            if (request.FileSize > 5 * 1024 * 1024)
            {
                throw new ValidationFailedException(new Dictionary<string, string[]> { ["File"] = ["Fotoğraf en fazla 5 MB olabilir."] });
            }

            if (!new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(Path.GetExtension(request.FileName).ToLowerInvariant()) ||
                !new[] { "image/jpeg", "image/png", "image/webp" }.Contains(request.ContentType))
            {
                throw new ValidationFailedException(new Dictionary<string, string[]> { ["File"] = ["Sadece jpg, jpeg, png veya webp yüklenebilir."] });
            }

            var asset = new FileAsset { OwnerUserId = request.OwnerUserId, OriginalFileName = request.FileName, StoredFileName = "test", ContentType = request.ContentType, FileSize = request.FileSize, RelativePath = "test" };
            db.FileAssets.Add(asset);
            return Task.FromResult(new FileAssetDto(asset.Id, asset.OriginalFileName, asset.ContentType, asset.FileSize, asset.RelativePath));
        }
    }
}
