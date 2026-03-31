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
        Assert.Empty(plan.NewVersions);
        Assert.False(plan.HasNewVersions);
        Assert.Equal("36", plan.LatestVersion);
    }

    [Fact]
    public void CreatePlan_ShouldSelectOnlyLatestAzureVersionForAutomaticDispatch()
    {
        var azureVersions = new[] { "1.1.0", "1.3.0", "1.2.0" };
        var githubReleases = new[] { "v1.0.0" };

        var plan = ReleaseVersionMonitorPlanner.CreatePlan(azureVersions, githubReleases);

        Assert.Equal(new[] { "1.3.0", "1.2.0", "1.1.0" }, plan.SortedAzureVersions);
        Assert.Equal(new[] { "1.3.0" }, plan.NewVersions);
        Assert.Equal("1.3.0", plan.SelectedVersion);
        Assert.Empty(plan.DeferredVersions);
        Assert.Equal(new[] { "1.3.0" }, plan.AutoDispatchVersions);
    }

    [Fact]
    public void CreatePlan_ShouldIgnoreHistoricalGapsWhenLatestAzureVersionAlreadyPublished()
    {
        var azureVersions = new[] { "0.1.0-beta.37", "0.1.0-beta.18", "0.1.0-beta.17", "0.1.0-beta.16" };
        var githubReleases = new[] { "0.1.0-beta.37" };

        var plan = ReleaseVersionMonitorPlanner.CreatePlan(azureVersions, githubReleases);

        Assert.Equal("0.1.0-beta.37", plan.LatestVersion);
        Assert.Empty(plan.NewVersions);
        Assert.False(plan.HasNewVersions);
        Assert.Equal(string.Empty, plan.SelectedVersion);
        Assert.Empty(plan.DeferredVersions);
    }

    [Fact]
    public void CreatePlan_ShouldKeepAutomaticDispatchEmptyWhenNoUnpublishedVersionsExist()
    {
        var azureVersions = new[] { "2.0.0", "1.9.0" };
        var githubReleases = new[] { "v2.0.0", "v1.9.0" };

        var plan = ReleaseVersionMonitorPlanner.CreatePlan(azureVersions, githubReleases);

        Assert.False(plan.HasSelectedVersion);
        Assert.Equal(string.Empty, plan.SelectedVersion);
        Assert.Empty(plan.DeferredVersions);
        Assert.Empty(plan.AutoDispatchVersions);
        Assert.Equal("2.0.0", plan.LatestVersion);
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
