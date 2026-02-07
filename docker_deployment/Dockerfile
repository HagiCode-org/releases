# Use .NET 10 Runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

# Install dependencies
RUN apt-get update && apt-get install -y \
    curl \
    gosu \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user with UID 1000
RUN groupadd -r hagicode -g 1000 && useradd -r -u 1000 -g hagicode hagicode

# Copy application files from extracted package
# The package should contain:
# - lib/ directory with .NET assemblies (framework-dependent deployment)
# - config/ directory with configuration files
# - Other application files
COPY --chown=hagicode:hagicode . /app/

# Copy docker-entrypoint.sh
COPY --chown=hagicode:hagicode docker-entrypoint.sh /usr/local/bin/
RUN chmod +x /usr/local/bin/docker-entrypoint.sh

# Expose port
EXPOSE 5000

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

# Set entry point
ENTRYPOINT ["docker-entrypoint.sh"]
