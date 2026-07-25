using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SporcuGelisim.Domain.Common;
using SporcuGelisim.Domain.Entities;
using SporcuGelisim.Domain.Enums;
using SporcuGelisim.Infrastructure.Identity;

namespace SporcuGelisim.Infrastructure.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<AthleteProfile> AthleteProfiles => Set<AthleteProfile>();
    public DbSet<SportBranch> SportBranches => Set<SportBranch>();
    public DbSet<MotivationWord> MotivationWords => Set<MotivationWord>();
    public DbSet<WordBranch> WordBranches => Set<WordBranch>();
    public DbSet<AthleteRelation> AthleteRelations => Set<AthleteRelation>();
    public DbSet<AthleteSession> AthleteSessions => Set<AthleteSession>();
    public DbSet<SessionWord> SessionWords => Set<SessionWord>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<FileAsset> FileAssets => Set<FileAsset>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureBase<AthleteProfile>(builder);
        ConfigureBase<SportBranch>(builder);
        ConfigureBase<MotivationWord>(builder);
        ConfigureBase<AthleteRelation>(builder);
        ConfigureBase<AthleteSession>(builder);
        ConfigureBase<SessionWord>(builder);
        ConfigureBase<Feedback>(builder);
        ConfigureBase<FileAsset>(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.FirstName).HasMaxLength(100);
            entity.Property(x => x.LastName).HasMaxLength(100);
            entity.HasIndex(x => x.IsActive);
            entity.HasOne<FileAsset>().WithMany().HasForeignKey(x => x.ProfilePhotoId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<AthleteProfile>(entity =>
        {
            entity.Property(x => x.Biography).HasMaxLength(2000);
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<SportBranch>().WithMany().HasForeignKey(x => x.PrimaryBranchId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<SportBranch>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.HasIndex(x => new { x.ParentBranchId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
            entity.HasOne<SportBranch>().WithMany().HasForeignKey(x => x.ParentBranchId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<MotivationWord>(entity =>
        {
            entity.Property(x => x.Text).HasMaxLength(120).IsRequired();
            entity.Property(x => x.NormalizedText).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.CreatedByRole).HasMaxLength(40);
            entity.HasIndex(x => x.NormalizedText).IsUnique().HasFilter("[IsDeleted] = 0");
            entity.HasOne<MotivationWord>().WithMany().HasForeignKey(x => x.ParentWordId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<WordBranch>(entity =>
        {
            entity.HasKey(x => new { x.MotivationWordId, x.SportBranchId });
            entity.HasOne<MotivationWord>().WithMany().HasForeignKey(x => x.MotivationWordId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<SportBranch>().WithMany().HasForeignKey(x => x.SportBranchId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AthleteRelation>(entity =>
        {
            entity.HasOne<AthleteProfile>().WithMany().HasForeignKey(x => x.AthleteProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RelatedUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.AthleteProfileId, x.RelatedUserId, x.RelationType, x.IsActive })
                .IsUnique()
                .HasFilter("[IsActive] = 1 AND [IsDeleted] = 0");
        });

        builder.Entity<AthleteSession>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.HasOne<AthleteProfile>().WithMany().HasForeignKey(x => x.AthleteProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.AthleteProfileId, x.SessionNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        builder.Entity<SessionWord>(entity =>
        {
            entity.Property(x => x.WordTextSnapshot).HasMaxLength(120).IsRequired();
            entity.Property(x => x.SelectedByRole).HasMaxLength(40);
            entity.HasOne<AthleteSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<MotivationWord>().WithMany().HasForeignKey(x => x.MotivationWordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<SessionWord>().WithMany().HasForeignKey(x => x.CopiedFromSessionWordId).OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(x => new { x.SessionId, x.MotivationWordId }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        builder.Entity<Feedback>(entity =>
        {
            entity.Property(x => x.Comment).HasMaxLength(2000).IsRequired();
            entity.HasOne<AthleteProfile>().WithMany().HasForeignKey(x => x.AthleteProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AthleteSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FileAsset>(entity =>
        {
            entity.Property(x => x.OriginalFileName).HasMaxLength(260);
            entity.Property(x => x.StoredFileName).HasMaxLength(260);
            entity.Property(x => x.ContentType).HasMaxLength(100);
            entity.Property(x => x.RelativePath).HasMaxLength(500);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.Property(x => x.Action).HasMaxLength(120);
            entity.Property(x => x.EntityName).HasMaxLength(120);
            entity.Property(x => x.EntityId).HasMaxLength(80);
            entity.Property(x => x.IpAddress).HasMaxLength(80);
        });
    }

    private void ConfigureBase<TEntity>(ModelBuilder builder)
        where TEntity : BaseEntity
    {
        builder.Entity<TEntity>().HasKey(x => x.Id);
        var rowVersion = builder.Entity<TEntity>().Property(x => x.RowVersion);
        if (Database.IsSqlServer())
        {
            rowVersion.IsRowVersion();
        }
        else
        {
            rowVersion.IsConcurrencyToken().HasDefaultValue(Array.Empty<byte>());
        }
        builder.Entity<TEntity>().HasQueryFilter(x => !x.IsDeleted);
    }
}
