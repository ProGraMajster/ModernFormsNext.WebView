using System.Text.Json;
using ModernFormsNext.WebView.Windows;
using Xunit;

namespace ModernFormsNext.WebView.Windows.Tests;

public sealed class MessageGuardTests
{
    private static string Packet(long id, string token, string message = "public data") => JsonSerializer.Serialize(new object[] { id, token, message });
    private static readonly Uri Origin = new("https://app.mfn-app.invalid/");

    [Fact]
    public void RequestPreparationIsIdempotentUntilRevocationOrReplacement()
    {
        var guard = new WindowsMessageGuard(); string first = guard.Prepare(1);
        Assert.Equal(first, guard.Prepare(1)); Assert.True(guard.TryGetToken(1, out string original)); Assert.Equal(first, original);
        string second = guard.Prepare(2); Assert.NotEqual(first, second); Assert.False(guard.TryGetToken(1, out var unavailable)); Assert.Empty(unavailable);
        Assert.False(guard.TryRead(Packet(2, first), Origin, Origin, out _, out _));
        Assert.True(guard.TryRead(Packet(2, second), Origin, Origin, out long id, out string data)); Assert.Equal(2, id); Assert.Equal("public data", data);
        guard.Revoke(); Assert.False(guard.TryRead(Packet(2, second), Origin, Origin, out _, out _)); Assert.NotEqual(second, guard.Prepare(2));
    }

    [Fact]
    public void StaleCompletionCannotRestoreRevokedTokenOrRevokeNewRequest()
    {
        var guard = new WindowsMessageGuard(); guard.Prepare(1); string current = guard.Prepare(2);
        guard.Revoke(1); Assert.True(guard.TryGetToken(2, out string token)); Assert.Equal(current, token);
        Assert.False(guard.TryGetToken(1, out _)); guard.Revoke(2); Assert.False(guard.TryGetToken(2, out _));
    }

    [Fact]
    public void RetiredPeerPacketCannotAcquireNewPeerIdentityByIdSubstitution()
    {
        var old = new WindowsMessageGuard(); string oldToken = old.Prepare(1); old.Revoke();
        var replacement = new WindowsMessageGuard(); replacement.Prepare(1);
        Assert.False(replacement.TryRead(Packet(1, oldToken), Origin, Origin, out _, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("about:blank")]
    [InlineData("https://app.mfn-app.invalid.attacker/")]
    [InlineData("https://app.mfn-app.invalid:8443/")]
    [InlineData("http://app.mfn-app.invalid/")]
    [InlineData("https://user@app.mfn-app.invalid/")]
    [InlineData("https://attacker/?source=https://app.mfn-app.invalid/")]
    [InlineData("relative")]
    public void LocalCollectionRequiresExactNativeHttpsOrigin(string? source)
    {
        var guard = new WindowsMessageGuard(); string token = guard.Prepare(1);
        Assert.False(guard.TryRead(Packet(1, token), source is null ? null : new Uri(source, UriKind.RelativeOrAbsolute), Origin, out _, out _));
    }

    [Fact]
    public void OriginCheckAcceptsCanonicalDefaultPortAndPathButDoesNotAuthorizePayload()
    {
        var guard = new WindowsMessageGuard(); string token = guard.Prepare(1);
        Assert.True(guard.TryRead(Packet(1, token, "claimed authorization"), new Uri("https://APP.mfn-app.invalid:443/path?q=1#x"), Origin, out _, out string data));
        Assert.Equal("claimed authorization", data); // Still untrusted; no authentication flag or dispatch exists here.
        Assert.True(guard.TryRead(Packet(1, token), null, null, out _, out _)); // Raw/remote Source remains informational.
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[1,\"legacy\"]")]
    [InlineData("[1,{},\"data\"]")]
    [InlineData("[1,\"guess\",\"data\"]")]
    [InlineData("[1.5,\"guess\",\"data\"]")]
    [InlineData("[\"1\",\"guess\",\"data\"]")]
    [InlineData("[1,\"guess\",{}]")]
    [InlineData("[")]
    public void MalformedGuessedAndLegacyPrivatePacketsAreRejected(string wire)
    {
        var guard = new WindowsMessageGuard(); guard.Prepare(1);
        Assert.False(guard.TryRead(wire, Origin, Origin, out long id, out string value)); Assert.Equal(0, id); Assert.Empty(value);
    }

    [Fact]
    public void BaselineStringsDoNotInheritRpcSizeOrPayloadPolicy()
    {
        var guard = new WindowsMessageGuard(); string token = guard.Prepare(1); string value = new('x', 70000);
        Assert.True(guard.TryRead(Packet(1, token, value), Origin, null, out _, out string returned)); Assert.Equal(value, returned);
        Assert.True(guard.TryRead(Packet(1, token, ""), Origin, null, out _, out returned)); Assert.Empty(returned);
    }
}
