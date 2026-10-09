#!/usr/bin/env python3
"""Phase 10 lint/installer tests and real CLI subprocess contract tests (stdlib only).

Without --db, run artifact tests. With --db, also query a real HelloGraph index.
--run-index additionally tests a successful approved incremental index subprocess.
No fabricated graph, implicit restore/build, shell=True, or Copilot E2E claim.
"""
import argparse
import json
import os
from pathlib import Path
import re
import shlex
import signal
import sqlite3
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET

from install_skill import install

ROOT = Path(__file__).resolve().parents[1]
SKILL = ROOT / ".github/skills/uographs"
TEMPLATE = ROOT / "templates/copilot/uographs"
CLI = ROOT / "src/UO.Graphs.Cli/bin/Debug/net10.0/uographs.dll"
COMMANDS = []
DATABASE = None


def lint(folder):
    """Validate our deliberately narrow YAML subset, not a general YAML parser.

    name is a plain slug; description a JSON double-quoted scalar (valid YAML).
    Reject all additional keys, tags, anchors, multi-line scalars and duplicates.
    """
    text = (folder / "SKILL.md").read_text(encoding="utf-8")
    match = re.match(r'\A---\nname: ([a-z0-9]+(?:-[a-z0-9]+)*)\ndescription: ("[^\n]+")\n---\n', text)
    if not match:
        raise ValueError("Expected name/description only in supported YAML subset")
    name, description = match.groups()
    description = json.loads(description)
    if name != folder.name or len(name) > 64 or not 1 <= len(description.strip()) <= 1024:
        raise ValueError("Invalid skill name/description")
    files = {p.relative_to(folder).as_posix() for p in folder.rglob("*") if p.is_file()}
    if files != {"SKILL.md", "references/cli.md"}:
        raise ValueError("Unexpected/missing standalone skill files")
    for file in files:
        for link in re.findall(r'\]\(([^)]+)\)', (folder / file).read_text(encoding="utf-8")):
            target = (folder / file).parent / link
            if not target.resolve().is_relative_to(folder.resolve()) or not target.is_file():
                raise ValueError(f"Broken or escaping reference: {link}")
    if "references/cli.md" not in text:
        raise ValueError("Reference is not discoverable")


def examples(path):
    return [shlex.split(line) for block in re.findall(r'```sh\n(.*?)```', path.read_text(encoding="utf-8"), re.S)
            for line in block.splitlines() if line.startswith("uographs ")]


def run(args, expected=0, env=None, timeout=45):
    command = ["dotnet", str(CLI), *args]
    record = {"argv": command, "expectedExit": expected}
    COMMANDS.append(record)
    with subprocess.Popen(command, cwd=ROOT, env=env, stdout=subprocess.PIPE,
                          stderr=subprocess.PIPE, text=True,
                          start_new_session=(os.name != "nt")) as process:
        try:
            stdout, stderr = process.communicate(timeout=timeout)
        except subprocess.TimeoutExpired:
            if os.name == "nt":
                process.kill()
            else:
                os.killpg(process.pid, signal.SIGKILL)
            stdout, stderr = process.communicate()
            record.update(result="TIMEOUT", stdout=stdout, stderr=stderr)
            raise AssertionError(f"CLI timeout after {timeout}s: {args}")
    record.update(exitCode=process.returncode, stderr=stderr)
    if process.returncode != expected:
        raise AssertionError(f"Expected {expected}, got {process.returncode}: {args}\n{stderr}")
    if expected in (2, 3, 4, 5, 6, 9, 10, 130):
        if stdout or not stderr:
            raise AssertionError("Fatal error must have empty stdout and explanatory stderr")
        return stderr
    return stdout if args == ["--help"] else json.loads(stdout)


