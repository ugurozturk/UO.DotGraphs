#!/usr/bin/env python3
"""Development-only inventory of restored, locked packages; never downloads anything."""
import hashlib
import json
import os
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
CACHE = Path(os.environ.get("NUGET_PACKAGES", Path.home() / ".nuget/packages"))
packages = {}
package_scopes = {}
for lock in sorted(ROOT.glob("**/packages.lock.json")):
    for dependencies in json.loads(lock.read_text())["dependencies"].values():
        for name, dependency in dependencies.items():
            if dependency["type"] == "Project":
                continue
            key = name, dependency["resolved"]
            packages.setdefault(key, set()).add(dependency["type"])
            package_scopes.setdefault(key, set()).add(lock.relative_to(ROOT).as_posix())

rows, notices, notice_hashes = [], [], set()
for (name, version), kinds in sorted(packages.items()):
    assert "-" not in version, f"Prerelease package: {name}/{version}"
    directory = CACHE / name.lower() / version.lower()
    metadata = ET.parse(directory / (name.lower() + ".nuspec")).getroot().find("{*}metadata")
    license_node = metadata.find("{*}license")
    url = f"https://www.nuget.org/packages/{name}/{version}"
    if license_node is not None and license_node.attrib["type"] == "expression":
        license_name = license_node.text
        if license_name == "LGPL-3.0-only":
            assert name.startswith("Volo.Abp.") and version == "10.1.1", (name, version)
            assert package_scopes[(name, version)] == {"samples/Phase9/Abp/packages.lock.json"}, "LGPL allowed only in isolated ABP fixture, never product/tests/runner closure"
            license_name += " (ABP fixture only)"
        else:
            assert license_name in {"MIT", "Apache-2.0"}, (name, version, license_name)
        evidence = f"[nuspec expression]({url}/License)"
    elif name == "MyCSharp.HttpUserAgentParser" and version == "3.0.28":
        assert license_node is not None and license_node.attrib["type"] == "file"
        assert (directory / license_node.text).read_text().startswith("MIT License")
        license_name = "MIT"
        evidence = f"[bundled LICENSE.txt]({url}/License)"
    elif name == "NUglify" and version == "1.21.17":
        assert metadata.findtext("{*}licenseUrl") == "https://github.com/trullock/NUglify/blob/master/license.txt"
        text = (ROOT / "docs/licenses/NUglify-1.21.17.txt").read_text()
        assert "Redistribution and use in source and binary forms" in text and "Apache License, Version 2.0" in text
        license_name = "BSD-2-Clause AND Apache-2.0"
        evidence = "[v1.21.17 upstream license](https://github.com/trullock/NUglify/blob/v1.21.17/license.txt); [retained text](licenses/NUglify-1.21.17.txt)"
        notices.append("\n===== NUglify/1.21.17 upstream license =====\n" + text)
    elif name == "SourceGear.sqlite3" and version == "3.50.4.5":
        assert license_node is not None and license_node.attrib["type"] == "file"
        text = (directory / license_node.text).read_text()
        assert "SQLite is Public Domain" in text and "https://sqlite.org/copyright.html" in text
        license_name = "Public domain"
        evidence = f"[bundled LICENSE.txt]({url}/License)"
    elif name == "xunit.abstractions" and version == "2.0.3":
        assert metadata.findtext("{*}licenseUrl") == "https://raw.githubusercontent.com/xunit/xunit/master/license.txt"
        license_name = "Apache-2.0"
        evidence = "[legacy nuspec license URL](https://raw.githubusercontent.com/xunit/xunit/master/license.txt); [abstractions source license](https://raw.githubusercontent.com/xunit/abstractions.xunit/master/license.txt)"
    else:
        raise AssertionError(f"Unreviewed license: {name}/{version}")
    attribution = metadata.findtext("{*}copyright") or metadata.findtext("{*}authors")
    rows.append(f"| [{name}]({url}) | {version} | {'/'.join(sorted(kinds))} | {license_name} | {evidence} | {attribution} |")
    for file in sorted(directory.rglob("*")):
        if file.is_file() and ("license" in file.name.lower() or "notice" in file.name.lower()) and file.suffix.lower() in {".txt", ".md", ".rtf", ""}:
            data = file.read_bytes()
            digest = hashlib.sha256(data).hexdigest()
            if digest not in notice_hashes:
                notice_hashes.add(digest)
                notices.append(f"\n===== {name}/{version}/{file.relative_to(directory)} (SHA256 {digest}) =====\n" + data.decode("utf-8-sig").replace("\0", ""))

