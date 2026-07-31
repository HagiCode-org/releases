from __future__ import annotations

import os
from pathlib import Path

from invoke import Collection, task

from .config import effective_release_version, env_bool, env_value, load_build_config, release_source
from .docker_ops import DockerImageInfo, docker_buildx_push, docker_login, downloaded_zip_files, prepare_context, resolve_platforms
from .github_ops import create_or_update_release, dispatch, get_releases, verify_dispatch_created
from .release_source import all_versions, download_all_for_version, download_index
from .runtime import write_github_output
from .versioning import create_plan

REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_DIR = REPO_ROOT / "output"
DOWNLOAD_DIR = OUTPUT_DIR / "download"
DOCKER_CONTEXT_DIR = OUTPUT_DIR / "docker-build-context"


def _value(explicit: str = "", *names: str) -> str:
    if explicit:
        return explicit
    for name in names:
        value = env_value(name)
        if value:
            return value
    return ""


def _release_version(explicit: str = "") -> str:
    return effective_release_version(REPO_ROOT, explicit)


def _github_token(explicit: str = "") -> str:
    return _value(explicit, "GitHubToken", "GITHUB_TOKEN")


def _github_repository(explicit: str = "") -> str:
    return _value(explicit, "GitHubRepository", "GITHUB_REPOSITORY")


def _source() -> tuple[str, str, str]:
    return release_source(REPO_ROOT)


@task(name="configuration-validate")
def configuration_validate(c) -> None:
    config = load_build_config(REPO_ROOT)
    print("[PYBUILD][config] build-config.yaml loaded")
    if not config.docker.image_name:
        raise RuntimeError("docker.image_name is required")
    if config.docker.build_timeout <= 0:
        raise RuntimeError("docker.build_timeout must be greater than zero")


@task(name="determine-build-config")
def determine_build_config(c, release_version: str = "") -> None:
    version = _release_version(release_version)
    print(f"[PYBUILD][config] Effective version: {version}")
    print(f"[PYBUILD][config] Is stable release: {'-' not in version}")


@task
def download(c, release_version: str = "", azure_blob_sas_url: str = "", release_package_index_url: str = "", release_package_base_url: str = "", download_platforms: str = "") -> None:
    version = _release_version(release_version)
    index_url, base_url, sas = _source()
    index_url = release_package_index_url or index_url
    base_url = release_package_base_url or base_url
    sas = azure_blob_sas_url or sas
    platforms = [p.strip() for p in download_platforms.split(",") if p.strip()]
    paths = download_all_for_version(index_url=index_url, base_url=base_url, azure_blob_sas_url=sas, version=version, output_directory=DOWNLOAD_DIR, platforms=platforms)
    print(f"[PYBUILD][download] downloaded {len(paths)} package(s)")


@task(name="version-monitor")
def version_monitor(c, dry_run: str = "", list_only: str = "", release_package_index_url: str = "", release_package_base_url: str = "", azure_blob_sas_url: str = "", github_token: str = "", github_repository: str = "") -> None:
    index_url, _, sas = _source()
    index_url = release_package_index_url or index_url
    sas = azure_blob_sas_url or sas
    token = _github_token(github_token)
    repository = _github_repository(github_repository)
    index = download_index(index_url, sas)
    package_versions = all_versions(index)
    releases = get_releases(token, repository)
    plan = create_plan(package_versions, releases)
    for ignored in plan.ignored_versions:
        print(f"[PYBUILD][version-monitor] Skipping invalid version format: {ignored}")
    print(f"[PYBUILD][version-monitor] Sorted package source versions: {', '.join(plan.sorted_package_versions) if plan.sorted_package_versions else '(none)'}")
    write_github_output("has_new_versions", "true" if plan.has_new_versions else "false")
    write_github_output("new_versions", ", ".join(plan.new_versions))
    write_github_output("latest_version", plan.latest_version)
    write_github_output("selected_version", plan.selected_version)
    write_github_output("deferred_versions", ", ".join(plan.deferred_versions))
    list_mode = env_bool("ListOnly") or str(list_only).lower() in {"1", "true", "yes", "on"}
    dry = env_bool("DryRun") or str(dry_run).lower() in {"1", "true", "yes", "on"}
    if list_mode:
        print("[PYBUILD][version-monitor] list-only mode completed")
        return
    if not plan.selected_version:
        print("[PYBUILD][version-monitor] latest package source version is already present on GitHub")
        return
    dispatch(repository, token, "version-monitor-release", {"version": plan.selected_version}, dry_run=dry)
    dispatch(repository, token, "version-monitor-docker-aliyun", {"version": plan.selected_version}, dry_run=dry)
    dispatch(repository, token, "version-monitor-docker-dockerhub", {"version": plan.selected_version}, dry_run=dry)
    if not dry:
        verify_dispatch_created(repository, token, "github-release-workflow.yml")


