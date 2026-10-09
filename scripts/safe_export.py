#!/usr/bin/env python3
"""Stream stdout to an exclusively created target; never overwrite, never run a shell.
Usage: python3 scripts/safe_export.py new.ndjson -- uographs export --format ndjson
A failed child leaves a partial file for inspection and returns its exit code.
"""
import os
import subprocess
import sys
from pathlib import Path


def export(destination, argv):
    path = Path(destination).absolute()
    for parent in [path, *path.parents]:
        if parent.is_symlink():
            raise ValueError(f'Refusing symlink destination: {parent}')
    with path.open('xb') as output:
        return subprocess.run(argv, stdout=output).returncode


if __name__ == '__main__':
    if len(sys.argv) < 4 or sys.argv[2] != '--':
        raise SystemExit(__doc__)
    try:
        raise SystemExit(export(sys.argv[1], sys.argv[3:]))
    except (OSError, ValueError) as error:
        print(f'Export refused: {error}', file=sys.stderr)
        raise SystemExit(1)
