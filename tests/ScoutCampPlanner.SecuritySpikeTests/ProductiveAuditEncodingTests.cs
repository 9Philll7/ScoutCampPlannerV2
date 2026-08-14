using ApplicationDraft = ScoutCampPlanner.Platform.Application.Auditing.AuditEventDraft;
using ProductiveEncoding = ScoutCampPlanner.Platform.Infrastructure.Auditing.AuditCanonicalEncoding;
using ScoutCampPlanner.AuditSecuritySpike;
using Xunit;

namespace ScoutCampPlanner.SecuritySpikeTests;

public sealed class ProductiveAuditEncodingTests
{
    [Fact]
    public void ProductiveEncodingMatchesAcceptedSpikeFormatExactly()
    {
        var metadata = new Dictionary<string, string> { ["z"] = "last", ["a"] = "first" };
        var productive = new ApplicationDraft(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new DateTimeOffset(2026, 8, 14, 10, 0, 0, TimeSpan.Zero),
            "identity.role.changed", "success",
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("33333333-3333-3333-3333-333333333333"), null,
            "membership", Guid.Parse("44444444-4444-4444-4444-444444444444"), "api",
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            Guid.Parse("66666666-6666-6666-6666-666666666666"), 1, 1, metadata);
        var spike = new AuditEventData(
            productive.EventId, productive.TimestampUtc, productive.Action, productive.Result,
            productive.ActorUserId, productive.TenantId, productive.CampId, productive.TargetType,
            productive.TargetId, productive.Origin, productive.InstanceId, productive.CorrelationId,
            productive.SecurityVersion, productive.RoleDefinitionVersion, metadata);
        byte[] previous = Enumerable.Repeat((byte)7, 32).ToArray();

        Assert.Equal(
            AuditCanonicalEncoding.Encode(42, previous, "key-1", spike),
            ProductiveEncoding.Encode(42, previous, "key-1", productive));
    }
}
