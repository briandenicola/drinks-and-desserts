using Azure.Identity;

namespace WhiskeyAndSmokes.Api;

/// <summary>
/// Centralised factory for Azure token credentials.
/// Order: EnvironmentCredential (SPN via env vars) → ManagedIdentityCredential → AzureCliCredential (local dev).
/// AzureCliCredential is placed last because it requires /bin/sh which is absent in
/// chiseled/distroless container images and throws an unrecoverable Win32Exception.
/// </summary>
public static class CredentialFactory
{
    public static ChainedTokenCredential Create() =>
        new(
            new EnvironmentCredential(),
            new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned),
            new AzureCliCredential());
}
