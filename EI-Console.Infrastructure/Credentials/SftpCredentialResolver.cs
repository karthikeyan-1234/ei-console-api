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
    public sealed class SftpCredentialResolver : ISftpCredentialResolver
    {
        private readonly IConfiguration _config;
        private readonly IStoredCredentialRepository _storedCredentials;
        private readonly ILogger<SftpCredentialResolver> _log;

        public SftpCredentialResolver(
            IConfiguration config,
            IStoredCredentialRepository storedCredentials,
            ILogger<SftpCredentialResolver> log)
        {
            _config = config;
            _storedCredentials = storedCredentials;
            _log = log;
        }

        public async Task<ResolvedSftpCredential?> ResolveAsync(
            AuthProfile? profile, CancellationToken ct = default)
        {
            if (profile is null)
            {
                _log.LogWarning("SFTP call has no auth profile");
                return null;
            }

            var subtype = profile.Sftp;
            if (subtype is null)
            {
                _log.LogWarning(
                    "Auth profile {Id} is not SftpKeyCredentials", profile.AuthProfileId);
                return null;
            }

            string? privateKey;
            string? passphrase;

            switch (subtype.CredentialStorageMode)
            {
                case CredentialStorageMode.Inline:
                    privateKey = subtype.InlinePrivateKey;
                    passphrase = subtype.InlinePassphrase;
                    break;

                case CredentialStorageMode.KeyVault:
                    privateKey = _config[subtype.PrivateKeySecretRef ?? string.Empty];
                    passphrase = string.IsNullOrWhiteSpace(subtype.PassphraseSecretRef)
                        ? null
                        : _config[subtype.PassphraseSecretRef!];
                    break;

                case CredentialStorageMode.StoredCredential:
                    var stored = subtype.StoredCredentialId is int id
                        ? await _storedCredentials.GetByIdAsync(id, ct)
                        : null;
                    privateKey = stored?.Secret;
                    passphrase = stored?.SecondarySecret;
                    break;

                default:
                    privateKey = null;
                    passphrase = null;
                    break;
            }

            if (string.IsNullOrWhiteSpace(privateKey))
            {
                _log.LogWarning(
                    "Could not resolve SFTP private key for profile {Id}", profile.AuthProfileId);
                return null;
            }

            return new ResolvedSftpCredential(subtype.Username, privateKey, passphrase);
        }
    }
}
