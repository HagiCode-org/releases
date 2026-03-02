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
        RegistryType.AzureAcr => "hagicode.azurecr.io",
        RegistryType.AliyunAcr => "registry.cn-hangzhou.aliyuncs.com",
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
        /// Gets the authentication credentials for the registry
        /// <returns>A tuple containing username and password</returns>
        (string username, string password) GetCredentials();
    }

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

        public (string username, string password) GetCredentials()
        {
            return (_build.EffectiveAzureAcrUsername, _build.EffectiveAzureAcrPassword);
        }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_build.EffectiveAzureAcrUsername) &&
            !string.IsNullOrWhiteSpace(_build.EffectiveAzureAcrPassword);
    }

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

        public (string username, string password) GetCredentials()
        {
            return (_build.EffectiveAliyunAcrUsername, _build.EffectiveAliyunAcrPassword);
        }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_build.EffectiveAliyunAcrUsername) &&
            !string.IsNullOrWhiteSpace(_build.EffectiveAliyunAcrPassword);
    }

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

        public (string username, string password) GetCredentials()
        {
            return (_build.EffectiveDockerHubUsername, _build.EffectiveDockerHubToken);
        }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_build.EffectiveDockerHubUsername) &&
            !string.IsNullOrWhiteSpace(_build.EffectiveDockerHubToken);
    }
}