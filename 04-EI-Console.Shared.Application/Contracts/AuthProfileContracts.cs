using System;
using System.Collections.Generic;
using System.Text;

namespace _04_EI_Console.Shared.Application.Contracts
{
    public sealed record AuthProfileDto(
       string Id,
       string Name,
       string Type,
       string? TenantId,
       string? Realm,
       string? Audience,
       string? KcBaseUrl,
       string? ClientId,
       string? SecretRef,
       string? Scope,
       string? CertRef,
       string? KeyRef,
       string? CaRef,
       string? PassRef,
       string? Thumbprint,
       string? CredentialStorageMode,
       string? ConnectionStringSecretRef,
       string? InlineConnectionString,
       string? Username,
       string? FtpPasswordSecretRef,
       string? FtpInlinePassword,
       string? SftpPrivateKeySecretRef,
       string? SftpPassphraseSecretRef,
       string? SftpInlinePrivateKey,
       string? SftpInlinePassphrase,
       string? StoredCredentialId);

    public sealed record CreateAuthProfileRequest(
        string Name,
        string Type,
        string? TenantId,
        string? Realm,
        string? Audience,
        string? KcBaseUrl,
        string? ClientId,
        string? SecretRef,
        string? Scope,
        string? CertRef,
        string? KeyRef,
        string? CaRef,
        string? PassRef,
        string? Thumbprint,
        string? CredentialStorageMode,
        string? ConnectionStringSecretRef,
        string? InlineConnectionString,
        string? Username,
        string? FtpPasswordSecretRef,
        string? FtpInlinePassword,
        string? SftpPrivateKeySecretRef,
        string? SftpPassphraseSecretRef,
        string? SftpInlinePrivateKey,
        string? SftpInlinePassphrase,
        string? StoredCredentialId);

    public sealed record UpdateAuthProfileRequest(
        string Name,
        string Type,
        string? Realm,
        string? Audience,
        string? KcBaseUrl,
        string? ClientId,
        string? SecretRef,
        string? Scope,
        string? CertRef,
        string? KeyRef,
        string? CaRef,
        string? PassRef,
        string? Thumbprint,
        string? CredentialStorageMode,
        string? ConnectionStringSecretRef,
        string? InlineConnectionString,
        string? Username,
        string? FtpPasswordSecretRef,
        string? FtpInlinePassword,
        string? SftpPrivateKeySecretRef,
        string? SftpPassphraseSecretRef,
        string? SftpInlinePrivateKey,
        string? SftpInlinePassphrase,
        string? StoredCredentialId);

    public sealed record TestAuthResultDto(bool Ok, string Message);
}
