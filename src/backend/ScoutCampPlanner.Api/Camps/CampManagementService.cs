using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Platform.Application.Auditing;
using ScoutCampPlanner.Platform.Application.Authorization;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Infrastructure;
using ScoutCampPlanner.Platform.Infrastructure.Auditing;

namespace ScoutCampPlanner.Api.Camps;

public sealed record TenantOption(Guid Id, string Name);
public sealed record CampAdministratorOption(Guid MembershipId, Guid UserId, string Email);
public sealed record CampSummary(
    Guid Id, Guid TenantId, string Name, DateOnly? StartDate, DateOnly? EndDate,
    bool IsFrozen, bool CanEdit, bool CanExport);
public sealed record CreateCampRequest(
    string Name, DateOnly StartDate, DateOnly EndDate,
    IReadOnlyCollection<Guid>? InitialAdministratorMembershipIds);
public sealed record UpdateCampRequest(string Name, DateOnly StartDate, DateOnly EndDate);
public sealed record StructureNodeSummary(Guid Id, Guid CampId, Guid? ParentId, string Name);
public sealed record CreateStructureNodeRequest(Guid? ParentId, string Name);
public sealed record StructureConfiguration(string Mode, IReadOnlyList<string> LevelNames);
public sealed record UpdateStructureConfigurationRequest(IReadOnlyCollection<string>? LevelNames);

public enum CreateCampFailure
{
    None,
    Forbidden,
    InvalidName,
    InvalidPeriod,
    DuplicateCamp,
    MissingAdministrator,
    InvalidAdministrator,
}

public sealed record CreateCampResult(CampSummary? Camp, CreateCampFailure Failure)
{
    public bool IsSuccessful => Camp is not null && Failure == CreateCampFailure.None;
}

public enum UpdateCampFailure { None, NotFound, InvalidName, InvalidPeriod, DuplicateCamp, Frozen }
public sealed record UpdateCampResult(CampSummary? Camp, UpdateCampFailure Failure)
{
    public bool IsSuccessful => Camp is not null && Failure == UpdateCampFailure.None;
}

public enum CreateStructureNodeFailure { None, NotFound, InvalidName, DuplicateName, Frozen, MaximumDepthReached }
public sealed record CreateStructureNodeResult(
    StructureNodeSummary? Node, CreateStructureNodeFailure Failure)
{
    public bool IsSuccessful => Node is not null && Failure == CreateStructureNodeFailure.None;
}

