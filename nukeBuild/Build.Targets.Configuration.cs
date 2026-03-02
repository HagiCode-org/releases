using Nuke.Common;
using Nuke.Common.IO;
using Serilog;
using YamlDotNet.RepresentationModel;

/// Build configuration management target
///
/// This partial class provides configuration loading from YAML files,
/// environment variable overrides, and parameter-based overrides.
/// Supports configuration validation with required parameter checks and type validation.

partial class Build
{
    // ==========================================================================
    // Configuration State Properties
    // ==========================================================================

    /// Gets the build configuration file path
    
    AbsolutePath BuildConfigFile => RootDirectory.Parent / "nukeBuild" / "build-config.yaml";

    /// Gets the loaded build configuration
    
    BuildConfiguration? _buildConfiguration;

    /// Gets the loaded build configuration (lazy loaded)
    
    BuildConfiguration BuildConfig => _buildConfiguration ??= LoadBuildConfiguration();

    // ==========================================================================
    // Configuration Targets
    // ==========================================================================

    /// Configuration validate target - validates build configuration
    
    Target ConfigurationValidate => _ => _
        .Description("Validates build configuration from build-config.yaml")
        .Executes(ValidateConfiguration);

    // ==========================================================================
    // Configuration Implementation
    // ==========================================================================

    /// Loads the build configuration from YAML file
    
    BuildConfiguration LoadBuildConfiguration()
    {
        Log.Information("Loading build configuration from: {ConfigFile}", BuildConfigFile);

        var config = new BuildConfiguration();

        if (!BuildConfigFile.Exists())
        {
            Log.Warning("Build configuration file not found, using default values");
            return config;
        }

        try
        {
            var yaml = new YamlStream();
            var reader = BuildConfigFile.ReadAllText();
            yaml.Load(new StringReader(reader));

            if (yaml.Documents.Count > 0)
            {
                var root = yaml.Documents[0].RootNode as YamlMappingNode;
                if (root != null)
                {
                    LoadDockerConfiguration(root, config);
                    LoadAzureAcrConfiguration(root, config);
                    LoadAliyunAcrConfiguration(root, config);
                    LoadDockerHubConfiguration(root, config);
                }
            }

            Log.Information("Build configuration loaded successfully");
            return config;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load build configuration, using default values");
            return config;
        }
    }

    /// Loads Docker-specific configuration from the YAML node
    
    void LoadDockerConfiguration(YamlMappingNode root, BuildConfiguration config)
    {
        var dockerNode = root.Children.FirstOrDefault(x => x.Key.ToString() == "docker").Value as YamlMappingNode;
        if (dockerNode == null) return;

        config.DockerImageName = GetYamlStringValue(dockerNode, "image_name", config.DockerImageName);
        config.DockerPlatform = GetYamlStringValue(dockerNode, "platform", config.DockerPlatform);
        config.DockerBuildTimeout = GetYamlIntValue(dockerNode, "build_timeout", config.DockerBuildTimeout);
        config.DockerForceRebuild = GetYamlBoolValue(dockerNode, "force_rebuild", config.DockerForceRebuild);
        config.DockerIndependentBuild = GetYamlBoolValue(dockerNode, "independent_build", config.DockerIndependentBuild);
    }

    /// Loads Azure ACR-specific configuration from the YAML node
    
    void LoadAzureAcrConfiguration(YamlMappingNode root, BuildConfiguration config)
    {
        var acrNode = root.Children.FirstOrDefault(x => x.Key.ToString() == "azure_acr").Value as YamlMappingNode;
        if (acrNode == null) return;

        config.AzureAcrRegistry = GetYamlStringValue(acrNode, "registry", config.AzureAcrRegistry);
        config.AzureAcrNamespace = GetYamlStringValue(acrNode, "namespace", config.AzureAcrNamespace);
        config.AzureAcrUsername = GetYamlStringValue(acrNode, "username", config.AzureAcrUsername);
        config.AzureAcrPassword = GetYamlStringValue(acrNode, "password", config.AzureAcrPassword);
    }

    /// Loads Aliyun ACR-specific configuration from the YAML node
    
