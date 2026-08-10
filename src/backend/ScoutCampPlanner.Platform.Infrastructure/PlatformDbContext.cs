using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Platform.Contracts;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Infrastructure.Authentication;

namespace ScoutCampPlanner.Platform.Infrastructure;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options), ITenantLookup
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<PasswordCredential> PasswordCredentials => Set<PasswordCredential>();

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
        modelBuilder.Entity<PasswordCredential>(entity =>
        {
            entity.ToTable("PasswordCredentials");
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.Verifier).HasMaxLength(512);
            entity.HasOne<UserAccount>().WithOne().HasForeignKey<PasswordCredential>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    public async Task<TenantReference?> FindAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await Tenants.Where(x => x.Id == tenantId)
            .Select(x => new TenantReference(x.Id, x.Name))
            .SingleOrDefaultAsync(cancellationToken);
}
