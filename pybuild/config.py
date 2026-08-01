from __future__ import annotations

import os
import subprocess
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

import yaml


def _upper_snake(name: str) -> str:
    value = ""
    for index, char in enumerate(name):
        if char in {"-", "_"}:
            value += "_"
        elif char.isupper() and index > 0 and name[index - 1] not in {"-", "_"}:
            value += "_" + char
        else:
            value += char.upper()
    return value


ENV_ALIASES: dict[str, tuple[str, ...]] = {
    "ReleaseVersion": ("RELEASE_VERSION",),
    "DockerPlatform": ("DOCKER_PLATFORM",),
    "DockerImageName": ("DOCKER_IMAGE_NAME",),
    "DockerForceRebuild": ("DOCKER_FORCE_REBUILD",),
    "DryRun": ("DRY_RUN",),
    "ListOnly": ("LIST_ONLY",),
    "ReleasePackageIndexUrl": ("RELEASE_PACKAGE_INDEX_URL",),
    "ReleasePackageBaseUrl": ("RELEASE_PACKAGE_BASE_URL",),
    "GitHubToken": ("GITHUB_TOKEN",),
    "GitHubRepository": ("GITHUB_REPOSITORY",),
    "AliyunAcrRegistry": ("ALIYUN_ACR_REGISTRY",),
    "AliyunAcrNamespace": ("ALIYUN_ACR_NAMESPACE",),
    "AliyunAcrUsername": ("ALIYUN_ACR_USERNAME",),
    "AliyunAcrPassword": ("ALIYUN_ACR_PASSWORD",),
    "DockerHubUsername": ("DOCKERHUB_USERNAME", "DOCKER_HUB_USERNAME"),
    "DockerHubToken": ("DOCKERHUB_TOKEN", "DOCKER_HUB_TOKEN"),
    "DockerHubNamespace": ("DOCKERHUB_NAMESPACE", "DOCKER_HUB_NAMESPACE"),
}


def _env_names(name: str) -> tuple[str, ...]:
    return (f"NUGEX_{name}", *ENV_ALIASES.get(name, ()), _upper_snake(name), name.upper(), name)


def env_value(name: str, default: str = "") -> str:
    for env_name in _env_names(name):
        value = os.environ.get(env_name)
        if value not in (None, ""):
            return value
    return default


def env_bool(name: str, default: bool = False) -> bool:
    value = env_value(name, "")
    if value == "":
        return default
    return value.strip().lower() in {"1", "true", "yes", "on"}


@dataclass
class DockerConfig:
    image_name: str = "hagicode"
    platform: str = "all"
    build_timeout: int = 3600
    force_rebuild: bool = False
    independent_build: bool = False


@dataclass
class RegistryConfig:
    registry: str = ""
    namespace: str = ""
    username: str = ""
    password: str = ""
    token: str = ""


@dataclass
class BuildConfig:
    docker: DockerConfig = field(default_factory=DockerConfig)
    aliyun_acr: RegistryConfig = field(default_factory=lambda: RegistryConfig(registry="registry.cn-hangzhou.aliyuncs.com"))
    dockerhub: RegistryConfig = field(default_factory=lambda: RegistryConfig(namespace="newbe36524"))


def _read_yaml(path: Path) -> dict[str, Any]:
    if not path.exists():
        return {}
    with path.open("r", encoding="utf-8") as fh:
        return yaml.safe_load(fh) or {}


def load_build_config(repo_root: Path) -> BuildConfig:
    data = _read_yaml(repo_root / "build-config.yaml")
    config = BuildConfig()
    docker = data.get("docker") or {}
    config.docker.image_name = str(docker.get("image_name", config.docker.image_name))
    config.docker.platform = str(docker.get("platform", config.docker.platform))
    config.docker.build_timeout = int(docker.get("build_timeout", config.docker.build_timeout))
    config.docker.force_rebuild = bool(docker.get("force_rebuild", config.docker.force_rebuild))
    config.docker.independent_build = bool(docker.get("independent_build", config.docker.independent_build))
    acr = data.get("aliyun_acr") or {}
    config.aliyun_acr.registry = str(acr.get("registry", config.aliyun_acr.registry))
    config.aliyun_acr.namespace = str(acr.get("namespace", config.aliyun_acr.namespace))
    config.aliyun_acr.username = str(acr.get("username", config.aliyun_acr.username))
    config.aliyun_acr.password = str(acr.get("password", config.aliyun_acr.password))
    dh = data.get("dockerhub") or {}
    config.dockerhub.username = str(dh.get("username", config.dockerhub.username))
    config.dockerhub.token = str(dh.get("token", config.dockerhub.token))
    config.dockerhub.namespace = str(dh.get("namespace", config.dockerhub.namespace))
    return config


def effective_release_version(repo_root: Path, explicit: str = "") -> str:
    value = explicit or env_value("ReleaseVersion")
    if value:
        return value
    try:
        tag = subprocess.check_output(["git", "describe", "--tags", "--abbrev=0"], cwd=repo_root, text=True, stderr=subprocess.DEVNULL).strip()
        if tag:
            return tag[1:] if tag.startswith("v") else tag
    except Exception:
        pass
    return "latest"


def release_source(repo_root: Path) -> tuple[str, str]:
    index_url = env_value("ReleasePackageIndexUrl")
    base_url = env_value("ReleasePackageBaseUrl")
    return index_url, base_url
