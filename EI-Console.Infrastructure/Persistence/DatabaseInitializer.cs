using EI_Console.Domain.Models;

using EiConsole.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EI_Console.Infrastructure.Persistence;

public sealed class DatabaseInitializer : IHostedService
{
    private readonly IServiceScopeFactory _scopes;
    public DatabaseInitializer(IServiceScopeFactory scopes) => _scopes = scopes;

    public async Task StartAsync(CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EiDbContext>();

        if (!await db.Users.AnyAsync(ct))
        {
            db.Users.Add(new User
            {
                // Remove: UserId = 1, 
                KeycloakSubject = "system",
                DisplayName = "System",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(ct);
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}