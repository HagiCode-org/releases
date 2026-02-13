## ADDED Requirements

### Requirement: Agent Teams Experimental Feature Configuration

The Docker entrypoint SHALL support enabling the Claude Code Agent Teams experimental feature through an environment variable, allowing multiple Claude Code instances to collaborate on the same project.

#### Scenario: Default Agent Teams Enabled

- **WHEN** a user starts a container without setting `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS`
- **THEN** the entrypoint SHALL set `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS` to `1`
- **AND** the Agent Teams feature SHALL be enabled by default

#### Scenario: Explicitly Enable Agent Teams

- **WHEN** a user starts a container with `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS=1`
- **THEN** the entrypoint SHALL preserve this value
- **AND** the Agent Teams feature SHALL be enabled

#### Scenario: Explicitly Disable Agent Teams

- **WHEN** a user starts a container with `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS=0`
- **THEN** the entrypoint SHALL preserve this value
- **AND** the Agent Teams feature SHALL be disabled

#### Scenario: Empty Value Disables Agent Teams

- **WHEN** a user starts a container with `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS=""` (empty string)
- **THEN** the entrypoint SHALL preserve this empty value
- **AND** the Agent Teams feature SHALL be disabled

### Requirement: Agent Teams Configuration in Settings

The Docker entrypoint SHALL include the Agent Teams environment variable in the generated Claude Code settings configuration.

#### Scenario: Settings JSON Includes Agent Teams

- **WHEN** the entrypoint generates the settings.json file
- **THEN** the settings SHALL include `"CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS"` with the configured value
