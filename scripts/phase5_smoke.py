#!/usr/bin/env python3
"""Compare actual full export/context stdout for the same HelloGraph task. Never restore/index."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--db', default='.uographs/phase5-smoke.db')
parser.add_argument('--output-dir', default='.uographs/phase5-evidence')
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
database = Path(args.db).resolve()
output = Path(args.output_dir).resolve()
output.mkdir(parents=True, exist_ok=True)
before = hashlib.sha256(database.read_bytes()).hexdigest()
base = ['dotnet', str(root / 'src/UO.Graphs.Cli/bin/Debug/net10.0/uographs.dll')]
checks = []


def run(*tokens, expected=0):
    result = subprocess.run([*base, *tokens, '--db', str(database)], cwd=root,
                            capture_output=True, text=True, encoding='utf-8', timeout=45, check=False)
    assert result.returncode == expected, (tokens, result.returncode, result.stderr)
    if expected == 2:
        assert not result.stdout and 'minimum context envelope' in result.stderr
    else:
        assert not result.stderr, result.stderr
    checks.append({'arguments': list(tokens), 'exit': result.returncode, 'stdoutChars': len(result.stdout)})
    return result.stdout


status = json.loads(run('status'))
full_text = run('export', '--format', 'json')
full = json.loads(full_text)
assert full['metadata']['scope'] == 'fullGraph'
assert not full['metadata']['truncated'], 'Comparison requires an actual complete graph dump'
assert len(full['relationships']) == status['relationshipCount']
assert sum(len(e['evidence']) for e in full['relationships']) == status['occurrenceCount']
assert sum(n['entity'] is not None for n in full['nodes']) == status['entityCount']
greet = next(n['symbol']['id'] for n in full['nodes'] if n['entity'] and n['entity']['name'] == 'Greet')
welcome = next(n['symbol']['id'] for n in full['nodes'] if n['entity'] and n['entity']['name'] == 'Welcome')
context_text = run('context', greet, '--max-chars', '12000', '--format', 'json')
context = json.loads(context_text)
assert context_text == run('context', greet, '--max-chars', '12000', '--format', 'json')
assert context['snapshotId'] == full['metadata']['snapshotId'] == status['analysis']['id']
assert len(context_text) == context['budget']['actualChars'] <= 12000
assert context['selected']['id'] == greet
assert context['snippets'] == []
call = next(e for e in context['relationships'] if e['kind'] == 'CALLS' and e['sourceId'] == welcome and e['targetId'] == greet)
original = next(e for e in full['relationships'] if e['relationship']['id'] == call['id'])
assert call['evidence'] == original['evidence']
assert context['callSummary']['callers'] == 1
for site in call['evidence']:
    assert site['source']['startLine'] > 0 and site['source']['sha256']
run('context', greet, '--max-chars', '1', expected=2)
# Budget large enough for this real snapshot's mandatory analysis metadata, but requiring record pruning.
bounded_text = run('context', greet, '--max-chars', '6500')
bounded = json.loads(bounded_text)
assert len(bounded_text) == bounded['budget']['actualChars'] <= 6500
assert bounded['truncated'] and 'maxChars' in bounded['truncationReasons']
assert bounded['selected']['id'] == greet
assert json.loads(run('context', 'DefinitelyAbsent', expected=8))['selected'] is None
assert json.loads(run('context', 'Gre', expected=7))['outcome'] == 'selectionRequired'
included = json.loads(run('context', greet, '--include-source', '--max-chars', '20000'))
assert included['snippets'][0]['status'] == 'snapshotMatched'
assert 'Hello' in included['snippets'][0]['text']
assert 'untrustedSourceData' in included['snippets'][0]['trust']
for fmt in ['json', 'ndjson', 'mermaid', 'dot']:
    text = run('export', '--format', fmt, '--symbol', greet, '--depth', '1')
    assert text == run('export', '--format', fmt, '--symbol', greet, '--depth', '1')
    (output / f'greet-subgraph.{fmt}').write_text(text, encoding='utf-8')
    if fmt == 'json':
        subset = json.loads(text)
        assert len(subset['nodes']) < len(full['nodes'])
        assert subset['metadata']['selectedId'] == greet
    else:
        prefix = {'ndjson': '', 'mermaid': '%% ', 'dot': '// '}[fmt]
        records = [json.loads(line[len(prefix):]) for line in text.splitlines()
                   if line and (not prefix or line.startswith(prefix))]
        assert records[0]['type'] == 'metadata'
        assert any(r['type'] == 'relationship' and r['data']['relationship']['id'] == call['id'] for r in records)
assert before == hashlib.sha256(database.read_bytes()).hexdigest()
(output / 'full-graph.json').write_text(full_text, encoding='utf-8')
(output / 'context.json').write_text(context_text, encoding='utf-8')
report = {'result': 'PASS', 'task': 'Who calls Greet? Show the static caller and its source location.',
          'snapshotId': context['snapshotId'], 'entityCount': status['entityCount'],
          'relationshipCount': status['relationshipCount'], 'occurrenceCount': status['occurrenceCount'],
          'fullGraphChars': len(full_text), 'contextChars': len(context_text),
          'fullGraphUtf8Bytes': len(full_text.encode('utf-8')), 'contextUtf8Bytes': len(context_text.encode('utf-8')),
          'characterReductionPercent': round((1 - len(context_text) / len(full_text)) * 100, 2),
          'countUnit': 'Unicode scalar values including final LF; not tokens',
          'sameTaskEvidencePreserved': True, 'databaseBytesUnchanged': True, 'checks': checks}
(output / 'measurement.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
print(json.dumps(report, indent=2))
