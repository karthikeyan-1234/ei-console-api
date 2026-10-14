using EI_Console.Domain.Models;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Interfaces.Infrastructure
{
    public sealed record ResolvedFtpCredential(string Username, string Password);

    public interface IFtpCredentialResolver
    {
        Task<ResolvedFtpCredential?> ResolveAsync(AuthProfile? profile, CancellationToken ct = default);
    }
}