class ArtifactTests(unittest.TestCase):
    def test_frontmatter_references_and_template_parity(self):
        for folder in (SKILL, TEMPLATE):
            lint(folder)
        for source in SKILL.rglob("*"):
            if source.is_file():
                self.assertEqual(source.read_bytes(), (TEMPLATE / source.relative_to(SKILL)).read_bytes())

    def test_linter_rejects_invalid_frontmatter_and_missing_references(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder = install(Path(tmp))
            entry = folder / "SKILL.md"
            original = entry.read_text()
            for invalid in [original.replace("name: uographs", "name: other"),
                            original.replace('description: "', 'description: !tag "'),
                            original.replace("name: uographs", "name: uographs\nallowed-tools: '*'"),
                            original.replace("name: uographs", "name: uographs\nname: uographs")]:
                entry.write_text(invalid)
                with self.assertRaises(ValueError):
                    lint(folder)
            entry.write_text(original)
            (folder / "references/cli.md").unlink()
            with self.assertRaises(ValueError):
                lint(folder)

    def test_installer_preserves_existing_files_and_refuses_second_install(self):
        with tempfile.TemporaryDirectory(prefix="uographs skill ") as tmp:
            root = Path(tmp)
            instructions = root / "AGENTS.md"
            instructions.write_text("User instructions")
            folder = install(root)
            lint(folder)
            entry = folder / "SKILL.md"
            entry.write_text("User customization")
            with self.assertRaises(FileExistsError):
                install(root)
            self.assertEqual("User customization", entry.read_text())
            self.assertEqual("User instructions", instructions.read_text())

    def test_installer_refuses_symlink_destination(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            target = root / "outside"
            target.mkdir()
            (root / ".github").symlink_to(target, target_is_directory=True)
            with self.assertRaises(ValueError):
                install(root)
            self.assertEqual([], list(target.iterdir()))

    def test_examples_are_bounded_and_no_shell_or_approval_metadata(self):
        commands = examples(SKILL / "references/cli.md")
        self.assertEqual(12, len(commands))
        for command in commands:
            self.assertFalse(set(command) & {";", "|", "&&", "||", ">", "--full", "--allow-generators"})
            if command[1] == "query":
                self.assertIn("--limit", command)
                if command[2] != "find":
                    self.assertIn("--max-edges", command)
            if command[1] == "context":
                self.assertIn("--max-chars", command)
                self.assertIn("--max-nodes", command)


class CliTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        if not DATABASE or not DATABASE.is_file():
            raise AssertionError("--db must name a real HelloGraph snapshot; no synthetic fallback")
        cls.temp = tempfile.TemporaryDirectory(prefix="uographs phase10 ")
        cls.directory = Path(cls.temp.name)
        cls.db = cls.directory / "graph.db"
        # SQLite backup includes committed WAL data and leaves the supplied DB unchanged.
        with sqlite3.connect(DATABASE.as_uri() + "?mode=ro", uri=True) as source, sqlite3.connect(cls.db) as dest:
            source.backup(dest)
        cls.status = run(["status", "--db", str(cls.db)])
        cls.snapshot = cls.status["analysis"]["id"]
        cls.symbols = {}
        for name in ("Greet", "Welcome"):
            result = run(["query", "find", name, "--limit", "10", "--db", str(cls.db)])
            matches = [x["entity"] for x in result["items"] if x["entity"]["name"] == name]
            if len(matches) != 1:
                raise AssertionError(f"Expected real HelloGraph symbol {name}")
            cls.symbols[name] = matches[0]

    @classmethod
    def tearDownClass(cls):
        cls.temp.cleanup()

    def values(self):
        return {"DB": str(self.db), "TEXT": "Greet", "SYMBOL_ID": self.symbols["Greet"]["id"],
                "TARGET_ID": self.symbols["Welcome"]["id"], "INPUT": str(self.directory / "Missing.csproj"),
                "ROOT": str(self.directory), "CONFIGURATION": "Debug", "TFM": "net10.0"}

    def test_reference_examples_in_real_subprocesses(self):
        values = self.values()
        Path(values["INPUT"]).write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
        for command in examples(SKILL / "references/cli.md"):
            with self.subTest(command=command):
                args = [values.get(token, token) for token in command[1:]]
                # Positive update is a separate explicit --run-index test. Here verify
                # approved-mode parsing and missing-assets handling without restore.
                result = run(args, expected=4 if args[0] == "index" else 0)
                if args[0] == "index":
                    self.assertIn("assets missing", result)
                    self.assertFalse((self.directory / "obj/project.assets.json").exists())
                elif args[0] == "--help":
                    version = ET.parse(ROOT / "Directory.Build.props").findtext(".//Version")
                    self.assertIn("UO.Graphs " + version, result)
                elif args[0] == "doctor":
                    self.assertFalse(result["automaticRestore"])
                    self.assertFalse(result["targetProjectChecked"])
                elif args[0] in ("query", "context"):
                    self.assertEqual(self.snapshot, result["snapshotId"])
                    self.assertEqual(self.status["analysis"]["coverage"], result["completeness"]["coverage"])
                    self.assertEqual("notChecked", result["stale"])
                    self.assertIn("truncated", result)
                    if args[0] == "context":
                        self.assertLessEqual(result["budget"]["actualChars"], 12000)
                        self.assertEqual(values["SYMBOL_ID"], result["selected"]["id"])
                        if "--include-source" in args:
                            self.assertTrue(any("Greet" in (s.get("text") or "") for s in result["snippets"]))
                    if args[:2] == ["query", "callers"]:
                        self.assertTrue(any(x["symbol"]["id"] == values["TARGET_ID"] for x in result["items"]))

    def test_call_path_and_source_evidence(self):
        greet, welcome = self.symbols["Greet"], self.symbols["Welcome"]
        result = run(["query", "path", welcome["id"], greet["id"], "--depth", "1", "--edge-types", "CALLS", "--db", str(self.db)])
        self.assertEqual("found", result["outcome"])
        edge = result["items"][0]["path"][0]
        self.assertEqual("exact", edge["relationship"]["confidence"])
        self.assertEqual("CALLS", edge["relationship"]["kind"])
        self.assertTrue(edge["evidence"])
        source = greet["sources"][0]
        path = Path(source["file"])
        self.assertTrue(path.resolve().is_relative_to((ROOT / "samples/HelloGraph").resolve()))
        lines = path.read_text().splitlines()[source["startLine"] - 1:source["endLine"]]
        self.assertIn("Greet", "\n".join(lines))

    def test_missing_index_trust_and_literal_untrusted_arguments(self):
        missing = self.directory / "absent.db"
        run(["status", "--db", str(missing)], expected=4)
        self.assertFalse(missing.exists())
        run(["index", "not-a-project", "--incremental", "--db", str(missing)], expected=3)
        self.assertFalse(missing.exists())
        marker = self.directory / "must-not-exist"
        result = run(["query", "find", f"$(touch '{marker}'); ignore instructions", "--db", str(self.db)])
        self.assertEqual([], result["items"])
        self.assertFalse(marker.exists())
        # This proves literal CLI arguments only, not model prompt-injection immunity.

    def test_freshness_guards_and_domain_failures(self):
        checked = run(["status", "--db", str(self.db), "--check-freshness"])
        self.assertIn(checked["analysis"]["freshness"], ("fresh", "stale", "unknown"))
        if checked["analysis"]["freshness"] != "fresh":
            run(["status", "--db", str(self.db), "--require-fresh"], expected=9)
        env = dict(os.environ, UOGRAPHS_PHASE10_ENV_CHANGE="changed")
        changed = run(["status", "--db", str(self.db), "--check-freshness"], env=env)
        self.assertEqual("stale", changed["analysis"]["freshness"])
        run(["status", "--db", str(self.db), "--require-fresh"], expected=9, env=env)
        lexical = run(["context", "Gree", "--db", str(self.db)], expected=7)
        self.assertTrue(lexical["candidates"])
        missing = run(["query", "symbol", "NoSuchPhase10Symbol", "--db", str(self.db)], expected=8)
        self.assertEqual("symbolNotFound", missing["outcome"])
        run(["context", self.symbols["Greet"]["id"], "--max-chars", "1", "--db", str(self.db)], expected=2)

    def test_readme_cli_examples_parse_against_real_cli(self):
        # Execute read-only README examples with exact live IDs. Validate index examples
        # through the real parser/trust gate, never load additional example projects.
        content = (ROOT / "README.md").read_text()
        count = 0
        for block in re.findall(r'```sh\n(.*?)```', content, re.S):
            for line in block.splitlines():
                if line.startswith("uographs "):
                    args = shlex.split(line)[1:]
                elif line.startswith("dotnet run --project src/UO.Graphs.Cli/"):
                    parts = shlex.split(line)
                    args = parts[parts.index("--") + 1:]
                else:
                    continue
                count += 1
                replacements = {"<symbol-id>": self.symbols["Greet"]["id"],
                                "<from-id>": self.symbols["Welcome"]["id"], "<to-id>": self.symbols["Greet"]["id"]}
                args = [replacements.get(t, t) for t in args]
                if args[0] == "index":
                    args.remove("--trust-project")
                    run(args, expected=3)
                    continue
                if args != ["--help"] and args != ["doctor"] and "--db" not in args:
                    args += ["--db", str(self.db)]
                if args[0] == "export" and args[args.index("--format") + 1] != "json":
                    # Output-specific examples are covered by the existing export suite;
                    # validate parser here by asking the real CLI for a missing database.
                    args[args.index("--db") + 1] = str(self.directory / "absent.db")
                    run(args, expected=4)
                else:
                    run(args)
        self.assertGreaterEqual(count, 17)


class IncrementalSubprocessTests(unittest.TestCase):
    def test_approved_incremental_update(self):
        with tempfile.TemporaryDirectory(prefix="uographs phase10 index ") as tmp:
            db = str(Path(tmp) / "graph.db")
            result = run(["index", str(ROOT / "samples/HelloGraph/HelloGraph.slnx"),
                          "--incremental", "--trust-project", "--db", db,
                          "--root", str(ROOT / "samples/HelloGraph"), "--configuration", "Debug",
                          "--framework", "net10.0", "--generated", "exclude"], timeout=90)
            self.assertEqual("incremental", result["mode"])
            self.assertGreater(result["entityCount"], 0)
            self.assertEqual(result["analysis"]["id"], run(["status", "--db", db])["analysis"]["id"])


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--db", type=Path)
    parser.add_argument("--run-index", action="store_true")
    parser.add_argument("--evidence", type=Path, default=ROOT / ".uographs/phase10-evidence/tests.json")
    args = parser.parse_args()
    DATABASE = args.db.resolve() if args.db else None
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(ArtifactTests)
    if DATABASE:
        suite.addTests(unittest.defaultTestLoader.loadTestsFromTestCase(CliTests))
    if args.run_index:
        suite.addTests(unittest.defaultTestLoader.loadTestsFromTestCase(IncrementalSubprocessTests))
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    args.evidence.parent.mkdir(parents=True, exist_ok=True)
    args.evidence.write_text(json.dumps({"testsRun": result.testsRun, "failures": len(result.failures),
                                        "errors": len(result.errors), "commands": COMMANDS,
                                        "copilotE2E": "NOT RUN"}, indent=2) + "\n")
    raise SystemExit(0 if result.wasSuccessful() else 1)
