namespace TSSE.Analytics.Models;

public sealed record RecordedTransaction(
    DateTimeOffset OccurredAt,
    decimal SignedAmount,
    string SourceReference);
