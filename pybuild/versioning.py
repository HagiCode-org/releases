from __future__ import annotations

import re
from dataclasses import dataclass
from functools import cmp_to_key


def normalize(version: str | None) -> str:
    value = (version or "").strip()
    return value[1:] if value.startswith(("v", "V")) else value


def _compare_ident(left: str, right: str, *, numeric_is_lower_precedence: bool) -> int:
    left_num = left.isdigit()
    right_num = right.isdigit()
    if left_num and right_num:
        return (int(left) > int(right)) - (int(left) < int(right))
    if left_num != right_num and numeric_is_lower_precedence:
        return -1 if left_num else 1
    return (left > right) - (left < right)


def _split(version: str) -> tuple[str, str]:
    main, _, pre = version.partition("-")
    return main, pre


def compare(left: str | None, right: str | None) -> int:
    l = normalize(left)
    r = normalize(right)
    if not l:
        return 0 if not r else -1
    if not r:
        return 1
    lmain, lpre = _split(l)
    rmain, rpre = _split(r)
    lparts = lmain.split(".")
    rparts = rmain.split(".")
    for i in range(max(len(lparts), len(rparts))):
        part = _compare_ident(lparts[i] if i < len(lparts) else "0", rparts[i] if i < len(rparts) else "0", numeric_is_lower_precedence=False)
        if part:
            return part
    if not lpre and rpre:
        return 1
    if lpre and not rpre:
        return -1
    if not lpre and not rpre:
        return 0
    lidents = [p for p in lpre.split(".") if p]
    ridents = [p for p in rpre.split(".") if p]
    for i in range(max(len(lidents), len(ridents))):
        if i >= len(lidents):
            return -1
        if i >= len(ridents):
            return 1
        part = _compare_ident(lidents[i], ridents[i], numeric_is_lower_precedence=True)
        if part:
            return part
    return 0


def sort_descending(versions: list[str]) -> list[str]:
    return sorted(versions, key=cmp_to_key(compare), reverse=True)


def is_valid_version(version: str) -> bool:
    return bool(re.match(r"^[0-9][A-Za-z0-9._-]*$", version or ""))


def has_published_release(version: str, releases: list[str]) -> bool:
    wanted = normalize(version).lower()
    return any(normalize(release).lower() == wanted for release in releases)


@dataclass(frozen=True)
class VersionMonitorPlan:
    sorted_package_versions: list[str]
    new_versions: list[str]
    ignored_versions: list[str]
    latest_version: str
    selected_version: str
    deferred_versions: list[str]

    @property
    def has_new_versions(self) -> bool:
        return bool(self.new_versions)


def create_plan(package_versions: list[str], github_releases: list[str]) -> VersionMonitorPlan:
    valid: list[str] = []
    ignored: list[str] = []
    for version in package_versions:
        (valid if is_valid_version(version) else ignored).append(version)
    sorted_versions = sort_descending(valid)
    latest = sorted_versions[0] if sorted_versions else ""
    needs_sync = bool(latest) and not has_published_release(latest, github_releases)
    selected = latest if needs_sync else ""
    new_versions = [latest] if needs_sync else []
    deferred = [v for v in sorted_versions[1:] if not has_published_release(v, github_releases)] if needs_sync else []
    return VersionMonitorPlan(sorted_versions, new_versions, ignored, latest, selected, deferred)
