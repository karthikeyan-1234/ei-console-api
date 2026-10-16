using _04_EI_Console.Shared.Application.Contracts;

using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Application.Interfaces.Services;
using EI_Console.Application.Mappings;
using EI_Console.Domain.Enums;
using EI_Console.Domain.Models;
using EI_Console.Shared.Application;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Services
{
    public sealed class AuthProfileService : IAuthProfileService
    {
        private readonly IAuthProfileRepository _profiles;
        private readonly ICredentialResolver _credentials;
        private readonly IUnitOfWork _uow;
        private readonly ILogger<AuthProfileService> _log;

        public AuthProfileService(
            IAuthProfileRepository profiles,
            ICredentialResolver credentials,
            IUnitOfWork uow,
            ILogger<AuthProfileService> log)
        {
            _profiles = profiles;
            _credentials = credentials;
            _uow = uow;
            _log = log;
        }

        // ------------------------------------------------------------------
        // Read
        // ------------------------------------------------------------------

        public async Task<List<AuthProfileDto>> ListAsync(CancellationToken ct = default)
        {
            var all = await _profiles.GetAllAsync(ct);
            return all.Select(p => p.ToDto()).ToList();
        }

        public async Task<AuthProfileDto?> GetAsync(int id, CancellationToken ct = default)
        {
            var p = await _profiles.GetByIdAsync(id, ct);
            return p?.ToDto();
        }

        // ------------------------------------------------------------------
        // Create
        // ------------------------------------------------------------------

        public async Task<AuthProfileDto> CreateAsync(CreateAuthProfileRequest request, CancellationToken ct = default)
        {
            var type = ParseAuthType(request.Type);

            var profile = new AuthProfile
            {
                Name = request.Name.Trim(),
                AuthType = type,
                TenantId = ParseNullableInt(request.TenantId),
                CredentialStorageMode = ParseStorageMode(request.CredentialStorageMode),
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            AttachSubtype(profile, type,
                realm: request.Realm,
                audience: request.Audience,
                kcBaseUrl: request.KcBaseUrl,
                clientId: request.ClientId,
                secretRef: request.SecretRef,
                scope: request.Scope,
                certRef: request.CertRef,
                keyRef: request.KeyRef,
                caRef: request.CaRef,
                passRef: request.PassRef,
                thumbprint: request.Thumbprint,
                connectionStringSecretRef: request.ConnectionStringSecretRef,
                inlineConnectionString: request.InlineConnectionString,
                username: request.Username,
                ftpPasswordSecretRef: request.FtpPasswordSecretRef,
                ftpInlinePassword: request.FtpInlinePassword,
                sftpPrivateKeySecretRef: request.SftpPrivateKeySecretRef,
                sftpPassphraseSecretRef: request.SftpPassphraseSecretRef,
                sftpInlinePrivateKey: request.SftpInlinePrivateKey,
                sftpInlinePassphrase: request.SftpInlinePassphrase,
                storedCredentialId: request.StoredCredentialId,
                // ── SubscriptionTokenExchange ─────────────────────────────
                stTokenEndpoint: request.StTokenEndpoint,
                stTokenContentType: request.StTokenContentType,
                stGrantType: request.StGrantType,
                stSubscriptionKeyMode: request.StSubscriptionKeyMode,
                stSubscriptionKeyHeader: request.StSubscriptionKeyHeader,
                stSubscriptionKeyQueryParam: request.StSubscriptionKeyQueryParam,
                stUsernameFieldName: request.StUsernameFieldName,
                stPasswordFieldName: request.StPasswordFieldName,
                stSecretKeyFieldName: request.StSecretKeyFieldName,
                stPasswordSecretRef: request.StPasswordSecretRef,
                stInlinePassword: request.StInlinePassword,
                stSecretKeySecretRef: request.StSecretKeySecretRef,
                stInlineSecretKey: request.StInlineSecretKey,
                stSubscriptionKeySecretRef: request.StSubscriptionKeySecretRef,
                stInlineSubscriptionKey: request.StInlineSubscriptionKey,
                stSuccessRequiresJsonPath: request.StSuccessRequiresJsonPath,
                stTokenJsonPath: request.StTokenJsonPath,
                stTokenPrefix: request.StTokenPrefix,
                stTtlSource: request.StTtlSource,
                stExpiresInJsonPath: request.StExpiresInJsonPath,
                stDefaultTtlSeconds: request.StDefaultTtlSeconds,
                stAdditionalBodyJson: request.StAdditionalBodyJson);

            _profiles.Add(profile);
            await _uow.SaveChangesAsync(ct);

            _log.LogInformation(
                "Auth profile {Name} ({Type}) created with id {Id}",
                profile.Name, profile.AuthType, profile.AuthProfileId);

            return profile.ToDto();
        }

        // ------------------------------------------------------------------
        // Update
        // ------------------------------------------------------------------

        public async Task<AuthProfileDto?> UpdateAsync(int id, UpdateAuthProfileRequest request, CancellationToken ct = default)
        {
            var profile = await _profiles.GetByIdAsync(id, ct);
            if (profile is null) return null;

            profile.Name = request.Name.Trim();
            profile.CredentialStorageMode = ParseStorageMode(request.CredentialStorageMode);
            profile.UpdatedAt = DateTimeOffset.UtcNow;

            var newType = ParseAuthType(request.Type);

            if (newType != profile.AuthType)
            {
                // Type changed — drop every subtype and attach a fresh one.
                // EF Core's cascade on the subtype FK removes the orphaned rows.
                ClearSubtypes(profile);
                profile.AuthType = newType;

                AttachSubtype(profile, newType,
                    realm: request.Realm,
                    audience: request.Audience,
                    kcBaseUrl: request.KcBaseUrl,
                    clientId: request.ClientId,
                    secretRef: request.SecretRef,
                    scope: request.Scope,
                    certRef: request.CertRef,
                    keyRef: request.KeyRef,
                    caRef: request.CaRef,
                    passRef: request.PassRef,
                    thumbprint: request.Thumbprint,
                    connectionStringSecretRef: request.ConnectionStringSecretRef,
                    inlineConnectionString: request.InlineConnectionString,
                    username: request.Username,
                    ftpPasswordSecretRef: request.FtpPasswordSecretRef,
                    ftpInlinePassword: request.FtpInlinePassword,
                    sftpPrivateKeySecretRef: request.SftpPrivateKeySecretRef,
                    sftpPassphraseSecretRef: request.SftpPassphraseSecretRef,
                    sftpInlinePrivateKey: request.SftpInlinePrivateKey,
                    sftpInlinePassphrase: request.SftpInlinePassphrase,
                    storedCredentialId: request.StoredCredentialId,
                    // ── SubscriptionTokenExchange ─────────────────────────
                    stTokenEndpoint: request.StTokenEndpoint,
                    stTokenContentType: request.StTokenContentType,
                    stGrantType: request.StGrantType,
                    stSubscriptionKeyMode: request.StSubscriptionKeyMode,
                    stSubscriptionKeyHeader: request.StSubscriptionKeyHeader,
                    stSubscriptionKeyQueryParam: request.StSubscriptionKeyQueryParam,
                    stUsernameFieldName: request.StUsernameFieldName,
                    stPasswordFieldName: request.StPasswordFieldName,
                    stSecretKeyFieldName: request.StSecretKeyFieldName,
                    stPasswordSecretRef: request.StPasswordSecretRef,
                    stInlinePassword: request.StInlinePassword,
                    stSecretKeySecretRef: request.StSecretKeySecretRef,
                    stInlineSecretKey: request.StInlineSecretKey,
                    stSubscriptionKeySecretRef: request.StSubscriptionKeySecretRef,
                    stInlineSubscriptionKey: request.StInlineSubscriptionKey,
                    stSuccessRequiresJsonPath: request.StSuccessRequiresJsonPath,
                    stTokenJsonPath: request.StTokenJsonPath,
                    stTokenPrefix: request.StTokenPrefix,
                    stTtlSource: request.StTtlSource,
                    stExpiresInJsonPath: request.StExpiresInJsonPath,
                    stDefaultTtlSeconds: request.StDefaultTtlSeconds,
                    stAdditionalBodyJson: request.StAdditionalBodyJson);
            }
            else
            {
                UpdateSubtypeInPlace(profile, newType,
                    realm: request.Realm,
                    audience: request.Audience,
                    kcBaseUrl: request.KcBaseUrl,
                    clientId: request.ClientId,
                    secretRef: request.SecretRef,
                    scope: request.Scope,
                    certRef: request.CertRef,
                    keyRef: request.KeyRef,
                    caRef: request.CaRef,
                    passRef: request.PassRef,
                    thumbprint: request.Thumbprint,
                    connectionStringSecretRef: request.ConnectionStringSecretRef,
                    inlineConnectionString: request.InlineConnectionString,
                    username: request.Username,
                    ftpPasswordSecretRef: request.FtpPasswordSecretRef,
                    ftpInlinePassword: request.FtpInlinePassword,
                    sftpPrivateKeySecretRef: request.SftpPrivateKeySecretRef,
                    sftpPassphraseSecretRef: request.SftpPassphraseSecretRef,
                    sftpInlinePrivateKey: request.SftpInlinePrivateKey,
                    sftpInlinePassphrase: request.SftpInlinePassphrase,
                    storedCredentialId: request.StoredCredentialId,
                    // ── SubscriptionTokenExchange ─────────────────────────
                    stTokenEndpoint: request.StTokenEndpoint,
                    stTokenContentType: request.StTokenContentType,
                    stGrantType: request.StGrantType,
                    stSubscriptionKeyMode: request.StSubscriptionKeyMode,
                    stSubscriptionKeyHeader: request.StSubscriptionKeyHeader,
                    stSubscriptionKeyQueryParam: request.StSubscriptionKeyQueryParam,
                    stUsernameFieldName: request.StUsernameFieldName,
                    stPasswordFieldName: request.StPasswordFieldName,
                    stSecretKeyFieldName: request.StSecretKeyFieldName,
                    stPasswordSecretRef: request.StPasswordSecretRef,
                    stInlinePassword: request.StInlinePassword,
                    stSecretKeySecretRef: request.StSecretKeySecretRef,
                    stInlineSecretKey: request.StInlineSecretKey,
                    stSubscriptionKeySecretRef: request.StSubscriptionKeySecretRef,
                    stInlineSubscriptionKey: request.StInlineSubscriptionKey,
                    stSuccessRequiresJsonPath: request.StSuccessRequiresJsonPath,
                    stTokenJsonPath: request.StTokenJsonPath,
                    stTokenPrefix: request.StTokenPrefix,
                    stTtlSource: request.StTtlSource,
                    stExpiresInJsonPath: request.StExpiresInJsonPath,
                    stDefaultTtlSeconds: request.StDefaultTtlSeconds,
                    stAdditionalBodyJson: request.StAdditionalBodyJson);
            }

            await _uow.SaveChangesAsync(ct);
            return await GetAsync(id, ct);
        }

        // ------------------------------------------------------------------
        // Delete
        // ------------------------------------------------------------------

        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            var p = await _profiles.GetByIdAsync(id, ct);
            if (p is null) return false;

            _profiles.Remove(p);
            await _uow.SaveChangesAsync(ct);
            return true;
        }

        // ------------------------------------------------------------------
        // Test
        // ------------------------------------------------------------------

        public async Task<TestAuthResultDto> TestAsync(int id, CancellationToken ct = default)
        {
            var profile = await _profiles.GetByIdAsync(id, ct);
            if (profile is null)
                return new TestAuthResultDto(false, "Profile not found.");

            switch (profile.AuthType)
            {
                case AuthType.ApiKey:
                case AuthType.WsSecurityUsernameToken:
                    {
                        var headers = await _credentials.ResolveHeadersAsync(profile, ct);
                        return headers.Count == 0
                            ? new TestAuthResultDto(false, "Credential could not be resolved. Check the referenced config key.")
                            : new TestAuthResultDto(true, $"Resolved {headers.Count} header(s).");
                    }

                case AuthType.KeycloakAuthCodeExchange:
                case AuthType.OAuth2ClientCredentials:
                case AuthType.MutualTls:
                case AuthType.SubscriptionTokenExchange:
                    {
                        var headers = await _credentials.ResolveHeadersAsync(profile, ct);
                        return headers.Count == 0
                            ? new TestAuthResultDto(false,
                                "Token acquisition failed. Check the API logs for the vendor's response body.")
                            : new TestAuthResultDto(true,
                                $"Token acquired. {headers.Count} header(s) will be sent: {string.Join(", ", headers.Keys)}.");
                    }

                default:
                    return new TestAuthResultDto(false,
                        $"{profile.AuthType} is not supported in v0.1.");
            }
        }

        // ------------------------------------------------------------------
        // Subtype plumbing
        // ------------------------------------------------------------------

        private static void ClearSubtypes(AuthProfile p)
        {
            p.Keycloak = null;
            p.OAuth2 = null;
            p.ApiKey = null;
            p.WsSecurity = null;
            p.Mtls = null;
            p.SqlServer = null;
            p.Ftp = null;
            p.Sftp = null;
            p.SubscriptionToken = null;
        }

        private static void AttachSubtype(AuthProfile p, AuthType type,
            string? realm, string? audience, string? kcBaseUrl, string? clientId,
            string? secretRef, string? scope,
            string? certRef, string? keyRef, string? caRef, string? passRef, string? thumbprint,
            string? connectionStringSecretRef, string? inlineConnectionString,
            string? username, string? ftpPasswordSecretRef, string? ftpInlinePassword,
            string? sftpPrivateKeySecretRef, string? sftpPassphraseSecretRef,
            string? sftpInlinePrivateKey, string? sftpInlinePassphrase,
            string? storedCredentialId,
            string? stTokenEndpoint, string? stTokenContentType, string? stGrantType,
            string? stSubscriptionKeyMode, string? stSubscriptionKeyHeader, string? stSubscriptionKeyQueryParam,
            string? stUsernameFieldName, string? stPasswordFieldName, string? stSecretKeyFieldName,
            string? stPasswordSecretRef, string? stInlinePassword,
            string? stSecretKeySecretRef, string? stInlineSecretKey,
            string? stSubscriptionKeySecretRef, string? stInlineSubscriptionKey,
            string? stSuccessRequiresJsonPath, string? stTokenJsonPath, string? stTokenPrefix,
            string? stTtlSource, string? stExpiresInJsonPath, int? stDefaultTtlSeconds,
            string? stAdditionalBodyJson)
        {
            switch (type)
            {
                case AuthType.KeycloakAuthCodeExchange:
                    p.Keycloak = new AuthProfileKeycloak
                    {
                        AuthType = type,
                        BaseUrl = kcBaseUrl ?? string.Empty,
                        Realm = realm ?? string.Empty,
                        ClientId = clientId ?? string.Empty,
                        ClientSecretRef = secretRef ?? string.Empty,
                        Audience = audience,
                        Scopes = scope,
                    };
                    break;

                case AuthType.OAuth2ClientCredentials:
                    p.OAuth2 = new AuthProfileOAuth2
                    {
                        AuthType = type,
                        TokenEndpoint = kcBaseUrl ?? string.Empty,
                        ClientId = clientId ?? string.Empty,
                        ClientSecretRef = secretRef ?? string.Empty,
                        Audience = audience,
                        Scopes = scope,
                    };
                    break;

                case AuthType.ApiKey:
                    p.ApiKey = new AuthProfileApiKey
                    {
                        AuthType = type,
                        HeaderName = kcBaseUrl ?? "X-Api-Key",
                        ApiKeyRef = secretRef ?? string.Empty,
                    };
                    break;

                case AuthType.WsSecurityUsernameToken:
                    p.WsSecurity = new AuthProfileWsSecurity
                    {
                        AuthType = type,
                        Username = clientId ?? string.Empty,
                        PasswordRef = secretRef ?? string.Empty,
                        EndpointUrl = kcBaseUrl,
                    };
                    break;

                case AuthType.MutualTls:
                    p.Mtls = new AuthProfileMtls
                    {
                        AuthType = type,
                        ClientCertRef = certRef ?? string.Empty,
                        PrivateKeyRef = keyRef ?? string.Empty,
                        CaCertRef = caRef,
                        PassphraseRef = passRef,
                        Thumbprint = thumbprint,
                    };
                    break;

                case AuthType.SqlServerConnectionString:
                    p.SqlServer = new AuthProfileSqlServer
                    {
                        AuthType = type,
                        CredentialStorageMode = ParseStorageMode(p.CredentialStorageMode.ToString()),
                        ConnectionStringSecretRef = connectionStringSecretRef,
                        InlineConnectionString = inlineConnectionString,
                        StoredCredentialId = ParseNullableInt(storedCredentialId),
                    };
                    break;

                case AuthType.FtpCredentials:
                    p.Ftp = new AuthProfileFtp
                    {
                        AuthType = type,
                        CredentialStorageMode = ParseStorageMode(p.CredentialStorageMode.ToString()),
                        Username = username ?? string.Empty,
                        PasswordSecretRef = ftpPasswordSecretRef,
                        InlinePassword = ftpInlinePassword,
                        StoredCredentialId = ParseNullableInt(storedCredentialId),
                    };
                    break;

                case AuthType.SftpKeyCredentials:
                    p.Sftp = new AuthProfileSftp
                    {
                        AuthType = type,
                        CredentialStorageMode = ParseStorageMode(p.CredentialStorageMode.ToString()),
                        Username = username ?? string.Empty,
                        PrivateKeySecretRef = sftpPrivateKeySecretRef,
                        PassphraseSecretRef = sftpPassphraseSecretRef,
                        InlinePrivateKey = sftpInlinePrivateKey,
                        InlinePassphrase = sftpInlinePassphrase,
                        StoredCredentialId = ParseNullableInt(storedCredentialId),
                    };
                    break;

                case AuthType.SubscriptionTokenExchange:
                    p.SubscriptionToken = new AuthProfileSubscriptionToken
                    {
                        AuthType = type,
                        TokenEndpoint = stTokenEndpoint ?? string.Empty,
                        TokenContentType = stTokenContentType ?? "application/json",
                        GrantType = stGrantType ?? "password",

                        SubscriptionKeyMode = stSubscriptionKeyMode ?? "Header",
                        SubscriptionKeyHeader = stSubscriptionKeyHeader,
                        SubscriptionKeyQueryParam = stSubscriptionKeyQueryParam,

                        UsernameFieldName = stUsernameFieldName ?? "username",
                        Username = username ?? string.Empty,
                        PasswordFieldName = stPasswordFieldName ?? "password",
                        PasswordSecretRef = stPasswordSecretRef,
                        InlinePassword = stInlinePassword,
                        SecretKeyFieldName = stSecretKeyFieldName,
                        SecretKeySecretRef = stSecretKeySecretRef,
                        InlineSecretKey = stInlineSecretKey,
                        SubscriptionKeySecretRef = stSubscriptionKeySecretRef,
                        InlineSubscriptionKey = stInlineSubscriptionKey,

                        CredentialStorageMode = ParseStorageMode(p.CredentialStorageMode.ToString()),

                        SuccessRequiresJsonPath = stSuccessRequiresJsonPath ?? "$.data",
                        TokenJsonPath = stTokenJsonPath ?? "$.data.accessToken",
                        TokenPrefix = stTokenPrefix ?? "Bearer",

                        TtlSource = stTtlSource ?? "JwtExp",
                        ExpiresInJsonPath = stExpiresInJsonPath,
                        DefaultTtlSeconds = stDefaultTtlSeconds ?? 7200,

                        AdditionalBodyJson = stAdditionalBodyJson,
                    };
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported auth type '{type}'.");
            }
        }

        /// <summary>
        /// Same profile type kept across an edit. Update the existing subtype row
        /// in place. Fields sent as null leave the existing value untouched.
        /// </summary>
        private static void UpdateSubtypeInPlace(AuthProfile p, AuthType type,
            string? realm, string? audience, string? kcBaseUrl, string? clientId,
            string? secretRef, string? scope,
            string? certRef, string? keyRef, string? caRef, string? passRef, string? thumbprint,
            string? connectionStringSecretRef, string? inlineConnectionString,
            string? username, string? ftpPasswordSecretRef, string? ftpInlinePassword,
            string? sftpPrivateKeySecretRef, string? sftpPassphraseSecretRef,
            string? sftpInlinePrivateKey, string? sftpInlinePassphrase,
            string? storedCredentialId,
            string? stTokenEndpoint, string? stTokenContentType, string? stGrantType,
            string? stSubscriptionKeyMode, string? stSubscriptionKeyHeader, string? stSubscriptionKeyQueryParam,
            string? stUsernameFieldName, string? stPasswordFieldName, string? stSecretKeyFieldName,
            string? stPasswordSecretRef, string? stInlinePassword,
            string? stSecretKeySecretRef, string? stInlineSecretKey,
            string? stSubscriptionKeySecretRef, string? stInlineSubscriptionKey,
            string? stSuccessRequiresJsonPath, string? stTokenJsonPath, string? stTokenPrefix,
            string? stTtlSource, string? stExpiresInJsonPath, int? stDefaultTtlSeconds,
            string? stAdditionalBodyJson)
        {
            switch (type)
            {
                case AuthType.KeycloakAuthCodeExchange when p.Keycloak is not null:
                    p.Keycloak.BaseUrl = kcBaseUrl ?? p.Keycloak.BaseUrl;
                    p.Keycloak.Realm = realm ?? p.Keycloak.Realm;
                    p.Keycloak.ClientId = clientId ?? p.Keycloak.ClientId;
                    p.Keycloak.ClientSecretRef = secretRef ?? p.Keycloak.ClientSecretRef;
                    p.Keycloak.Audience = audience ?? p.Keycloak.Audience;
                    p.Keycloak.Scopes = scope ?? p.Keycloak.Scopes;
                    break;

                case AuthType.OAuth2ClientCredentials when p.OAuth2 is not null:
                    p.OAuth2.TokenEndpoint = kcBaseUrl ?? p.OAuth2.TokenEndpoint;
                    p.OAuth2.ClientId = clientId ?? p.OAuth2.ClientId;
                    p.OAuth2.ClientSecretRef = secretRef ?? p.OAuth2.ClientSecretRef;
                    p.OAuth2.Audience = audience ?? p.OAuth2.Audience;
                    p.OAuth2.Scopes = scope ?? p.OAuth2.Scopes;
                    break;

                case AuthType.ApiKey when p.ApiKey is not null:
                    p.ApiKey.HeaderName = kcBaseUrl ?? p.ApiKey.HeaderName;
                    p.ApiKey.ApiKeyRef = secretRef ?? p.ApiKey.ApiKeyRef;
                    break;

                case AuthType.WsSecurityUsernameToken when p.WsSecurity is not null:
                    p.WsSecurity.Username = clientId ?? p.WsSecurity.Username;
                    p.WsSecurity.PasswordRef = secretRef ?? p.WsSecurity.PasswordRef;
                    p.WsSecurity.EndpointUrl = kcBaseUrl ?? p.WsSecurity.EndpointUrl;
                    break;

                case AuthType.MutualTls when p.Mtls is not null:
                    p.Mtls.ClientCertRef = certRef ?? p.Mtls.ClientCertRef;
                    p.Mtls.PrivateKeyRef = keyRef ?? p.Mtls.PrivateKeyRef;
                    p.Mtls.CaCertRef = caRef ?? p.Mtls.CaCertRef;
                    p.Mtls.PassphraseRef = passRef ?? p.Mtls.PassphraseRef;
                    p.Mtls.Thumbprint = thumbprint ?? p.Mtls.Thumbprint;
                    break;

                case AuthType.SqlServerConnectionString when p.SqlServer is not null:
                    p.SqlServer.ConnectionStringSecretRef = connectionStringSecretRef;
                    p.SqlServer.InlineConnectionString = inlineConnectionString;
                    p.SqlServer.StoredCredentialId = ParseNullableInt(storedCredentialId);
                    break;

                case AuthType.FtpCredentials when p.Ftp is not null:
                    p.Ftp.Username = username ?? p.Ftp.Username;
                    p.Ftp.PasswordSecretRef = ftpPasswordSecretRef;
                    p.Ftp.InlinePassword = ftpInlinePassword;
                    p.Ftp.StoredCredentialId = ParseNullableInt(storedCredentialId);
                    break;

                case AuthType.SftpKeyCredentials when p.Sftp is not null:
                    p.Sftp.Username = username ?? p.Sftp.Username;
                    p.Sftp.PrivateKeySecretRef = sftpPrivateKeySecretRef;
                    p.Sftp.PassphraseSecretRef = sftpPassphraseSecretRef;
                    p.Sftp.InlinePrivateKey = sftpInlinePrivateKey;
                    p.Sftp.InlinePassphrase = sftpInlinePassphrase;
                    p.Sftp.StoredCredentialId = ParseNullableInt(storedCredentialId);
                    break;

                // ── SubscriptionTokenExchange — update the existing row in place ──
                case AuthType.SubscriptionTokenExchange when p.SubscriptionToken is not null:
                    if (stTokenEndpoint != null) p.SubscriptionToken.TokenEndpoint = stTokenEndpoint;
                    if (stTokenContentType != null) p.SubscriptionToken.TokenContentType = stTokenContentType;
                    if (stGrantType != null) p.SubscriptionToken.GrantType = stGrantType;

                    if (stSubscriptionKeyMode != null) p.SubscriptionToken.SubscriptionKeyMode = stSubscriptionKeyMode;
                    if (stSubscriptionKeyHeader != null) p.SubscriptionToken.SubscriptionKeyHeader = stSubscriptionKeyHeader;
                    if (stSubscriptionKeyQueryParam != null) p.SubscriptionToken.SubscriptionKeyQueryParam = stSubscriptionKeyQueryParam;
                    if (stUsernameFieldName != null) p.SubscriptionToken.UsernameFieldName = stUsernameFieldName;
                    if (username != null) p.SubscriptionToken.Username = username;
                    if (stPasswordFieldName != null) p.SubscriptionToken.PasswordFieldName = stPasswordFieldName;
                    p.SubscriptionToken.PasswordSecretRef = stPasswordSecretRef;
                    p.SubscriptionToken.InlinePassword = stInlinePassword;
                    if (stSecretKeyFieldName != null) p.SubscriptionToken.SecretKeyFieldName = stSecretKeyFieldName;
                    p.SubscriptionToken.SecretKeySecretRef = stSecretKeySecretRef;
                    p.SubscriptionToken.InlineSecretKey = stInlineSecretKey;
                    p.SubscriptionToken.SubscriptionKeySecretRef = stSubscriptionKeySecretRef;
                    p.SubscriptionToken.InlineSubscriptionKey = stInlineSubscriptionKey;

                    if (stSuccessRequiresJsonPath != null) p.SubscriptionToken.SuccessRequiresJsonPath = stSuccessRequiresJsonPath;
                    if (stTokenJsonPath != null) p.SubscriptionToken.TokenJsonPath = stTokenJsonPath;
                    if (stTokenPrefix != null) p.SubscriptionToken.TokenPrefix = stTokenPrefix;

                    if (stTtlSource != null) p.SubscriptionToken.TtlSource = stTtlSource;
                    p.SubscriptionToken.ExpiresInJsonPath = stExpiresInJsonPath;
                    if (stDefaultTtlSeconds is int ttl) p.SubscriptionToken.DefaultTtlSeconds = ttl;

                    p.SubscriptionToken.AdditionalBodyJson = stAdditionalBodyJson;
                    break;

                default:
                    // Subtype row is missing or the type is not supported. Attach a fresh one.
                    ClearSubtypes(p);
                    AttachSubtype(p, type,
                        realm, audience, kcBaseUrl, clientId, secretRef, scope,
                        certRef, keyRef, caRef, passRef, thumbprint,
                        connectionStringSecretRef, inlineConnectionString,
                        username, ftpPasswordSecretRef, ftpInlinePassword,
                        sftpPrivateKeySecretRef, sftpPassphraseSecretRef,
                        sftpInlinePrivateKey, sftpInlinePassphrase,
                        storedCredentialId,
                        // ── SubscriptionTokenExchange ─────────────────────
                        stTokenEndpoint, stTokenContentType, stGrantType,
                        stSubscriptionKeyMode, stSubscriptionKeyHeader, stSubscriptionKeyQueryParam,
                        stUsernameFieldName, stPasswordFieldName, stSecretKeyFieldName,
                        stPasswordSecretRef, stInlinePassword,
                        stSecretKeySecretRef, stInlineSecretKey,
                        stSubscriptionKeySecretRef, stInlineSubscriptionKey,
                        stSuccessRequiresJsonPath, stTokenJsonPath, stTokenPrefix,
                        stTtlSource, stExpiresInJsonPath, stDefaultTtlSeconds,
                        stAdditionalBodyJson);
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Parsing helpers
        // ------------------------------------------------------------------

        private static AuthType ParseAuthType(string raw) =>
            Enum.TryParse<AuthType>(raw, ignoreCase: true, out var t)
                ? t
                : throw new InvalidOperationException($"Unknown auth type '{raw}'.");

        private static int? ParseNullableInt(string? raw) =>
            int.TryParse(raw, out var v) ? v : null;

        private static CredentialStorageMode ParseStorageMode(string? raw) =>
            Enum.TryParse<CredentialStorageMode>(raw, ignoreCase: true, out var m)
                ? m
                : CredentialStorageMode.KeyVault;
    }
}