#!/usr/bin/env python3
"""Independent module acceptance against three real, directly indexed package graphs.
Run the three documented index commands first. No synthetic graph data or workspace inside this script.
"""
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / '.uographs/phase9-evidence'
OUT.mkdir(parents=True, exist_ok=True)
PREFIX = ['dotnet', str(ROOT / 'src/UO.Graphs.Cli/bin/Debug/net10.0/uographs.dll')]
commands = []
results = {}

def run(module, name, *args):
    db = '.uographs/phase9-' + module + '.db'
    command = PREFIX + list(args) + ['--db', db]
    result = subprocess.run(command, cwd=ROOT, text=True, capture_output=True, timeout=45)
    (OUT / (module + '-' + name + '.json')).write_text(result.stdout)
    (OUT / (module + '-' + name + '.stderr')).write_text(result.stderr)
    commands.append({'module': module, 'command': command, 'exitCode': result.returncode})
    assert result.returncode == 0, (result.returncode, result.stderr)
    return json.loads(result.stdout)

def graph(module):
    status = run(module, 'status', 'status')
    assert status['analysis']['toolVersion'] == '0.9.0'
    assert status['analysis']['coverage'] == 'partial'
    assert not any('error CS' in d for d in status['analysis']['diagnostics'])
    export = run(module, 'export', 'export', '--format', 'json', '--max-nodes', '1000', '--limit', '10000')
    assert not export["metadata"]["truncated"]
    return status, {"entities": [n["entity"] for n in export["nodes"] if n["entity"] is not None], "relationships": [r["relationship"] for r in export["relationships"]]}

def one(items, predicate):
    matches = [e for e in items if predicate(e)]
    assert len(matches) == 1, matches
    return matches[0]

def abp():
    status, export = graph('abp')
    entities, edges = export['entities'], export['relationships']
    endpoint = one(entities, lambda e: e.get('httpEndpoint', {}).get('framework') == 'abp' and 'BookAppService.GetAsync' in e['name'])
    assert endpoint['confidence'] == 'inferred' and endpoint['httpEndpoint']['routePattern'] is None
    assert not any('Hidden' in e['name'] for e in entities if e.get('httpEndpoint', {}).get('framework') == 'abp')
    assert not any(e.get('modelMapping') for e in entities)
    book = one(entities, lambda e: e['documentationId'] == 'T:AbpFixture.Book')
    path = run('abp', 'path', 'query', 'path', endpoint['id'], book['id'], '--edge-types', 'HANDLED_BY,REPOSITORY_ENTITY', '--include-inferred')
    assert [s['relationship']['kind'] for s in path['items'][0]['path']] == ['HANDLED_BY', 'REPOSITORY_ENTITY']
    exact = run('abp', 'exact-path', 'query', 'path', endpoint['id'], book['id'], '--edge-types', 'HANDLED_BY,REPOSITORY_ENTITY')
    assert not exact['items']
    context = run('abp', 'context', 'context', endpoint['id'], '--depth', '2', '--max-chars', '100000')
    assert 'REPOSITORY_ENTITY' in json.dumps(context)
    assert any(e['kind'] == 'MODULE_DEPENDS_ON' for e in edges)
    return status

def ef():
    status, export = graph('efcore')
    entities = export['entities']
    mappings = [e for e in entities if e.get('modelMapping')]
    assert any(e['modelMapping']['table'] == 'books' and e['modelMapping']['schema'] == 'library' for e in mappings)
    assert any(e['modelMapping']['table'] == 'annotated_books' for e in mappings)
    assert any(e['modelMapping']['form'] == 'ApplyConfigurationsFromAssembly' and e['confidence'] == 'unresolved' for e in mappings)
    assert any(e['modelMapping']['form'] == 'HasColumnName' and e['modelMapping']['column'] == 'title' for e in mappings)
    assert not any(e['modelMapping']['column'] in ['wrong_receiver', 'computed'] for e in mappings)
    assert any(e['modelMapping']['table'] == 'tenant_books' and e['confidence'] == 'unresolved' and e['modelMapping']['schema'] is None for e in mappings)
    context = one(entities, lambda e: e.get('framework', {}).get('role') == 'dbContext')
    query = run('efcore', 'dependencies', 'query', 'dependencies', context['id'], '--edge-types', 'CONTEXT_ENTITY')
    assert 'EfFixture.Book' in json.dumps(query) and 'EfFixture.Author' in json.dumps(query)
    return status

def refit():
    status, export = graph('refit')
    entities, edges = export['entities'], export['relationships']
    same = [e for e in entities if e.get('httpContract', {}).get('route') == '/shared/{id}']
    assert len(same) == 2 and same[0]['id'] != same[1]['id']
    assert all(e['httpContract']['baseAddress'] is None for e in entities if e.get('httpContract'))
    assert all(e['target'] is None for e in edges if e['kind'] == 'POSSIBLE_ENDPOINT')
    assert any(e.get('httpEndpoint', {}).get('routePattern') == '/shared/{id}' for e in entities)
    caller = one(entities, lambda e: e['documentationId'] == 'M:RefitFixture.Caller.First(RefitFixture.IFirstApi)')
    edge = one(edges, lambda e: e['kind'] == 'INVOKES_HTTP' and e['source']['id'] == caller['id'])
    path = run('refit', 'path', 'query', 'path', caller['id'], edge['target']['id'], '--edge-types', 'INVOKES_HTTP')
    assert len(path['items'][0]['path']) == 1
    context = run('refit', 'context', 'context', caller['id'], '--depth', '2', '--max-chars', '100000')
    assert 'INVOKES_HTTP' in json.dumps(context) and 'Base address' in json.dumps(context)
    return status

for module, check in [('abp', abp), ('efcore', ef), ('refit', refit)]:
    try:
        status = check()
        results[module] = {'result': 'PASS', 'snapshot': status['analysis']['id'], **{k: status[k] for k in ['entityCount', 'relationshipCount', 'occurrenceCount']}}
    except Exception as error:
        results[module] = {'result': 'FAIL', 'error': str(error)}
(OUT / 'smoke-commands.json').write_text(json.dumps(commands, indent=2))
(OUT / 'smoke-results.json').write_text(json.dumps(results, indent=2))
print(json.dumps(results, indent=2))
raise SystemExit(0 if all(r['result'] == 'PASS' for r in results.values()) else 1)