    void LoadAliyunAcrConfiguration(YamlMappingNode root, BuildConfiguration config)
    {
        var acrNode = root.Children.FirstOrDefault(x => x.Key.ToString() == "aliyun_acr").Value as YamlMappingNode;
        if (acrNode == null) return;

        config.AliyunAcrRegistry = GetYamlStringValue(acrNode, "registry", config.AliyunAcrRegistry);
        config.AliyunAcrNamespace = GetYamlStringValue(acrNode, "namespace", config.AliyunAcrNamespace);
        config.AliyunAcrUsername = GetYamlStringValue(acrNode, "username", config.AliyunAcrUsername);
        config.AliyunAcrPassword = GetYamlStringValue(acrNode, "password", config.AliyunAcrPassword);
    }

    /// Loads DockerHub-specific configuration from the YAML node
    
    void LoadDockerHubConfiguration(YamlMappingNode root, BuildConfiguration config)
    {
        var dockerHubNode = root.Children.FirstOrDefault(x => x.Key.ToString() == "dockerhub").Value as YamlMappingNode;
        if (dockerHubNode == null) return;

        config.DockerHubUsername = GetYamlStringValue(dockerHubNode, "username", config.DockerHubUsername);
        config.DockerHubToken = GetYamlStringValue(dockerHubNode, "token", config.DockerHubToken);
        config.DockerHubNamespace = GetYamlStringValue(dockerHubNode, "namespace", config.DockerHubNamespace);
    }

    /// Gets a string value from a YAML node
    
    string GetYamlStringValue(YamlMappingNode node, string key, string defaultValue)
    {
        if (node.Children.TryGetValue(key, out var value))
        {
            return value.ToString() ?? defaultValue;
        }
        return defaultValue;
    }

    /// Gets an integer value from a YAML node
    
    int GetYamlIntValue(YamlMappingNode node, string key, int defaultValue)
    {
        if (node.Children.TryGetValue(key, out var value))
        {
            if (int.TryParse(value.ToString(), out int result))
            {
                return result;
            }
        }
        return defaultValue;
    }

    /// Gets a boolean value from a YAML node
    
    bool GetYamlBoolValue(YamlMappingNode node, string key, bool defaultValue)
    {
        if (node.Children.TryGetValue(key, out var value))
        {
            if (bool.TryParse(value.ToString(), out bool result))
            {
                return result;
            }
        }
        return defaultValue;
    }

    /// Validates the build configuration
    
    void ValidateConfiguration()
    {
        Log.Information("Validating build configuration...");

        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate Docker configuration
        if (string.IsNullOrWhiteSpace(BuildConfig.DockerImageName))
        {
            errors.Add("Docker image name is required");
        }

        // Validate Docker platform
        var validPlatforms = new[] { "all", "linux-amd64", "linux-arm64", "amd64", "arm64" };
        if (!validPlatforms.Contains(BuildConfig.DockerPlatform.ToLowerInvariant()))
        {
            warnings.Add($"Invalid Docker platform: {BuildConfig.DockerPlatform}. Valid values: {string.Join(", ", validPlatforms)}");
        }

        // Validate Azure ACR configuration
        if (!string.IsNullOrWhiteSpace(BuildConfig.AzureAcrRegistry))
        {
            if (string.IsNullOrWhiteSpace(BuildConfig.AzureAcrUsername))
            {
                errors.Add("Azure ACR username is required when registry is configured");
            }

            if (string.IsNullOrWhiteSpace(BuildConfig.AzureAcrPassword))
            {
                errors.Add("Azure ACR password is required when registry is configured");
            }
        }

        // Log warnings
        foreach (var warning in warnings)
        {
            Log.Warning("Configuration warning: {Warning}", warning);
        }

        // Fail on errors
        if (errors.Count > 0)
        {
            Log.Error("Configuration validation failed with {Count} error(s):", errors.Count);
            foreach (var error in errors)
            {
                Log.Error("  - {Error}", error);
            }
            throw new Exception("Build configuration validation failed");
        }

        Log.Information("Build configuration is valid");
        LogConfigurationSummary();
    }

    /// Logs a summary of the current configuration
    
