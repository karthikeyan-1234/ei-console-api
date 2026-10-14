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
    public sealed class FtpCredentialResolver : IFtpCredentialResolver
    {
        private readonly IConfiguration _config;
        private readonly IStoredCredentialRepository _storedCredentials;
        private readonly ILogger<FtpCredentialResolver> _log;

        public FtpCredentialResolver(
            IConfiguration config,
            IStoredCredentialRepository storedCredentials,
            ILogger<FtpCredentialResolver> log)
        {
            _config = config;
            _storedCredentials = storedCredentials;
            _log = log;
        }

        public async Task<ResolvedFtpCredential?> ResolveAsync(
            AuthProfile? profile, CancellationToken ct = default)
        {
            if (profile is null)
            {
                _log.LogWarning("FTP call has no auth profile — connection will be anonymous");
                return new ResolvedFtpCredential("anonymous", "anonymous@");
            }

            var subtype = profile.Ftp;
            if (subtype is null)
            {
                _log.LogWarning(
                    "Auth profile {Id} is not FtpCredentials", profile.AuthProfileId);
                return null;
            }

            string? password = subtype.CredentialStorageMode switch
            {
                CredentialStorageMode.Inline => subtype.InlinePassword,
                CredentialStorageMode.KeyVault => _config[subtype.PasswordSecretRef ?? string.Empty],
                CredentialStorageMode.StoredCredential => await ResolveFromStoredAsync(
                    subtype.StoredCredentialId, ct),
                _ => null,
            };

            if (string.IsNullOrEmpty(password))
            {
                _log.LogWarning(
                    "Could not resolve FTP password for profile {Id}", profile.AuthProfileId);
                return null;
            }

            return new ResolvedFtpCredential(subtype.Username, password);
        }

        private async Task<string?> ResolveFromStoredAsync(int? id, CancellationToken ct)
        {
            if (id is not int credId) return null;
            var stored = await _storedCredentials.GetByIdAsync(credId, ct);
            return stored?.Secret;
        }
    }
}
