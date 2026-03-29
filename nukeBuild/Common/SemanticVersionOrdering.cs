using System.Globalization;

public static class SemanticVersionOrdering
{
    public static string Normalize(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return string.Empty;
        }

        var normalized = version.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        return normalized;
    }

    public static int Compare(string? left, string? right)
    {
        var normalizedLeft = Normalize(left);
        var normalizedRight = Normalize(right);

        if (normalizedLeft.Length == 0)
        {
            return normalizedRight.Length == 0 ? 0 : -1;
        }

        if (normalizedRight.Length == 0)
        {
            return 1;
        }

        var (leftMainVersion, leftPrerelease) = SplitVersion(normalizedLeft);
        var (rightMainVersion, rightPrerelease) = SplitVersion(normalizedRight);

        var mainComparison = CompareMainVersion(leftMainVersion, rightMainVersion);
        if (mainComparison != 0)
        {
            return mainComparison;
        }

        var leftIsStable = string.IsNullOrEmpty(leftPrerelease);
        var rightIsStable = string.IsNullOrEmpty(rightPrerelease);
        if (leftIsStable && !rightIsStable)
        {
            return 1;
        }

        if (!leftIsStable && rightIsStable)
        {
            return -1;
        }

        if (leftIsStable && rightIsStable)
        {
            return 0;
        }

        return ComparePrerelease(leftPrerelease, rightPrerelease);
    }

    public static IReadOnlyList<string> SortDescending(IEnumerable<string> versions)
    {
        ArgumentNullException.ThrowIfNull(versions);

        return versions
            .OrderByDescending(static version => version, Comparer<string>.Create(Compare))
            .ToList();
    }

    static (string MainVersion, string Prerelease) SplitVersion(string version)
    {
        var dashIndex = version.IndexOf('-');
        return dashIndex > 0
            ? (version[..dashIndex], version[(dashIndex + 1)..])
            : (version, string.Empty);
    }

    static int CompareMainVersion(string left, string right)
    {
        var leftParts = left.Split('.');
        var rightParts = right.Split('.');
        var maxLength = Math.Max(leftParts.Length, rightParts.Length);

        for (var index = 0; index < maxLength; index++)
        {
            var leftPart = index < leftParts.Length ? leftParts[index] : "0";
            var rightPart = index < rightParts.Length ? rightParts[index] : "0";

            var partComparison = CompareIdentifiers(leftPart, rightPart, numericIsLowerPrecedence: false);
            if (partComparison != 0)
            {
                return partComparison;
            }
        }

        return 0;
    }

    static int ComparePrerelease(string left, string right)
    {
        var leftIdentifiers = left.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var rightIdentifiers = right.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var maxLength = Math.Max(leftIdentifiers.Length, rightIdentifiers.Length);

        for (var index = 0; index < maxLength; index++)
        {
            if (index >= leftIdentifiers.Length)
            {
                return -1;
            }

            if (index >= rightIdentifiers.Length)
            {
                return 1;
            }

            var identifierComparison = CompareIdentifiers(
                leftIdentifiers[index],
                rightIdentifiers[index],
                numericIsLowerPrecedence: true);
            if (identifierComparison != 0)
            {
                return identifierComparison;
            }
        }

        return 0;
    }

    static int CompareIdentifiers(string left, string right, bool numericIsLowerPrecedence)
    {
        var leftIsNumeric = long.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
        var rightIsNumeric = long.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);

        if (leftIsNumeric && rightIsNumeric)
        {
            return leftNumber.CompareTo(rightNumber);
        }

        if (leftIsNumeric != rightIsNumeric)
        {
            if (!numericIsLowerPrecedence)
            {
                return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
            }

            return leftIsNumeric ? -1 : 1;
        }

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
