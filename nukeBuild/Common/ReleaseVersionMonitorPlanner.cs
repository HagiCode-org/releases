using System.Text.RegularExpressions;

public sealed record ReleaseVersionMonitorPlan(
    IReadOnlyList<string> SortedAzureVersions,
    IReadOnlyList<string> NewVersions,
    IReadOnlyList<string> IgnoredVersions,
    string LatestVersion,
    string SelectedVersion,
    IReadOnlyList<string> DeferredVersions)
{
    public bool HasNewVersions => NewVersions.Count > 0;
    public bool HasSelectedVersion => !string.IsNullOrWhiteSpace(SelectedVersion);
    public IReadOnlyList<string> AutoDispatchVersions =>
        HasSelectedVersion ? new[] { SelectedVersion } : Array.Empty<string>();
}

public static partial class ReleaseVersionMonitorPlanner
{
    public static ReleaseVersionMonitorPlan CreatePlan(
        IEnumerable<string> azureVersions,
        IEnumerable<string> githubReleases)
    {
        ArgumentNullException.ThrowIfNull(azureVersions);
        ArgumentNullException.ThrowIfNull(githubReleases);

        var invalidVersions = new List<string>();
        var validVersions = new List<string>();

        foreach (var version in azureVersions)
        {
            if (IsValidVersionFormat(version))
            {
                validVersions.Add(version);
            }
            else
            {
                invalidVersions.Add(version);
            }
        }

        var sortedAzureVersions = SemanticVersionOrdering.SortDescending(validVersions).ToList();
        var newVersions = sortedAzureVersions
            .Where(version => !HasPublishedRelease(version, githubReleases))
            .ToList();

        var latestVersion = newVersions.Count > 0
            ? newVersions[0]
            : sortedAzureVersions.FirstOrDefault() ?? string.Empty;
        var selectedVersion = newVersions.FirstOrDefault() ?? string.Empty;
        var deferredVersions = newVersions.Skip(1).ToList();

        return new ReleaseVersionMonitorPlan(
            sortedAzureVersions,
            newVersions,
            invalidVersions,
            latestVersion,
            selectedVersion,
            deferredVersions);
    }

    public static bool HasPublishedRelease(string version, IEnumerable<string> githubReleases)
    {
        ArgumentNullException.ThrowIfNull(githubReleases);

        var normalizedVersion = SemanticVersionOrdering.Normalize(version);
        return githubReleases.Any(tag =>
            string.Equals(
                SemanticVersionOrdering.Normalize(tag),
                normalizedVersion,
                StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsValidVersionFormat(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var trimmedVersion = version.Trim();
        if (!char.IsDigit(trimmedVersion[0]))
        {
            return false;
        }

        return ValidVersionPattern().IsMatch(trimmedVersion);
    }

    [GeneratedRegex("^[0-9A-Za-z._-]+$")]
    private static partial Regex ValidVersionPattern();
}
