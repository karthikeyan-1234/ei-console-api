using EI_Console.Domain.Models;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Interfaces.Infrastructure
{
    public interface ISqlConnectionStringResolver
    {
        /// <summary>
        /// Builds a full SQL Server connection string by combining the connection's
        /// base fragment with the credential portion resolved from the auth profile.
        /// Returns null if the credential can't be resolved.
        /// </summary>
        Task<string?> ResolveAsync(AuthProfile? profile, string baseFragment, CancellationToken ct = default);
    }
}
