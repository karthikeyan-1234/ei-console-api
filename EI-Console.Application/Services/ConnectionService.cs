using _04_EI_Console.Shared.Application.Contracts;

using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Application.Interfaces.Services;
using EI_Console.Application.Mappings;
using EI_Console.Shared.Application;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace EI_Console.Application.Services
{
    public sealed class ConnectionService : IConnectionService
    {
        private readonly IConnectionRepository _connections;
        private readonly IAuthProfileRepository _authProfiles;
        private readonly ICredentialResolver _credentials;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IUnitOfWork _uow;
        private readonly ILogger<ConnectionService> _log;

        public ConnectionService(
            IConnectionRepository connections,
            IAuthProfileRepository authProfiles,
            ICredentialResolver credentials,
            IHttpClientFactory httpFactory,
            IUnitOfWork uow,
            ILogger<ConnectionService> log)
        {
            _connections = connections;
            _authProfiles = authProfiles;
            _credentials = credentials;
            _httpFactory = httpFactory;
            _uow = uow;
            _log = log;
        }

        public async Task<List<ConnectionDto>> ListAsync(CancellationToken ct = default)
        {
            var all = await _connections.GetAllAsync(ct);
            var profiles = await _authProfiles.GetAllAsync(ct);
            var nameById = profiles.ToDictionary(p => p.AuthProfileId, p => p.Name);

            return all.Select(c => c.ToDto(
                c.AuthProfileId is int id && nameById.TryGetValue(id, out var name) ? name : null
            )).ToList();
        }

        public async Task<ConnectionDto?> GetAsync(int id, CancellationToken ct = default)
        {
            var c = await _connections.GetByIdAsync(id, ct);
            if (c is null) return null;

            string? profileName = null;
            if (c.AuthProfileId is int pid)
            {
                var p = await _authProfiles.GetByIdAsync(pid, ct);
                profileName = p?.Name;
            }

            return c.ToDto(profileName);
        }

        public async Task<ConnectionDto> CreateAsync(CreateConnectionRequest request, CancellationToken ct = default)
        {
            var c = new Domain.Models.Connection();
            c.Apply(request);
            c.IsActive = true;
            c.CreatedAt = DateTimeOffset.UtcNow;

            _connections.Add(c);
            await _uow.SaveChangesAsync(ct);

            _log.LogInformation("Connection {Name} created with id {Id}", c.Name, c.ConnectionId);

            return await GetAsync(c.ConnectionId, ct) ?? c.ToDto();
        }

        public async Task<ConnectionDto?> UpdateAsync(int id, UpdateConnectionRequest request, CancellationToken ct = default)
        {
            var c = await _connections.GetByIdAsync(id, ct);
            if (c is null) return null;

            c.Apply(request);
            c.UpdatedAt = DateTimeOffset.UtcNow;

            await _uow.SaveChangesAsync(ct);
            return await GetAsync(id, ct);
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            var c = await _connections.GetByIdAsync(id, ct);
            if (c is null) return false;

            _connections.Remove(c);
            await _uow.SaveChangesAsync(ct);
            return true;
        }

        public async Task<TestConnectionResultDto> TestAsync(int id, CancellationToken ct = default)
        {
            var c = await _connections.GetWithAuthProfileAsync(id, ct);
            if (c is null)
                return new TestConnectionResultDto(false, 0, 0, "Connection not found.");

            var headers = await _credentials.ResolveHeadersAsync(c.AuthProfile, ct);

            var sw = Stopwatch.StartNew();
            try
            {
                using var client = _httpFactory.CreateClient("ConnectionTest");
                client.Timeout = TimeSpan.FromSeconds(Math.Clamp(c.TimeoutSeconds, 5, 30));

                using var req = new HttpRequestMessage(HttpMethod.Get, c.BaseUrl);
                foreach (var h in headers)
                    req.Headers.TryAddWithoutValidation(h.Key, h.Value);

                using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                sw.Stop();

                var ok = (int)resp.StatusCode < 500;
                return new TestConnectionResultDto(
                    Ok: ok,
                    LatencyMs: (int)sw.ElapsedMilliseconds,
                    Status: (int)resp.StatusCode,
                    Message: resp.IsSuccessStatusCode ? "OK" : resp.ReasonPhrase);
            }
            catch (TaskCanceledException)
            {
                sw.Stop();
                return new TestConnectionResultDto(false, (int)sw.ElapsedMilliseconds, 0, "Timeout");
            }
            catch (HttpRequestException ex)
            {
                sw.Stop();
                return new TestConnectionResultDto(false, (int)sw.ElapsedMilliseconds, 0, ex.Message);
            }
        }
    }
}
