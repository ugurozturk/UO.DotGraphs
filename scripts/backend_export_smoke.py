#!/usr/bin/env python3
"""Read-only backend/graph export smoke against a prepared snapshot; never index or restore."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--cli', default='src/UO.Graphs.Cli/bin/Release/net10.0/uographs.dll')
parser.add_argument('--db', required=True)
parser.add_argument('--symbol', required=True)
parser.add_argument('--output-dir', required=True)
args = parser.parse_args()
cli, db, output = (Path(p).resolve() for p in (args.cli, args.db, args.output_dir))
output.mkdir(parents=True, exist_ok=False)
before = hashlib.sha256(db.read_bytes()).hexdigest()
checks = []


def run(tokens, expected=0):
    argv = ['dotnet', str(cli), *tokens, '--db', str(db)]
    result = subprocess.run(argv, capture_output=True, timeout=60, check=False)
    checks.append({'argv': argv, 'exit': result.returncode, 'expectedExit': expected,
                   'stdoutBytes': len(result.stdout), 'stderr': result.stderr.decode('utf-8')})
    assert result.returncode == expected, checks[-1]
    return result.stdout


try:
    selected = json.loads(run(['query', 'symbol', args.symbol, '--details', 'full']))
    assert selected['outcome'] == 'ok'
    backend = None
    for profile in ('graph', 'backend'):
        for fmt in ('json', 'ndjson', 'mermaid', 'dot'):
            tokens = ['export', '--profile', profile, '--symbol', args.symbol, '--format', fmt]
            stdout = run(tokens)
            assert stdout == run(tokens), 'Non-deterministic export'
            destination = output / f'{profile}.{fmt}'
            assert not run([*tokens, '--output', str(destination)])
            assert destination.read_bytes() == stdout
            assert not run([*tokens, '--output', str(destination)], expected=6)
            assert destination.read_bytes() == stdout
            if profile == 'graph':
                assert stdout == run(['export', '--symbol', args.symbol, '--format', fmt])
            if fmt == 'json':
                document = json.loads(stdout)
                if profile == 'backend':
                    backend = document
                    assert document['metadata']['selection']['calls'] == 'exclude-properties'
                else:
                    assert 'selection' not in document['metadata']
            else:
                prefix = {'ndjson': '', 'mermaid': '%% ', 'dot': '// '}[fmt]
                records = [json.loads(line[len(prefix):]) for line in stdout.decode('utf-8').splitlines()
                           if line and (not prefix or line.startswith(prefix))]
                assert records[0] == {'type': 'metadata', 'data': document['metadata']}
                assert [r['data'] for r in records if r['type'] == 'node'] == document['nodes']
                assert [r['data'] for r in records if r['type'] == 'relationship'] == document['relationships']
    assert backend is not None
    nodes = {n['symbol']['id'] for n in backend['nodes']}
    for edge in backend['relationships']:
        r = edge['relationship']
        assert r['source']['id'] in nodes and (r['target'] is None or r['target']['id'] in nodes)
        assert r['kind'] in backend['metadata']['selection']['edgeTypes']
        assert edge['evidence']
    for calls in ('all', 'only-properties'):
        result = run(['export', '--profile', 'backend', '--symbol', args.symbol, '--calls', calls])
        assert json.loads(result)['metadata']['selection']['calls'] == calls
        (output / f'backend-{calls}.json').write_bytes(result)
    context = run(['context', args.symbol, '--depth', '0', '--max-chars', '1000000'])
    assert json.loads(context)['selected']['id'] == backend['metadata']['selectedId']
    (output / 'context.json').write_bytes(context)
    missing = output / 'must-not-exist.json'
    failed = run(['export', '--profile', 'backend', '--symbol', 'uographs-missing-54d589e8-0c82-4796-8cb1-228f5ec30280',
                  '--output', str(missing)], expected=8)
    assert json.loads(failed)['metadata']['outcome'] == 'symbolNotFound' and not missing.exists()
    assert before == hashlib.sha256(db.read_bytes()).hexdigest(), 'Database bytes changed'
    summary = {'result': 'PASS', 'snapshotId': backend['metadata']['snapshotId'],
               'freshness': backend['metadata']['stale'], 'coverage': backend['metadata']['completeness']['coverage'],
               'backendNodes': len(nodes), 'backendRelationships': len(backend['relationships']),
               'truncationReasons': backend['metadata']['truncationReasons'],
               'databaseSha256Unchanged': before, 'checks': len(checks)}
    (output / 'summary.json').write_text(json.dumps(summary, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(summary))
finally:
    (output / 'commands.json').write_text(json.dumps(checks, indent=2) + '\n', encoding='utf-8')
