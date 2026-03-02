using Nuke.Common;
using Serilog;

/// Docker login target
///
/// This partial class provides Docker registry login functionality.
/// Login is executed as a dependency before building images to ensure
/// the Docker client is authenticated before pushing to registries.
partial class Build
{
    /// Docker login target - logs in to Docker registries before building
    /// This target should be a dependency of any target that pushes images

    Target DockerLogin => _ => _
        .Description("Login to Docker registries before building")
        .Unlisted()
        .Executes(() =>
        {
            if (string.IsNullOrEmpty(EffectiveAzureAcrRegistry))
            {
                Log.Warning("Edge ACR registry not configured, skipping login");
                return;
            }

            // Login using the adapter pattern
            var adapter = new AzureAcrAdapter(this);
            if (!adapter.IsConfigured)
            {
                Log.Warning("Edge ACR credentials not configured, skipping login");
                return;
            }

            LoginToRegistry(adapter);

            // Note: Aliyun ACR login is handled separately in DockerPush
            // if needed in the future, can add here
        });


    /// Logs in to Azure Container Registry
    /// This is a specialized login method for Azure ACR
    void LoginToAzureAcr()
    {
        if (string.IsNullOrEmpty(EffectiveAzureAcrRegistry))
        {
            Log.Warning("Azure ACR registry not configured, skipping login");
            return;
        }

        var adapter = new AzureAcrAdapter(this);
        if (!adapter.IsConfigured)
        {
            Log.Warning("Azure ACR credentials not configured, skipping login");
            return;
        }

        LoginToRegistry(adapter);
        Log.Information("Successfully logged in to Azure ACR");
    }


    /// Logs in to Aliyun Container Registry
    /// This is a specialized login method for Aliyun ACR
    void LoginToAliyunAcr()
    {
        if (string.IsNullOrEmpty(EffectiveAliyunAcrRegistry))
        {
            Log.Warning("Aliyun ACR registry not configured, skipping login");
            return;
        }

        var adapter = new AliyunAcrAdapter(this);
        if (!adapter.IsConfigured)
        {
            Log.Warning("Aliyun ACR credentials not configured, skipping login");
            return;
        }

        LoginToRegistry(adapter);
        Log.Information("Successfully logged in to Aliyun ACR");
    }


    /// Logs in to DockerHub
    /// This is a specialized login method for DockerHub
    void LoginToDockerHub()
    {
        if (string.IsNullOrEmpty(EffectiveDockerHubUsername))
        {
            Log.Warning("DockerHub username not configured, skipping login");
            return;
        }

        var adapter = new DockerHubAdapter(this);
        if (!adapter.IsConfigured)
        {
            Log.Warning("DockerHub credentials not configured, skipping login");
            return;
        }

        LoginToRegistry(adapter);
        Log.Information("Successfully logged in to DockerHub");
    }
}