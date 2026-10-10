#!/usr/bin/env python3
"""Install only from a local feed and test the actual packaged CLI with a fresh DB."""
import argparse
import json
import os
import platform
from pathlib import Path
import signal
import subprocess
import tempfile
import time
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("feed", type=Path)
    parser.add_argument("--version", required=True)
    parser.add_argument("--evidence", required=True, type=Path)
    args = parser.parse_args()
    out = args.evidence.resolve()
    out.mkdir(parents=True, exist_ok=False)
    results = []
    with tempfile.TemporaryDirectory(prefix="uographs-package-") as directory:
        temporary = Path(directory)
        config = temporary / "NuGet.Config"
        configuration = ET.Element("configuration")
        sources = ET.SubElement(configuration, "packageSources")
        ET.SubElement(sources, "clear")
        ET.SubElement(sources, "add", key="local", value=str(args.feed.resolve()))
        ET.ElementTree(configuration).write(config)
        env = dict(os.environ, DOTNET_CLI_HOME=str(temporary / "home"),
                   NUGET_PACKAGES=str(temporary / "packages"), DOTNET_CLI_TELEMETRY_OPTOUT="1")

        def run(name, argv, expected=0):
            print(f"Running packaged CLI check: {name}", flush=True)
            start = time.monotonic()
            with (out / f"{name}.stdout").open("x") as stdout, (out / f"{name}.stderr").open("x") as stderr:
                process = subprocess.Popen(argv, cwd=ROOT, env=env, stdout=stdout, stderr=stderr,
                                           start_new_session=os.name == "posix")
                try:
                    code = process.wait(timeout=180)
                except subprocess.TimeoutExpired:
                    if os.name == "posix":
                        os.killpg(process.pid, signal.SIGKILL)
                    else:
                        subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"], check=False)
                    process.wait()
                    code = "TIMEOUT"
            raw = (out / f"{name}.stdout").read_bytes()
            text = raw.decode("utf-8")
            try:
                document = json.loads(text)
            except json.JSONDecodeError:
                document = {}
            results.append({"name": name, "argv": argv, "exit": code, "expected": expected,
                            "seconds": time.monotonic() - start, "utf8Bytes": len(raw),
                            "unicodeScalars": len(text),
                            "snapshot": document.get("snapshotId", document.get("analysis", {}).get("id")),
                            "platform": platform.platform(), "cwd": str(ROOT)})
            (out / "commands.json").write_text(json.dumps(results, indent=2))
            if code != expected:
                raise RuntimeError(f"{name}: expected {expected}, got {code}; see {out / (name + '.stderr')}")

        run("install", ["dotnet", "tool", "install", "UO.Graphs.Cli", "--version", args.version,
                        "--tool-path", str(temporary / "bin"), "--configfile", str(config), "--no-cache"])
        tool = str(temporary / "bin" / ("uographs.exe" if os.name == "nt" else "uographs"))
        database = out / "smoke.db"
        common = ["--db", str(database)]
        run("help", [tool, "--help"])
        if not (out / "help.stdout").read_text().startswith(f"UO.Graphs {args.version} —"):
            raise RuntimeError("Installed CLI help does not match the package version")
        run("doctor", [tool, "doctor"])
        run("trust-denied", [tool, "index", "samples/HelloGraph/HelloGraph.slnx", *common], expected=3)
        if database.exists():
            raise RuntimeError("Trust rejection created a database")
        run("index", [tool, "index", "samples/HelloGraph/HelloGraph.slnx", "--trust-project", "--full", *common])
        run("query", [tool, "query", "find", "Greet", *common])
        run("context", [tool, "context", "HelloGraph.Domain.Greeter.Greet(string)", *common])
        run("callers", [tool, "query", "callers", "HelloGraph.Domain.Greeter.Greet(string)", *common])
        run("callees", [tool, "query", "callees", "HelloGraph.App.WelcomeService.Welcome(string)", *common])
        run("status", [tool, "status", *common])
        for name in ("query", "callers", "callees", "status"):
            text = (out / f"{name}.stdout").read_text()
            data = json.loads(text)
            version = data["contractVersion"] if name == "status" else data["schemaVersion"]
            if version != 5 or data["budget"]["actualChars"] != len(text) or len(text) > 12000:
                raise RuntimeError(f"Packaged {name} violated the v5 default budget")
        for name in ("callers", "callees"):
            if not json.loads((out / f"{name}.stdout").read_text())["items"]:
                raise RuntimeError(f"Packaged {name} lost HelloGraph relationships")
        index = json.loads((out / "index.stdout").read_text())
        query = json.loads((out / "query.stdout").read_text())
        context = json.loads((out / "context.stdout").read_text())
        if index["entityCount"] != 15 or not query["items"] or context["selected"] is None:
            raise RuntimeError("Packaged index/query/context returned no expected HelloGraph evidence")
        if index["analysis"]["toolVersion"] != args.version:
            raise RuntimeError("Graph toolVersion does not match the package version")
    print("PASS: local-feed install, help, doctor, trust rejection, fresh index, query/callers/callees/context/status")


if __name__ == "__main__":
    main()
