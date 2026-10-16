namespace EI_Console.Domain.Enums;

public enum AuthType
{
    ApiKey,
    FtpCredentials,
    KeycloakAuthCodeExchange,
    MutualTls,
    OAuth2ClientCredentials,
    SftpKeyCredentials,
    SqlServerConnectionString,
    WsSecurityUsernameToken,

    SubscriptionTokenExchange
}

public enum CredentialStorageMode { KeyVault, StoredCredential, Inline }

public enum Protocol { Rest, Soap, Json, SqlServer, Sftp, Ftp }

public enum CircuitBreakerStatus { Closed, Open, HalfOpen }

public enum JobStatus { Draft, Active, Running, Paused, Archived }

public enum TriggerType { Manual, Scheduled, Webhook }

public enum ExecutionStatus { Running, Completed, Failed, Cancelled }

public enum TriggerSource { Manual, Scheduled, Webhook, Replay }

public enum TaskRole { Root, ForkBranch, ForkChild, IteratorSubtask }

public enum TaskType { ApiPull, SqlQuery, Transform, ApiPush, Notify, Branch, JoinPoint, FtpDownload }

public enum TaskExecutionStatus { Pending, Running, Success, Failed, Skipped }

public enum BranchExecutionMode { Sequential, Parallel }

public enum DeadLetterStatus { Pending, Replayed, Discarded, Investigating }

public enum ScatterItemStatus { Queued, Dispatched, Completed, Failed, Cancelled }

public enum OutboxStatus { Pending, Processing, Processed, Failed }

public enum ChatRole { System, User, Assistant }

public enum ChatMessageStatus { Sending, Sent, Failed }

public enum WatermarkType { Timestamp, SequenceToken, PageCursor }

public enum RateLimitScope { Global, PerTenant, PerConnection }

public enum StoredCredentialKind { FtpPassword, SftpPrivateKey }

public enum EncryptionState { Plaintext, Encrypted }

public enum TenantUserRole { Owner, Operator, Viewer }

public enum ExecutionTriggerSource
{
    Scheduled = 1,
    Webhook = 2,
    Manual = 3,
    Replay = 4,
}
