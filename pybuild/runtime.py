from __future__ import annotations

import hashlib
import os
import shutil
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Mapping, Sequence


@dataclass
class BuildResult:
    command: Sequence[str]
    exit_code: int


class BuildRuntime:
    def __init__(self, repo_root: Path, venv_dir: Path | None = None, lock_file: Path | None = None) -> None:
        self.repo_root = repo_root
        self.venv_dir = venv_dir or (repo_root / ".venv")
        self.lock_file = lock_file or (repo_root / "requirements.lock.txt")
        self.log_prefix = "[PYBUILD]"

    def _venv_python(self) -> Path:
        return self.venv_dir / ("Scripts/python.exe" if os.name == "nt" else "bin/python")

    def _resolve_system_python(self) -> str:
        for candidate in (os.environ.get("PYTHON_EXE"), "python3", "python"):
            if candidate and shutil.which(candidate):
                return shutil.which(candidate) or candidate
        raise RuntimeError("Python executable not found. Install python3 or set PYTHON_EXE.")

    def ensure_venv(self) -> Path:
        python = self._venv_python()
        if python.exists():
            return python
        self.venv_dir.parent.mkdir(parents=True, exist_ok=True)
        print(f"{self.log_prefix}[setup] creating virtualenv at {self.venv_dir}")
        subprocess.check_call([self._resolve_system_python(), "-m", "venv", str(self.venv_dir)], cwd=self.repo_root)
        return python

    def _lock_hash(self) -> str:
        return hashlib.sha256(self.lock_file.read_bytes()).hexdigest()

    def install_locked_requirements(self) -> None:
        if not self.lock_file.exists():
            raise RuntimeError(f"Missing lock file: {self.lock_file}")
        python = self.ensure_venv()
        stamp = self.venv_dir / ".requirements.lock.sha256"
        digest = self._lock_hash()
        if stamp.exists() and stamp.read_text(encoding="utf-8") == digest:
            return
        print(f"{self.log_prefix}[setup] installing locked requirements")
        subprocess.check_call([str(python), "-m", "pip", "install", "--disable-pip-version-check", "-r", str(self.lock_file)], cwd=self.repo_root)
        stamp.write_text(digest, encoding="utf-8")

    def run(self, command: Sequence[str], *, env: Mapping[str, str] | None = None, input_text: str | None = None, check: bool = False) -> BuildResult:
        merged_env = os.environ.copy()
        if env:
            merged_env.update(env)
        print(f"{self.log_prefix}[run] {' '.join(map(str, command))}")
        completed = subprocess.run(list(map(str, command)), cwd=self.repo_root, env=merged_env, input=input_text, text=True, check=False)
        if check and completed.returncode != 0:
            raise subprocess.CalledProcessError(completed.returncode, command)
        return BuildResult(command=command, exit_code=completed.returncode)


def normalize_bool(value: str | bool | None) -> bool:
    if isinstance(value, bool):
        return value
    if value is None:
        return False
    return str(value).strip().lower() in {"1", "true", "yes", "on"}


def write_github_output(name: str, value: str) -> None:
    path = os.environ.get("GITHUB_OUTPUT")
    if path:
        with open(path, "a", encoding="utf-8") as fh:
            fh.write(f"{name}={value}\n")
