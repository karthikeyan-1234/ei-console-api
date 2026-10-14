using EI_Console.Domain.Models;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Interfaces.Infrastructure
{
    public sealed record ResolvedSftpCredential(
        string Username,
        string PrivateKey,
        string? Passphrase);

    public interface ISftpCredentialResolver
    {
        Task<ResolvedSftpCredential?> ResolveAsync(AuthProfile? profile, CancellationToken ct = default);
    }
}
