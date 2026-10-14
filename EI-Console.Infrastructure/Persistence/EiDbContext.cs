using EI_Console.Domain.Enums;
using EI_Console.Domain.Models;


using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.VisualBasic;

using System.Reflection.Emit;
using System.Text;

using CircuitBreakerState = EI_Console.Domain.Models.CircuitBreakerState;

namespace EiConsole.Infrastructure.Persistence;

public class EiDbContext : DbContext
{
    public const string Schema = "ei";

    public EiDbContext(DbContextOptions<EiDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<TenantUser> TenantUsers => Set<TenantUser>();
    public DbSet<StoredCredential> StoredCredentials => Set<StoredCredential>();
    public DbSet<AuthProfile> AuthProfiles => Set<AuthProfile>();
    public DbSet<AuthProfileApiKey> AuthProfileApiKeys => Set<AuthProfileApiKey>();
    public DbSet<AuthProfileFtp> AuthProfileFtp => Set<AuthProfileFtp>();
    public DbSet<AuthProfileSftp> AuthProfileSftp => Set<AuthProfileSftp>();
    public DbSet<AuthProfileSqlServer> AuthProfileSqlServer => Set<AuthProfileSqlServer>();
    public DbSet<AuthProfileOAuth2> AuthProfileOAuth2 => Set<AuthProfileOAuth2>();
    public DbSet<AuthProfileKeycloak> AuthProfileKeycloak => Set<AuthProfileKeycloak>();
    public DbSet<AuthProfileMtls> AuthProfileMtls => Set<AuthProfileMtls>();
    public DbSet<AuthProfileWsSecurity> AuthProfileWsSecurity => Set<AuthProfileWsSecurity>();
    public DbSet<Connection> Connections => Set<Connection>();
    public DbSet<RateLimitPolicy> RateLimitPolicies => Set<RateLimitPolicy>();
    public DbSet<CircuitBreakerState> CircuitBreakerStates => Set<CircuitBreakerState>();
    public DbSet<IntegrationJob> IntegrationJobs => Set<IntegrationJob>();
    public DbSet<JobVersion> JobVersions => Set<JobVersion>();
    public DbSet<JobSchedule> JobSchedules => Set<JobSchedule>();
    public DbSet<JobTask> JobTasks => Set<JobTask>();
    public DbSet<JobExecution> JobExecutions => Set<JobExecution>();
    public DbSet<TaskExecutionLog> TaskExecutionLogs => Set<TaskExecutionLog>();
    public DbSet<TaskBranch> TaskBranches => Set<TaskBranch>();
    public DbSet<TaskIterator> TaskIterators => Set<TaskIterator>();
    public DbSet<TaskSqlQuery> TaskSqlQueries => Set<TaskSqlQuery>();
    public DbSet<TaskTransform> TaskTransforms => Set<TaskTransform>();
    public DbSet<ScatterGatherItem> ScatterGatherItems => Set<ScatterGatherItem>();
    public DbSet<DeadLetterItem> DeadLetterItems => Set<DeadLetterItem>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<SyncWatermark> SyncWatermarks => Set<SyncWatermark>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        // Enum <-> string conversions (all enum columns are NVARCHAR in the schema)
        b.Properties<AuthType>().HaveConversion<string>();
        b.Properties<CredentialStorageMode>().HaveConversion<string>();
        b.Properties<Protocol>().HaveConversion<string>();
        b.Properties<CircuitBreakerStatus>().HaveConversion<string>();
        b.Properties<JobStatus>().HaveConversion<string>();
        b.Properties<TriggerType>().HaveConversion<string>();
        b.Properties<ExecutionStatus>().HaveConversion<string>();
        b.Properties<TriggerSource>().HaveConversion<string>();
        b.Properties<TaskRole>().HaveConversion<string>();
        b.Properties<TaskType>().HaveConversion<string>();
        b.Properties<TaskExecutionStatus>().HaveConversion<string>();
        b.Properties<BranchExecutionMode>().HaveConversion<string>();
        b.Properties<DeadLetterStatus>().HaveConversion<string>();
        b.Properties<ScatterItemStatus>().HaveConversion<string>();
        b.Properties<OutboxStatus>().HaveConversion<string>();
        b.Properties<WatermarkType>().HaveConversion<string>();
        b.Properties<RateLimitScope>().HaveConversion<string>();
        b.Properties<StoredCredentialKind>().HaveConversion<string>();
        b.Properties<EncryptionState>().HaveConversion<string>();
        b.Properties<TenantUserRole>().HaveConversion<string>();

        // Chat enums use lowercase strings in the DB
        b.Properties<ChatRole>().HaveConversion<ChatRoleConverter>();
        b.Properties<ChatMessageStatus>().HaveConversion<ChatMessageStatusConverter>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(Schema);
        ApplySnakeCaseNaming(b);

        // ---------- Tenant / User / Membership ----------
        b.Entity<Tenant>(e =>
        {
            e.ToTable("tenants");
            e.HasKey(x => x.TenantId);
            e.HasIndex(x => x.TenantCode).IsUnique().HasDatabaseName("UQ_tenants_tenant_code");
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("sysdatetimeoffset()");
            e.Property(x => x.RowVersion).IsRowVersion();
        });

        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.UserId);
            e.HasIndex(x => x.KeycloakSubject).IsUnique().HasDatabaseName("UQ_users_keycloak_subject");
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("sysdatetimeoffset()");
            e.Property(x => x.RowVersion).IsRowVersion();
        });

        b.Entity<TenantUser>(e =>
        {
            e.ToTable("tenant_users");
            e.HasKey(x => x.TenantUserId);
            e.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique().HasDatabaseName("UQ_tenant_users_tenant_user");
            e.Property(x => x.GrantedAt).HasDefaultValueSql("sysdatetimeoffset()");
            e.HasOne(x => x.Tenant).WithMany(t => t.TenantUsers).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany(u => u.TenantUsers).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.GrantedByUser).WithMany().HasForeignKey(x => x.GrantedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Stored credentials ----------
        b.Entity<StoredCredential>(e =>
        {
            e.ToTable("stored_credentials");
            e.HasKey(x => x.StoredCredentialId);
            e.Property(x => x.EncryptionState).HasDefaultValue(EncryptionState.Plaintext);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("sysdatetimeoffset()");
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Tenant).WithMany(t => t.StoredCredentials).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.SetNull);
        });

        // ---------- Auth profiles (TPT-style, composite key on subtypes) ----------
        b.Entity<AuthProfile>(e =>
        {
            e.ToTable("auth_profiles");
            e.HasKey(x => x.AuthProfileId);
            e.HasAlternateKey(x => new { x.AuthProfileId, x.AuthType })
                .HasName("UQ_auth_profiles_id_type");
            e.Property(x => x.CredentialStorageMode).HasDefaultValue(CredentialStorageMode.KeyVault);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("sysdatetimeoffset()");
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Tenant).WithMany(t => t.AuthProfiles).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<AuthProfileApiKey>(e =>
        {
            e.ToTable("auth_profile_api_key");
            e.HasKey(x => x.AuthProfileId);
            e.Property(x => x.AuthType).HasDefaultValue(AuthType.ApiKey);
            e.HasOne(x => x.AuthProfile).WithOne(p => p.ApiKey)
                .HasForeignKey<AuthProfileApiKey>(x => new { x.AuthProfileId, x.AuthType })
                .HasPrincipalKey<AuthProfile>(p => new { p.AuthProfileId, p.AuthType })
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AuthProfileFtp>(e =>
        {
            e.ToTable("auth_profile_ftp");
            e.HasKey(x => x.AuthProfileId);
            e.Property(x => x.AuthType).HasDefaultValue(AuthType.FtpCredentials);
            e.HasOne(x => x.AuthProfile).WithOne(p => p.Ftp)
                .HasForeignKey<AuthProfileFtp>(x => new { x.AuthProfileId, x.AuthType })
                .HasPrincipalKey<AuthProfile>(p => new { p.AuthProfileId, p.AuthType })
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.StoredCredential).WithMany(c => c.FtpProfiles)
                .HasForeignKey(x => x.StoredCredentialId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AuthProfileSftp>(e =>
        {
            e.ToTable("auth_profile_sftp");
            e.HasKey(x => x.AuthProfileId);
            e.Property(x => x.AuthType).HasDefaultValue(AuthType.SftpKeyCredentials);
            e.HasOne(x => x.AuthProfile).WithOne(p => p.Sftp)
                .HasForeignKey<AuthProfileSftp>(x => new { x.AuthProfileId, x.AuthType })
                .HasPrincipalKey<AuthProfile>(p => new { p.AuthProfileId, p.AuthType })
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.StoredCredential).WithMany(c => c.SftpProfiles)
                .HasForeignKey(x => x.StoredCredentialId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AuthProfileSqlServer>(e =>
        {
            e.ToTable("auth_profile_sql_server");
            e.HasKey(x => x.AuthProfileId);
            e.Property(x => x.AuthType).HasDefaultValue(AuthType.SqlServerConnectionString);
            e.HasOne(x => x.AuthProfile).WithOne(p => p.SqlServer)
                .HasForeignKey<AuthProfileSqlServer>(x => new { x.AuthProfileId, x.AuthType })
                .HasPrincipalKey<AuthProfile>(p => new { p.AuthProfileId, p.AuthType })
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.StoredCredential).WithMany(c => c.SqlServerProfiles)
                .HasForeignKey(x => x.StoredCredentialId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AuthProfileOAuth2>(e =>
        {
            e.ToTable("auth_profile_oauth2");
            e.HasKey(x => x.AuthProfileId);
            e.Property(x => x.AuthType).HasDefaultValue(AuthType.OAuth2ClientCredentials);
            e.HasOne(x => x.AuthProfile).WithOne(p => p.OAuth2)
                .HasForeignKey<AuthProfileOAuth2>(x => new { x.AuthProfileId, x.AuthType })
                .HasPrincipalKey<AuthProfile>(p => new { p.AuthProfileId, p.AuthType })
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AuthProfileKeycloak>(e =>
        {
            e.ToTable("auth_profile_keycloak");
            e.HasKey(x => x.AuthProfileId);
            e.Property(x => x.AuthType).HasDefaultValue(AuthType.KeycloakAuthCodeExchange);
            e.HasOne(x => x.AuthProfile).WithOne(p => p.Keycloak)
                .HasForeignKey<AuthProfileKeycloak>(x => new { x.AuthProfileId, x.AuthType })
                .HasPrincipalKey<AuthProfile>(p => new { p.AuthProfileId, p.AuthType })
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AuthProfileMtls>(e =>
        {
            e.ToTable("auth_profile_mtls");
            e.HasKey(x => x.AuthProfileId);
            e.Property(x => x.AuthType).HasDefaultValue(AuthType.MutualTls);
            e.HasOne(x => x.AuthProfile).WithOne(p => p.Mtls)
                .HasForeignKey<AuthProfileMtls>(x => new { x.AuthProfileId, x.AuthType })
                .HasPrincipalKey<AuthProfile>(p => new { p.AuthProfileId, p.AuthType })
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AuthProfileWsSecurity>(e =>
        {
            e.ToTable("auth_profile_ws_security");
            e.HasKey(x => x.AuthProfileId);
            e.Property(x => x.AuthType).HasDefaultValue(AuthType.WsSecurityUsernameToken);
            e.HasOne(x => x.AuthProfile).WithOne(p => p.WsSecurity)
                .HasForeignKey<AuthProfileWsSecurity>(x => new { x.AuthProfileId, x.AuthType })
                .HasPrincipalKey<AuthProfile>(p => new { p.AuthProfileId, p.AuthType })
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- Rate limits & connections ----------
        b.Entity<RateLimitPolicy>(e =>
        {
            e.ToTable("rate_limit_policies");
            e.HasKey(x => x.RateLimitPolicyId);
            e.Property(x => x.MaxRequestsPerSecond).HasDefaultValue(50);
            e.Property(x => x.MaxConcurrentRequests).HasDefaultValue(20);
            e.Property(x => x.BurstCapacity).HasDefaultValue(100);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("sysdatetimeoffset()");
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Tenant).WithMany(t => t.RateLimitPolicies).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.SetNull);
            e.ToTable(t => t.HasCheckConstraint("CK_rate_limit_policies_scope_tenant",
                "([scope]='Global' AND [tenant_id] IS NULL) OR ([scope]='PerTenant' AND [tenant_id] IS NOT NULL) OR [scope]='PerConnection'"));
            e.ToTable(t => t.HasCheckConstraint("CK_rate_limit_policies_values",
                "[max_requests_per_second]>0 AND [max_concurrent_requests]>0 AND [burst_capacity]>0"));
        });

        b.Entity<Connection>(e =>
        {
            e.ToTable("connections");
            e.HasKey(x => x.ConnectionId);
            e.Property(x => x.TimeoutSeconds).HasDefaultValue(30);
            e.Property(x => x.RetryCount).HasDefaultValue(3);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("sysdatetimeoffset()");
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Tenant).WithMany(t => t.Connections).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.AuthProfile).WithMany(p => p.Connections).HasForeignKey(x => x.AuthProfileId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.RateLimitPolicy).WithMany(r => r.Connections).HasForeignKey(x => x.RateLimitPolicyId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_connections_protocol",
                "[protocol] IN ('Sftp','Ftp','SqlServer','Json','Soap','Rest')"));
            e.ToTable(t => t.HasCheckConstraint("CK_connections_port",
                "[port] IS NULL OR ([port] BETWEEN 1 AND 65535)"));
            e.ToTable(t => t.HasCheckConstraint("CK_connections_port_protocol",
                "(([protocol] IN ('Sftp','Ftp')) AND [port] IS NOT NULL) OR (NOT ([protocol] IN ('Sftp','Ftp')) AND [port] IS NULL)"));
        });

        b.Entity<CircuitBreakerState>(e =>
        {
            e.ToTable("circuit_breaker_states");
            e.HasKey(x => x.CircuitId);
            e.HasIndex(x => x.ConnectionId).IsUnique().HasDatabaseName("UQ_circuit_breaker_connection");
            e.Property(x => x.State).HasDefaultValue(CircuitBreakerStatus.Closed);
            e.Property(x => x.FailureCount).HasDefaultValue(0);
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("sysdatetimeoffset()");
            e.HasOne(x => x.Connection).WithOne(c => c.CircuitBreaker)
                .HasForeignKey<CircuitBreakerState>(x => x.ConnectionId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- Jobs ----------
        b.Entity<IntegrationJob>(e =>
        {
            e.ToTable("integration_jobs");
            e.HasKey(x => x.JobId);
            e.HasIndex(x => new { x.TenantId, x.EndpointSlug }).IsUnique().HasDatabaseName("UQ_integration_jobs_slug");
            e.Property(x => x.Status).HasDefaultValue(JobStatus.Draft);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("sysdatetimeoffset()");
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Tenant).WithMany(t => t.IntegrationJobs).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<JobSchedule>(e =>
        {
            e.ToTable("job_schedules");
            e.HasKey(x => x.JobId);
            e.Property(x => x.Timezone).HasDefaultValue("UTC");
            e.HasOne(x => x.Job).WithOne(j => j.Schedule)
                .HasForeignKey<JobSchedule>(x => x.JobId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<JobVersion>(e =>
        {
            e.ToTable("job_versions");
            e.HasKey(x => x.VersionId);
            e.HasIndex(x => new { x.JobId, x.VersionNumber }).IsUnique().HasDatabaseName("UQ_job_versions_job_number");
            e.Property(x => x.PipelineSchemaVersion).HasDefaultValue(1);
            e.Property(x => x.IsPublished).HasDefaultValue(false);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("sysdatetimeoffset()");
            e.HasOne(x => x.Job).WithMany(j => j.Versions).HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PublishedByUser).WithMany().HasForeignKey(x => x.PublishedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_job_versions_publish_consistency",
                "([is_published]=1 AND [published_at] IS NOT NULL) OR [is_published]=0"));
            e.ToTable(t => t.HasCheckConstraint("CK_job_versions_version_number", "[version_number] > 0"));
        });

        b.Entity<JobTask>(e =>
        {
            e.ToTable("job_tasks");
            e.HasKey(x => x.TaskId);
            e.HasAlternateKey(x => new { x.TaskId, x.TaskType }).HasName("UQ_job_tasks_id_type");
            e.Property(x => x.TaskRole).HasDefaultValue(TaskRole.Root);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("sysdatetimeoffset()");

            // NEW
            e.Property(x => x.ConfigurationJson).HasColumnName("configuration_json").HasColumnType("nvarchar(max)");

            e.HasOne(x => x.Version).WithMany(v => v.Tasks).HasForeignKey(x => x.VersionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ParentTask).WithMany(t => t.ChildTasks).HasForeignKey(x => x.ParentTaskId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Connection).WithMany(c => c.JobTasks).HasForeignKey(x => x.ConnectionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AuthProfile).WithMany(p => p.JobTasks).HasForeignKey(x => x.AuthProfileId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_job_tasks_root_parent",
                "([task_role]='Root' AND [parent_task_id] IS NULL) OR ([task_role]<>'Root' AND [parent_task_id] IS NOT NULL)"));
            e.ToTable(t => t.HasCheckConstraint("CK_job_tasks_type",
                "[task_type] IN ('JoinPoint','Branch','Notify','ApiPush','Transform','SqlQuery','ApiPull')"));
        });

        // Task subtypes share the same PK+TaskType alternate key
        b.Entity<TaskBranch>(e =>
        {
            e.ToTable("task_branches");
            e.HasKey(x => x.TaskId);
            e.Property(x => x.TaskType).HasDefaultValue(TaskType.Branch);
            e.Property(x => x.ExecutionMode).HasDefaultValue(BranchExecutionMode.Sequential);
            e.HasOne(x => x.Task).WithOne(t => t.Branch)
                .HasForeignKey<TaskBranch>(x => new { x.TaskId, x.TaskType })
                .HasPrincipalKey<JobTask>(t => new { t.TaskId, t.TaskType })
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TaskIterator>(e =>
        {
            e.ToTable("task_iterators");
            e.HasKey(x => x.TaskId);
            e.Property(x => x.TaskType).HasDefaultValue(TaskType.Transform);
            e.Property(x => x.BatchSize).HasDefaultValue(100);
            e.Property(x => x.MaxConcurrency).HasDefaultValue(1);
            e.HasOne(x => x.Task).WithOne(t => t.Iterator)
                .HasForeignKey<TaskIterator>(x => new { x.TaskId, x.TaskType })
                .HasPrincipalKey<JobTask>(t => new { t.TaskId, t.TaskType })
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TaskSqlQuery>(e =>
        {
            e.ToTable("task_sql_queries");
            e.HasKey(x => x.TaskId);
            e.Property(x => x.TaskType).HasDefaultValue(TaskType.SqlQuery);
            e.Property(x => x.QueryTimeoutSeconds).HasDefaultValue(30);
            e.HasOne(x => x.Task).WithOne(t => t.SqlQuery)
                .HasForeignKey<TaskSqlQuery>(x => new { x.TaskId, x.TaskType })
                .HasPrincipalKey<JobTask>(t => new { t.TaskId, t.TaskType })
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TaskTransform>(e =>
        {
            e.ToTable("task_transforms");
            e.HasKey(x => x.TaskId);
            e.Property(x => x.TaskType).HasDefaultValue(TaskType.Transform);
            e.Property(x => x.UseSampleInput).HasDefaultValue(false);
            e.HasOne(x => x.Task).WithOne(t => t.Transform)
                .HasForeignKey<TaskTransform>(x => new { x.TaskId, x.TaskType })
                .HasPrincipalKey<JobTask>(t => new { t.TaskId, t.TaskType })
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- Executions ----------
        b.Entity<JobExecution>(e =>
        {
            e.ToTable("job_executions");
            e.HasKey(x => x.ExecutionId);
            e.Property(x => x.ExecutionId).HasDefaultValueSql("NEWSEQUENTIALID()");
            e.Property(x => x.Status).HasDefaultValue(ExecutionStatus.Running);
            e.Property(x => x.ItemCount).HasDefaultValue(0);
            e.Property(x => x.SuccessCount).HasDefaultValue(0);
            e.Property(x => x.FailureCount).HasDefaultValue(0);
            e.Property(x => x.StartedAt).HasDefaultValueSql("sysdatetimeoffset()");
            e.Property(x => x.PayloadJson).HasColumnName("payload_json").HasColumnType("nvarchar(max)");
            e.HasOne(x => x.Job).WithMany(j => j.Executions).HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Version).WithMany(v => v.Executions).HasForeignKey(x => x.VersionId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_job_executions_completion",
                "([status]='Running' AND [completed_at] IS NULL) OR [status]<>'Running'"));
            e.ToTable(t => t.HasCheckConstraint("CK_job_executions_status",
                "[status] IN ('Cancelled','Failed','Completed','Running')"));
        });

        b.Entity<TaskExecutionLog>(e =>
        {
            e.ToTable("task_execution_log");
            e.HasKey(x => x.LogId);
            e.Property(x => x.AttemptNumber).HasDefaultValue(1);
            e.Property(x => x.StartedAt).HasDefaultValueSql("sysdatetimeoffset()");
            e.Property(x => x.DurationMs)
                .HasComputedColumnSql("datediff_big(millisecond,[started_at],[completed_at])", stored: true);
            e.HasOne(x => x.Execution).WithMany(je => je.TaskLogs).HasForeignKey(x => x.ExecutionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ParentLog).WithMany(l => l.ChildLogs).HasForeignKey(x => x.ParentLogId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ScatterGatherItem>(e =>
        {
            e.ToTable("scatter_gather_items");
            e.HasKey(x => x.ItemId);
            e.Property(x => x.Status).HasDefaultValue(ScatterItemStatus.Queued);
            e.Property(x => x.AttemptCount).HasDefaultValue(0);
            e.HasOne(x => x.Execution).WithMany(je => je.ScatterItems).HasForeignKey(x => x.ExecutionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ParentTask).WithMany().HasForeignKey(x => x.ParentTaskId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<DeadLetterItem>(e =>
        {
            e.ToTable("dead_letter_items");
            e.HasKey(x => x.DlqId);
            e.Property(x => x.FailureCount).HasDefaultValue(1);
            e.Property(x => x.Status).HasDefaultValue(DeadLetterStatus.Pending);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("sysdatetimeoffset()");
            e.HasOne(x => x.Execution).WithMany(je => je.DeadLetters).HasForeignKey(x => x.ExecutionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ScatterItem).WithMany(si => si.DeadLetters).HasForeignKey(x => x.ScatterItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ReplayedByUser).WithMany().HasForeignKey(x => x.ReplayedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Outbox, watermarks, chat, audit ----------
        b.Entity<OutboxMessage>(e =>
        {
            e.ToTable("outbox_messages");
            e.HasKey(x => x.OutboxId);
            e.Property(x => x.Status).HasDefaultValue(OutboxStatus.Pending);
            e.Property(x => x.RetryCount).HasDefaultValue(0);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("sysdatetimeoffset()");
            e.HasOne(x => x.Tenant).WithMany(t => t.OutboxMessages).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<SyncWatermark>(e =>
        {
            e.ToTable("sync_watermarks");
            e.HasKey(x => x.WatermarkId);
            e.HasIndex(x => new { x.JobId, x.EntityName }).IsUnique().HasDatabaseName("UQ_sync_watermarks_job_entity");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("sysdatetimeoffset()");
            e.HasOne(x => x.Job).WithMany(j => j.Watermarks).HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ChatSession>(e =>
        {
            e.ToTable("chat_sessions");
            e.HasKey(x => x.SessionId);
            e.Property(x => x.SessionId).HasDefaultValueSql("NEWSEQUENTIALID()");
            e.Property(x => x.LastMessageAt).HasDefaultValueSql("sysdatetimeoffset()");
            e.Property(x => x.MessageCount).HasDefaultValue(0);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("sysdatetimeoffset()");
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Tenant).WithMany(t => t.ChatSessions).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.User).WithMany(u => u.ChatSessions).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ChatMessage>(e =>
        {
            e.ToTable("chat_messages");
            e.HasKey(x => x.MessageId);
            e.HasIndex(x => new { x.SessionId, x.SequenceNumber }).IsUnique().HasDatabaseName("UQ_chat_messages_sequence");
            e.Property(x => x.Status).HasDefaultValue(ChatMessageStatus.Sent);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("sysdatetimeoffset()");
            e.HasOne(x => x.Session).WithMany(s => s.Messages).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_logs");
            e.HasKey(x => x.AuditId);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("sysdatetimeoffset()");
            e.HasOne(x => x.Tenant).WithMany(t => t.AuditLogs).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.PerformedByUser).WithMany().HasForeignKey(x => x.PerformedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    // ------------------------------------------------------------------
    // Global snake_case naming convention
    // ------------------------------------------------------------------
    private static void ApplySnakeCaseNaming(ModelBuilder b)
    {
        foreach (var entity in b.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.GetColumnName()));
            }

            foreach (var key in entity.GetKeys())
                key.SetName(ToSnakeCase(key.GetName()!));

            foreach (var fk in entity.GetForeignKeys())
                fk.SetConstraintName(ToSnakeCase(fk.GetConstraintName()!));

            foreach (var index in entity.GetIndexes())
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName()!));
        }
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var sb = new StringBuilder(input.Length + 8);
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (char.IsUpper(c))
            {
                if (i > 0 &&
                    (char.IsLower(input[i - 1]) || char.IsDigit(input[i - 1]) ||
                     (i + 1 < input.Length && char.IsLower(input[i + 1]))))
                {
                    sb.Append('_');
                }
                sb.Append(char.ToLowerInvariant(c));
            }
            else sb.Append(c);
        }
        return sb.ToString();
    }
}

// ------------------------------------------------------------------
// Custom enum converters for lowercase DB values
// ------------------------------------------------------------------
internal sealed class ChatRoleConverter : ValueConverter<ChatRole, string>
{
    public ChatRoleConverter() : base(
        v => v == ChatRole.System ? "system" : v == ChatRole.User ? "user" : "assistant",
        v => v == "system" ? ChatRole.System : v == "user" ? ChatRole.User : ChatRole.Assistant)
    { }
}

internal sealed class ChatMessageStatusConverter : ValueConverter<ChatMessageStatus, string>
{
    public ChatMessageStatusConverter() : base(
        v => v == ChatMessageStatus.Sending ? "sending" : v == ChatMessageStatus.Sent ? "sent" : "failed",
        v => v == "sending" ? ChatMessageStatus.Sending : v == "sent" ? ChatMessageStatus.Sent : ChatMessageStatus.Failed)
    { }
}