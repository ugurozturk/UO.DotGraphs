#!/usr/bin/env python3
"""Explicit prepared-dataset benchmark. No restore; raw results survive failed operations."""
import argparse
import hashlib
import json
import os
import platform
import signal
import subprocess
import time
import sys
from pathlib import Path


def run(argv, out, name, timeout):
    started = time.monotonic()
    peak = {'parentKiB': 0, 'childrenSumKiB': 0, 'treeKiB': 0, 'method': 'ps sampled 100ms; lower bound, not OS high-water mark'}
    try:
        ps_ok = subprocess.run(['ps', '-axo', 'pid,ppid,rss'], capture_output=True).returncode == 0 if os.name == 'posix' else False
    except OSError:
        ps_ok = False
    if not ps_ok:
        peak = {'status': 'NOT RUN', 'reason': 'Process inventory unavailable/denied; no fabricated child RSS'}
    with (out / (name + '.stdout')).open('x') as stdout, (out / (name + '.stderr')).open('x') as stderr:
        process = subprocess.Popen(argv, stdout=stdout, stderr=stderr, start_new_session=os.name == 'posix')
        timed_out = False
        while process.poll() is None:
            if time.monotonic() - started > timeout:
                timed_out = True
                if os.name == 'posix': os.killpg(process.pid, signal.SIGKILL)
                else: process.kill()
                break
            if ps_ok:
                sample = subprocess.run(['ps', '-axo', 'pid,ppid,rss'], capture_output=True, text=True)
                if sample.returncode == 0:
                    rows = [tuple(map(int, line.split())) for line in sample.stdout.splitlines()[1:] if len(line.split()) == 3]
                    children = {process.pid}
                    while True:
                        expanded = children | {pid for pid, parent, rss in rows if parent in children}
                        if expanded == children: break
                        children = expanded
                    parent_rss = sum(rss for pid, parent, rss in rows if pid == process.pid)
                    child_rss = sum(rss for pid, parent, rss in rows if pid in children and pid != process.pid)
                    peak['parentKiB'] = max(peak['parentKiB'], parent_rss)
                    peak['childrenSumKiB'] = max(peak['childrenSumKiB'], child_rss)
                    peak['treeKiB'] = max(peak['treeKiB'], parent_rss + child_rss)
            time.sleep(.1)
        process.wait()
    record = {'name': name, 'argv': argv, 'exit': process.returncode, 'timeout': timed_out,
              'wallSeconds': time.monotonic() - started, 'memory': peak}
    try:
        output = json.loads((out / (name + '.stdout')).read_text())
        record['result'] = {k: output[k] for k in ('outcome', 'projectsAnalyzed', 'fingerprintMilliseconds', 'analysisMilliseconds', 'filesHashed', 'bytesHashed', 'entityCount', 'relationshipCount', 'occurrenceCount') if k in output}
    except (ValueError, OSError): pass
    with (out / 'raw.jsonl').open('a') as file: file.write(json.dumps(record) + '\n')
    return record


def main():
    parser = argparse.ArgumentParser(description=__doc__, epilog='Last option: --cli EXECUTABLE [PREFIX ARGS ...]')
    parser.add_argument('dataset', type=Path)
    parser.add_argument('evidence', type=Path)
    # Split verbatim CLI argv before argparse interprets -- or child options.
    parser.add_argument('--repeats', type=int, default=3)
    parser.add_argument('--timeout', type=int, default=600)
    parser.add_argument('--context-chars', type=int, default=50000)
    if '--help' in sys.argv or '-h' in sys.argv: parser.parse_args(['--help'])
    if '--cli' not in sys.argv: parser.error('--cli executable [args...] is required as the last option')
    split = sys.argv.index('--cli')
    args = parser.parse_args(sys.argv[1:split])
    args.cli = sys.argv[split + 1:]
    if args.repeats < 1 or args.timeout < 1: parser.error('repeats and timeout must be positive')
    if not args.cli: parser.error('--cli requires executable')
    dataset, out = args.dataset.resolve(), args.evidence.absolute()
    out.mkdir(parents=True, exist_ok=False)
    info = {'os': platform.platform(), 'architecture': platform.machine(), 'logicalCpu': os.cpu_count(),
            'physicalMemory': 'NOT RUN: platform inventory may require permissions', 'python': platform.python_version(),
            'sdk': subprocess.run(['dotnet', '--info'], capture_output=True, text=True).stdout,
            'dataset': json.loads((dataset / 'dataset.json').read_text()), 'repeats': args.repeats,
            'coldDefinition': 'fresh process and absent database; OS page cache NOT evicted, SDK/assets already prepared',
            'warmDefinition': 'same CLI argv/environment, existing snapshot, previously read inputs; fresh process',
            'cliSha256': {a: hashlib.sha256(Path(a).read_bytes()).hexdigest() for a in args.cli if Path(a).is_file()}}
    (out / 'environment.json').write_text(json.dumps(info, indent=2) + '\n')
    db = out / 'graph.db'
    # Evidence must be outside source inventory; only source files are deliberately edited below.
    if out.is_relative_to(dataset): raise ValueError('Evidence must be outside dataset')
    index = args.cli + ['index', str(dataset / 'Synthetic.slnx'), '--trust-project', '--db', str(db)]
    sources = sorted(dataset.glob('P*/*.cs'))
    original = {p: p.read_bytes() for p in sources}
    expected = dict(original)
    def invoke(name, command): return run(command, out, name, args.timeout)
    try:
        result = invoke('cold-full-0', index + ['--full'])
        if result['exit'] != 0:
            (out / 'NOT-RUN.json').write_text(json.dumps({'warm, change, dependent, query, context': 'Prerequisite full index failed'}))
            return 1
        for r in range(args.repeats):
            warm = invoke(f'warm-noop-{r}', index + ['--incremental'])
            payload = json.loads((out / f'warm-noop-{r}.stdout').read_text()) if warm['exit'] == 0 else {}
            if payload.get('outcome') != 'noOp':
                raise RuntimeError('Warm operation was not a verified noOp; inspect raw output')
            for operation, argv in [('query', ['query', 'find', 'M0', '--limit', '20']), ('context', ['context', 'P0000.C0.M0()', '--max-chars', str(args.context_chars)])]:
                result = invoke(f'{operation}-{r}', args.cli + argv + ['--db', str(db)])
                if result['exit'] != 0: return 1
                payload = json.loads((out / f'{operation}-{r}.stdout').read_text())
                if operation == 'query' and not payload.get('items'): raise RuntimeError('Expected symbols missing')
                if operation == 'context' and payload.get('selected') is None: raise RuntimeError('Expected context selection missing')
        for label, source in [('single-project', sources[-1]), ('dependent-invalidation', sources[0])]:
            expected[source] = original[source].replace(b'=> ', b'=> 1 + ', 1)
            source.write_bytes(expected[source])
            result = invoke(label, index + ['--incremental'])
            if result['exit'] != 0: return 1
        (out / 'sqlite.json').write_text(json.dumps({p.name: p.stat().st_size for p in out.glob('graph.db*')}, indent=2))
        if any(p.read_bytes() != expected[p] for p in sources): raise RuntimeError('Unexpected analyzed source modification')
        return 0
    finally:
        for path, content in original.items(): path.write_bytes(content)


if __name__ == '__main__':
    raise SystemExit(main())
