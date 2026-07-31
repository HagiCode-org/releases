from __future__ import annotations

import os
import shutil
import subprocess
from dataclasses import dataclass
from pathlib import Path

from .release_source import extract_zip_files


@dataclass(frozen=True)
class DockerImageInfo:
    registry: str
    namespace: str
    image_name: str

    @property
    def full_image_name(self) -> str:
        return f"{self.registry}/{self.namespace}/{self.image_name}" if self.namespace else f"{self.registry}/{self.image_name}"

    def with_tag(self, tag: str) -> str:
        return f"{self.full_image_name}:{tag}"


def resolve_platforms(value: str) -> list[str]:
    raw = (value or "all").strip()
    tokens = [part.strip() for part in raw.split(",") if part.strip()]
    if not tokens or any(token.lower() == "all" for token in tokens):
        return ["linux/amd64", "linux/arm64"]
    mapping = {"linux-amd64": "linux/amd64", "amd64": "linux/amd64", "linux-arm64": "linux/arm64", "arm64": "linux/arm64"}
    return [mapping.get(token.lower(), token) for token in tokens]


def platform_download_name(platform: str) -> str:
    return {"linux/amd64": "linux-x64", "linux/arm64": "linux-arm64"}.get(platform, platform.replace("/", "-"))


def platform_dir_name(platform: str) -> str:
    return {"linux/amd64": "amd64", "linux/arm64": "arm64"}.get(platform, platform.replace("linux/", "").replace("/", "-"))


def downloaded_zip_files(download_dir: Path, version: str, platform: str | None = None) -> list[Path]:
    files = sorted(download_dir.glob("*.zip")) if download_dir.exists() else []
    if version:
        files = [p for p in files if version in p.name]
    if platform:
        token = platform_download_name(platform)
        files = [p for p in files if token in p.name]
    return files


def prepare_context(repo_root: Path, *, version: str, platforms: list[str], download_dir: Path, context_dir: Path) -> None:
    docker_deployment = repo_root / "docker_deployment"
    if context_dir.exists():
        shutil.rmtree(context_dir)
    context_dir.mkdir(parents=True, exist_ok=True)
    shutil.copy2(docker_deployment / "docker-entrypoint.sh", context_dir / "docker-entrypoint.sh")
    shutil.copy2(docker_deployment / "hagiscript-sync-manifest.json", context_dir / "hagiscript-sync-manifest.json")
    for platform in platforms:
        zip_files = downloaded_zip_files(download_dir, version, platform)
        if not zip_files:
            raise RuntimeError(f"No downloaded zip packages for version '{version}' platform '{platform}' were found in '{download_dir}'.")
        target = context_dir / f"lib-{platform_dir_name(platform)}"
        extract_zip_files(zip_files, target)
    template = (docker_deployment / "Dockerfile.template").read_text(encoding="utf-8")
    (context_dir / "Dockerfile").write_text(template, encoding="utf-8")
    print(f"[PYBUILD][docker] Docker build context prepared at {context_dir}")


def docker_login(registry: str, username: str, password: str) -> None:
    if not username or not password:
        print(f"[PYBUILD][docker] credentials not configured for {registry}; skipping login")
        return
    process = subprocess.run(["docker", "login", "--username", username, "--password-stdin", registry], input=password, text=True, check=False)
    if process.returncode != 0:
        raise RuntimeError(f"docker login failed for {registry}")


def docker_buildx_push(image: DockerImageInfo, *, version: str, platforms: list[str], context_dir: Path, dry_run: bool = False, no_cache: bool = False) -> None:
    tag = image.with_tag(version)
    cmd = ["docker", "buildx", "build", f"--platform={','.join(platforms)}", "--file", str(context_dir / "Dockerfile"), "--tag", tag, "--output", "type=registry"]
    if no_cache:
        cmd.insert(4, "--no-cache")
    cmd.append(str(context_dir))
    if dry_run:
        print(f"[PYBUILD][docker][dry-run] {' '.join(cmd)}")
        return
    result = subprocess.run(cmd, check=False)
    if result.returncode != 0:
        raise RuntimeError(f"docker buildx build failed for {tag}")
