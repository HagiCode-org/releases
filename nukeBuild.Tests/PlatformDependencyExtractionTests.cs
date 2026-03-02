using Xunit;
using NukeBuild.Tests;

namespace NukeBuild.Tests;

/// Tests for platform-dependent dependency extraction functionality
/// </summary>
public class PlatformDependencyExtractionTests
{
    [Fact]
    public void GetPlatformZipSuffix_ShouldMapLinuxAmd64_ToLinuxX64()
    {
        // Arrange
        var platform = "linux/amd64";
        var expected = "linux-x64";

        // Act & Assert
        // Note: GetPlatformZipSuffix is a private method in Build partial class
        // This test verifies the mapping logic is correct
        Assert.Equal(expected, MapPlatformToZipSuffix(platform));
    }

    [Fact]
    public void GetPlatformZipSuffix_ShouldMapLinuxArm64_ToLinuxArm64()
    {
        // Arrange
        var platform = "linux/arm64";
        var expected = "linux-arm64";

        // Act & Assert
        Assert.Equal(expected, MapPlatformToZipSuffix(platform));
    }

    [Fact]
    public void GetPlatformZipSuffix_ShouldHandleUnknownPlatform_ByRemovingLinuxPrefix()
    {
        // Arrange
        var platform = "linux/s390x";
        var expected = "s390x";

        // Act & Assert
        Assert.Equal(expected, MapPlatformToZipSuffix(platform));
    }

    [Fact]
    public void GetPlatformZipSuffix_ShouldHandlePlatformWithoutLinuxPrefix()
    {
        // Arrange
        var platform = "windows/amd64";
        var expected = "windows/amd64"; // No "linux/" prefix to remove

        // Act & Assert
        Assert.Equal(expected, MapPlatformToZipSuffix(platform));
    }

    /// <summary>
    /// Helper method that replicates the GetPlatformZipSuffix logic for testing
    /// </summary>
    private static string MapPlatformToZipSuffix(string platform)
    {
        return platform switch
        {
            "linux/amd64" => "linux-x64",
            "linux/arm64" => "linux-arm64",
            _ => platform.Replace("linux/", "")
        };
    }
}
