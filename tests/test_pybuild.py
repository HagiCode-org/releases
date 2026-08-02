from __future__ import annotations

import contextlib
import io
import os
import subprocess
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))


class PyBuildTests(unittest.TestCase):
    def test_legacy_target_and_parameter_mapping(self):
        from pybuild.entry import parse_build_args, invoke_command
        from pybuild.runtime import BuildRuntime

        parsed = parse_build_args(["DockerRelease", "--ReleaseVersion", "1.2.3", "--DockerPlatform", "all"])
        self.assertEqual(parsed.target, "DockerRelease")
        self.assertIn("--release-version", parsed.passthrough)
        self.assertIn("--docker-platform", parsed.passthrough)
        runtime = BuildRuntime(ROOT)
        runtime.ensure_venv = lambda: Path(sys.executable)  # type: ignore[method-assign]
        runtime.install_locked_requirements = lambda: None  # type: ignore[method-assign]
        command = invoke_command(runtime, parsed)
        self.assertEqual(command[5], "docker-release")

    def test_target_option_forms_and_nuke_rejection(self):
        from pybuild.entry import _reject_nuke, parse_build_args

        self.assertEqual(parse_build_args(["--target", "VersionMonitor"]).target, "VersionMonitor")
        self.assertEqual(parse_build_args(["--target=VersionMonitor"]).target, "VersionMonitor")
        with self.assertRaisesRegex(RuntimeError, "Nuke orchestration has been removed"):
            _reject_nuke("nuke")

    def test_nugex_alias_compatibility(self):
        from pybuild.config import effective_release_version, env_value

        saved = {name: os.environ.get(name) for name in ("NUGEX_ReleaseVersion", "NUGEX_DockerPlatform", "RELEASE_VERSION", "DOCKER_PLATFORM")}
        for name in saved:
            os.environ.pop(name, None)
        os.environ["RELEASE_VERSION"] = "2.0.0"
        os.environ["DOCKER_PLATFORM"] = "linux-arm64"
        try:
            self.assertEqual(effective_release_version(ROOT), "2.0.0")
            self.assertEqual(env_value("DockerPlatform"), "linux-arm64")
            os.environ["NUGEX_ReleaseVersion"] = "2.1.0"
            self.assertEqual(effective_release_version(ROOT), "2.1.0")
        finally:
            for name, value in saved.items():
                if value is None:
                    os.environ.pop(name, None)
                else:
                    os.environ[name] = value

    def test_release_source_defaults(self):
        from pybuild.config import release_source

        saved = {name: os.environ.get(name) for name in ("NUGEX_ReleasePackageIndexUrl", "NUGEX_ReleasePackageBaseUrl", "RELEASE_PACKAGE_INDEX_URL", "RELEASE_PACKAGE_BASE_URL")}
        for name in saved:
            os.environ.pop(name, None)
        try:
            self.assertEqual(release_source(ROOT), ("https://dl-server.hagicode.com/", "https://dl-server.hagicode.com/"))
        finally:
            for name, value in saved.items():
                if value is None:
                    os.environ.pop(name, None)
                else:
                    os.environ[name] = value

    def test_version_monitor_plan_and_sorting(self):
        from pybuild.versioning import create_plan, sort_descending

        self.assertEqual(sort_descending(["0.1.0-beta.9", "0.1.0-beta.36"])[0], "0.1.0-beta.36")
        plan = create_plan(["1.0.0", "1.1.0", "bad version"], ["v1.0.0"])
        self.assertEqual(plan.selected_version, "1.1.0")
        self.assertEqual(plan.deferred_versions, [])
        self.assertEqual(plan.ignored_versions, ["bad version"])

    def test_release_source_index_parsing_and_selection(self):
        from pybuild.release_source import parse_index, select_package_assets

        index = parse_index({
            "versions": [
                {"version": "1.2.3", "files": [
                    {"path": "hagicode-1.2.3-linux-x64.zip"},
                    {"path": "hagicode-1.2.3-linux-arm64.zip"},
                    {"path": "hagicode-1.2.3.json"},
                ]}
            ]
        })
        selected = select_package_assets(index, "1.2.3", ["linux-x64"])
        self.assertEqual([asset.path for _, asset in selected], ["hagicode-1.2.3-linux-x64.zip"])

    def test_release_source_index_url_normalization(self):
        from pybuild.release_source import _normalize_index_url

        self.assertEqual(_normalize_index_url("https://dl-server.hagicode.com/"), "https://dl-server.hagicode.com/index.json")
        self.assertEqual(_normalize_index_url("https://dl-server.hagicode.com/index.json"), "https://dl-server.hagicode.com/index.json")


    def test_github_release_notes_and_docker_dry_run(self):
        import tempfile
        from pybuild.docker_ops import DockerImageInfo, docker_buildx_push
        from pybuild.github_ops import build_release_notes

        self.assertIn("Release 1.2.3", build_release_notes("1.2.3"))
        with tempfile.TemporaryDirectory() as tmp:
            context_dir = Path(tmp)
            (context_dir / "Dockerfile").write_text("FROM scratch\n", encoding="utf-8")
            out = io.StringIO()
            with contextlib.redirect_stdout(out):
                docker_buildx_push(DockerImageInfo("docker.io", "hagicode", "hagicode"), version="1.2.3", platforms=["linux/amd64"], context_dir=context_dir, dry_run=True)
            self.assertIn("[dry-run]", out.getvalue())

    def test_wrapper_rejects_nuke_engine(self):
        result = subprocess.run([str(ROOT / "build.sh"), "VersionMonitor"], cwd=ROOT, env={**os.environ, "BUILD_ENGINE": "nuke"}, text=True, capture_output=True, check=False)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("Nuke build engine is no longer supported", result.stderr)


if __name__ == "__main__":
    unittest.main()
