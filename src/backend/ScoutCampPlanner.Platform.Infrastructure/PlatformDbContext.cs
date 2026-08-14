using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Platform.Contracts;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Infrastructure.Authentication;
using ScoutCampPlanner.Platform.Infrastructure.Auditing;

namespace ScoutCampPlanner.Platform.Infrastructure;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options), ITenantLookup
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<TenantRoleAssignment> TenantRoleAssignments => Set<TenantRoleAssignment>();
    public DbSet<PasswordCredential> PasswordCredentials => Set<PasswordCredential>();
    public DbSet<AuditEventRecord> AuditEvents => Set<AuditEventRecord>();
    public DbSet<AuditJournalHead> AuditJournalHeads => Set<AuditJournalHead>();
    public DbSet<AuditSegmentRecord> AuditSegments => Set<AuditSegmentRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true)
            modelBuilder.HasDefaultSchema("platform");
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("Tenants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200);
        });
        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("UserAccounts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.NormalizedEmail).HasMaxLength(320);
            entity.HasIndex(x => x.NormalizedEmail).IsUnique();
        });
        modelBuilder.Entity<TenantMembership>(entity =>
        {
            entity.ToTable("TenantMemberships");
            entity.HasKey(x => x.Id);
            entity.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => new { x.UserId, x.TenantId })
                .IsUnique()
                .HasFilter("\"State\" <> 2");
        });
        modelBuilder.Entity<TenantRoleAssignment>(entity =>
        {
            entity.ToTable("TenantRoleAssignments");
            entity.HasKey(x => new { x.MembershipId, x.RoleIdentifier });
            entity.Property(x => x.RoleIdentifier).HasMaxLength(100);
            entity.HasOne<TenantMembership>().WithMany().HasForeignKey(x => x.MembershipId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.MembershipId)
                .IsUnique()
                .HasFilter("\"RoleIdentifier\" IN ('TenantOwner', 'TenantAdmin', 'TenantMember')");
        });
        modelBuilder.Entity<PasswordCredential>(entity =>
        {
            entity.ToTable("PasswordCredentials");
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.Verifier).HasMaxLength(512);
            entity.HasOne<UserAccount>().WithOne().HasForeignKey<PasswordCredential>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<AuditEventRecord>(entity =>
        {
            entity.ToTable("AuditEvents");
            entity.HasKey(x => new { x.InstanceId, x.Sequence });
            entity.HasIndex(x => new { x.InstanceId, x.EventId }).IsUnique();
            entity.HasIndex(x => new { x.InstanceId, x.SegmentId, x.Sequence });
            entity.HasIndex(x => x.TimestampUtc);
            entity.Property(x => x.Action).HasMaxLength(128);
            entity.Property(x => x.Result).HasMaxLength(64);
            entity.Property(x => x.TargetType).HasMaxLength(128);
            entity.Property(x => x.Origin).HasMaxLength(64);
            entity.Property(x => x.MetadataJson);
            entity.Property(x => x.PreviousHash).HasMaxLength(32);
            entity.Property(x => x.Hmac).HasMaxLength(32);
            entity.Property(x => x.KeyId).HasMaxLength(100);
            entity.HasOne<AuditSegmentRecord>().WithMany()
                .HasForeignKey(x => new { x.InstanceId, x.SegmentId })
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<AuditJournalHead>(entity =>
        {
            entity.ToTable("AuditJournalHeads");
            entity.HasKey(x => x.InstanceId);
            entity.Property(x => x.Head).HasMaxLength(32);
            entity.Property(x => x.KeyId).HasMaxLength(100);
            entity.HasOne<AuditSegmentRecord>().WithMany()
                .HasForeignKey(x => new { x.InstanceId, x.ActiveSegmentId })
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<AuditSegmentRecord>(entity =>
        {
            entity.ToTable("AuditSegments");
            entity.HasKey(x => new { x.InstanceId, x.SegmentId });
            entity.HasIndex(x => new { x.InstanceId, x.FirstSequence }).IsUnique();
            entity.Property(x => x.KeyId).HasMaxLength(100);
            entity.Property(x => x.FirstPredecessorHash).HasMaxLength(32);
            entity.Property(x => x.ClosingHash).HasMaxLength(32);
        });
    }

    public async Task<TenantReference?> FindAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await Tenants.Where(x => x.Id == tenantId)
            .Select(x => new TenantReference(x.Id, x.Name))
            .SingleOrDefaultAsync(cancellationToken);
}
