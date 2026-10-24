---
public: true
multilingual: true
languages:
  - en-US
  - zh-CN
sourceLanguage: en-US
style: product
---

# README requirements

## Product focus

- Present HagiCode Release as the delivery hub that turns built HagiCode packages into GitHub Releases and multi-architecture container images.
- Main features: version monitoring that picks the newest unpublished version, GitHub Release publishing, Docker Hub image builds, a unified container runtime image with the HagiScript-synced agent CLI baseline, persistent data and saves volumes, and optional SSH key bootstrap at container start.
- Address people who deploy HagiCode from the container image and maintainers who run the release commands. Lead with what the product delivers, then how to use it.

## Required content

- Open with a short product introduction and what the repository delivers.
- Keep the common release commands, the local container build and test commands, the container runtime contract, the persistence volume layout and the SSH bootstrap options that users act on, as the current README does.
- Link to `ENVIRONMENT_VARIABLES.md` instead of copying variable lists.

## README languages

- Maintain English in `README.md` and Simplified Chinese in `README_cn.md`, keeping the reciprocal language link and the existing filenames.
- Keep both files aligned in scope and meaning.

## Exclusions

- Exclude registry credentials, values from `.env.local.example` and `.env.secrets.local.example`, SSH keys, tokens, internal hosts, machine-specific paths and private links.
- Keep repository maintenance instructions out of the README: repository layout, internal architecture, CI workflow wiring, build internals and contribution workflow.
- Do not list features the repository does not show.
