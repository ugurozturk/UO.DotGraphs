#!/usr/bin/env python3
"""Check the deliberately small CLI-envelope schema against real prepared graph reads.
This checks exactly the keywords used by the checked-in schema, not arbitrary JSON Schema.
"""
import argparse
import copy
import json
import subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
SCHEMA=ROOT/'docs/schemas/cli-envelope.schema.json'
TYPES={'object':dict,'array':list,'string':str,'boolean':bool,'integer':int}


def matches(value, schema):
    known={'$schema','$id','title','description','oneOf','type','required','properties','additionalProperties','const','enum','items'}
    if set(schema)-known: raise ValueError('Unsupported schema keywords: '+str(set(schema)-known))
    if 'oneOf' in schema and sum(matches(value, item) for item in schema['oneOf'])!=1: return False
    if 'const' in schema and (value!=schema['const'] or type(value)!=type(schema['const'])): return False
    if 'enum' in schema and not any(value==v and type(value)==type(v) for v in schema['enum']): return False
    if 'type' in schema and type(value)!=TYPES[schema['type']]: return False
    if isinstance(value,dict):
        if any(key not in value for key in schema.get('required',[])): return False
        if any(key in value and not matches(value[key], item) for key,item in schema.get('properties',{}).items()): return False
    if isinstance(value,list) and 'items' in schema:
        if any(not matches(item,schema['items']) for item in value): return False
    return True


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--db',type=Path,required=True)
    parser.add_argument('--index-raw',type=Path,required=True)
    parser.add_argument('--evidence',type=Path,required=True)
    parser.add_argument('--cli',type=Path,default=ROOT/'src/UO.Graphs.Cli/bin/Release/net10.0/uographs.dll')
    parser.add_argument('--symbol',default='HelloGraph.Domain.Greeter.Greet(string)')
    args=parser.parse_args()
    args.evidence.mkdir(parents=True,exist_ok=False)
    schema=json.loads(SCHEMA.read_text())
    cli=['dotnet',str(args.cli)]
    commands=[['doctor'],['status','--db',str(args.db)],['query','find','Greet','--limit','1','--db',str(args.db)],
              ['query','callees',args.symbol,'--view','compact','--db',str(args.db)],
              ['context',args.symbol,'--max-chars','50000','--db',str(args.db)],
              ['export','--format','json','--max-nodes','2','--limit','1','--db',str(args.db)]]
    count=0
    for i,argv in enumerate(commands):
        result=subprocess.run(cli+argv,capture_output=True,text=True,timeout=30)
        (args.evidence/f'{i}.json').write_text(result.stdout)
        assert result.returncode==0,result.stderr
        document=json.loads(result.stdout)
        assert matches(document,schema),argv
        invalid=copy.deepcopy(document)
        if 'metadata' in invalid: invalid['metadata']['formatVersion']=999
        else: invalid[next(k for k in ['compactVersion','contractVersion','schemaVersion','formatVersion'] if k in invalid)]=999
        assert not matches(invalid,schema)
        count+=1
    raw=args.index_raw.read_text();document=json.loads(raw[raw.index('{'):])
    assert matches(document,schema)
    document['contractVersion']=999;assert not matches(document,schema)
    print(f'PASS: {count+1} actual envelopes and {count+1} incompatible-version negatives; schema keyword subset only')


if __name__=='__main__': main()
