using Nuke.Common;
using Serilog;

partial class Build
{
    Target DockerPrepareLocalContext => _ => _
        .Description("Prepare a Docker build context for local compose-based image builds using already-downloaded packages")
        .DependsOn(DetermineBuildConfig)
        .Executes(() =>
        {
            var version = EffectiveBuildVersion;
            var platforms = ResolveDockerPlatforms(DockerPlatform);
            var versionedZipFiles = GetDownloadedZipFilesForVersion(version);

            if (!versionedZipFiles.Any())
            {
                throw new Exception(
                    $"No downloaded zip packages for version '{version}' were found in '{DownloadDirectory}'. " +
                    $"Run ./build.sh Download --ReleaseVersion \"{version}\" first, or use the local docker build script with Azure Blob access configured.");
            }

            Log.Information(
                "Preparing local Docker build context for version {Version} on platform set: {Platforms}",
                version,
                string.Join(", ", platforms));

            PrepareBuildContext(version, isMultiPlatform: platforms.Count > 1, platforms: platforms);
        });

    List<string> ResolveDockerPlatforms(string dockerPlatform)
    {
        var rawValue = string.IsNullOrWhiteSpace(dockerPlatform) ? "all" : dockerPlatform.Trim();
        var normalizedTokens = rawValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeDockerPlatformToken)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedTokens.Count == 0 || normalizedTokens.Contains("all", StringComparer.OrdinalIgnoreCase))
        {
            return new List<string> { "linux/amd64", "linux/arm64" };
        }

        return normalizedTokens;
    }

    string NormalizeDockerPlatformToken(string rawToken)
    {
        var normalized = rawToken.Trim().ToLowerInvariant();

        return normalized switch
        {
            "all" => "all",
            "amd64" => "linux/amd64",
            "x64" => "linux/amd64",
            "linux-amd64" => "linux/amd64",
            "linux-x64" => "linux/amd64",
            "linux/amd64" => "linux/amd64",
            "arm64" => "linux/arm64",
            "aarch64" => "linux/arm64",
            "linux-arm64" => "linux/arm64",
            "linux/arm64" => "linux/arm64",
            _ => throw new Exception(
                $"Unsupported Docker platform '{rawToken}'. Use one of: all, linux/amd64, linux/arm64, linux-amd64, linux-arm64, linux-x64, x64, amd64, arm64.")
        };
    }
}
