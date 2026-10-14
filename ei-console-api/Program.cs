using EI_Console.Application.Pipeline;
using EI_Console.Infrastructure.DependencyInjection;

using ei_console_api.Hubs;
using ei_console_api.Registrations;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Wolverine;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "EI Console API",
        Version = "v1",
        Description = "Enterprise Integration workflow orchestration backend",
    });
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:4200")
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()));

builder.Services.AddRepositories();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Host.UseWolverine(opts =>
{
    opts.UseRuntimeCompilation();
    opts.Discovery.IncludeAssembly(typeof(RunJobCommandHandler).Assembly);
    opts.Policies.AutoApplyTransactions();

    // EF Core's DbContext is registered via an internal lambda factory that
    // Wolverine's codegen can't see through. This is the documented escape
    // hatch for DbContext-injected handlers. See:
    // https://wolverinefx.net/guide/codegen.html
    opts.ServiceLocationPolicy =JasperFx.CodeGeneration.Model.ServiceLocationPolicy.AllowedButWarn;
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "EI Console API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ExecutionHub>("/hubs/ei");

app.Run();