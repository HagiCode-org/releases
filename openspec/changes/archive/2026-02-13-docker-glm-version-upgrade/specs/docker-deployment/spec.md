## ADDED Requirements

### Requirement: GLM Model Version Environment Variable Configuration

The Docker entrypoint SHALL support optional environment variables to override the default GLM model versions used for Sonnet and Opus tiers when using the ZAI API endpoint.

#### Scenario: Default GLM Model Versions

- **WHEN** a user starts a container with `ZAI_API_KEY` environment variable set but without model version override variables
- **THEN** the entrypoint SHALL use the default model versions:
  - `ANTHROPIC_DEFAULT_HAIKU_MODEL` set to `glm-4.5-air`
  - `ANTHROPIC_DEFAULT_SONNET_MODEL` set to `glm-4.7`
  - `ANTHROPIC_DEFAULT_OPUS_MODEL` set to `glm-4.7`

#### Scenario: Override Sonnet Model Version

- **WHEN** a user starts a container with `ZAI_API_KEY` and `ZAI_SONNET_MODEL` environment variables set
- **THEN** the entrypoint SHALL use the value of `ZAI_SONNET_MODEL` for `ANTHROPIC_DEFAULT_SONNET_MODEL`
- **AND** other model versions SHALL remain at their defaults

#### Scenario: Override Opus Model Version

- **WHEN** a user starts a container with `ZAI_API_KEY` and `ZAI_OPUS_MODEL` environment variables set
- **THEN** the entrypoint SHALL use the value of `ZAI_OPUS_MODEL` for `ANTHROPIC_DEFAULT_OPUS_MODEL`
- **AND** other model versions SHALL remain at their defaults

#### Scenario: Override Both Sonnet and Opus to GLM 5

- **WHEN** a user starts a container with:
  - `ZAI_API_KEY` set
  - `ZAI_SONNET_MODEL` set to `glm-5`
  - `ZAI_OPUS_MODEL` set to `glm-5`
- **THEN** the entrypoint SHALL configure:
  - `ANTHROPIC_DEFAULT_SONNET_MODEL` set to `glm-5`
  - `ANTHROPIC_DEFAULT_OPUS_MODEL` set to `glm-5`
- **AND** `ANTHROPIC_DEFAULT_HAIKU_MODEL` SHALL remain `glm-4.5-air`

#### Scenario: Environment Variable Not Set

- **WHEN** `ZAI_SONNET_MODEL` or `ZAI_OPUS_MODEL` environment variables are not set or are empty
- **THEN** the entrypoint SHALL fall back to the default values (`glm-4.7` for both)
