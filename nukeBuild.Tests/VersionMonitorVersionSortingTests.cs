using NukeBuild.Adapters;
using Xunit;

namespace NukeBuild.Tests;

public class VersionMonitorVersionSortingTests
{
    [Fact]
    public void Compare_ShouldTreatBeta36AsNewerThanBeta9()
    {
        var comparison = SemanticVersionOrdering.Compare("0.1.0-beta.36", "0.1.0-beta.9");

        Assert.True(comparison > 0);
    }

    [Fact]
    public void CreatePlan_ShouldSortUnorderedNumericVersionsAndFallbackToLatestAzureCandidate()
    {
        var azureVersions = new[] { "9", "36", "10", "7", "8" };
        var githubReleases = new[] { "v36", "v10", "v9", "v8", "v7" };

        var plan = ReleaseVersionMonitorPlanner.CreatePlan(azureVersions, githubReleases);

        Assert.Equal(new[] { "36", "10", "9", "8", "7" }, plan.SortedAzureVersions);
        Assert.Empty(plan.NewVersions);
        Assert.False(plan.HasNewVersions);
        Assert.Equal("36", plan.LatestVersion);
    }

    [Fact]
    public void CreatePlan_ShouldTreatPrefixedGitHubTagsAsPublishedVersions()
    {
        var azureVersions = new[] { "36", "10", "9" };
        var githubReleases = new[] { "v36", "v9" };

        var plan = ReleaseVersionMonitorPlanner.CreatePlan(azureVersions, githubReleases);

        Assert.Equal(new[] { "36", "10", "9" }, plan.SortedAzureVersions);
        Assert.Equal(new[] { "10" }, plan.NewVersions);
        Assert.True(plan.HasNewVersions);
        Assert.Equal("10", plan.LatestVersion);
    }

    [Fact]
    public void SortDescending_ShouldPreferStableReleaseOverSameBaselinePrerelease()
    {
        var versions = new[] { "1.2.3-beta.2", "1.2.3", "1.2.3-beta.10", "1.2.4-rc.1" };

        var sorted = SemanticVersionOrdering.SortDescending(versions);

        Assert.Equal(new[] { "1.2.4-rc.1", "1.2.3", "1.2.3-beta.10", "1.2.3-beta.2" }, sorted);
    }

    [Fact]
    public void GetLatestVersionForChannel_ShouldReuseSemanticSortingForChannelLookups()
    {
        var adapter = new AzureBlobAdapter();
        var index = new PackageIndex
        {
            Versions =
            [
                new PackageVersion { Version = "0.1.0-beta.9" },
                new PackageVersion { Version = "0.1.0-beta.36" },
                new PackageVersion { Version = "0.1.0-beta.10" }
            ]
        };

        var latest = adapter.GetLatestVersionForChannel(index, "beta");

        Assert.Equal("0.1.0-beta.36", latest);
    }
}
