#!/usr/bin/env python3
"""Real CLI/JSON acceptance on a previously indexed HelloGraph snapshot; never indexes/restores."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--db', default='.uographs/phase4-smoke.db')
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
database = Path(args.db).resolve()
original_hash = hashlib.sha256(database.read_bytes()).hexdigest()
base = ['dotnet', 'run', '--project', str(root / 'src/UO.Graphs.Cli/UO.Graphs.Cli.csproj'),
        '--no-build', '--no-restore', '--']
results = []


def run(*tokens, expected=0):
    command = [*base, *tokens, '--db', str(database)]
    result = subprocess.run(command, cwd=root, capture_output=True, text=True, timeout=45, check=False)
    assert result.returncode == expected, (tokens, result.returncode, result.stderr)
    assert not result.stderr, result.stderr
    data = json.loads(result.stdout)
    results.append({'arguments': list(tokens), 'exit': result.returncode,
                    'items': len(data.get('items', [])), 'outcome': data.get('outcome')})
    if tokens[0] == 'query':
        for key in ['schemaVersion', 'snapshotId', 'analysisContext', 'completeness', 'stale',
                    'truncated', 'items', 'diagnostics', 'options', 'limits']:
            assert key in data, key
        assert data['schemaVersion'] == 6
        assert data['stale'] == 'notChecked'
        assert data['snapshotId'] == status['analysis']['id']
    return data


status = run('status')
assert status['schemaVersion'] == 3
found = run('query', 'find', 'Greet')
greet = next(i['symbol']['id'] for i in found['items'] if i['entity']['name'] == 'Greet')
welcome = next(i['symbol']['id'] for i in run('query', 'find', 'Welcome')['items'] if i['entity']['name'] == 'Welcome')
assert run('query', 'symbol', greet)['items'][0]['entity']['name'] == 'Greet'
callers = run('query', 'callers', greet)
assert [i['symbol']['id'] for i in callers['items']] == [welcome]
assert greet in [i['symbol']['id'] for i in run('query', 'callees', welcome)['items']]
assert greet in [i['symbol']['id'] for i in run('query', 'dependencies', welcome)['items']]
path = run('query', 'path', welcome, greet)
assert path['outcome'] == 'found'
assert len(path['items']) == 1
assert len(path['items'][0]['path']) == 1
step = path['items'][0]['path'][0]
assert step['relationship']['kind'] == 'CALLS'
assert step['evidence'][0]['reason']
source = step['evidence'][0]['source']
line = Path(source['file']).read_text().splitlines()[source['startLine'] - 1]
assert line[source['startColumn'] - 1:source['endColumn'] - 1] == 'new Greeter().Greet(name)'
assert path == run('query', 'path', welcome, greet)
impact = run('query', 'impact', greet)
assert [i['symbol']['id'] for i in impact['items']] == [welcome]
assert impact['items'][0]['path'][0]['reversed']
assert impact['items'][0]['path'][0]['evidence'] == step['evidence']
assert run('query', 'path', greet, welcome, '--direction', 'incoming')['outcome'] == 'found'
bounded = run('query', 'path', welcome, greet, '--depth', '0')
assert bounded['outcome'] == 'boundedSearchIncomplete'
assert bounded['truncated'] and 'depth' in bounded['limits']['reasons']
missing = run('query', 'symbol', 'DefinitelyAbsent', expected=8)
assert missing['outcome'] == 'symbolNotFound'
assert original_hash == hashlib.sha256(database.read_bytes()).hexdigest()
print(json.dumps({'result': 'PASS', 'snapshotId': status['analysis']['id'],
                  'entityCount': status['entityCount'], 'relationshipCount': status['relationshipCount'],
                  'occurrenceCount': status['occurrenceCount'], 'databaseBytesUnchanged': True,
                  'checks': results}, indent=2))
