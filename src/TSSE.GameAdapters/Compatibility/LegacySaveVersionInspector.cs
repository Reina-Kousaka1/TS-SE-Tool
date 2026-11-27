using System.Globalization;
using TSSE.Savegames.Syntax;

namespace TSSE.GameAdapters.Compatibility;

/// <summary>Reads a legacy-shaped synthetic version field; does not establish game compatibility.</summary>
public static class LegacySaveVersionInspector
{
    public static LegacySaveVersionResult Inspect(SiiSyntaxDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.OpaqueLines.Count != 0) return new(LegacySaveVersionStatus.UnclassifiedSyntax, null);
        SiiBlock[] roots = document.Blocks.Where(block => block.NestingDepth == 0).ToArray();
        if (roots.Length != 1 || roots[0].Type != "SiiNunit" || roots[0].Id is not null)
            return new(LegacySaveVersionStatus.InvalidWrapper, null);
        SiiBlock[] containers = document.Blocks.Where(block => block.Type == "save_container").ToArray();
        if (containers.Length == 0) return new(LegacySaveVersionStatus.MissingContainer, null);
        if (containers.Length != 1) return new(LegacySaveVersionStatus.AmbiguousContainer, null);
        SiiBlock container = containers[0];
        if (container.NestingDepth != 1 || container.ParentStartLine != roots[0].StartLine ||
            container.ParentStartColumn != roots[0].StartColumn || string.IsNullOrWhiteSpace(container.Id))
            return new(LegacySaveVersionStatus.InvalidContainer, null);
        SiiField[] versions = container.Fields.Where(item => item.Name == "version").ToArray();
        if (versions.Length == 0) return new(LegacySaveVersionStatus.MissingVersion, null);
        if (versions.Length != 1) return new(LegacySaveVersionStatus.AmbiguousVersion, null);
        if (!ushort.TryParse(versions[0].Value, NumberStyles.None, CultureInfo.InvariantCulture, out ushort version) || version == 0)
            return new(LegacySaveVersionStatus.InvalidVersion, null);
        return new(LegacySaveVersionStatus.LegacyFieldFound, version);
    }
}

public enum LegacySaveVersionStatus
{
    LegacyFieldFound, UnclassifiedSyntax, InvalidWrapper, MissingContainer, AmbiguousContainer,
    InvalidContainer, MissingVersion, AmbiguousVersion, InvalidVersion
}

/// <summary>Value is a syntactic legacy-field hint, not a verified game or format version.</summary>
public sealed record LegacySaveVersionResult(LegacySaveVersionStatus Status, ushort? Version);