public sealed class CampManagementService(
    PlatformDbContext platform,
    CampDbContext camps,
    IAuditedOperationExecutor auditedOperation,
    AuditRuntimeState auditRuntime,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<TenantOption>> ListTenantsAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        await platform.TenantMemberships
            .Where(membership => membership.UserId == userId && membership.State == TenantMembershipState.Active)
            .Join(platform.Tenants, membership => membership.TenantId, tenant => tenant.Id,
                (_, tenant) => tenant)
            .OrderBy(tenant => tenant.Name)
            .Select(tenant => new TenantOption(tenant.Id, tenant.Name))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CampAdministratorOption>> ListAdministratorCandidatesAsync(
        Guid actorUserId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (!await HasTenantPermissionAsync(actorUserId, tenantId, Permissions.Tenant.AssignCampMembers, cancellationToken))
            return [];

        return await platform.TenantMemberships
            .Where(membership => membership.TenantId == tenantId && membership.State == TenantMembershipState.Active)
            .Join(platform.UserAccounts.Where(user => user.State == UserAccountState.Active),
                membership => membership.UserId, user => user.Id,
                (membership, user) => new { MembershipId = membership.Id, User = user })
            .OrderBy(candidate => candidate.User.Email)
            .Select(candidate => new CampAdministratorOption(
                candidate.MembershipId, candidate.User.Id, candidate.User.Email))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CampSummary>> ListCampsAsync(
        Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        Guid[] campIds = await GetAuthorizedCampIdsAsync(
            userId, tenantId, Permissions.Camp.View, cancellationToken);
        HashSet<Guid> editableCampIds = (await GetAuthorizedCampIdsAsync(
            userId, tenantId, Permissions.Camp.Edit, cancellationToken)).ToHashSet();
        HashSet<Guid> exportableCampIds = (await GetAuthorizedCampIdsAsync(
            userId, tenantId, Permissions.Camp.ExportPackage, cancellationToken)).ToHashSet();

        var visibleCamps = await camps.Camps.Where(camp => camp.TenantId == tenantId && campIds.Contains(camp.Id))
            .OrderBy(camp => camp.Name)
            .Select(camp => new CampSummary(
                camp.Id, camp.TenantId, camp.Name, camp.StartDate, camp.EndDate, camp.IsFrozen, false, false))
            .ToListAsync(cancellationToken);
        return visibleCamps.Select(camp => camp with
        {
            CanEdit = editableCampIds.Contains(camp.Id),
            CanExport = exportableCampIds.Contains(camp.Id),
        }).ToList();
    }

    public async Task<bool> HasCampPermissionAsync(
        Guid userId, Guid campId, string permission, CancellationToken cancellationToken = default)
    {
        Guid? tenantId = await camps.Camps.Where(camp => camp.Id == campId)
            .Select(camp => (Guid?)camp.TenantId).SingleOrDefaultAsync(cancellationToken);
        return tenantId is not null &&
            (await GetAuthorizedCampIdsAsync(userId, tenantId.Value, permission, cancellationToken)).Contains(campId);
    }

    public async Task<IReadOnlyList<StructureNodeSummary>?> ListStructureAsync(
        Guid actorUserId, Guid campId, CancellationToken cancellationToken = default)
    {
        if (!await HasCampPermissionAsync(actorUserId, campId, Permissions.Camp.View, cancellationToken))
            return null;
        return await camps.StructureNodes.Where(node => node.CampId == campId)
            .OrderBy(node => node.Name)
            .Select(node => new StructureNodeSummary(node.Id, node.CampId, node.ParentId, node.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<StructureConfiguration?> GetStructureConfigurationAsync(
        Guid actorUserId, Guid campId, CancellationToken cancellationToken = default)
    {
        if (!await HasCampPermissionAsync(actorUserId, campId, Permissions.Camp.View, cancellationToken)) return null;
        var camp = await camps.Camps.SingleAsync(value => value.Id == campId, cancellationToken);
        return new(camp.StructureMode.ToString(), camp.GetStructureLevelNames());
    }

    public async Task<bool> UpdateStructureConfigurationAsync(
        Guid actorUserId, Guid campId, UpdateStructureConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await HasCampPermissionAsync(actorUserId, campId, Permissions.Camp.Edit, cancellationToken)) return false;
        var camp = await camps.Camps.SingleOrDefaultAsync(value => value.Id == campId, cancellationToken);
        if (camp is null || camp.IsFrozen) return false;
        string[] levels = request.LevelNames?.ToArray() ?? [];
        int maximumExistingDepth = 0;
        var nodes = await camps.StructureNodes.Where(node => node.CampId == campId).ToListAsync(cancellationToken);
        var byId = nodes.ToDictionary(node => node.Id);
        foreach (var node in nodes)
        {
            int depth = 1; Guid? parentId = node.ParentId;
            while (parentId is Guid id && byId.TryGetValue(id, out var parent)) { depth++; parentId = parent.ParentId; }
            maximumExistingDepth = Math.Max(maximumExistingDepth, depth);
        }
        if (levels.Length > 0 && levels.Length < maximumExistingDepth) return false;
        try { camp.ConfigureStructure(levels); }
        catch (ArgumentException) { return false; }
        var auditEvent = new AuditEventDraft(
            Guid.NewGuid(), timeProvider.GetUtcNow(), "camp.structure-configuration.updated", "success",
            actorUserId, camp.TenantId, camp.Id, "camp", camp.Id, "server", auditRuntime.InstanceId,
            Guid.NewGuid(), null, null, new Dictionary<string, string>
            {
                ["mode"] = camp.StructureMode.ToString(), ["levelCount"] = levels.Length.ToString(),
            });
        await auditedOperation.ExecuteAsync(auditEvent, async operationCancellationToken =>
        {
            var transaction = platform.Database.CurrentTransaction
                ?? throw new InvalidOperationException("The Platform transaction is unavailable.");
            await camps.Database.UseTransactionAsync(transaction.GetDbTransaction(), operationCancellationToken);
            try { await camps.SaveChangesAsync(operationCancellationToken); }
            finally { await camps.Database.UseTransactionAsync(null, CancellationToken.None); }
        }, cancellationToken);
        return true;
    }

    public async Task<CreateStructureNodeResult> CreateStructureNodeAsync(
        Guid actorUserId, Guid campId, CreateStructureNodeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
            return new(null, CreateStructureNodeFailure.InvalidName);
        if (!await HasCampPermissionAsync(actorUserId, campId, Permissions.Camp.Edit, cancellationToken))
            return new(null, CreateStructureNodeFailure.NotFound);
        var camp = await camps.Camps.SingleOrDefaultAsync(value => value.Id == campId, cancellationToken);
        if (camp is null) return new(null, CreateStructureNodeFailure.NotFound);
        if (camp.IsFrozen) return new(null, CreateStructureNodeFailure.Frozen);
        if (request.ParentId is Guid parentId && !await camps.StructureNodes.AnyAsync(
            node => node.Id == parentId && node.CampId == campId, cancellationToken))
            return new(null, CreateStructureNodeFailure.NotFound);

        if (camp.StructureMode == ScoutCampPlanner.Camp.Domain.CampStructureMode.Fixed)
        {
            int depth = 1; Guid? current = request.ParentId;
            while (current is Guid id)
            {
                var ancestor = await camps.StructureNodes.AsNoTracking().SingleAsync(
                    node => node.Id == id && node.CampId == campId, cancellationToken);
                depth++; current = ancestor.ParentId;
            }
            if (depth > camp.GetStructureLevelNames().Count)
                return new(null, CreateStructureNodeFailure.MaximumDepthReached);
        }

        string normalizedName = request.Name.Trim().ToUpperInvariant();
        if (await camps.StructureNodes.AnyAsync(node => node.CampId == campId &&
            node.ParentId == request.ParentId && node.NormalizedName == normalizedName, cancellationToken))
            return new(null, CreateStructureNodeFailure.DuplicateName);

        var node = new ScoutCampPlanner.Camp.Domain.StructureNode(
            Guid.NewGuid(), campId, request.ParentId, request.Name);
        var auditEvent = new AuditEventDraft(
            Guid.NewGuid(), timeProvider.GetUtcNow(), "camp.structure-node.created", "success",
            actorUserId, camp.TenantId, camp.Id, "camp-structure-node", node.Id, "server",
            auditRuntime.InstanceId, Guid.NewGuid(), null, null,
            request.ParentId is Guid parent
                ? new Dictionary<string, string> { ["parentNodeId"] = parent.ToString() }
                : new Dictionary<string, string>());
        try
        {
            await auditedOperation.ExecuteAsync(auditEvent, async operationCancellationToken =>
            {
                var transaction = platform.Database.CurrentTransaction
                    ?? throw new InvalidOperationException("The Platform transaction is unavailable.");
                await camps.Database.UseTransactionAsync(transaction.GetDbTransaction(), operationCancellationToken);
                try
                {
                    camps.StructureNodes.Add(node);
                    await camps.SaveChangesAsync(operationCancellationToken);
                }
                finally
                {
                    await camps.Database.UseTransactionAsync(null, CancellationToken.None);
                }
            }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            camps.ChangeTracker.Clear();
            if (await camps.StructureNodes.AsNoTracking().AnyAsync(existing => existing.CampId == campId &&
                existing.ParentId == request.ParentId && existing.NormalizedName == normalizedName,
                cancellationToken))
                return new(null, CreateStructureNodeFailure.DuplicateName);
            throw;
        }
        catch
        {
            camps.ChangeTracker.Clear();
            throw;
        }

        return new(new StructureNodeSummary(node.Id, node.CampId, node.ParentId, node.Name),
            CreateStructureNodeFailure.None);
    }

    public async Task<CreateCampResult> CreateAsync(
        Guid actorUserId, Guid tenantId, CreateCampRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
            return new(null, CreateCampFailure.InvalidName);
        if (request.StartDate == default || request.EndDate == default || request.EndDate < request.StartDate)
            return new(null, CreateCampFailure.InvalidPeriod);
        Guid[] administratorIds = request.InitialAdministratorMembershipIds?.Distinct().ToArray() ?? [];
        if (administratorIds.Length == 0)
            return new(null, CreateCampFailure.MissingAdministrator);
        if (!await HasTenantPermissionAsync(actorUserId, tenantId, Permissions.Tenant.CreateCamps, cancellationToken) ||
            !await HasTenantPermissionAsync(actorUserId, tenantId, Permissions.Tenant.AssignCampMembers, cancellationToken))
            return new(null, CreateCampFailure.Forbidden);

        var administrators = await platform.TenantMemberships
            .Where(membership => administratorIds.Contains(membership.Id) && membership.TenantId == tenantId &&
                membership.State == TenantMembershipState.Active)
            .Where(membership => platform.UserAccounts.Any(user =>
                user.Id == membership.UserId && user.State == UserAccountState.Active))
            .ToListAsync(cancellationToken);
        if (administrators.Count != administratorIds.Length)
            return new(null, CreateCampFailure.InvalidAdministrator);

        string normalizedName = request.Name.Trim().ToUpperInvariant();
        if (await camps.Camps.AnyAsync(existing => existing.TenantId == tenantId &&
            existing.NormalizedName == normalizedName && existing.StartDate == request.StartDate &&
            existing.EndDate == request.EndDate, cancellationToken))
            return new(null, CreateCampFailure.DuplicateCamp);

        var camp = new Camp.Domain.Camp(
            Guid.NewGuid(), tenantId, request.Name, request.StartDate, request.EndDate);
        var auditEvent = new AuditEventDraft(
            Guid.NewGuid(), timeProvider.GetUtcNow(), "camp.created", "success", actorUserId, tenantId, camp.Id,
            "camp", camp.Id, "server", auditRuntime.InstanceId, Guid.NewGuid(), null, null,
            new Dictionary<string, string> { ["initialCampAdminCount"] = administratorIds.Length.ToString() });

        try
        {
            await auditedOperation.ExecuteAsync(auditEvent, async operationCancellationToken =>
            {
                var transaction = platform.Database.CurrentTransaction
                    ?? throw new InvalidOperationException("The Platform transaction is unavailable.");
                await camps.Database.UseTransactionAsync(transaction.GetDbTransaction(), operationCancellationToken);
                try
                {
                    camps.Camps.Add(camp);
                    foreach (TenantMembership administrator in administrators)
                    {
                        var membership = new CampMembership(Guid.NewGuid(), administrator.Id, camp.Id);
                        platform.CampMemberships.Add(membership);
                        platform.CampRoleAssignments.Add(new CampRoleAssignment(membership.Id, Roles.CampAdmin));
                    }
                    await camps.SaveChangesAsync(operationCancellationToken);
                }
                finally
                {
                    await camps.Database.UseTransactionAsync(null, CancellationToken.None);
                }
            }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            camps.ChangeTracker.Clear();
            if (await camps.Camps.AsNoTracking().AnyAsync(existing => existing.TenantId == tenantId &&
                existing.NormalizedName == normalizedName && existing.StartDate == request.StartDate &&
                existing.EndDate == request.EndDate, cancellationToken))
                return new(null, CreateCampFailure.DuplicateCamp);
            throw;
        }
        catch
        {
            camps.ChangeTracker.Clear();
            throw;
        }

        return new(new CampSummary(
            camp.Id, camp.TenantId, camp.Name, camp.StartDate, camp.EndDate, camp.IsFrozen, false, false),
            CreateCampFailure.None);
    }

    public async Task<UpdateCampResult> UpdateAsync(
        Guid actorUserId, Guid campId, UpdateCampRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
            return new(null, UpdateCampFailure.InvalidName);
        if (request.StartDate == default || request.EndDate == default || request.EndDate < request.StartDate)
            return new(null, UpdateCampFailure.InvalidPeriod);
        if (!await HasCampPermissionAsync(actorUserId, campId, Permissions.Camp.Edit, cancellationToken))
            return new(null, UpdateCampFailure.NotFound);

        var camp = await camps.Camps.SingleOrDefaultAsync(value => value.Id == campId, cancellationToken);
        if (camp is null) return new(null, UpdateCampFailure.NotFound);
        if (camp.IsFrozen) return new(null, UpdateCampFailure.Frozen);
        string normalizedName = request.Name.Trim().ToUpperInvariant();
        if (await camps.Camps.AnyAsync(existing => existing.Id != campId &&
            existing.TenantId == camp.TenantId && existing.NormalizedName == normalizedName &&
            existing.StartDate == request.StartDate && existing.EndDate == request.EndDate, cancellationToken))
            return new(null, UpdateCampFailure.DuplicateCamp);

        var auditEvent = new AuditEventDraft(
            Guid.NewGuid(), timeProvider.GetUtcNow(), "camp.updated", "success", actorUserId,
            camp.TenantId, camp.Id, "camp", camp.Id, "server", auditRuntime.InstanceId,
            Guid.NewGuid(), null, null, new Dictionary<string, string>());
        try
        {
            await auditedOperation.ExecuteAsync(auditEvent, async operationCancellationToken =>
            {
                var transaction = platform.Database.CurrentTransaction
                    ?? throw new InvalidOperationException("The Platform transaction is unavailable.");
                await camps.Database.UseTransactionAsync(transaction.GetDbTransaction(), operationCancellationToken);
                try
                {
                    camp.UpdateDetails(request.Name, request.StartDate, request.EndDate);
                    await camps.SaveChangesAsync(operationCancellationToken);
                }
                finally
                {
                    await camps.Database.UseTransactionAsync(null, CancellationToken.None);
                }
            }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            camps.ChangeTracker.Clear();
            if (await camps.Camps.AsNoTracking().AnyAsync(existing => existing.Id != campId &&
                existing.TenantId == camp.TenantId && existing.NormalizedName == normalizedName &&
                existing.StartDate == request.StartDate && existing.EndDate == request.EndDate, cancellationToken))
                return new(null, UpdateCampFailure.DuplicateCamp);
            throw;
        }
        catch
        {
            camps.ChangeTracker.Clear();
            throw;
        }

        bool canExport = await HasCampPermissionAsync(
            actorUserId, campId, Permissions.Camp.ExportPackage, cancellationToken);
        return new(new CampSummary(
            camp.Id, camp.TenantId, camp.Name, camp.StartDate, camp.EndDate, camp.IsFrozen, true, canExport),
            UpdateCampFailure.None);
    }

    private async Task<bool> HasTenantPermissionAsync(
        Guid userId, Guid tenantId, string permission, CancellationToken cancellationToken)
    {
        string[] roles = await platform.TenantMemberships
            .Where(membership => membership.UserId == userId && membership.TenantId == tenantId &&
                membership.State == TenantMembershipState.Active)
            .Join(platform.TenantRoleAssignments, membership => membership.Id, role => role.MembershipId,
                (_, role) => role.RoleIdentifier)
            .ToArrayAsync(cancellationToken);
        return TenantRoleSetValidator.Validate(roles).IsValid &&
            AuthorizationCatalogue.ResolvePermissions(AuthorizationScope.Tenant, roles).Contains(permission);
    }


    private async Task<Guid[]> GetAuthorizedCampIdsAsync(
        Guid userId, Guid tenantId, string permission, CancellationToken cancellationToken)
    {
        var assignments = await platform.TenantMemberships
            .Where(tenantMembership => tenantMembership.UserId == userId &&
                tenantMembership.TenantId == tenantId && tenantMembership.State == TenantMembershipState.Active)
            .Join(platform.CampMemberships.Where(campMembership => campMembership.State == CampMembershipState.Active),
                tenantMembership => tenantMembership.Id, campMembership => campMembership.TenantMembershipId,
                (_, campMembership) => campMembership)
            .Join(platform.CampRoleAssignments, membership => membership.Id, role => role.MembershipId,
                (membership, role) => new { membership.CampId, role.RoleIdentifier })
            .ToListAsync(cancellationToken);

        return assignments.GroupBy(value => value.CampId)
            .Where(group => AuthorizationCatalogue.ResolvePermissions(
                AuthorizationScope.Camp, group.Select(value => value.RoleIdentifier)).Contains(permission))
            .Select(group => group.Key)
            .ToArray();
    }
}
