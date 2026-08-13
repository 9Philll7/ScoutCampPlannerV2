using System.Security.Cryptography;
using System.Text.Json;
using ScoutCampPlanner.AuditSecuritySpike;
using Xunit;

namespace ScoutCampPlanner.SecuritySpikeTests;

public sealed class AuditRecoveryArchiveTests
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task EncryptedRecoverySetRoundTripsAllThreeComponents()
    {
        AuditRecoverySet expected = RecoverySet();
        byte[] archive = await AuditRecoveryArchive.CreateAsync(expected, Password, TestContext.Current.CancellationToken);

        AuditRecoverySet restored = await AuditRecoveryArchive.RestoreAsync(
            archive, Password, TestContext.Current.CancellationToken);

        Assert.Equal(expected.Database, restored.Database);
        Assert.Equal(expected.KeyBundle, restored.KeyBundle);
        Assert.Equal(expected.Checkpoint, restored.Checkpoint);
        Assert.DoesNotContain(Convert.ToBase64String(expected.KeyBundle), Convert.ToBase64String(archive));
    }

    [Fact]
    public async Task WrongPasswordAndModifiedArchiveAreRejected()
    {
        byte[] archive = await AuditRecoveryArchive.CreateAsync(
            RecoverySet(), Password, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() => AuditRecoveryArchive.RestoreAsync(
            archive, "wrong backup password", TestContext.Current.CancellationToken));

        using JsonDocument document = JsonDocument.Parse(archive);
        string ciphertext = document.RootElement.GetProperty("Ciphertext").GetString()!;
        byte[] ciphertextBytes = Convert.FromBase64String(ciphertext);
        ciphertextBytes[^1] ^= 1;
        byte[] modified = ReplaceCiphertext(archive, Convert.ToBase64String(ciphertextBytes));
        await Assert.ThrowsAsync<InvalidDataException>(() => AuditRecoveryArchive.RestoreAsync(
            modified, Password, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IndependentArchivesUseIndependentSaltAndNonce()
    {
        byte[] first = await AuditRecoveryArchive.CreateAsync(RecoverySet(), Password, TestContext.Current.CancellationToken);
        byte[] second = await AuditRecoveryArchive.CreateAsync(RecoverySet(), Password, TestContext.Current.CancellationToken);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task EmptyComponentIsRejectedBeforeEncryption()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => AuditRecoveryArchive.CreateAsync(
            new AuditRecoverySet([1], [], [2]), Password, TestContext.Current.CancellationToken));
    }

    private static AuditRecoverySet RecoverySet() =>
        new(RandomNumberGenerator.GetBytes(257), RandomNumberGenerator.GetBytes(96), RandomNumberGenerator.GetBytes(128));

    private static byte[] ReplaceCiphertext(byte[] archive, string ciphertext)
    {
        using JsonDocument document = JsonDocument.Parse(archive);
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (property.NameEquals("Ciphertext")) writer.WriteString(property.Name, ciphertext);
                else property.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        return output.ToArray();
    }
}
