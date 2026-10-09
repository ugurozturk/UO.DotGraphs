#!/usr/bin/env python3
"""Query actual pre-indexed package graphs in separate CLI processes; no host/container/broker."""
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / '.uographs/phase8-evidence'
OUT.mkdir(parents=True, exist_ok=True)
PREFIX = ['dotnet', str(ROOT / 'src/UO.Graphs.Cli/bin/Debug/net10.0/uographs.dll')]
commands = []

def run(name, *args):
    result = subprocess.run(PREFIX + list(args), cwd=ROOT, text=True, capture_output=True, timeout=180)
    (OUT / (name + '.json')).write_text(result.stdout)
    (OUT / (name + '.stderr')).write_text(result.stderr)
    commands.append({'name': name, 'command': PREFIX + list(args), 'exitCode': result.returncode})
    assert result.returncode == 0, (name, result.returncode, result.stderr)
    return json.loads(result.stdout)

uo = '.uographs/phase8-direct-uo.db'
other = '.uographs/phase8-direct-packages.db'
indexed = run('uo-status', 'status', '--db', uo)
run('packages-status', 'status', '--db', other)
path = run('path', 'query', 'path', 'GET /phase8/start', 'UoFlows.HandlerB.HandleAsync(UoFlows.RequestB)', '--depth', '10', '--edge-types', 'HANDLED_BY,CALLS,DISPATCHES,POSSIBLE_TARGET', '--include-inferred', '--db', uo)
steps = path['items'][0]['path']
assert [step['relationship']['kind'] for step in steps] == ['HANDLED_BY', 'CALLS', 'DISPATCHES', 'POSSIBLE_TARGET', 'DISPATCHES', 'POSSIBLE_TARGET']
assert sum(step['relationship']['confidence'] == 'inferred' for step in steps) == 2
context = run('context', 'context', 'GET /phase8/start', '--depth', '8', '--max-nodes', '300', '--limit', '800', '--max-edges', '10000', '--max-chars', '900000', '--db', uo)
assert 'UoFlows.HandlerB' in json.dumps(context)
assert 'other services/repositories' in json.dumps(context)
run('uo-export', 'export', '--format', 'json', '--max-nodes', '1000', '--limit', '10000', '--db', uo)
run('packages-export', 'export', '--format', 'json', '--max-nodes', '1000', '--limit', '10000', '--db', other)
run('event-dependencies', 'query', 'dependencies', 'massTransit message ContractFlows.Event', '--depth', '2', '--include-inferred', '--db', other)
(OUT / 'commands.json').write_text(json.dumps(commands, indent=2))
print(json.dumps({'result': 'PASS', 'commands': len(commands), 'pathSteps': len(steps), 'inferredSteps': 2, 'snapshot': indexed['analysis']['id'], 'evidence': str(OUT)}, indent=2))
