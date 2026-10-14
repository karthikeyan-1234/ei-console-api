using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Domain.Enums;
using EI_Console.Domain.Models;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Infrastructure.Credentials
{
    public sealed class SqlConnectionStringResolver : ISqlConnectionStringResolver
    {
        private readonly IConfiguration _config;
        private readonly IStoredCredentialRepository _storedCredentials;
        private readonly ILogger<SqlConnectionStringResolver> _log;

        public SqlConnectionStringResolver(
            IConfiguration config,
            IStoredCredentialRepository storedCredentials,
            ILogger<SqlConnectionStringResolver> log)
        {
            _config = config;
            _storedCredentials = storedCredentials;
            _log = log;
        }

        public async Task<string?> ResolveAsync(
            AuthProfile? profile, string baseFragment, CancellationToken ct = default)
        {
            if (profile is null)
            {
                // No auth — the base fragment must already carry credentials.
                return EnsureTrailingSemicolon(baseFragment);
            }

            var subtype = profile.SqlServer;
            if (subtype is null)
            {
                _log.LogWarning(
                    "Auth profile {Id} is not a SqlServerConnectionString profile",
                    profile.AuthProfileId);
                return null;
            }

            string? credentialFragment = subtype.CredentialStorageMode switch
            {
                CredentialStorageMode.Inline => subtype.InlineConnectionString,
                CredentialStorageMode.KeyVault => _config[subtype.ConnectionStringSecretRef ?? string.Empty],
                CredentialStorageMode.StoredCredential => await ResolveFromStoredAsync(
                    subtype.StoredCredentialId, ct),
                _ => null,
            };

            if (string.IsNullOrWhiteSpace(credentialFragment))
            {
                _log.LogWarning(
                    "Could not resolve SQL credential for profile {Id} (mode {Mode})",
                    profile.AuthProfileId, subtype.CredentialStorageMode);
                return null;
            }

            var combined = EnsureTrailingSemicolon(baseFragment)
                         + EnsureTrailingSemicolon(credentialFragment);
            return combined;
        }

        private async Task<string?> ResolveFromStoredAsync(int? id, CancellationToken ct)
        {
            if (id is not int credId) return null;
            var stored = await _storedCredentials.GetByIdAsync(credId, ct);
            return stored?.Secret;
        }

        private static string EnsureTrailingSemicolon(string value) =>
            value.EndsWith(';') ? value : value + ";";
    }
}
