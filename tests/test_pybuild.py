from __future__ import annotations

import contextlib
import io
import os
import subprocess
import sys
import unittest
from pathlib import Path
from unittest import mock

import yaml

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

    def test_version_monitor_does_not_dispatch_aliyun_acr(self):
        from pybuild import tasks

        dispatched = mock.Mock()
        with (
            mock.patch.object(tasks, "download_index", return_value={}),
            mock.patch.object(tasks, "all_versions", return_value=["1.2.3"]),
            mock.patch.object(tasks, "get_releases", return_value=[]),
            mock.patch.object(tasks, "dispatch", dispatched),
            mock.patch.object(tasks, "write_github_output"),
        ):
            tasks.version_monitor.body(
                None,
                dry_run="true",
                release_package_index_url="https://example.invalid/index.json",
                github_token="token",
                github_repository="owner/repo",
            )

        event_types = [call.args[2] for call in dispatched.call_args_list]
        self.assertEqual(event_types, ["version-monitor-release", "version-monitor-docker-dockerhub"])
        self.assertNotIn("version-monitor-docker-aliyun", event_types)

    def test_aliyun_recommendation_and_workflow_are_disabled(self):
        settings = yaml.safe_load((ROOT / ".hagihub" / "settings.yaml").read_text(encoding="utf-8"))
        recommendations = {item["id"]: item for item in settings["action_recommendations"]}
        aliyun = recommendations["docker-build-aliyun-acr"]
        self.assertFalse(aliyun["watch"])
        self.assertFalse(aliyun["include"])
        self.assertIn("disabled", aliyun["reason"])
        self.assertTrue(recommendations["docker-build-dockerhub"]["include"])
        self.assertTrue(recommendations["github-release-workflow"]["include"])

        workflow = yaml.safe_load((ROOT / ".github" / "workflows" / "docker-build-aliyun-acr.yml").read_text(encoding="utf-8"))
        steps = workflow["jobs"]["docker-build-aliyun-acr"]["steps"]
        guard = steps[0]
        self.assertIn("disabled", guard["name"])
        self.assertIn("no longer supported", guard["run"])
        self.assertIn("exit 1", guard["run"])
        self.assertNotIn("uses", guard)
        retained_workflow = "\n".join(str(step) for step in steps[1:])
        self.assertIn("PushToAliyunAcr", retained_workflow)
        self.assertIn("ALIYUN_ACR_USERNAME", retained_workflow)

    def test_historical_aliyun_contract_is_retained_but_task_is_disabled(self):
        from pybuild import tasks
        from pybuild.config import ENV_ALIASES, load_build_config
        from pybuild.targets import resolve_invoke_task

        config = load_build_config(ROOT)
        self.assertTrue(config.aliyun_acr.registry)
        expected_aliases = {
            "AliyunAcrRegistry": "ALIYUN_ACR_REGISTRY",
            "AliyunAcrNamespace": "ALIYUN_ACR_NAMESPACE",
            "AliyunAcrUsername": "ALIYUN_ACR_USERNAME",
            "AliyunAcrPassword": "ALIYUN_ACR_PASSWORD",
        }
        for name, alias in expected_aliases.items():
            self.assertIn(alias, ENV_ALIASES[name])
        self.assertEqual(resolve_invoke_task("PushToAliyunAcr"), "push-to-aliyun-acr")
        with (
            mock.patch.object(tasks, "_docker_release") as docker_release,
            self.assertRaisesRegex(RuntimeError, "disabled and no longer supported"),
        ):
            tasks.push_to_aliyun_acr.body(None)
        docker_release.assert_not_called()
        with (
            mock.patch.object(tasks, "ALIYUN_ACR_PERSONAL_ENABLED", True),
            mock.patch.object(tasks, "_docker_release") as retained_implementation,
        ):
            tasks.push_to_aliyun_acr.body(None, release_version="1.2.3", docker_platform="linux-amd64", dry_run="true")
        retained_implementation.assert_called_once_with(
            "aliyun",
            release_version="1.2.3",
            docker_platform="linux-amd64",
            dry_run="true",
        )

    def test_dockerhub_release_does_not_read_aliyun_credentials(self):
        from pybuild import tasks
        from pybuild.config import BuildConfig

        config = BuildConfig()
        config.aliyun_acr.username = "must-not-be-read"
        config.aliyun_acr.password = "must-not-be-read"
        requested_environment_names: list[str] = []

        def dockerhub_environment(name: str, default: str = "") -> str:
            requested_environment_names.append(name)
            if name.startswith("AliyunAcr"):
                raise AssertionError(f"Docker Hub read disabled ACR setting {name}")
            return default

        with (
            mock.patch.object(tasks, "load_build_config", return_value=config),
            mock.patch.object(tasks, "_release_version", return_value="1.2.3"),
            mock.patch.object(tasks, "resolve_platforms", return_value=["linux/amd64"]),
            mock.patch.object(tasks, "_source", return_value=("index", "base")),
            mock.patch.object(tasks, "downloaded_zip_files", return_value=[ROOT / "package.zip"]),
            mock.patch.object(tasks, "prepare_context"),
            mock.patch.object(tasks, "env_value", side_effect=dockerhub_environment),
            mock.patch.object(tasks, "env_bool", return_value=False),
            mock.patch.object(tasks, "docker_login") as docker_login,
            mock.patch.object(tasks, "docker_buildx_push") as docker_build,
        ):
            tasks.push_to_dockerhub.body(None, release_version="1.2.3", docker_platform="linux-amd64")

        self.assertFalse(any(name.startswith("AliyunAcr") for name in requested_environment_names))
        docker_login.assert_called_once_with("docker.io", "", "")
        image = docker_build.call_args.args[0]
        self.assertEqual((image.registry, image.namespace), ("docker.io", "newbe36524"))
        dockerhub_workflow = (ROOT / ".github" / "workflows" / "docker-build-dockerhub.yml").read_text(encoding="utf-8")
        self.assertNotIn("ALIYUN_ACR_", dockerhub_workflow)
        self.assertNotIn("AliyunAcr", dockerhub_workflow)

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


    def test_release_source_http_request_sets_user_agent(self):
        from pybuild.release_source import DEFAULT_HTTP_USER_AGENT, _http_request

        req = _http_request("https://dl-server.hagicode.com/index.json")
        self.assertEqual(req.get_header("User-agent"), DEFAULT_HTTP_USER_AGENT)

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
