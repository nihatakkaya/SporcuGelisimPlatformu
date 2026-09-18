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
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Coach, TestFixture.CoachUserId);
        var service = new SessionWordService(fixture.Db, fixture.CurrentUser, fixture.Access, fixture.Audit);
        await service.AddWordsAsync(new AddSessionWordsRequest(TestFixture.Session2Id, [TestFixture.AdminWordId]), CancellationToken.None);
        var second = await service.AddWordsAsync(new AddSessionWordsRequest(TestFixture.Session2Id, [TestFixture.AdminWordId]), CancellationToken.None);
        Assert.Empty(second);
    }

    [Fact]
    public async Task Copy_previous_session_words_works()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Coach, TestFixture.CoachUserId);
        var service = new SessionWordService(fixture.Db, fixture.CurrentUser, fixture.Access, fixture.Audit);
        fixture.Db.SessionWords.Add(new SessionWord { SessionId = TestFixture.Session1Id, MotivationWordId = TestFixture.AdminWordId, WordTextSnapshot = "Güç" });
        await fixture.Db.SaveChangesAsync();
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
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Coach, TestFixture.CoachUserId);
        var service = new SessionWordService(fixture.Db, fixture.CurrentUser, fixture.Access, fixture.Audit);
        var added = await service.AddWordsAsync(new AddSessionWordsRequest(TestFixture.Session2Id, [TestFixture.PassiveWordId]), CancellationToken.None);
        Assert.Empty(added);
    }

    [Fact]
    public async Task Session_word_snapshot_is_preserved_after_word_update()
    {
        await using var fixture = await TestFixture.CreateAsync(RoleNames.Coach, TestFixture.CoachUserId);
        var service = new SessionWordService(fixture.Db, fixture.CurrentUser, fixture.Access, fixture.Audit);
        var added = await service.AddWordsAsync(new AddSessionWordsRequest(TestFixture.Session2Id, [TestFixture.AdminWordId]), CancellationToken.None);
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

    [Theory]
    [InlineData(RoleNames.Athlete)]
    [InlineData(RoleNames.Parent)]
    public async Task Readers_cannot_mutate_sessions_notes_or_assignments(string role)
    {
        var userId = role == RoleNames.Athlete ? TestFixture.AthleteUser1Id : TestFixture.ParentUserId;
        await using var f = await TestFixture.CreateAsync(role, userId);
        var sessions = new AthleteSessionService(f.Db, f.CurrentUser, f.Access, f.Audit);
        var words = new AthleteWordWorkflow(f.Db, f.CurrentUser, f.Access);
        await Assert.ThrowsAsync<ForbiddenException>(() => sessions.CreateAsync(new(TestFixture.AthleteProfile1Id, "", DateTimeOffset.Now, null), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => sessions.SaveNotesAsync(new(TestFixture.Session2Id, DateTimeOffset.Now, "özel", "ortak"), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => words.ChangeAsync(new(TestFixture.AthleteProfile1Id, [TestFixture.AdminWordId], []), default));
    }

    [Fact]
    public async Task Athlete_detail_excludes_private_note_and_other_athletes_sessions()
    {
        await using var f = await TestFixture.CreateAsync(RoleNames.Athlete, TestFixture.AthleteUser1Id);
        var entity = await f.Db.AthleteSessions.FindAsync(TestFixture.Session2Id);
        entity!.PrivateCoachNote = "GİZLİ-NOT";
        entity.SharedNote = "Paylaşılan görüşme";
        await f.Db.SaveChangesAsync();
        var service = new AthleteSessionService(f.Db, f.CurrentUser, f.Access, f.Audit);
        var detail = await service.GetDetailAsync(TestFixture.AthleteProfile1Id, TestFixture.Session2Id, default);
        Assert.Null(detail.PrivateCoachNote);
        Assert.DoesNotContain("GİZLİ-NOT", System.Text.Json.JsonSerializer.Serialize(detail));
        Assert.Equal("Paylaşılan görüşme", detail.SharedNote);
        Assert.False(detail.CanEditWords);
        Assert.All(await service.GetForAthleteAsync(TestFixture.AthleteProfile1Id, default), x => Assert.Equal(TestFixture.AthleteProfile1Id, x.AthleteProfileId));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetDetailAsync(TestFixture.AthleteProfile2Id, TestFixture.Session3Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetDetailAsync(TestFixture.AthleteProfile1Id, TestFixture.Session3Id, default));
    }

    [Fact]
    public async Task Coach_without_active_relation_cannot_read_or_change_history()
    {
        await using var f = await TestFixture.CreateAsync(RoleNames.Coach, TestFixture.CoachUserId);
        var service = new AthleteSessionService(f.Db, f.CurrentUser, f.Access, f.Audit);
        var words = new AthleteWordWorkflow(f.Db, f.CurrentUser, f.Access);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetForAthleteAsync(TestFixture.AthleteProfile2Id, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.SaveNotesAsync(new(TestFixture.Session3Id, DateTimeOffset.Now, "özel", null), default));
        var relation = await f.Db.AthleteRelations.FirstAsync(x => x.RelatedUserId == TestFixture.CoachUserId);
        relation.EndDate = DateTimeOffset.UtcNow.AddDays(-1);
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetDetailAsync(TestFixture.AthleteProfile1Id, TestFixture.Session2Id, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => words.ChangeAsync(new(TestFixture.AthleteProfile1Id, [TestFixture.AdminWordId], []), default));
    }

    [Fact]
    public async Task Bulk_changes_carry_forward_freeze_history_and_record_each_transition_once()
    {
        await using var f = await TestFixture.CreateAsync(RoleNames.Coach, TestFixture.CoachUserId);
        var sessions = new AthleteSessionService(f.Db, f.CurrentUser, f.Access, f.Audit);
        var words = new AthleteWordWorkflow(f.Db, f.CurrentUser, f.Access);
        await words.ChangeAsync(new(TestFixture.AthleteProfile1Id, [TestFixture.AdminWordId], []), default);
        var first = await sessions.CreateAsync(new(TestFixture.AthleteProfile1Id, "", DateTimeOffset.UtcNow.AddYears(-1), null), default);
        Assert.Equal(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(3)).Date, first.SessionDate.Date);
        await words.ChangeAsync(new(TestFixture.AthleteProfile1Id, [TestFixture.ChildWordId, TestFixture.ChildWordId], [TestFixture.AdminWordId], first.Id), default);
        await words.ChangeAsync(new(TestFixture.AthleteProfile1Id, [TestFixture.ChildWordId], [TestFixture.AdminWordId], first.Id), default);
        var detail = await sessions.GetDetailAsync(TestFixture.AthleteProfile1Id, first.Id, default);
        Assert.Equal("Güç", Assert.Single(detail.BeginningWords).Text);
        Assert.Equal("Odak", Assert.Single(detail.EndingWords).Text);
        Assert.Equal(2, detail.Changes.Count);
        Assert.All(detail.Changes, x => Assert.Equal("session", x.Source));
        var second = await sessions.CreateAsync(new(TestFixture.AthleteProfile1Id, "", DateTimeOffset.Now, null), default);
        await sessions.SaveNotesAsync(new(second.Id, DateTimeOffset.UtcNow.AddYears(-2), "Özel", "Paylaşılan"), default);
        await words.ChangeAsync(new(TestFixture.AthleteProfile1Id, [TestFixture.AdminWordId], []), default);
        var secondDetail = await sessions.GetDetailAsync(TestFixture.AthleteProfile1Id, second.Id, default);
        Assert.Equal("Odak", Assert.Single(secondDetail.BeginningWords).Text);
        Assert.Equal("assigned_words", Assert.Single(secondDetail.Changes).Source);
        Assert.Equal(2, secondDetail.EndingWords.Count);
        Assert.Equal("Özel", secondDetail.PrivateCoachNote);
        Assert.Equal(second.Id, (await sessions.GetForAthleteAsync(TestFixture.AthleteProfile1Id, default))[0].Id);
        var originalWord = await f.Db.MotivationWords.FindAsync(TestFixture.ChildWordId);
        originalWord!.Text = "Yeni ad";
        await f.Db.SaveChangesAsync();
        var frozen = await sessions.GetDetailAsync(TestFixture.AthleteProfile1Id, first.Id, default);
        Assert.True(frozen.CanEditWords);
        Assert.Equal("Odak", Assert.Single(frozen.EndingWords).Text);
        Assert.Equal(2, frozen.Changes.Count);
        await Assert.ThrowsAsync<ConflictException>(() => words.ChangeAsync(new(TestFixture.AthleteProfile1Id, [], [TestFixture.ChildWordId], first.Id), default));
        Assert.Equal(2, (await words.GetCurrentAsync(TestFixture.AthleteProfile1Id, default)).Count);
    }

    [Fact]
    public async Task Bulk_assignment_is_atomic_and_repeated_add_remove_is_idempotent()
    {
        await using var f = await TestFixture.CreateAsync(RoleNames.Coach, TestFixture.CoachUserId);
        var words = new AthleteWordWorkflow(f.Db, f.CurrentUser, f.Access);
        await Assert.ThrowsAsync<ForbiddenException>(() => words.ChangeAsync(new(TestFixture.AthleteProfile1Id, [TestFixture.AdminWordId, TestFixture.PassiveWordId], []), default));
        Assert.Empty(await words.GetCurrentAsync(TestFixture.AthleteProfile1Id, default));
        Assert.Empty(await f.Db.AthleteWordChanges.ToListAsync());
        await words.ChangeAsync(new(TestFixture.AthleteProfile1Id, [TestFixture.AdminWordId, TestFixture.ChildWordId], []), default);
        await words.ChangeAsync(new(TestFixture.AthleteProfile1Id, [TestFixture.AdminWordId, TestFixture.ChildWordId], []), default);
        Assert.Equal(2, await f.Db.AthleteWordChanges.CountAsync());
        await words.ChangeAsync(new(TestFixture.AthleteProfile1Id, [], [TestFixture.AdminWordId]), default);
        await words.ChangeAsync(new(TestFixture.AthleteProfile1Id, [], [TestFixture.AdminWordId]), default);
        Assert.Equal(3, await f.Db.AthleteWordChanges.CountAsync());
        await words.ChangeAsync(new(TestFixture.AthleteProfile1Id, [TestFixture.AdminWordId], []), default);
        Assert.Equal(4, await f.Db.AthleteWordChanges.CountAsync());
        Assert.Equal(2, (await words.GetCurrentAsync(TestFixture.AthleteProfile1Id, default)).Count);
    }

    [Fact]
    public async Task Assignment_without_sessions_is_preserved_and_captured_at_first_meeting()
    {
        await using var f = await TestFixture.CreateAsync(RoleNames.Admin, TestFixture.AdminUserId);
        f.Db.AthleteSessions.RemoveRange(f.Db.AthleteSessions);
        await f.Db.SaveChangesAsync();
        var words = new AthleteWordWorkflow(f.Db, f.CurrentUser, f.Access);
        await words.ChangeAsync(new(TestFixture.AthleteProfile2Id, [TestFixture.AdminWordId, TestFixture.ChildWordId], []), default);
        Assert.All(await f.Db.AthleteWordChanges.ToListAsync(), x => Assert.Null(x.SessionId));
        var sessions = new AthleteSessionService(f.Db, f.CurrentUser, f.Access, f.Audit);
        var meeting = await sessions.CreateAsync(new(TestFixture.AthleteProfile2Id, "", DateTimeOffset.Now, null), default);
        var detail = await sessions.GetDetailAsync(TestFixture.AthleteProfile2Id, meeting.Id, default);
        Assert.Equal(1, meeting.SessionNumber);
        Assert.Equal(2, detail.BeginningWords.Count);
        Assert.Equal(2, detail.EndingWords.Count);
        Assert.Empty(detail.Changes);
    }

    [Fact]
    public async Task Assignment_removal_checks_current_relationship_even_for_original_assigner()
    {
        await using var f = await TestFixture.CreateAsync(RoleNames.Coach, TestFixture.CoachUserId);
        var workflow = new AthleteWordWorkflow(f.Db, f.CurrentUser, f.Access);
        await workflow.ChangeAsync(new(TestFixture.AthleteProfile1Id, [TestFixture.AdminWordId], []), default);
        var assignment = await f.Db.AthleteWordAssignments.SingleAsync();
        var relation = await f.Db.AthleteRelations.FirstAsync(x => x.RelatedUserId == TestFixture.CoachUserId);
        relation.IsActive = false;
        await f.Db.SaveChangesAsync();
        var service = new MotivationWordService(f.Db, f.CurrentUser, f.Access, f.Audit);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.RemoveAthleteAssignmentAsync(assignment.Id, default));
        Assert.True((await f.Db.AthleteWordAssignments.SingleAsync()).IsActive);
    }
    [Fact]
    public async Task Correcting_early_session_replays_later_events_and_preserves_notes_dates_and_ids()
    {
        await using var f = await TestFixture.CreateAsync(RoleNames.Coach, TestFixture.CoachUserId);
        var service = new AthleteSessionService(f.Db, f.CurrentUser, f.Access, f.Audit);
        var words = new AthleteWordWorkflow(f.Db, f.CurrentUser, f.Access);
        var first = await service.CreateAsync(new(TestFixture.AthleteProfile1Id, "", DateTimeOffset.Now, null), default);
        await words.ChangeAsync(new(TestFixture.AthleteProfile1Id, [TestFixture.AdminWordId], [], first.Id), default);
        var second = await service.CreateAsync(new(TestFixture.AthleteProfile1Id, "", DateTimeOffset.Now, null), default);
        await words.ChangeAsync(new(TestFixture.AthleteProfile1Id, [TestFixture.ChildWordId], [], second.Id), default);
        await service.SaveNotesAsync(new(second.Id, new DateTimeOffset(2026, 9, 4, 0, 0, 0, TimeSpan.FromHours(3)), "Sonraki özel", "Sonraki ortak"), default);
        var before = await service.GetDetailAsync(first.AthleteProfileId, second.Id, default);
        var original = await service.GetDetailAsync(first.AthleteProfileId, first.Id, default);
        var request = new EditSessionRequest(first.AthleteProfileId, first.Id, original.Revision, first.SessionDate, "Düzeltildi", "Ortak",
            [original.Changes.Single().Id], []);
        await service.EditAsync(request, default);
        var after = await service.GetDetailAsync(first.AthleteProfileId, second.Id, default);
        Assert.Empty(after.BeginningWords);
        Assert.Equal(TestFixture.ChildWordId, Assert.Single(after.EndingWords).MotivationWordId);
        Assert.Equal(before.Changes.Single(), after.Changes.Single());
        Assert.Equal(before.PrivateCoachNote, after.PrivateCoachNote);
        Assert.Equal(before.SharedNote, after.SharedNote);
        Assert.Equal(before.Session.SessionDate, after.Session.SessionDate);
        Assert.Equal(TestFixture.ChildWordId, Assert.Single(await words.GetCurrentAsync(first.AthleteProfileId, default)).MotivationWordId);
        await Assert.ThrowsAsync<ConflictException>(() => service.EditAsync(request, default));
        Assert.Single((await service.GetDetailAsync(first.AthleteProfileId, second.Id, default)).Changes);
    }

    [Fact]
    public async Task Deleting_middle_session_removes_its_addition_and_preserves_later_removal()
    {
        await using var f = await TestFixture.CreateAsync(RoleNames.Coach, TestFixture.CoachUserId);
        var service = new AthleteSessionService(f.Db, f.CurrentUser, f.Access, f.Audit);
        var words = new AthleteWordWorkflow(f.Db, f.CurrentUser, f.Access);
        await words.ChangeAsync(new(TestFixture.AthleteProfile1Id, [TestFixture.AdminWordId], []), default);
        var middle = await service.CreateAsync(new(TestFixture.AthleteProfile1Id, "", DateTimeOffset.Now, null), default);
        await words.ChangeAsync(new(middle.AthleteProfileId, [TestFixture.ChildWordId], [], middle.Id), default);
        var last = await service.CreateAsync(new(middle.AthleteProfileId, "", DateTimeOffset.Now, null), default);
        await words.ChangeAsync(new(middle.AthleteProfileId, [], [TestFixture.AdminWordId], last.Id), default);
        var laterEvent = (await service.GetDetailAsync(last.AthleteProfileId, last.Id, default)).Changes.Single();
        var detail = await service.GetDetailAsync(middle.AthleteProfileId, middle.Id, default);
        await service.DeleteAsync(new(middle.AthleteProfileId, middle.Id, detail.Revision), default);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetDetailAsync(middle.AthleteProfileId, middle.Id, default));
        var result = await service.GetDetailAsync(last.AthleteProfileId, last.Id, default);
        Assert.Equal(TestFixture.AdminWordId, Assert.Single(result.BeginningWords).MotivationWordId);
        Assert.Empty(result.EndingWords);
        Assert.Equal(laterEvent, Assert.Single(result.Changes));
        Assert.Empty(await words.GetCurrentAsync(last.AthleteProfileId, default));
        Assert.Empty(await f.Db.AthleteWordChanges.Where(x => x.SessionId == middle.Id).ToListAsync());
        Assert.True((await f.Db.AthleteSessions.IgnoreQueryFilters().SingleAsync(x => x.Id == middle.Id)).IsDeleted);
    }

    [Fact]
    public async Task Deleting_first_and_only_session_restores_baseline_and_does_not_reuse_number()
    {
        await using var f = await TestFixture.CreateAsync(RoleNames.Admin, TestFixture.AdminUserId);
        f.Db.AthleteSessions.RemoveRange(f.Db.AthleteSessions);
        await f.Db.SaveChangesAsync();
        var service = new AthleteSessionService(f.Db, f.CurrentUser, f.Access, f.Audit);
        var words = new AthleteWordWorkflow(f.Db, f.CurrentUser, f.Access);
        await words.ChangeAsync(new(TestFixture.AthleteProfile1Id, [TestFixture.AdminWordId], []), default);
        var session = await service.CreateAsync(new(TestFixture.AthleteProfile1Id, "", DateTimeOffset.Now, null), default);
        await words.ChangeAsync(new(session.AthleteProfileId, [TestFixture.ChildWordId], [TestFixture.AdminWordId]), default);
        var detail = await service.GetDetailAsync(session.AthleteProfileId, session.Id, default);
        await service.DeleteAsync(new(session.AthleteProfileId, session.Id, detail.Revision), default);
        Assert.Equal(TestFixture.AdminWordId, Assert.Single(await words.GetCurrentAsync(session.AthleteProfileId, default)).MotivationWordId);
        var next = await service.CreateAsync(new(session.AthleteProfileId, "", DateTimeOffset.Now, null), default);
        Assert.Equal(2, next.SessionNumber);
        Assert.NotEqual(session.Id, next.Id);
        Assert.Equal(TestFixture.AdminWordId, Assert.Single((await service.GetDetailAsync(next.AthleteProfileId, next.Id, default)).BeginningWords).MotivationWordId);
    }

    [Theory]
    [InlineData(RoleNames.Athlete)]
    [InlineData(RoleNames.Parent)]
    public async Task Readers_cannot_edit_or_delete_session_history(string role)
    {
        await using var f = await TestFixture.CreateAsync(role, role == RoleNames.Athlete ? TestFixture.AthleteUser1Id : TestFixture.ParentUserId);
        var service = new AthleteSessionService(f.Db, f.CurrentUser, f.Access, f.Audit);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.EditAsync(new(TestFixture.AthleteProfile1Id, TestFixture.Session1Id, "", DateTimeOffset.Now, null, null, [], []), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.DeleteAsync(new(TestFixture.AthleteProfile1Id, TestFixture.Session1Id, ""), default));
    }

    [Fact]
    public async Task Coach_cannot_delete_another_creators_session_or_edit_unrelated_athlete()
    {
        await using var f = await TestFixture.CreateAsync(RoleNames.Coach, TestFixture.CoachUserId);
        var service = new AthleteSessionService(f.Db, f.CurrentUser, f.Access, f.Audit);
        var detail = await service.GetDetailAsync(TestFixture.AthleteProfile1Id, TestFixture.Session1Id, default);
        Assert.False(detail.CanDelete);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.DeleteAsync(new(TestFixture.AthleteProfile1Id, TestFixture.Session1Id, detail.Revision), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.DeleteAsync(new(TestFixture.AthleteProfile2Id, TestFixture.Session3Id, ""), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.EditAsync(new(TestFixture.AthleteProfile2Id, TestFixture.Session3Id, "", DateTimeOffset.Now, null, null, [], []), default));
    }

    [Fact]
    public async Task Invalid_history_edit_rolls_back_notes_events_and_assignments()
    {
        await using var f = await TestFixture.CreateAsync(RoleNames.Coach, TestFixture.CoachUserId);
        var service = new AthleteSessionService(f.Db, f.CurrentUser, f.Access, f.Audit);
        var session = await service.CreateAsync(new(TestFixture.AthleteProfile1Id, "", DateTimeOffset.Now, null), default);
        var detail = await service.GetDetailAsync(session.AthleteProfileId, session.Id, default);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.EditAsync(new(session.AthleteProfileId, session.Id, detail.Revision, session.SessionDate, "Kaydedilmemeli", null, [],
            [new(TestFixture.AdminWordId, true), new(TestFixture.PassiveWordId, true)]), default));
        var after = await service.GetDetailAsync(session.AthleteProfileId, session.Id, default);
        Assert.Equal(detail.Revision, after.Revision);
        Assert.Empty(after.Changes);
        Assert.Empty(await f.Db.AthleteWordAssignments.ToListAsync());
    }

    [Fact]
    public async Task Removing_wrong_removal_restores_word_in_all_later_sessions()
    {
        await using var f = await TestFixture.CreateAsync(RoleNames.Coach, TestFixture.CoachUserId);
        var service = new AthleteSessionService(f.Db, f.CurrentUser, f.Access, f.Audit);
        var words = new AthleteWordWorkflow(f.Db, f.CurrentUser, f.Access);
        await words.ChangeAsync(new(TestFixture.AthleteProfile1Id, [TestFixture.AdminWordId], []), default);
        var first = await service.CreateAsync(new(TestFixture.AthleteProfile1Id, "", DateTimeOffset.Now, null), default);
        await words.ChangeAsync(new(first.AthleteProfileId, [], [TestFixture.AdminWordId], first.Id), default);
        var second = await service.CreateAsync(new(first.AthleteProfileId, "", DateTimeOffset.Now, null), default);
        var detail = await service.GetDetailAsync(first.AthleteProfileId, first.Id, default);
        await service.EditAsync(new(first.AthleteProfileId, first.Id, detail.Revision, first.SessionDate, null, null, [detail.Changes.Single().Id], [new(TestFixture.ChildWordId, true), new(TestFixture.ChildWordId, true)]), default);
        var after = await service.GetDetailAsync(second.AthleteProfileId, second.Id, default);
        Assert.Equal(2, after.BeginningWords.Count);
        Assert.Equal(2, after.EndingWords.Count);
        Assert.Single((await service.GetDetailAsync(first.AthleteProfileId, first.Id, default)).Changes);
        Assert.Equal(2, (await words.GetCurrentAsync(first.AthleteProfileId, default)).Count);
    }
    [Fact]
    public async Task Failure_after_recalculation_rolls_back_entire_database_transaction()
    {
        await using var f = await TestFixture.CreateAsync(RoleNames.Coach, TestFixture.CoachUserId);
        var service = new AthleteSessionService(f.Db, f.CurrentUser, f.Access, f.Audit);
        var words = new AthleteWordWorkflow(f.Db, f.CurrentUser, f.Access);
        var first = await service.CreateAsync(new(TestFixture.AthleteProfile1Id, "", DateTimeOffset.Now, null), default);
        await words.ChangeAsync(new(first.AthleteProfileId, [TestFixture.AdminWordId], []), default);
        var detail = await service.GetDetailAsync(first.AthleteProfileId, first.Id, default);
        var failing = new AthleteSessionService(f.Db, f.CurrentUser, f.Access, new FailingAuditService());
        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.DeleteAsync(new(first.AthleteProfileId, first.Id, detail.Revision), default));
        Assert.Equal(detail.Revision, (await service.GetDetailAsync(first.AthleteProfileId, first.Id, default)).Revision);
        Assert.Equal(TestFixture.AdminWordId, Assert.Single(await words.GetCurrentAsync(first.AthleteProfileId, default)).MotivationWordId);
        Assert.Single(await f.Db.AthleteWordChanges.Where(x => x.SessionId == first.Id).ToListAsync());
    }

    [Fact]
    public async Task Legacy_note_edit_preserves_existing_assignments_without_inventing_history()
    {
        await using var f = await TestFixture.CreateAsync(RoleNames.Coach, TestFixture.CoachUserId);
        f.Db.AthleteWordAssignments.Add(new AthleteWordAssignment { AthleteProfileId = TestFixture.AthleteProfile1Id, MotivationWordId = TestFixture.AdminWordId, AssignedByUserId = TestFixture.CoachUserId });
        await f.Db.SaveChangesAsync();
        var service = new AthleteSessionService(f.Db, f.CurrentUser, f.Access, f.Audit);
        var detail = await service.GetDetailAsync(TestFixture.AthleteProfile1Id, TestFixture.Session2Id, default);
        await service.EditAsync(new(detail.Session.AthleteProfileId, detail.Session.Id, detail.Revision, detail.Session.SessionDate, "Eski not", "Paylaşılan", [], []), default);
        Assert.True((await f.Db.AthleteWordAssignments.SingleAsync()).IsActive);
        Assert.Empty(await f.Db.AthleteWordChanges.ToListAsync());
    }

    [Fact]
    public async Task Deleting_first_session_rebases_next_and_keeps_later_explicit_addition()
    {
        await using var f = await TestFixture.CreateAsync(RoleNames.Coach, TestFixture.CoachUserId);
        var service = new AthleteSessionService(f.Db, f.CurrentUser, f.Access, f.Audit);
        var words = new AthleteWordWorkflow(f.Db, f.CurrentUser, f.Access);
        var first = await service.CreateAsync(new(TestFixture.AthleteProfile1Id, "", DateTimeOffset.Now, null), default);
        await words.ChangeAsync(new(first.AthleteProfileId, [TestFixture.AdminWordId], []), default);
        var second = await service.CreateAsync(new(first.AthleteProfileId, "", DateTimeOffset.Now, null), default);
        // A later explicit intention must survive even when currently redundant.
        var secondDetail = await service.GetDetailAsync(second.AthleteProfileId, second.Id, default);
        await service.EditAsync(new(second.AthleteProfileId, second.Id, secondDetail.Revision, second.SessionDate, null, null, [], [new(TestFixture.AdminWordId, true)]), default);
        var firstDetail = await service.GetDetailAsync(first.AthleteProfileId, first.Id, default);
        await service.DeleteAsync(new(first.AthleteProfileId, first.Id, firstDetail.Revision), default);
        var result = await service.GetDetailAsync(second.AthleteProfileId, second.Id, default);
        Assert.Empty(result.BeginningWords);
        Assert.Equal(TestFixture.AdminWordId, Assert.Single(result.EndingWords).MotivationWordId);
        Assert.Single(result.Changes);
    }

    [Fact]
    public async Task Corrected_legacy_selection_does_not_reappear_in_detail_or_reports()
    {
        await using var f = await TestFixture.CreateAsync(RoleNames.Coach, TestFixture.CoachUserId);
        f.Db.SessionWords.Add(new SessionWord { SessionId = TestFixture.Session2Id, MotivationWordId = TestFixture.AdminWordId, WordTextSnapshot = "Güç" });
        f.Db.AthleteWordAssignments.Add(new AthleteWordAssignment { AthleteProfileId = TestFixture.AthleteProfile1Id, MotivationWordId = TestFixture.AdminWordId, AssignedByUserId = TestFixture.CoachUserId });
        await f.Db.SaveChangesAsync();
        var service = new AthleteSessionService(f.Db, f.CurrentUser, f.Access, f.Audit);
        var detail = await service.GetDetailAsync(TestFixture.AthleteProfile1Id, TestFixture.Session2Id, default);
        await service.EditAsync(new(detail.Session.AthleteProfileId, detail.Session.Id, detail.Revision, detail.Session.SessionDate, null, null, [detail.Changes.Single().Id], []), default);
        var after = await service.GetDetailAsync(detail.Session.AthleteProfileId, detail.Session.Id, default);
        Assert.Empty(after.Changes);
        Assert.Empty(after.EndingWords);
        Assert.True(after.HasWordHistory);
        var reports = new DashboardService(f.Db);
        Assert.Empty(await reports.GetTopWordsAsync(default));
        await service.EditAsync(new(after.Session.AthleteProfileId, after.Session.Id, after.Revision, after.Session.SessionDate, "Not", null, [], []), default);
        Assert.Empty((await service.GetDetailAsync(after.Session.AthleteProfileId, after.Session.Id, default)).Changes);
    }
    private sealed class FailingAuditService : IAuditService
    {
        public Task WriteAsync(string action, string entityName, string entityId, object? oldValues, object? newValues, CancellationToken cancellationToken) => throw new InvalidOperationException("Simulated audit failure");
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
