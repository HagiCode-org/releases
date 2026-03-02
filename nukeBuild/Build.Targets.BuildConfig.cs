using Nuke.Common.Git;
using Serilog;
using System.Text.RegularExpressions;

/// Build configuration determination target
///
/// This partial class provides version and platform determination from Git context.
/// Used by GitHub Actions workflows to determine what to build.
partial class Build
{
    // ==========================================================================
    // Build Configuration State Properties
    // ==========================================================================

    /// Gets the Git repository
    [GitRepository] readonly GitRepository? GitRepository;

    /// Gets the effective build version determined from Git tags or environment
    string? _determinedVersion;

    string EffectiveBuildVersion => _determinedVersion ?? EffectiveReleaseVersion;

    /// Gets whether this is a stable release (not pre-release)

    bool IsStableRelease => !IsPreReleaseVersion(EffectiveBuildVersion);

    // ==========================================================================
    // Build Configuration Implementation
    // ==========================================================================

    /// Determines the build version and platform from Git context or environment variables
    void DetermineBuildConfigExecute()
    {
        Log.Information("Determining build configuration...");

        // Determine version
        var version = GetVersionFromGit();
        Log.Information("Effective version: {Version}", version);
        Log.Information("Is stable release: {IsStable}", IsStableRelease);
    }

    /// Gets the version from Git tags or environment
    string GetVersionFromGit()
    {
        // Priority 1: Environment variable (set by GitHub Actions)
        var envVersion = Environment.GetEnvironmentVariable("NUGEX_ReleaseVersion");
        if (!string.IsNullOrEmpty(envVersion))
        {
            Log.Debug("Using version from environment: {Version}", envVersion);
            _determinedVersion = envVersion;
            return envVersion;
        }

        // Priority 2: Git tag
        if (GitRepository?.Tags != null && GitRepository.Tags.Any())
        {
            // Get the latest tag that matches version pattern
            var versionTag = GitRepository.Tags
                .Where(t => Regex.IsMatch(t, @"^v?\d+\.\d+\.\d+"))
                .OrderByDescending(t => t)
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(versionTag))
            {
                Log.Debug("Using Git tag: {Tag}", versionTag);
                // Remove 'v' prefix if present
                var version = versionTag.StartsWith("v") ? versionTag[1..] : versionTag;
                _determinedVersion = version;
                return version;
            }
        }

        // Priority 3: Parameter
        if (!string.IsNullOrEmpty(ReleaseVersion))
        {
            Log.Debug("Using parameter version: {Version}", ReleaseVersion);
            _determinedVersion = ReleaseVersion;
            return ReleaseVersion;
        }

        // Default
        _determinedVersion = "latest";
        Log.Debug("Using default version: latest");
        return "latest";
    }

    /// Checks if the version string indicates a pre-release
    bool IsPreReleaseVersion(string version)
    {
        if (string.IsNullOrEmpty(version) || version == "latest")
        {
            return true;
        }

        // Check for pre-release suffixes: rc, beta, alpha, preview, dev
        var preReleasePattern =
            @"^(rc|beta|alpha|preview|dev)|-(rc|beta|alpha|preview|dev)|\.(rc|beta|alpha|preview|dev)";
        return Regex.IsMatch(version.ToLower(), preReleasePattern);
    }

    /// Gets the major version number
    int GetMajorVersion(string version)
    {
        if (string.IsNullOrEmpty(version) || version == "latest")
        {
            return 0;
        }

        // Remove 'v' prefix if present
        var cleanVersion = version.StartsWith("v") ? version[1..] : version;
        var parts = cleanVersion.Split('.');
        return parts.Length > 0 && int.TryParse(parts[0], out int major) ? major : 0;
    }

    /// Gets the minor version number
    int GetMinorVersion(string version)
    {
        if (string.IsNullOrEmpty(version) || version == "latest")
        {
            return 0;
        }

        // Remove 'v' prefix if present
        var cleanVersion = version.StartsWith("v") ? version[1..] : version;
        var parts = cleanVersion.Split('.');
        return parts.Length > 1 && int.TryParse(parts[1], out int minor) ? minor : 0;
    }

    /// Gets the patch version number
    int GetPatchVersion(string version)
    {
        if (string.IsNullOrEmpty(version) || version == "latest")
        {
            return 0;
        }

        // Remove 'v' prefix if present
        var cleanVersion = version.StartsWith("v") ? version[1..] : version;
        var parts = cleanVersion.Split('.');
        return parts.Length > 2 && int.TryParse(parts[2], out int patch) ? patch : 0;
    }

    /// Gets the clean version without 'v' prefix
    string GetCleanVersion(string version)
    {
        if (string.IsNullOrEmpty(version))
        {
            return "latest";
        }

        return version.StartsWith("v") ? version[1..] : version;
    }

    /// Gets the version tag for Docker images
    string GetVersionTag(string version)
    {
        return GetCleanVersion(version);
    }

    /// Gets the major.minor tag for Docker images
    string GetMajorMinorTag(string version)
    {
        var major = GetMajorVersion(version);
        var minor = GetMinorVersion(version);
        return $"{major}.{minor}";
    }

    /// Gets the major tag for Docker images
    string GetMajorTag(string version)
    {
        return GetMajorVersion(version).ToString();
    }

    string GetPatchVersionTag(string version)
    {
        var major = GetMajorVersion(version);
        var minor = GetMinorVersion(version);
        var patch = GetPatchVersion(version);
        return $"{major}.{minor}.{patch}";
    }

    /// Gets all version tags for Docker images
    string[] GetVersionTags(string version)
    {
        if (version == "latest" || string.IsNullOrEmpty(version))
        {
            return new[] { "latest" };
        }

        var tags = new List<string>
        {
            GetVersionTag(version),
            GetPatchVersionTag(version),
            GetMajorMinorTag(version),
            GetMajorTag(version)
        };

        // Add latest tag only for stable releases
        if (IsStableRelease)
        {
            tags.Add("latest");
        }

        return tags.ToArray();
    }
}