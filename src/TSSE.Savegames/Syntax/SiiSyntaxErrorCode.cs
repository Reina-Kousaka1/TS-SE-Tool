namespace TSSE.Savegames.Syntax;

public enum SiiSyntaxErrorCode
{
    InvalidSyntax,
    DocumentLimitExceeded,
    LineLimitExceeded,
    LineLengthExceeded,
    NestingLimitExceeded,
    MissingBlockHeader,
    UnexpectedClosingBrace,
    BlockLimitExceeded,
    UnterminatedQuotedValue,
    FieldLimitExceeded,
    UnclosedBlock,
    NoBlocks,
    InvalidBlockHeader
}
