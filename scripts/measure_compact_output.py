#!/usr/bin/env python3
"""Read-only representation/selection comparison on one committed snapshot.
Times include dotnet process startup, DB reads, projection/serialization and pipe drain.
No indexing, restore, tokenizer or application startup. Raw output is private evidence.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--db', required=True, type=Path)
    parser.add_argument('--symbol', required=True)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--cli', type=Path, default=Path(__file__).resolve().parents[1] / 'src/UO.Graphs.Cli/bin/Release/net10.0/uographs.dll')
    parser.add_argument('--repeats', type=int, default=3)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    before = hashlib.sha256(args.db.read_bytes()).hexdigest()
    scenarios = {
        'standard-small': ['--limit', '100', '--max-chars', '12000'],
        'standard-complete': ['--limit', '100', '--max-chars', '1000000'],
        'compact-complete': ['--view', 'compact', '--limit', '100', '--max-chars', '1000000'],
        'compact-workflow': ['--view', 'compact', '--calls', 'exclude-properties', '--limit', '100', '--max-chars', '12000'],
    }
    records = []
    snapshots = set()
    for name, options in scenarios.items():
        previous = None
        for repeat in range(args.repeats):
            argv = ['dotnet', str(args.cli), 'query', 'callees', args.symbol, '--depth', '1', '--max-edges', '500', '--evidence-limit', '2', '--db', str(args.db), *options]
            start = time.perf_counter()
            result = subprocess.run(argv, capture_output=True, timeout=90)
            elapsed = time.perf_counter() - start
            stem = args.output / f'{name}-{repeat}'
            stem.with_suffix('.json').write_bytes(result.stdout)
            stem.with_suffix('.stderr').write_bytes(result.stderr)
            if result.returncode != 0:
                raise RuntimeError((argv, result.returncode, result.stderr.decode()))
            raw = result.stdout.decode('utf-8')
            doc = json.loads(raw)
            assert raw.endswith('\n') and len(raw) == doc['budget']['actualChars']
            snapshots.add(doc['snapshotId'])
            if previous is not None:
                assert previous == result.stdout, 'Nondeterministic output'
            previous = result.stdout
            if doc.get('view') == 'compact':
                edges = doc['relationships']
                evidence = doc['evidence']
                targets = [doc['symbols'][e['target']]['displayName'] for e in edges if e['target'] is not None]
                properties = sum(e['callKind'] == 'propertyAccessor' for e in edges)
            else:
                steps = [s for i in doc['items'] for s in i['path']]
                edges = list({s['relationship']['id']: s['relationship'] for s in steps}.values())
                evidence = list({e['id']: e for s in steps for e in s['evidence']}.values())
                targets = [e['target']['displayName'] for e in edges if e['target'] is not None]
                properties = sum(e['target'] is not None and e['target']['declarationId'].startswith('accessor:') and e['target']['declarationId'].endswith((':PropertyGet', ':PropertySet')) for e in edges)
            records.append(dict(scenario=name, repeat=repeat, argv=argv, seconds=elapsed,
                characters=len(raw), bytes=len(result.stdout), relationships=len(edges), properties=properties,
                otherRelationships=len(edges)-properties, evidence=len(evidence), targets=targets,
                relationshipIds=[e['id'] for e in edges], evidenceIds=[e['id'] for e in evidence],
                selection=doc.get('selection'), reasons=doc['limits']['reasons']))
    # Follow the returned cursor at the same small budget and verify set completeness
    # against this snapshot's all-relationship compact selection.
    first = json.loads((args.output / 'compact-workflow-0.json').read_text())
    complete = json.loads((args.output / 'compact-complete-0.json').read_text())
    expected = {e['id'] for e in complete['relationships'] if e['callKind'] != 'propertyAccessor'}
    seen = {e['id'] for e in first['relationships']}
    pages = [dict(characters=first['budget']['actualChars'], relationships=len(seen), seconds=None)]
    page = first
    while page['selection']['continuation'] is not None:
        cursor = page['selection']['continuation']
        argv = ['dotnet', str(args.cli), 'query', 'callees', args.symbol, '--view', 'compact',
                '--calls', 'exclude-properties', '--depth', '1', '--limit', '100', '--max-edges', '500',
                '--evidence-limit', '2', '--max-chars', '12000', '--after', cursor['after'],
                '--snapshot', cursor['snapshot'], '--db', str(args.db)]
        start = time.perf_counter()
        result = subprocess.run(argv, capture_output=True, timeout=90)
        elapsed = time.perf_counter() - start
        assert result.returncode == 0, result.stderr
        page = json.loads(result.stdout)
        assert page['snapshotId'] == first['snapshotId']
        assert len(result.stdout.decode('utf-8')) == page['budget']['actualChars'] <= 12000
        new = {e['id'] for e in page['relationships']}
        assert new and not seen.intersection(new)
        seen.update(new)
        (args.output / f'workflow-page-{len(pages)+1}.json').write_bytes(result.stdout)
        pages.append(dict(characters=page['budget']['actualChars'], relationships=len(new), seconds=elapsed, argv=argv))
    assert seen == expected, (seen, expected)
    after = hashlib.sha256(args.db.read_bytes()).hexdigest()
    assert before == after and len(snapshots) == 1
    report = dict(timer='whole CLI process including startup, DB read, serialization, pipe collection',
        cache='not flushed; scenario order fixed; concurrent load uncontrolled', tokenizer=None,
        workflowPages=pages, workflowComplete=True, snapshot=next(iter(snapshots)), dbSha256Before=before, dbSha256After=after, measurements=records)
    (args.output / 'measurements.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n')
    for name in scenarios:
        rows = [r for r in records if r['scenario'] == name]
        r = rows[0]
        print(name, r['characters'], r['relationships'], r['otherRelationships'], r['evidence'],
              f"{min(x['seconds'] for x in rows):.3f}–{max(x['seconds'] for x in rows):.3f}s")


if __name__ == '__main__':
    main()
