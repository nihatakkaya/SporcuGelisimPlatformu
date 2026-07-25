using Microsoft.EntityFrameworkCore;
using SporcuGelisim.Application.Common;
using SporcuGelisim.Application.DTOs;
using SporcuGelisim.Application.Services;
using SporcuGelisim.Domain.Common;
using SporcuGelisim.Domain.Entities;
using SporcuGelisim.Infrastructure.Data;

namespace SporcuGelisim.Infrastructure.Services;

public sealed class BranchService(ApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit) : IBranchService
{
    public async Task<BranchDto> CreateAsync(CreateBranchRequest request, CancellationToken cancellationToken)
    {
        EnsureAdmin();
        await EnsureUniqueNameAsync(request.Name, request.ParentBranchId, null, cancellationToken);
        if (request.ParentBranchId.HasValue)
        {
            await EnsureBranchExistsAsync(request.ParentBranchId.Value, cancellationToken);
        }

        var entity = new SportBranch
        {
            Name = request.Name.Trim(),
            Slug = Slugify(request.Name),
            Description = request.Description,
            ParentBranchId = request.ParentBranchId,
            DisplayOrder = request.DisplayOrder,
            CreatedByUserId = currentUser.UserId
        };

        db.SportBranches.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("BranchCreated", nameof(SportBranch), entity.Id.ToString(), null, entity, cancellationToken);
        return ToDto(entity);
    }

    public async Task<BranchDto> UpdateAsync(UpdateBranchRequest request, CancellationToken cancellationToken)
    {
        EnsureAdmin();
        var entity = await db.SportBranches.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Branş bulunamadı.");
        await EnsureUniqueNameAsync(request.Name, request.ParentBranchId, request.Id, cancellationToken);
        await EnsureNoCycleAsync(request.Id, request.ParentBranchId, cancellationToken);

        entity.Name = request.Name.Trim();
        entity.Slug = Slugify(request.Name);
        entity.Description = request.Description;
        entity.ParentBranchId = request.ParentBranchId;
        entity.DisplayOrder = request.DisplayOrder;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("BranchUpdated", nameof(SportBranch), entity.Id.ToString(), null, entity, cancellationToken);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<BranchDto>> GetTreeAsync(CancellationToken cancellationToken) =>
        await db.SportBranches.AsNoTracking()
            .OrderBy(x => x.ParentBranchId).ThenBy(x => x.DisplayOrder).ThenBy(x => x.Name)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);

    private void EnsureAdmin()
    {
        if (!currentUser.Roles.Contains(RoleNames.Admin))
        {
            throw new ForbiddenException("Bu işlem yalnızca Admin rolüyle yapılabilir.");
        }
    }

    private async Task EnsureBranchExistsAsync(Guid branchId, CancellationToken cancellationToken)
    {
        var exists = await db.SportBranches.AnyAsync(x => x.Id == branchId && x.IsActive, cancellationToken);
        if (!exists)
        {
            throw new ConflictException("Pasif veya silinmiş branş parent olarak seçilemez.");
        }
    }

    private async Task EnsureUniqueNameAsync(string name, Guid? parentId, Guid? excludedId, CancellationToken cancellationToken)
    {
        var normalized = name.Trim();
        var exists = await db.SportBranches.AnyAsync(x =>
            x.Name == normalized && x.ParentBranchId == parentId && (!excludedId.HasValue || x.Id != excludedId), cancellationToken);
        if (exists)
        {
            throw new ConflictException("Aynı üst branş altında aynı isimde branş bulunuyor.");
        }
    }

    private async Task EnsureNoCycleAsync(Guid branchId, Guid? parentId, CancellationToken cancellationToken)
    {
        if (branchId == parentId)
        {
            throw new ConflictException("Bir branş kendisinin üst branşı olamaz.");
        }

        var visited = new HashSet<Guid> { branchId };
        var currentParent = parentId;
        for (var depth = 0; currentParent.HasValue && depth < 32; depth++)
        {
            if (!visited.Add(currentParent.Value))
            {
                throw new ConflictException("Branş hiyerarşisinde döngü oluşturulamaz.");
            }

            currentParent = await db.SportBranches.AsNoTracking()
                .Where(x => x.Id == currentParent.Value)
                .Select(x => x.ParentBranchId)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }

    public static string Slugify(string value) =>
        value.Trim().ToLowerInvariant()
            .Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s").Replace("ö", "o").Replace("ç", "c")
            .Replace(" ", "-");

    private static BranchDto ToDto(SportBranch entity) =>
        new(entity.Id, entity.Name, entity.Slug, entity.Description, entity.ParentBranchId, entity.IsActive, entity.DisplayOrder);
}
