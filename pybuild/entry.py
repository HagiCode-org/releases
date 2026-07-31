from __future__ import annotations

import argparse
import os
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

from .runtime import BuildRuntime
from .targets import resolve_invoke_task

REPO_ROOT = Path(__file__).resolve().parents[1]

PARAM_ALIASES = {
    "releaseversion": "release-version",
    "dockerplatform": "docker-platform",
    "dryrun": "dry-run",
    "listonly": "list-only",
    "azureblobsasurl": "azure-blob-sas-url",
    "releasepackageindexurl": "release-package-index-url",
    "releasepackagebaseurl": "release-package-base-url",
    "githubtoken": "github-token",
    "githubrepository": "github-repository",
    "downloadplatforms": "download-platforms",
}


@dataclass
class ParsedArgs:
    target: str
    engine: str
    passthrough: list[str]


def _kebab(name: str) -> str:
    compact = name.strip().lstrip("-")
    if not compact:
        return compact
    key = compact.replace("-", "").replace("_", "").lower()
    if key in PARAM_ALIASES:
        return PARAM_ALIASES[key]
    return re.sub(r"(?<!^)([A-Z])", r"-\1", compact).replace("_", "-").lower()


def _normalize_option(token: str) -> str:
    if not token.startswith("--"):
        return token
    if "=" in token:
        key, value = token[2:].split("=", 1)
        return f"--{_kebab(key)}={value}"
    return f"--{_kebab(token[2:])}"


def parse_build_args(args: Iterable[str]) -> ParsedArgs:
    tokens = list(args)
    target: str | None = None
    engine = os.environ.get("BUILD_ENGINE", "invoke") or "invoke"
    passthrough: list[str] = []
    i = 0
    while i < len(tokens):
        token = tokens[i]
        if token in {"--target", "-target"} and i + 1 < len(tokens):
            i += 1
            target = tokens[i]
        elif token.startswith("--target="):
            target = token.split("=", 1)[1]
        elif token == "--engine" and i + 1 < len(tokens):
            i += 1
            engine = tokens[i]
        elif token.startswith("--engine="):
            engine = token.split("=", 1)[1]
        elif token.startswith("--"):
            passthrough.append(_normalize_option(token))
        elif target is None:
            target = token
        else:
            passthrough.append(token)
        i += 1
    return ParsedArgs(target=target or "ConfigurationValidate", engine=engine.lower(), passthrough=passthrough)


def _reject_nuke(engine: str) -> None:
    if engine == "nuke":
        raise RuntimeError("Nuke orchestration has been removed for repos/hagicode-release; use PyBuild/Invoke.")
    if engine not in {"invoke", "pybuild"}:
        raise RuntimeError(f"Unsupported build engine '{engine}'. Supported engine: invoke")


def invoke_command(runtime: BuildRuntime, parsed: ParsedArgs) -> list[str]:
    task_name = resolve_invoke_task(parsed.target)
    python = runtime.ensure_venv()
    runtime.install_locked_requirements()
    return [str(python), "-m", "invoke", "-c", "tasks", task_name, *parsed.passthrough]


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="hagicode-release PyBuild entry")
    parser.add_argument("args", nargs=argparse.REMAINDER)
    ns = parser.parse_args(argv)
    parsed = parse_build_args(ns.args)
    try:
        _reject_nuke(parsed.engine)
        runtime = BuildRuntime(REPO_ROOT)
        command = invoke_command(runtime, parsed)
        print(f"[PYBUILD] using PyBuild/Invoke target {parsed.target}")
        return runtime.run(command).exit_code
    except Exception as exc:
        print(f"[PYBUILD][error] {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