header = """# Dependency license evidence — Phase 9

Generated from every checked-in packages.lock.json and the restored NuGet nuspecs by
`python3 scripts/audit_licenses.py`; verified 2026-10-09. No runtime network use.
Direct versions are central; all transitive versions/content hashes are locked. Stable
package versions target net10.0, net9.0 or compatible netstandard frameworks; the real
.NET 10 build/tests validate the selected assets. Links point to exact official releases.

ABP 10.1.1 is **LGPL-3.0-only**, not permissive and not relicensed as MIT. It is isolated
in the non-packable `samples/Phase9/Abp` analysis fixture. No product, test or runner
project references this fixture; Roslyn only reads its metadata/source. The audit fails
if an ABP package enters another lock-file closure. Do not distribute ABP binaries as
part of the MIT product. Separate fixture redistribution must meet LGPL terms; see
[upstream license](https://github.com/abpframework/abp/blob/1ecba3756736acb90fc9c52340716530e001c7cd/LICENSE).
NUglify's BSD-2-Clause/Apache-2.0 and MyCSharp's bundled MIT were explicitly verified.

MIT requires retaining copyright and permission notices. Apache-2.0 requires the
license, applicable notices and identification of modifications when distributing modified
third-party files; no dependency was modified. SQLite is public domain. These terms
permit inclusion in an MIT product; they do not relicense dependencies as MIT.
Preserve THIRD-PARTY-NOTICES.txt and the MIT/Apache license texts in any future distribution.
The CLI tool package retains these notices. Phase 2, 8 and 9 fixture dependencies are included in this inventory, not the product package.
Source contract fixtures carry separate upstream licenses under samples/Phase8/ContractFixtures;
see docs/MESSAGING.md for their provenance and distinction from package compatibility tests.

`xunit.abstractions` uses legacy licenseUrl metadata; its publisher's source license
was checked explicitly. Native `SourceGear.sqlite3` 3.50.4.5 contains a public-domain
LICENSE.txt, avoiding the absent license metadata of 3.50.4.2. SQLitePCLRaw 3.0.2
replaces the vulnerable 2.1.11 native dependency without warning suppression.

| Package | Version | Lock classification (across projects) | License | Evidence | Copyright / authors |
| --- | --- | --- | --- | --- | --- |
"""
footer = """
## Files bundled by Roslyn's stable MSBuild package

`Microsoft.CodeAnalysis.Workspaces.MSBuild` 5.0.0 also carries build-host binaries.
The netcore host's deps.json pins Microsoft.Build.Locator 1.10.2 (MIT), Newtonsoft.Json
13.0.3 (MIT), System.Collections.Immutable 9.0.0 (MIT), and System.CommandLine
2.0.0-rtm.25509.106 (MIT). The host itself reports 5.0.0-2.25567.12; these are internal
build identifiers inside the stable 5.0.0 package, not extra floating/prerelease references.
The net472 host also includes Microsoft.IO.Redist, System.Buffers, System.Memory,
System.Numerics.Vectors, System.Runtime.CompilerServices.Unsafe and
System.Threading.Tasks.Extensions, covered by their Microsoft MIT licenses. It is
shipped by the upstream package but not used for this net10.0 fixture acceptance.
The Roslyn package content hash in the lock files pins both hosts and their binaries.

Primary sources: [Roslyn MIT](https://github.com/dotnet/roslyn/blob/main/License.txt),
[Locator MIT](https://github.com/dotnet/msbuildlocator/blob/main/LICENSE),
[System.CommandLine MIT](https://github.com/dotnet/command-line-api/blob/main/LICENSE.md),
[.NET runtime MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT),
[Microsoft.IO.Redist 6.1.0 license](https://www.nuget.org/packages/Microsoft.IO.Redist/6.1.0/License),
[SQLite public domain](https://sqlite.org/copyright.html).

The installed SDK/runtime is a development/host prerequisite, not a copied product
dependency. No native binaries were built or modified by UO.Graphs.
"""
outputs = {
    ROOT / "docs/DEPENDENCIES.md": header + "\n".join(rows) + "\n" + footer,
    ROOT / "THIRD-PARTY-NOTICES.txt": "Upstream license and notice files from locked packages (identical files deduplicated).\nSee docs/DEPENDENCIES.md for per-package expressions, authors and authoritative license links.\n" + "\n".join(notices),
}
for path, content in outputs.items():
    if "--check" in sys.argv:
        assert path.read_bytes() == content.encode("utf-8"), f"Stale license inventory: {path}"
    else:
        path.write_text(content)
print(f"PASS: {len(packages)} locked packages; MIT/Apache/BSD/SQLite public domain plus isolated LGPL ABP fixture")
