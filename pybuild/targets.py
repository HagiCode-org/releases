"""Legacy target inventory and Invoke task aliases for hagicode-release."""

from __future__ import annotations

TARGET_ALIASES: dict[str, str] = {
    "default": "configuration-validate",
    "configurationvalidate": "configuration-validate",
    "configuration-validate": "configuration-validate",
    "determinebuildconfig": "determine-build-config",
    "determine-build-config": "determine-build-config",
    "download": "download",
    "versionmonitor": "version-monitor",
    "version-monitor": "version-monitor",
    "githubrelease": "github-release",
    "github-release": "github-release",
    "dockerrelease": "docker-release",
    "docker-release": "docker-release",
    "pushtoaliyunacr": "push-to-aliyun-acr",
    "push-to-aliyun-acr": "push-to-aliyun-acr",
    "pushtodockerhub": "push-to-dockerhub",
    "push-to-dockerhub": "push-to-dockerhub",
    "dockerpreparelocalcontext": "docker-prepare-local-context",
    "docker-prepare-local-context": "docker-prepare-local-context",
}

KNOWN_TARGETS = frozenset(TARGET_ALIASES)


def resolve_invoke_task(target: str) -> str:
    key = target.strip().lower()
    if key not in TARGET_ALIASES:
        known = ", ".join(sorted(TARGET_ALIASES))
        raise KeyError(f"Unknown build target '{target}'. Known targets: {known}")
    return TARGET_ALIASES[key]
