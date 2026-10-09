#!/usr/bin/env python3
"""Actual CLI SIGINT/kill/competing-writer regression on a backup of a real graph."""
import argparse
import json
import os
import signal
import sqlite3
import subprocess
import time
from pathlib import Path


def snapshot(db):
    with sqlite3.connect(db) as connection:
        return connection.execute('select json from analysis').fetchone()[0]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--db', required=True, type=Path)
    parser.add_argument('--input', required=True, type=Path)
    parser.add_argument('--cli', required=True, type=Path)
    parser.add_argument('--evidence', required=True, type=Path)
    args = parser.parse_args()
    out = args.evidence.absolute(); out.mkdir(parents=True, exist_ok=False)
    if os.name != 'posix':
        (out / 'result.json').write_text(json.dumps({'status':'NOT RUN','reason':'POSIX signals required'}))
        return 1
    db=out/'graph.db'
    with sqlite3.connect(args.db) as source, sqlite3.connect(db) as target: source.backup(target)
    original=snapshot(db)
    argv=['dotnet',str(args.cli.resolve()),'index',str(args.input.resolve()),'--trust-project','--full','--db',str(db)]
    records=[]
    for name,sig in [('cancel',signal.SIGINT),('kill',signal.SIGKILL)]:
        with (out/(name+'.stderr')).open('x') as stderr, (out/(name+'.stdout')).open('x') as stdout:
            process=subprocess.Popen(argv,stdout=stdout,stderr=stderr,start_new_session=True)
            try:
                deadline=time.monotonic()+10
                while 'Checking installed SDK' not in (out/(name+'.stderr')).read_text():
                    if process.poll() is not None or time.monotonic()>deadline: raise RuntimeError('Index did not reach preparation')
                    time.sleep(.02)
                other=subprocess.run(argv,capture_output=True,text=True,timeout=10)
                assert other.returncode==10,other.stderr
                assert not other.stdout
                os.kill(process.pid,sig)
                code=process.wait(timeout=15)
                assert code==(130 if name=='cancel' else -signal.SIGKILL),(name,code)
                assert snapshot(db)==original
                assert not (out/(name+'.stdout')).read_text()
                records.append({'operation':name,'signal':sig.name,'exit':code,'competingWriterExit':other.returncode,'snapshotPreserved':True})
            finally:
                # The group belongs exclusively to this test. Clean descendants even after parent kill.
                try: os.killpg(process.pid,signal.SIGKILL)
                except ProcessLookupError: pass
                process.wait()
        (out/'result.json').write_text(json.dumps(records,indent=2))
    print(json.dumps(records,indent=2))
    return 0


if __name__=='__main__': raise SystemExit(main())
