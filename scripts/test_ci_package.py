#!/usr/bin/env python3
"""Negative release-gate tests; synthetic ZIPs are not real package acceptance."""
from pathlib import Path
import contextlib
import io
import json
import os
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile
from verify_ci_package import REQUIRED, TOOL, REPOSITORY, main, read_version, verify

COMMIT = "a" * 40


class PackageGateTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def package(self, version="0.9.1", omit=None, extra=None):
        files = {name: b"fixture" for name in REQUIRED}
        for rid in ("linux-x64", "win-x64", "osx-x64", "osx-arm64"):
            files[TOOL + f"runtimes/{rid}/native/sqlite"] = b"fixture"
        files[TOOL + "DotnetToolSettings.xml"] = b'<DotNetCliTool><Commands><Command Name="uographs" EntryPoint="uographs.dll" /></Commands></DotNetCliTool>'
        files["UO.Graphs.Cli.nuspec"] = f'''<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"><metadata>
          <id>UO.Graphs.Cli</id><version>{version}</version><license type="expression">MIT</license>
          <packageTypes><packageType name="DotnetTool" /></packageTypes>
          <repository url="{REPOSITORY}" commit="{COMMIT}" /></metadata></package>'''.encode()
        if omit:
            del files[omit]
        files.update(extra or {})
        with zipfile.ZipFile(self.root / "UO.Graphs.Cli.0.9.1.nupkg", "w") as archive:
            for name, content in files.items():
                archive.writestr(name, content)

    def test_version_is_read_unchanged_from_props_regardless_of_ci_run(self):
        props = self.root / "Directory.Build.props"
        for version in ("0.9.0", "1.2.7"):
            props.write_text(f"<Project><PropertyGroup><Version>{version}</Version></PropertyGroup></Project>")
            for run in ("1", "42"):
                output = io.StringIO()
                with patch("verify_ci_package.ROOT", self.root), patch.dict(os.environ, {"GITHUB_RUN_NUMBER": run}), \
                     patch.object(sys, "argv", ["verify_ci_package.py", "--print-version"]), contextlib.redirect_stdout(output):
                    main()
                self.assertEqual(version + "\n", output.getvalue())

    def test_invalid_or_missing_props_version_is_rejected(self):
        props = self.root / "Directory.Build.props"
        for version in ("0.9.0-preview", "", "01.0.0"):
            props.write_text(f"<Project><PropertyGroup><Version>{version}</Version></PropertyGroup></Project>")
            with self.assertRaises(ValueError):
                read_version(props)
        props.write_text("<Project />")
        with self.assertRaises(ValueError):
            read_version(props)

    def test_valid_layout_and_dynamic_nuspec_namespace(self):
        self.package()
        result = verify(self.root, "0.9.1", COMMIT)
        self.assertEqual("0.9.1", result["version"])
        self.assertEqual(64, len(result["sha256"]))

    def test_wrong_nuspec_version_is_rejected(self):
        self.package(version="0.9.2")
        with self.assertRaisesRegex(ValueError, "Wrong package version"):
            verify(self.root, "0.9.1", COMMIT)

    def test_wrong_source_commit_is_rejected(self):
        self.package()
        with self.assertRaisesRegex(ValueError, "repository"):
            verify(self.root, "0.9.1", "b" * 40)

    def test_missing_buildhost_is_rejected(self):
        self.package(omit=TOOL + "BuildHost-netcore/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll")
        with self.assertRaisesRegex(ValueError, "Missing payload"):
            verify(self.root, "0.9.1", COMMIT)

    def test_fixture_database_and_traversal_are_rejected(self):
        for extra in (TOOL + "Volo.Abp.Core.dll", "graph.db", "../secret.txt"):
            with self.subTest(extra=extra):
                self.package(extra={extra: b"fixture"})
                with self.assertRaises(ValueError):
                    verify(self.root, "0.9.1", COMMIT)

    def test_multiple_packages_are_rejected(self):
        self.package()
        (self.root / "Other.1.0.0.nupkg").write_bytes(b"unexpected")
        with self.assertRaisesRegex(ValueError, "exactly one"):
            verify(self.root, "0.9.1", COMMIT)

    def test_artifact_hash_tampering_is_rejected_before_publish(self):
        self.package()
        manifest = verify(self.root, "0.9.1", COMMIT)
        path = self.root / "manifest.json"
        path.write_text(json.dumps(manifest))
        argv = ["verify_ci_package.py", str(self.root), "--version", "0.9.1",
                "--commit", COMMIT, "--verify-manifest"]
        with patch.object(sys, "argv", argv), contextlib.redirect_stdout(io.StringIO()):
            main()
            # Content remains a valid tool package but no longer matches the CI artifact digest.
            with zipfile.ZipFile(self.root / manifest["package"], "a") as archive:
                archive.writestr("extra.txt", "changed after verification")
            with self.assertRaisesRegex(ValueError, "manifest/hash/source mismatch"):
                main()


if __name__ == "__main__":
    unittest.main()
