using TSSE.Savegames.Syntax;

namespace TSSE.GameAdapters.Editing;

/// <summary>Read-only inspection of a synthetic legacy-shaped player trailer array.</summary>
public static class LegacyPlayerTrailerInspector
{
    public static IReadOnlyList<PlayerTrailerReference> Inspect(SiiSyntaxDocument document, string playerId)
    {
        IReadOnlyList<string> references = LegacyReferenceArray.Read(document, "player", playerId, "trailers");
        var duplicates = references.Where(item => item is not ("nil" or "null"))
            .GroupBy(item => item, StringComparer.Ordinal).Where(group => group.Count() > 1)
            .Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        var targets = document.Blocks.Where(block => block.Id is not null)
            .GroupBy(block => block.Id!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var results = new List<PlayerTrailerReference>(references.Count);
        for (int index = 0; index < references.Count; index++)
        {
            string reference = references[index];
            TrailerReferenceState state;
            if (reference is "nil" or "null") state = TrailerReferenceState.Empty;
            else if (!targets.TryGetValue(reference, out SiiBlock[]? matches)) state = TrailerReferenceState.Missing;
            else if (matches.Length != 1) state = TrailerReferenceState.Ambiguous;
            else state = matches[0].Type == "trailer" ? TrailerReferenceState.TrailerBlock : TrailerReferenceState.OtherBlock;
            results.Add(new(index, reference, state, duplicates.Contains(reference)));
        }
        return results.AsReadOnly();
    }
}

public enum TrailerReferenceState { Empty, Missing, Ambiguous, TrailerBlock, OtherBlock }
public sealed record PlayerTrailerReference(int Index, string RawReference, TrailerReferenceState State, bool IsDuplicate);
