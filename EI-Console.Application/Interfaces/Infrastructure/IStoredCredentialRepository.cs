using EI_Console.Domain.Models;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Interfaces.Infrastructure
{
    public interface IStoredCredentialRepository
    {
        Task<StoredCredential?> GetByIdAsync(int id, CancellationToken ct = default);
    }
}