    void LogConfigurationSummary()
    {
        Log.Information("Configuration summary:");
        Log.Information("  Docker Image Name: {ImageName}", BuildConfig.DockerImageName);
        Log.Information("  Docker Platform: {Platform}", BuildConfig.DockerPlatform);
        Log.Information("  Docker Build Timeout: {Timeout}s", BuildConfig.DockerBuildTimeout);
        Log.Information("  Docker Force Rebuild: {ForceRebuild}", BuildConfig.DockerForceRebuild);
        Log.Information("  Docker Independent Build: {IndependentBuild}", BuildConfig.DockerIndependentBuild);

        if (!string.IsNullOrEmpty(BuildConfig.AzureAcrRegistry))
        {
            Log.Information("  Azure ACR Registry: {Registry}", BuildConfig.AzureAcrRegistry);
            Log.Information("  Azure ACR Username: {Username}",
                string.IsNullOrEmpty(BuildConfig.AzureAcrUsername) ? "(not set)" : MaskSensitiveValue(BuildConfig.AzureAcrUsername));
        }
        else
        {
            Log.Information("  Azure ACR: (not configured)");
        }
    }

    /// Masks sensitive values for logging
    
    string MaskSensitiveValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "(empty)";
        }

        if (value.Length <= 4)
        {
            return "****";
        }

        return value[..2] + "****" + value[^2..];
    }

    /// Gets the effective configuration value with environment variable and parameter override
    
    /// <param name="configValue">Configuration file value</param>
    /// <param name="envVarName">Environment variable name</param>
    /// <param name="paramValue">Parameter value</param>
    /// <returns>Effective value (parameter > env var > config file)</returns>
    string GetEffectiveValue(string configValue, string envVarName, string paramValue)
    {
        // Parameter value has highest priority
        if (!string.IsNullOrEmpty(paramValue))
        {
            return paramValue;
        }

        // Environment variable has medium priority
        var envValue = Environment.GetEnvironmentVariable(envVarName);
        if (!string.IsNullOrEmpty(envValue))
        {
            return envValue;
        }

        // Configuration file value has lowest priority
        return configValue;
    }

    /// Gets the effective configuration value with environment variable and parameter override (boolean)
    
    bool GetEffectiveValue(bool configValue, string envVarName, bool paramValue)
    {
        // Parameter value has highest priority
        // (In Nuke, boolean parameters can't be easily distinguished from defaults,
        // so we prioritize environment variable first)

        // Environment variable has medium priority
        var envValue = Environment.GetEnvironmentVariable(envVarName);
        if (!string.IsNullOrEmpty(envValue) && bool.TryParse(envValue, out bool envBool))
        {
            return envBool;
        }

        // Parameter value has lower priority
        return paramValue;
    }

    /// Gets the effective configuration value with environment variable and parameter override (integer)
    
    int GetEffectiveValue(int configValue, string envVarName, int paramValue)
    {
        // Parameter value has highest priority
        if (paramValue != 0)
        {
            return paramValue;
        }

        // Environment variable has medium priority
        var envValue = Environment.GetEnvironmentVariable(envVarName);
        if (!string.IsNullOrEmpty(envValue) && int.TryParse(envValue, out int envInt))
        {
            return envInt;
        }

        // Configuration file value has lowest priority
        return configValue;
    }
}

/// Build configuration model

internal class BuildConfiguration
{
    public string DockerImageName { get; set; } = "hagicode";
    public string DockerPlatform { get; set; } = "all";
    public int DockerBuildTimeout { get; set; } = 3600;
    public bool DockerForceRebuild { get; set; } = false;
    public bool DockerIndependentBuild { get; set; } = false;
    public string AzureAcrRegistry { get; set; } = string.Empty;
    public string AzureAcrNamespace { get; set; } = string.Empty;
    public string AzureAcrUsername { get; set; } = string.Empty;
    public string AzureAcrPassword { get; set; } = string.Empty;
    public string AliyunAcrRegistry { get; set; } = "registry.cn-hangzhou.aliyuncs.com";
    public string AliyunAcrNamespace { get; set; } = "hagicode";
    public string AliyunAcrUsername { get; set; } = string.Empty;
    public string AliyunAcrPassword { get; set; } = string.Empty;
    public string DockerHubUsername { get; set; } = string.Empty;
    public string DockerHubToken { get; set; } = string.Empty;
    public string DockerHubNamespace { get; set; } = string.Empty;
}