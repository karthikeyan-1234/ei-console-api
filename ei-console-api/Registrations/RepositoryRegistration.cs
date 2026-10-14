using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Infrastructure.Repositories;

using Microsoft.Extensions.DependencyInjection;

using System;
using System.Collections.Generic;
using System.Text;

namespace ei_console_api.Registrations
{
    public static class RepositoryRegistration
    {
        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            services.AddScoped<ITenantRepository, TenantRepository>();
            services.AddScoped<IConnectionRepository, ConnectionRepository>();
            services.AddScoped<IAuthProfileRepository, AuthProfileRepository>();
            services.AddScoped<IJobRepository, JobRepository>();
            services.AddScoped<IExecutionRepository, ExecutionRepository>();
            services.AddScoped<ITaskExecutionLogRepository, TaskExecutionLogRepository>();
            return services;
        }
    }
}
