namespace TSSE.Analytics.Models;

public enum InGameAnalyticsStatus
{
    Available,
    NoSnapshots,
    NoInGameTime,
    NoSnapshotsInWindow,
    InsufficientSnapshots,
    InsufficientCoverage,
    MissingInGameTime,
    ClockResetDetected,
    SaveVersionBoundary,
    AmbiguousGameTime,
    InvalidSnapshotData
}
