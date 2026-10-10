#!/usr/bin/env python3
"""Measure prepared read-only CLI queries, three subprocess repetitions; no index update."""
import argparse
import hashlib
import json
import platform
import subprocess
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def digest(path):
    value = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(chunk)
    return value.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--db', type=Path, required=True)
    parser.add_argument('--symbol', required=True, help='Explicit exact ID or unique qualified name')
    parser.add_argument('--find', required=True)
    parser.add_argument('--evidence', type=Path, required=True)
    parser.add_argument('--max-chars', type=int, default=50000, help='Additional bounded comparison; default reads still use 12000')
    parser.add_argument('--cli', type=Path, default=ROOT / 'src/UO.Graphs.Cli/bin/Release/net10.0/uographs.dll')
    args = parser.parse_args()
    args.evidence.mkdir(parents=True, exist_ok=False)
    db = args.db.resolve()
    before = digest(db)
    results = []
    environment = {'platform': platform.platform(), 'machine': platform.machine(),
                   'sdk': subprocess.check_output(['dotnet', '--version'], cwd=ROOT, text=True).strip(),
                   'runtime': subprocess.check_output(['dotnet', '--list-runtimes'], cwd=ROOT, text=True).strip(),
                   'cwd': str(ROOT), 'timerScope': 'whole subprocess wall clock including startup and pipe collection',
                   'cache': 'not flushed', 'concurrentLoad': 'uncontrolled'}
    commands = [('find-default', ['query', 'find', args.find, '--limit', '1']),
                ('symbol-default', ['query', 'symbol', args.symbol]),
                ('callers-default', ['query', 'callers', args.symbol]),
                ('callees-default', ['query', 'callees', args.symbol]),
                ('status-default', ['status']),
                ('context-default', ['context', args.symbol]),
                ('status-bounded', ['status', '--max-chars', str(args.max_chars)]),
                ('symbol-bounded', ['query', 'symbol', args.symbol, '--max-chars', str(args.max_chars)]),
                ('callers-bounded', ['query', 'callers', args.symbol, '--max-chars', str(args.max_chars)]),
                ('find-full', ['query', 'find', args.find, '--limit', '1', '--details', 'full']),
                ('callees-full', ['query', 'callees', args.symbol, '--details', 'full']),
                ('symbol-full', ['query', 'symbol', args.symbol, '--details', 'full']),
                ('callers-full', ['query', 'callers', args.symbol, '--details', 'full']),
                ('status-full', ['status', '--details', 'full'])]
    for name, command in commands:
        for repeat in range(1, 4):
            argv = ['dotnet', str(args.cli.resolve()), *command, '--db', str(db)]
            start = time.perf_counter()
            result = subprocess.run(argv, cwd=ROOT, capture_output=True, timeout=180)
            seconds = time.perf_counter() - start
            stdout = result.stdout.decode('utf-8')
            (args.evidence / f'{name}-{repeat}.stdout').write_bytes(result.stdout)
            (args.evidence / f'{name}-{repeat}.stderr').write_bytes(result.stderr)
            document = json.loads(stdout) if stdout else {}
            if 'budget' in document:
                assert document['budget']['actualChars'] == len(stdout)
                if document['budget']['maxChars'] is not None:
                    assert len(stdout) <= document['budget']['maxChars']
            results.append({'name': name, 'repeat': repeat, 'argv': argv, 'exit': result.returncode,
                            'seconds': seconds, 'utf8Bytes': len(result.stdout), 'unicodeScalars': len(stdout),
                            'snapshot': document.get('snapshotId', document.get('analysis', {}).get('id')),
                            'outcome': document.get('outcome'), 'returned': document.get('limits', {}).get('returned'),
                            'stderr': result.stderr.decode('utf-8')})
    # Equal query/evidence limits must yield equal answers even when traversal is
    # incomplete. Only explicit output-budget pruning may change returned items.
    for name in ('find', 'symbol', 'callers', 'callees'):
        compact_path = args.evidence / f'{name}-default-1.stdout'
        full_path = args.evidence / f'{name}-full-1.stdout'
        if compact_path.stat().st_size and full_path.stat().st_size:
            compact = json.loads(compact_path.read_text())
            full = json.loads(full_path.read_text())
            if 'maxChars' not in compact['limits']['reasons']:
                assert compact['items'] == full['items'], name + ' answer differs'
    after = digest(db)
    snapshots = sorted({r['snapshot'] for r in results if r['snapshot'] is not None})
    manifest = {'environment': environment, 'database': str(db), 'observedSnapshots': snapshots, 'sha256Before': before,
                'sha256After': after, 'databaseUnchanged': before == after, 'measurements': results}
    (args.evidence / 'measurements.json').write_text(json.dumps(manifest, indent=2) + '\n')
    assert before == after, 'Database changed during read measurements'
    print(json.dumps({'measurements': len(results), 'databaseUnchanged': True,
                      'exits': sorted(set(r['exit'] for r in results))}))


if __name__ == '__main__':
    main()
