#!/usr/bin/env python3
"""Host-neutral local verification. Prepared SDK/assets only; no restore or uploads.
All generated evidence stays in a new explicit directory. Tool install uses only the local feed.
"""
import argparse
import hashlib
import json
import os
import subprocess
import tempfile
import time
import signal
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--evidence', type=Path, required=True)
    parser.add_argument('--skip-tests', action='store_true', help='Explicitly record tests NOT RUN')
    parser.add_argument('--offline-prefix-json', type=json.loads, dest='offline_prefix', help='JSON argv of a preconfigured isolated launcher, e.g. ["unshare","-n","--"]; operator must supply isolation evidence')
    args = parser.parse_args()
    if args.offline_prefix is not None and (not isinstance(args.offline_prefix, list) or not args.offline_prefix or not all(isinstance(v, str) for v in args.offline_prefix)):
        parser.error('--offline-prefix-json must be a nonempty string array')
    out = args.evidence.absolute()
    out.mkdir(parents=True, exist_ok=False)
    results = []
    def run(name, argv, timeout=900):
        start = time.monotonic()
        with (out / (name + '.stdout')).open('x') as stdout, (out / (name + '.stderr')).open('x') as stderr:
            process = subprocess.Popen(argv, cwd=ROOT, stdout=stdout, stderr=stderr, start_new_session=os.name == 'posix')
            try:
                process.wait(timeout=timeout)
                code = process.returncode
            except subprocess.TimeoutExpired:
                code = 'TIMEOUT'
            finally:
                if process.poll() is None:
                    if os.name == 'posix': os.killpg(process.pid, signal.SIGKILL)
                    else: subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'], check=False)
                    process.wait()
        results.append({'name': name, 'argv': argv, 'exit': code, 'seconds': time.monotonic() - start})
        (out / 'commands.json').write_text(json.dumps(results, indent=2))
        return code == 0
    common = ['--no-restore', '-m:1', '-nr:false']
    if not run('build', ['dotnet', 'build', 'UO.Graphs.slnx', *common, '-p:UseSharedCompilation=false']): return 1
    if not args.skip_tests:
        for project in ['UnitTests', 'IntegrationTests']:
            if not run(project, ['dotnet', 'test', 'tests/UO.Graphs.' + project, '--no-build', *common,
                                  '--logger', 'trx', '--results-directory', str(out / 'vstest')]): return 1
    else:
        results.append({'name': 'tests', 'status': 'NOT RUN', 'reason': '--skip-tests'})
    if not run('licenses', ['python3', 'scripts/audit_licenses.py', '--check']): return 1
    feed = out / 'feed'
    if not run('pack', ['dotnet', 'pack', 'src/UO.Graphs.Cli', '-c', 'Debug', *common,
                        '-p:UseSharedCompilation=false', '-o', str(feed)]): return 1
    package, = feed.glob('*.nupkg')
    with zipfile.ZipFile(package) as archive:
        names = archive.namelist()
        for required in ['LICENSE', 'THIRD-PARTY-NOTICES.txt', 'README.md', 'tools/net10.0/any/DotnetToolSettings.xml',
                         'tools/net10.0/any/BuildHost-netcore/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll',
                         'templates/copilot/uographs/SKILL.md', 'templates/copilot/uographs/references/cli.md',
                         'templates/codex/uographs/SKILL.md', 'templates/codex/uographs/references/cli.md',
                         'scripts/install_skill.py',
                         'docs/history/PHASE10.md', 'docs/schemas/cli-envelope.schema.json', 'scripts/safe_export.py']:
            assert required in names, required
        assert any('runtimes/osx-arm64/native/' in p for p in names)
        assert any(p.startswith('licenses/') for p in names)
    (out / 'package.json').write_text(json.dumps({'sha256': hashlib.sha256(package.read_bytes()).hexdigest(), 'entries': names}, indent=2))
    # Temporary tool path never touches user's global tools or manifest.
    temporary = Path(tempfile.mkdtemp(prefix='uographs-release-'))
    (out / 'tool-path.txt').write_text(str(temporary))
    config = temporary / 'NuGet.Config'
    import xml.etree.ElementTree as ET
    configuration = ET.Element('configuration'); sources = ET.SubElement(configuration, 'packageSources')
    ET.SubElement(sources, 'clear'); ET.SubElement(sources, 'add', key='local', value=str(feed))
    settings = ET.SubElement(configuration, 'config')
    ET.SubElement(settings, 'add', key='globalPackagesFolder', value=str(temporary / 'packages'))
    ET.ElementTree(configuration).write(config)
    if not run('install', ['dotnet', 'tool', 'install', 'UO.Graphs.Cli', '--tool-path', str(temporary / 'bin'),
                           '--configfile', str(config), '--no-cache']): return 1
    tool = str(temporary / 'bin' / ('uographs.exe' if os.name == 'nt' else 'uographs'))
    cli = (args.offline_prefix or []) + [tool]
    database = str(out / 'smoke.db')
    for name, argv in [('help', ['--help']), ('doctor', ['doctor']),
                       ('index', ['index', 'samples/HelloGraph/HelloGraph.slnx', '--trust-project', '--db', database]),
                       ('query', ['query', 'find', 'Greet', '--db', database]),
                       ('context', ['context', 'HelloGraph.Domain.Greeter.Greet(string)', '--db', database])]:
        if not run(name, cli + argv, timeout=180): return 1
    index = json.loads((out / 'index.stdout').read_text())
    assert index['entityCount'] == 15
    query = json.loads((out / 'query.stdout').read_text())
    assert query['items']
    context = json.loads((out / 'context.stdout').read_text())
    assert context['selected'] is not None
    (out / 'offline.json').write_text(json.dumps({'status': 'operator isolation requires independent evidence' if args.offline_prefix else 'NOT RUN', 'launcher': args.offline_prefix}))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
