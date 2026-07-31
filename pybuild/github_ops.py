from __future__ import annotations

import json
import os
import subprocess
import time
from datetime import datetime, timezone
from pathlib import Path


def _gh(args: list[str], *, token: str, input_text: str | None = None, capture: bool = False) -> subprocess.CompletedProcess[str]:
    env = os.environ.copy()
    if token:
        env["GH_TOKEN"] = token
    return subprocess.run(["gh", *args], input=input_text, text=True, capture_output=capture, env=env, check=False)


def get_releases(token: str, repository: str) -> list[str]:
    if not token or not repository:
        return []
    result = _gh(["release", "list", "--repo", repository, "--limit", "100", "--json", "tagName"], token=token, capture=True)
    if result.returncode != 0:
        print(f"[PYBUILD][github] gh release list failed: {result.stderr.strip()}")
        return []
    data = json.loads(result.stdout or "[]")
    return [str(item.get("tagName", "")) for item in data if item.get("tagName")]


def dispatch(repository: str, token: str, event_type: str, payload: dict[str, str], *, dry_run: bool = False) -> None:
    body = json.dumps({"event_type": event_type, "client_payload": payload})
    if dry_run:
        print(f"[PYBUILD][github][dry-run] would dispatch {event_type}: {payload}")
        return
    result = _gh(["api", "--method", "POST", "-H", "Accept: application/vnd.github.v3+json", "-H", "Content-Type: application/json", f"/repos/{repository}/dispatches", "--input", "-"], token=token, input_text=body, capture=True)
    if result.returncode != 0:
        raise RuntimeError(f"gh repository_dispatch failed: {result.stderr.strip()}")


def verify_dispatch_created(repository: str, token: str, workflow_name: str, *, timeout_seconds: int = 60) -> str:
    deadline = time.time() + timeout_seconds
    while time.time() < deadline:
        result = _gh(["run", "list", "--repo", repository, "--workflow", workflow_name, "--limit", "10", "--json", "databaseId,url,createdAt,event,status"], token=token, capture=True)
        if result.returncode == 0 and result.stdout.strip():
            runs = json.loads(result.stdout)
            if runs:
                url = runs[0].get("url") or ""
                print(f"[PYBUILD][github] dispatch verified: {url}")
                return url
        time.sleep(5)
    print(f"[PYBUILD][github] dispatch verification timed out for workflow {workflow_name}")
    return ""


def release_exists(token: str, repository: str, tag: str) -> bool:
    result = _gh(["release", "view", tag, "--repo", repository, "--json", "tagName"], token=token, capture=True)
    return result.returncode == 0


def build_release_notes(version: str) -> str:
    now = datetime.now(timezone.utc).strftime("%Y-%m-%d")
    tag = version
    return f"Release {version.lstrip('v')}\n\nAutomated release from the version monitor workflow.\n\n**Workflow:** github-release-workflow\n**Version:** {tag}\n**Date:** {now}"


def upload_assets(token: str, repository: str, tag: str, zip_files: list[Path]) -> None:
    if not zip_files:
        print("[PYBUILD][github] no zip packages found to upload")
        return
    args = ["release", "upload", tag, *map(str, zip_files), "--repo", repository, "--clobber"]
    result = _gh(args, token=token, capture=True)
    if result.returncode != 0:
        raise RuntimeError(f"gh release upload failed: {result.stderr.strip()}")


def create_or_update_release(token: str, repository: str, version: str, zip_files: list[Path]) -> str:
    if not token:
        print("[PYBUILD][github] GitHub token not available, skipping release creation")
        return ""
    if not repository:
        raise RuntimeError("GitHub repository is not specified")
    if not version:
        raise RuntimeError("Release version is not specified")
    tag = version
    if release_exists(token, repository, tag):
        print(f"[PYBUILD][github] release {tag} already exists; uploading assets")
        upload_assets(token, repository, tag, zip_files)
        return f"https://github.com/{repository}/releases/tag/{tag}"
    notes = build_release_notes(version)
    result = _gh(["release", "create", tag, "--repo", repository, "--title", f"Release {version.lstrip('v')}", "--notes", notes, *map(str, zip_files)], token=token, capture=True)
    if result.returncode != 0:
        raise RuntimeError(f"gh release create failed: {result.stderr.strip()}")
    return result.stdout.strip().splitlines()[-1] if result.stdout.strip() else f"https://github.com/{repository}/releases/tag/{tag}"
