using _04_EI_Console.Shared.Application.Contracts;

using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Application.Interfaces.Services;
using EI_Console.Application.Mappings;
using EI_Console.Domain.Models;
using EI_Console.Shared.Application;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Services
{
    public sealed class TenantService : ITenantService
    {
        private readonly ITenantRepository _tenants;
        private readonly IJobRepository _jobs;
        private readonly IConnectionRepository _connections;
        private readonly IAuthProfileRepository _authProfiles;
        private readonly IUnitOfWork _uow;
        private readonly ILogger<TenantService> _log;

        public TenantService(
            ITenantRepository tenants,
            IJobRepository jobs,
            IConnectionRepository connections,
            IAuthProfileRepository authProfiles,
            IUnitOfWork uow,
            ILogger<TenantService> log)
        {
            _tenants = tenants;
            _jobs = jobs;
            _connections = connections;
            _authProfiles = authProfiles;
            _uow = uow;
            _log = log;
        }

        // ---------------------------------------------------------------------
        // Read
        // ---------------------------------------------------------------------

        public async Task<List<TenantDto>> ListAsync(CancellationToken ct = default)
        {
            var all = await _tenants.GetAllAsync(ct);
            var jobs = await _jobs.GetAllAsync(ct);
            var conns = await _connections.GetAllAsync(ct);
            var auths = await _authProfiles.GetAllAsync(ct);

            var result = new List<TenantDto>(all.Count);

            foreach (var t in all)
            {
                var jobCount = jobs.Count(j => j.TenantId == t.TenantId);
                var connCount = conns.Count(c => c.TenantId == t.TenantId);
                var authCount = auths.Count(a => a.TenantId == t.TenantId);

                result.Add(t.ToDto(jobCount, connCount, authCount, watermarkCount: 0));
            }

            return result;
        }

        public async Task<TenantDto?> GetAsync(int id, CancellationToken ct = default)
        {
            var tenant = await _tenants.GetByIdAsync(id, ct);
            if (tenant is null) return null;

            var jobCount = (await _jobs.GetByTenantAsync(id, ct)).Count;
            var connCount = (await _connections.GetAllAsync(ct)).Count(c => c.TenantId == id);
            var authCount = (await _authProfiles.GetAllAsync(ct)).Count(a => a.TenantId == id);

            return tenant.ToDto(jobCount, connCount, authCount, watermarkCount: 0);
        }

        // ---------------------------------------------------------------------
        // Create
        // ---------------------------------------------------------------------

        public async Task<TenantDto> CreateAsync(CreateTenantRequest request, CancellationToken ct = default)
        {
            var code = request.Code.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(code))
                throw new InvalidOperationException("Tenant code is required.");

            if (!System.Text.RegularExpressions.Regex.IsMatch(code, "^[a-z0-9-]+$"))
                throw new InvalidOperationException(
                    "Tenant code must contain only lowercase letters, digits, and hyphens.");

            if (await _tenants.GetByCodeAsync(code, ct) is not null)
                throw new InvalidOperationException($"Tenant code '{code}' already exists.");

            var tenant = new Tenant
            {
                TenantCode = code,
                Name = request.Name.Trim(),
                CoreApiBaseUrl = string.IsNullOrWhiteSpace(request.CoreUrl) ? null : request.CoreUrl.Trim(),
                CoreDbConnectionSecretRef = string.IsNullOrWhiteSpace(request.CoreDbRef) ? null : request.CoreDbRef.Trim(),
                IsActive = request.Active,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            _tenants.Add(tenant);
            await _uow.SaveChangesAsync(ct);

            _log.LogInformation(
                "Tenant {Code} created with id {Id}",
                tenant.TenantCode, tenant.TenantId);

            return tenant.ToDto(0, 0, 0, 0);
        }

        // ---------------------------------------------------------------------
        // Update
        // ---------------------------------------------------------------------

        public async Task<TenantDto?> UpdateAsync(int id, UpdateTenantRequest request, CancellationToken ct = default)
        {
            var tenant = await _tenants.GetByIdAsync(id, ct);
            if (tenant is null) return null;

            tenant.Name = request.Name.Trim();
            tenant.CoreApiBaseUrl = string.IsNullOrWhiteSpace(request.CoreUrl) ? null : request.CoreUrl.Trim();
            tenant.CoreDbConnectionSecretRef = string.IsNullOrWhiteSpace(request.CoreDbRef) ? null : request.CoreDbRef.Trim();
            tenant.IsActive = request.Active;
            tenant.UpdatedAt = DateTimeOffset.UtcNow;

            await _uow.SaveChangesAsync(ct);

            return await GetAsync(id, ct);
        }

        // ---------------------------------------------------------------------
        // Delete
        // ---------------------------------------------------------------------

        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            var tenant = await _tenants.GetByIdAsync(id, ct);
            if (tenant is null) return false;

            // Guard against orphaning jobs — those are expensive artefacts and
            // deleting a tenant with jobs would cascade-delete the pipeline
            // definitions, execution history, and outbox entries.
            var jobs = await _jobs.GetByTenantAsync(id, ct);
            if (jobs.Count > 0)
                throw new InvalidOperationException(
                    $"Cannot delete tenant '{tenant.Name}' — it owns {jobs.Count} job(s). " +
                    "Delete or reassign the jobs first.");

            _tenants.Remove(tenant);
            await _uow.SaveChangesAsync(ct);

            _log.LogWarning("Tenant {Code} (id {Id}) deleted", tenant.TenantCode, tenant.TenantId);
            return true;
        }

        // ---------------------------------------------------------------------
        // Activate / Deactivate
        // ---------------------------------------------------------------------

        public async Task<TenantDto?> SetActiveAsync(int id, bool active, CancellationToken ct = default)
        {
            var tenant = await _tenants.GetByIdAsync(id, ct);
            if (tenant is null) return null;

            tenant.IsActive = active;
            tenant.UpdatedAt = DateTimeOffset.UtcNow;
            await _uow.SaveChangesAsync(ct);

            _log.LogInformation(
                "Tenant {Code} (id {Id}) set to {State}",
                tenant.TenantCode, tenant.TenantId, active ? "Active" : "Inactive");

            return await GetAsync(id, ct);
        }
    }
}
