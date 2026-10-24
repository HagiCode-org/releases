---
public: true
multilingual: true
languages:
  - en-US
  - zh-CN
sourceLanguage: en-US
style: technical
---

# README requirements

## Audience and focus

- Address maintainers of the HagiCode release automation: version discovery, GitHub Releases and container image publishing.

## Required content

- Keep product overview, what this repository handles, main areas, common release commands, local container build and test, desktop artifact verification, trigger boundaries, container CLI contract, runtime startup and persistence, startup SSH bootstrap and ecosystem role.
- Link to `ENVIRONMENT_VARIABLES.md` instead of copying variable lists.

## README languages

- Maintain English in `README.md` and Simplified Chinese in `README_cn.md`, keeping the reciprocal language link and the existing filenames.
- Keep both files aligned in scope and meaning.

## Exclusions

- Exclude registry credentials, values from `.env.local.example` and `.env.secrets.local.example`, SSH keys, tokens, internal hosts, machine-specific paths and private links.
