using EI_Console.Domain.Models;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Interfaces.Services
{
    public interface ICredentialResolver
    {
        Task<IReadOnlyDictionary<string, string>> ResolveHeadersAsync(
            AuthProfile? profile,
            CancellationToken ct = default);
    }
}