@task(name="github-release")
def github_release(c, release_version: str = "", github_token: str = "", github_repository: str = "") -> None:
    version = _release_version(release_version)
    token = _github_token(github_token)
    repository = _github_repository(github_repository)
    index_url, base_url, sas = _source()
    if not downloaded_zip_files(DOWNLOAD_DIR, version):
        download_all_for_version(index_url=index_url, base_url=base_url, azure_blob_sas_url=sas, version=version, output_directory=DOWNLOAD_DIR)
    url = create_or_update_release(token, repository, version, downloaded_zip_files(DOWNLOAD_DIR, version))
    write_github_output("release_url", url)


def _docker_release(registry_kind: str, *, release_version: str = "", docker_platform: str = "", dry_run: str = "") -> None:
    config = load_build_config(REPO_ROOT)
    version = _release_version(release_version)
    platform_value = docker_platform or env_value("DockerPlatform") or config.docker.platform
    platforms = resolve_platforms(platform_value)
    index_url, base_url, sas = _source()
    package_platforms = ["linux-x64" if p == "linux/amd64" else "linux-arm64" if p == "linux/arm64" else p for p in platforms]
    if not all(downloaded_zip_files(DOWNLOAD_DIR, version, p) for p in platforms):
        download_all_for_version(index_url=index_url, base_url=base_url, azure_blob_sas_url=sas, version=version, output_directory=DOWNLOAD_DIR, platforms=package_platforms)
    prepare_context(REPO_ROOT, version=version, platforms=platforms, download_dir=DOWNLOAD_DIR, context_dir=DOCKER_CONTEXT_DIR)
    dry = env_bool("DryRun") or str(dry_run).lower() in {"1", "true", "yes", "on"}
    if registry_kind == "aliyun":
        registry = env_value("AliyunAcrRegistry", config.aliyun_acr.registry) or config.aliyun_acr.registry
        namespace = env_value("AliyunAcrNamespace", config.aliyun_acr.namespace) or config.aliyun_acr.namespace
        username = env_value("AliyunAcrUsername", config.aliyun_acr.username) or config.aliyun_acr.username
        password = env_value("AliyunAcrPassword", config.aliyun_acr.password) or config.aliyun_acr.password
    elif registry_kind == "dockerhub":
        registry = "docker.io"
        namespace = env_value("DockerHubNamespace", config.dockerhub.namespace) or env_value("DockerHubUsername", config.dockerhub.username) or config.dockerhub.namespace
        username = env_value("DockerHubUsername", config.dockerhub.username) or config.dockerhub.username
        password = env_value("DockerHubToken", config.dockerhub.token) or config.dockerhub.token
    else:
        registry = "docker.io"
        namespace = env_value("DockerHubNamespace", config.dockerhub.namespace) or config.dockerhub.namespace
        username = env_value("DockerHubUsername", config.dockerhub.username) or config.dockerhub.username
        password = env_value("DockerHubToken", config.dockerhub.token) or config.dockerhub.token
    image = DockerImageInfo(registry=registry, namespace=namespace, image_name=env_value("DockerImageName", config.docker.image_name) or config.docker.image_name)
    if not dry:
        docker_login(registry, username, password)
    docker_buildx_push(image, version=version, platforms=platforms, context_dir=DOCKER_CONTEXT_DIR, dry_run=dry, no_cache=env_bool("DockerForceRebuild"))


@task(name="docker-release")
def docker_release(c, release_version: str = "", docker_platform: str = "", dry_run: str = "") -> None:
    _docker_release("dockerhub", release_version=release_version, docker_platform=docker_platform, dry_run=dry_run)


@task(name="push-to-aliyun-acr")
def push_to_aliyun_acr(c, release_version: str = "", docker_platform: str = "", dry_run: str = "") -> None:
    _docker_release("aliyun", release_version=release_version, docker_platform=docker_platform, dry_run=dry_run)


@task(name="push-to-dockerhub")
def push_to_dockerhub(c, release_version: str = "", docker_platform: str = "", dry_run: str = "") -> None:
    _docker_release("dockerhub", release_version=release_version, docker_platform=docker_platform, dry_run=dry_run)


@task(name="docker-prepare-local-context")
def docker_prepare_local_context(c, release_version: str = "", docker_platform: str = "") -> None:
    version = _release_version(release_version)
    config = load_build_config(REPO_ROOT)
    platforms = resolve_platforms(docker_platform or env_value("DockerPlatform") or config.docker.platform)
    prepare_context(REPO_ROOT, version=version, platforms=platforms, download_dir=DOWNLOAD_DIR, context_dir=DOCKER_CONTEXT_DIR)


ns = Collection(
    configuration_validate,
    determine_build_config,
    download,
    version_monitor,
    github_release,
    docker_release,
    push_to_aliyun_acr,
    push_to_dockerhub,
    docker_prepare_local_context,
)
