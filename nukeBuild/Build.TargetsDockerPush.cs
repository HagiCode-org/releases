using Serilog;
using System.Diagnostics;

/// Docker image push target
///
/// This partial class provides Docker image push functionality to multiple registries.
/// Uses adapter pattern for flexible registry support (Azure ACR, Aliyun ACR, DockerHub).
/// Includes login, push, verification, and retry logic with exponential backoff.
partial class Build
{
    void LoginToRegistry(IRegistryAdapter adapter)
    {
        var (username, password) = adapter.GetCredentials();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            Log.Warning("{RegistryType} credentials not configured, skipping login", adapter.Type);
            return;
        }

        var registry = GetRegistryForAdapter(adapter);

        Log.Information("Logging in to {RegistryType}: {Registry}", adapter.Type, registry);
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "docker",
                ArgumentList =
                {
                    "login",
                    "--username", username,
                    "--password-stdin",
                    registry
                },
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                Log.Warning("Failed to start docker login process for {Registry}", registry);
                return;
            }

            process.StandardInput.Write(password);
            process.StandardInput.Close();

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Log.Warning("Failed to login to {Registry}: {Error}", registry, error);
                return;
            }

            Log.Information("Successfully logged in to {Registry}", registry);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to login to {Registry}", registry);
        }
    }

    string GetRegistryForAdapter(IRegistryAdapter adapter) => adapter.Type switch
    {
        RegistryType.AzureAcr => EffectiveAzureAcrRegistry,
        RegistryType.AliyunAcr => EffectiveAliyunAcrRegistry,
        RegistryType.DockerHub => "docker.io",
        _ => throw new ArgumentOutOfRangeException(nameof(adapter.Type))
    };


    enum RegistryType
    {
        AzureAcr,
        AliyunAcr,
        DockerHub
    }

    /// Interface for container registry adapters
    /// Defines the contract for building image paths and retrieving credentials
    /// for different container registry providers
    interface IRegistryAdapter
    {
        /// <summary>
        /// Gets the registry type this adapter handles

        RegistryType Type { get; }

        /// <summary>
        /// Gets the full image path for a given image name and tag
        /// <param name="imageName">The base image name</param>
        /// <param name="tag">The image tag</param>
        /// <returns>The full image path including registry and tag</returns>
        string GetImagePath(string imageName, string tag);

        /// <summary>
        /// Gets the authentication credentials for the registry
        /// <returns>A tuple containing username and password</returns>
        (string username, string password) GetCredentials();

        /// <summary>
        /// Gets whether this adapter is properly configured with valid credentials

        bool IsConfigured { get; }
    }

    /// Factory class for creating registry adapter instances
    /// Provides a single point of entry for obtaining the appropriate adapter
    /// based on the registry type
    static class RegistryFactory
    {
        /// Gets the registry adapter for the specified registry type
        /// <param name="type">The registry type</param>
        /// <param name="build">The Build instance for accessing configuration</param>
        /// <returns>An instance of the appropriate registry adapter</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when an unsupported registry type is specified</exception>
        internal static IRegistryAdapter GetAdapter(RegistryType type, Build build) => type switch
        {
            RegistryType.AzureAcr => new AzureAcrAdapter(build),
            RegistryType.AliyunAcr => new AliyunAcrAdapter(build),
            RegistryType.DockerHub => new DockerHubAdapter(build),
            _ => throw new ArgumentOutOfRangeException(nameof(type), $"Unsupported registry type: {type}")
        };
    }

    /// <summary>
    /// Adapter for Azure Container Registry (ACR)
    /// Handles image path building and credential retrieval for Azure ACR
    /// Path format: {registry}/{image}
    /// Example: hagicode.azurecr.io/hagicode
    /// Adapter for Azure Container Registry (ACR)
    /// Handles image path building and credential retrieval for Azure ACR
    /// Path format: {registry}/{namespace}/{image} or {registry}/{image} if namespace is empty
    /// Examples: hagicode.azurecr.io/hagicode/hagicode:latest or hagicode.azurecr.io/hagicode:latest
    class AzureAcrAdapter : IRegistryAdapter
    {
        private readonly Build _build;

        public AzureAcrAdapter(Build build)
        {
            _build = build;
        }

        public RegistryType Type => RegistryType.AzureAcr;

        public string GetImagePath(string imageName, string tag)
        {
            var shortImageName = imageName.Contains('/') ? imageName.Substring(imageName.IndexOf('/') + 1) : imageName;
            var registry = _build.EffectiveAzureAcrRegistry;

            if (string.IsNullOrEmpty(registry))
            {
                Log.Warning("Azure ACR registry not configured for image path: {Image}", imageName);
                return $"{shortImageName}:{tag}";
            }

            // Get namespace from build config
            var namespaceValue = _build.EffectiveAzureAcrNamespace;

            if (string.IsNullOrEmpty(namespaceValue))
            {
                return $"{registry}/{shortImageName}:{tag}";
            }

            return $"{registry}/{namespaceValue}/{shortImageName}:{tag}";
        }

        public (string username, string password) GetCredentials()
        {
            return (_build.EffectiveAzureAcrUsername, _build.EffectiveAzureAcrPassword);
        }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_build.EffectiveAzureAcrRegistry) &&
            !string.IsNullOrWhiteSpace(_build.EffectiveAzureAcrUsername) &&
            !string.IsNullOrWhiteSpace(_build.EffectiveAzureAcrPassword);
    }

    /// <summary>
    /// Adapter for Aliyun Container Registry (ACR)
    /// Handles image path building and credential retrieval for Aliyun ACR
    /// Path format: {registry}/{namespace}/{image}
    /// Example: registry.cn-hangzhou.aliyuncs.com/hagicode/hagicode
    class AliyunAcrAdapter : IRegistryAdapter
    {
        private readonly Build _build;

        public AliyunAcrAdapter(Build build)
        {
            _build = build;
        }

        public RegistryType Type => RegistryType.AliyunAcr;

        public string GetImagePath(string imageName, string tag)
        {
            var shortImageName = imageName.Contains('/') ? imageName.Substring(imageName.IndexOf('/') + 1) : imageName;
            var registry = _build.EffectiveAliyunAcrRegistry;

            if (string.IsNullOrEmpty(registry))
            {
                Log.Warning("Aliyun ACR registry not configured for image path: {Image}", imageName);
                return $"{shortImageName}:{tag}";
            }

            // Get namespace from effective configuration
            var namespaceValue = _build.EffectiveAliyunAcrNamespace;

            if (string.IsNullOrEmpty(namespaceValue))
            {
                return $"{registry}/{shortImageName}:{tag}";
            }

            return $"{registry}/{namespaceValue}/{shortImageName}:{tag}";
        }

        public (string username, string password) GetCredentials()
        {
            return (_build.EffectiveAliyunAcrUsername, _build.EffectiveAliyunAcrPassword);
        }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_build.EffectiveAliyunAcrRegistry) &&
            !string.IsNullOrWhiteSpace(_build.EffectiveAliyunAcrUsername) &&
            !string.IsNullOrWhiteSpace(_build.EffectiveAliyunAcrPassword);
    }

    /// <summary>
    /// Adapter for DockerHub registry
    /// Handles image path building and credential retrieval for DockerHub
    /// Path format: {namespace}/{image} or {username}/{image} if namespace is empty
    /// Examples: hagicode/hagicode:latest or newbe36524/hagicode:latest
    class DockerHubAdapter : IRegistryAdapter
    {
        private readonly Build _build;

        public DockerHubAdapter(Build build)
        {
            _build = build;
        }

        public RegistryType Type => RegistryType.DockerHub;

        public string GetImagePath(string imageName, string tag)
        {
            var shortImageName = imageName.Contains('/') ? imageName.Substring(imageName.IndexOf('/') + 1) : imageName;
            var username = _build.EffectiveDockerHubUsername;

            if (string.IsNullOrEmpty(username))
            {
                Log.Warning("DockerHub username not configured for image path: {Image}", imageName);
                return $"{shortImageName}:{tag}";
            }

            // Get namespace from build config
            var namespaceValue = _build.EffectiveDockerHubNamespace;

            // Use namespace if specified, otherwise use username
            var userOrNamespace = string.IsNullOrEmpty(namespaceValue) ? username : namespaceValue;

            return $"{userOrNamespace}/{shortImageName}:{tag}";
        }

        public (string username, string password) GetCredentials()
        {
            return (_build.EffectiveDockerHubUsername, _build.EffectiveDockerHubToken);
        }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_build.EffectiveDockerHubUsername) &&
            !string.IsNullOrWhiteSpace(_build.EffectiveDockerHubToken);
    }
}