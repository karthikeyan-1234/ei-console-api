using _04_EI_Console.Shared.Application.Contracts;

using EI_Console.Domain.Models;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Mappings
{
    public static class AuthProfileMappings
    {
        public static AuthProfileDto ToDto(this AuthProfile p)
        {
            var storageMode = p.CredentialStorageMode.ToString();
            var kc = p.Keycloak;
            var oauth = p.OAuth2;
            var mtls = p.Mtls;
            var sql = p.SqlServer;
            var ftp = p.Ftp;
            var sftp = p.Sftp;

            return new AuthProfileDto(
                Id: p.AuthProfileId.ToString(),
                Name: p.Name.ToString(),
                Type: p.AuthType.ToString(),
                TenantId: p.TenantId.ToString(),
                Realm: kc?.Realm,
                Audience: kc?.Audience ?? oauth?.Audience,
                KcBaseUrl: kc?.BaseUrl ?? oauth?.TokenEndpoint,
                ClientId: kc?.ClientId ?? oauth?.ClientId,
                SecretRef: kc?.ClientSecretRef ?? oauth?.ClientSecretRef,
                Scope: kc?.Scopes ?? oauth?.Scopes,
                CertRef: mtls?.ClientCertRef,
                KeyRef: mtls?.PrivateKeyRef,
                CaRef: mtls?.CaCertRef,
                PassRef: mtls?.PassphraseRef,
                Thumbprint: mtls?.Thumbprint,
                CredentialStorageMode: storageMode,
                ConnectionStringSecretRef: sql?.ConnectionStringSecretRef,
                InlineConnectionString: sql?.InlineConnectionString,
                Username: ftp?.Username ?? sftp?.Username,
                FtpPasswordSecretRef: ftp?.PasswordSecretRef,
                FtpInlinePassword: ftp?.InlinePassword,
                SftpPrivateKeySecretRef: sftp?.PrivateKeySecretRef,
                SftpPassphraseSecretRef: sftp?.PassphraseSecretRef,
                SftpInlinePrivateKey: sftp?.InlinePrivateKey,
                SftpInlinePassphrase: sftp?.InlinePassphrase,
                StoredCredentialId: sql?.StoredCredentialId?.ToString() ?? ftp?.StoredCredentialId?.ToString() ?? sftp?.StoredCredentialId?.ToString(),
                StTokenEndpoint: p.SubscriptionToken?.TokenEndpoint,
                StTokenContentType: p.SubscriptionToken?.TokenContentType,
                StGrantType: p.SubscriptionToken?.GrantType,
                StSubscriptionKeyMode: p.SubscriptionToken?.SubscriptionKeyMode,
                StSubscriptionKeyHeader: p.SubscriptionToken?.SubscriptionKeyHeader,
                StSubscriptionKeyQueryParam: p.SubscriptionToken?.SubscriptionKeyQueryParam,
                StUsernameFieldName: p.SubscriptionToken?.UsernameFieldName,
                StPasswordFieldName: p.SubscriptionToken?.PasswordFieldName,
                StSecretKeyFieldName: p.SubscriptionToken?.SecretKeyFieldName,
                StPasswordSecretRef: p.SubscriptionToken?.PasswordSecretRef,
                StInlinePassword: p.SubscriptionToken?.InlinePassword,
                StSecretKeySecretRef: p.SubscriptionToken?.SecretKeySecretRef,
                StInlineSecretKey: p.SubscriptionToken?.InlineSecretKey,
                StSubscriptionKeySecretRef: p.SubscriptionToken?.SubscriptionKeySecretRef,
                StInlineSubscriptionKey: p.SubscriptionToken?.InlineSubscriptionKey,
                StSuccessRequiresJsonPath: p.SubscriptionToken?.SuccessRequiresJsonPath,
                StTokenJsonPath: p.SubscriptionToken?.TokenJsonPath,
                StTokenPrefix: p.SubscriptionToken?.TokenPrefix,
                StTtlSource: p.SubscriptionToken?.TtlSource,
                StExpiresInJsonPath: p.SubscriptionToken?.ExpiresInJsonPath,
                StDefaultTtlSeconds: p.SubscriptionToken?.DefaultTtlSeconds,
                StAdditionalBodyJson: p.SubscriptionToken?.AdditionalBodyJson);
        }
    }
}
