namespace TSSE.Core.IO;

public enum FileCommitStatus
{
    Succeeded,
    ValidationRejected,
    Conflict,
    FailedUnchanged,
    RecoveryRequired
}

public sealed record FileCommitResult(
    FileCommitStatus Status,
    string? BackupPath = null,
    string? RecoveryArtifactPath = null,
    string? ErrorCode = null)
{
    public bool Succeeded => Status == FileCommitStatus.Succeeded;
}
