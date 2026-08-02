from __future__ import annotations

import json
import shutil
import time
import urllib.parse
import urllib.request
import zipfile
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any


@dataclass
class PackageAsset:
    path: str = ""
    file_name: str = ""
    url: str = ""
    sources: list[dict[str, Any]] = field(default_factory=list)


@dataclass
class PackageVersion:
    version: str = ""
    assets: list[PackageAsset] = field(default_factory=list)


@dataclass
class PackageIndex:
    versions: list[PackageVersion] = field(default_factory=list)


def _first(data: dict[str, Any], *names: str, default: Any = "") -> Any:
    lower = {str(k).lower(): v for k, v in data.items()}
    for name in names:
        if name in data:
            return data[name]
        key = name.lower()
        if key in lower:
            return lower[key]
    return default


def _asset_from(data: dict[str, Any]) -> PackageAsset:
    return PackageAsset(
        path=str(_first(data, "path", "name", default="")),
        file_name=str(_first(data, "fileName", "file_name", "name", default="")),
        url=str(_first(data, "url", "downloadUrl", "download_url", default="")),
        sources=list(_first(data, "sources", "downloadSources", "download_sources", default=[]) or []),
    )


def parse_index(raw: str | bytes | dict[str, Any]) -> PackageIndex:
    data = json.loads(raw) if isinstance(raw, (str, bytes)) else raw
    versions_raw = _first(data, "versions", "packageVersions", default=[])
    versions: list[PackageVersion] = []
    if isinstance(versions_raw, dict):
        versions_raw = [{"version": key, **(value or {})} for key, value in versions_raw.items()]
    for item in versions_raw or []:
        if not isinstance(item, dict):
            continue
        version = str(_first(item, "version", "name", default=""))
        assets_raw = _first(item, "assets", "files", "packages", default=[])
        assets = [_asset_from(asset) for asset in assets_raw if isinstance(asset, dict)]
        versions.append(PackageVersion(version=version, assets=assets))
    return PackageIndex(versions=versions)


def _normalize_index_url(index_url: str) -> str:
    normalized = (index_url or "").strip()
    if not normalized:
        return ""
    if normalized.endswith("/"):
        return urllib.parse.urljoin(normalized, "index.json")
    return normalized

def download_index(index_url: str) -> PackageIndex:
    resolved_index_url = _normalize_index_url(index_url)
    if not resolved_index_url:
        raise RuntimeError("Release package source is missing. Set RELEASE_PACKAGE_INDEX_URL.")
    with urllib.request.urlopen(resolved_index_url, timeout=60) as response:
        return parse_index(response.read())



def all_versions(index: PackageIndex) -> list[str]:
    return [v.version for v in index.versions if v.version]


def _asset_name(asset: PackageAsset) -> str:
    return asset.file_name or Path(asset.path).name


def _matches_platform(asset: PackageAsset, platforms: list[str]) -> bool:
    if not platforms:
        return True
    haystack = f"{asset.path} {_asset_name(asset)}".lower()
    normalized = [p.strip().lower().replace("linux/", "linux-").replace("/", "-") for p in platforms if p.strip()]
    return any(token in haystack for token in normalized)


def select_package_assets(index: PackageIndex, version: str, platforms: list[str] | None = None) -> list[tuple[PackageVersion, PackageAsset]]:
    requested = (version or "").strip().lstrip("v")
    selected: list[tuple[PackageVersion, PackageAsset]] = []
    for package_version in index.versions:
        if package_version.version.strip().lstrip("v") != requested:
            continue
        for asset in package_version.assets:
            name = _asset_name(asset)
            if not name.lower().endswith(".zip"):
                continue
            if _matches_platform(asset, platforms or []):
                selected.append((package_version, asset))
    return selected


def resolve_download_url(asset: PackageAsset, *, base_url: str = "", index_url: str = "") -> str:
    if asset.url:
        return asset.url
    for source in asset.sources:
        url = str(_first(source, "url", "downloadUrl", default=""))
        if url:
            return url
    path = asset.path or _asset_name(asset)
    if urllib.parse.urlparse(path).scheme in {"http", "https"}:
        return path
    if base_url:
        return urllib.parse.urljoin(base_url.rstrip("/") + "/", path.lstrip("/"))
    if index_url:
        return urllib.parse.urljoin(index_url.rsplit("/", 1)[0] + "/", path.lstrip("/"))
    return ""


def download_file(url: str, destination: Path, retries: int = 3) -> int:
    destination.parent.mkdir(parents=True, exist_ok=True)
    last_error: Exception | None = None
    for attempt in range(1, retries + 1):
        try:
            with urllib.request.urlopen(url, timeout=120) as response, destination.open("wb") as out:
                shutil.copyfileobj(response, out)
            return destination.stat().st_size
        except Exception as exc:
            last_error = exc
            if attempt < retries:
                time.sleep(attempt)
    raise RuntimeError(f"Failed to download {url}: {last_error}")


def download_all_for_version(*, index_url: str, base_url: str, version: str, output_directory: Path, platforms: list[str] | None = None) -> list[Path]:
    index = download_index(index_url)
    selected = select_package_assets(index, version, platforms)
    if not selected:
        raise RuntimeError(f"Version {version} not found in package index or no zip assets matched requested platforms")
    output_directory.mkdir(parents=True, exist_ok=True)
    paths: list[Path] = []
    for _, asset in selected:
        url = resolve_download_url(asset, base_url=base_url, index_url=index_url)
        if not url:
            raise RuntimeError(f"No download URL resolved for {asset.path}")
        destination = output_directory / _asset_name(asset)
        print(f"[PYBUILD][download] {url} -> {destination}")
        download_file(url, destination)
        paths.append(destination)
    return paths


def extract_zip_files(zip_files: list[Path], destination: Path) -> None:
    destination.mkdir(parents=True, exist_ok=True)
    for archive in zip_files:
        with zipfile.ZipFile(archive) as zf:
            zf.extractall(destination)
