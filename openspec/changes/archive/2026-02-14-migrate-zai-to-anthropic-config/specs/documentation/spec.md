## RENAMED Requirements

- FROM: `ZAI_SONNET_MODEL` - Sonnet layer model version for ZAI API
- TO: `ANTHROPIC_SONNET_MODEL` - Sonnet layer model version for Anthropic models

- FROM: `ZAI_OPUS_MODEL` - Opus layer model version for ZAI API
- TO: `ANTHROPIC_OPUS_MODEL` - Opus layer model version for Anthropic models

## MODIFIED Requirements

### Requirement: Container Environment Variables Documentation
The project SHALL provide comprehensive documentation for all Docker container environment variables using Anthropic naming conventions.

#### Scenario: User finds Anthropic model configuration variables
- **WHEN** a user reads the container environment variables documentation
- **THEN** they find `ANTHROPIC_SONNET_MODEL` and `ANTHROPIC_OPUS_MODEL` listed
- **AND** each variable includes its purpose, default value (`glm-4.7`), and usage notes
- **AND** the documentation clarifies these configure Anthropic model versions (Sonnet/Opus layers)

#### Scenario: User understands variable naming alignment
- **WHEN** a user reviews the environment variables
- **THEN** they see consistent Anthropic naming (`ANTHROPIC_*`) across all model-related variables
- **AND** this aligns with `ANTHROPIC_AUTH_TOKEN` and other Anthropic configuration

#### Scenario: User migrates from old variable names
- **WHEN** a user has existing deployments using `ZAI_SONNET_MODEL` or `ZAI_OPUS_MODEL`
- **THEN** the documentation clearly shows the mapping to new variable names
- **AND** they can update their configurations without changing default behavior

#### Scenario: User finds usage examples with new variable names
- **WHEN** a user needs to configure the container for a specific deployment scenario
- **THEN** all examples (Docker, Docker Compose, Kubernetes) use `ANTHROPIC_SONNET_MODEL` and `ANTHROPIC_OPUS_MODEL`
- **AND** they can adapt these examples to their needs
