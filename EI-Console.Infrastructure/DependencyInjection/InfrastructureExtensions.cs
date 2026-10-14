using _04_EI_Console.Shared.Application;

using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Application.Interfaces.Services;
using EI_Console.Application.Pipeline;
using EI_Console.Application.Pipeline.Executors;
using EI_Console.Application.Services;
using EI_Console.Infrastructure.Credentials;
using EI_Console.Infrastructure.Identity;
using EI_Console.Infrastructure.Persistence;
using EI_Console.Infrastructure.Pipeline;
using EI_Console.Infrastructure.Repositories;
using EI_Console.Infrastructure.Scheduling;
using EI_Console.Infrastructure.Tenancy;
using EI_Console.Shared;
using EI_Console.Shared.Application;
using EI_Console.Shared.Contracts;

using EiConsole.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Quartz;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Infrastructure.DependencyInjection
{
    public static class InfrastructureExtensions
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration config)
        {
            // DbContext
            services.AddDbContext<EiDbContext>(opt =>
                opt.UseSqlServer(config.GetConnectionString("EiDb")));

            // HttpContext-based cross-cutting services
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUser, CurrentUser>();
            services.AddScoped<ITenantContext, TenantContext>();
            services.AddSingleton<IClock, SystemClock>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Credential resolution
            services.AddScoped<ICredentialResolver, ConfigCredentialResolver>();

            // Application services
            services.AddScoped<ITenantService, TenantService>();
            services.AddScoped<IConnectionService, ConnectionService>();
            services.AddScoped<IAuthProfileService, AuthProfileService>();
            services.AddScoped<IJobService, JobService>();

            // HTTP clients
            services.AddHttpClient("ConnectionTest");

            // Repositories — if you already have an AddRepositories() extension,
            // uncomment the following line. If repositories are registered by the
            // API host itself, leave this commented.
            // services.AddRepositories();


            // Pipeline execution
            services.AddScoped<EI_Console.Application.Pipeline.ITaskExecutor, ApiPullExecutor>();
            services.AddScoped<EI_Console.Application.Pipeline.ITaskExecutor, ApiPushExecutor>();
            services.AddScoped<EI_Console.Application.Pipeline.ITaskExecutor, TransformExecutor>();
            services.AddScoped<EI_Console.Application.Pipeline.ITaskExecutor, NotifyExecutor>();
            services.AddScoped<EI_Console.Application.Pipeline.ITaskExecutorFactory, TaskExecutorFactory>();
            services.AddScoped<EI_Console.Application.Pipeline.IJsonataEvaluator, PassthroughJsonataEvaluator>();
            services.AddScoped<EI_Console.Application.Pipeline.IRealtimePublisher, SignalRRealtimePublisher>();
            services.AddScoped<EI_Console.Application.Pipeline.PipelineRunner>();

            // Application services
            services.AddScoped<IExecutionService, ExecutionService>();
            services.AddScoped<IExecutionServiceBridge>(sp =>
    (IExecutionServiceBridge)sp.GetRequiredService<IExecutionService>());

            // HTTP client for executors
            services.AddHttpClient("EiExecutor");

            // Quartz scheduler
            services.AddQuartz(_ => { /* Quartz 4.x auto-configures */ });
            services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

            // Scoped, not singleton. The bridge resolves a scoped IJobRepository, so it
            // must run in the same scope as its dependencies.
            services.AddScoped<IJobRepositoryBridge>(sp =>
                (IJobRepositoryBridge)sp.GetRequiredService<IJobRepository>());
            services.AddScoped<IJobScheduler, QuartzJobScheduler>();

            services.AddHostedService<SchedulerReconciliationService>();

            return services;
        }
    }
}
