#!/usr/bin/env python3
"""Deterministic, dependency-free C# benchmark fixture generator (never overwrites)."""
import argparse
import json
from pathlib import Path


def generate(root, projects=10, classes=4, methods=8):
    root = Path(root).absolute()
    if min(projects, classes, methods) < 1:
        raise ValueError('All dimensions must be positive')
    root.mkdir(parents=True, exist_ok=False)
    # Stop ancestor repo build/package configuration from contaminating the dataset.
    (root / 'Directory.Build.props').write_text('<Project />\n')
    (root / 'Directory.Build.targets').write_text('<Project />\n')
    (root / 'Directory.Packages.props').write_text('<Project />\n')
    (root / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>\n')
    (root / 'global.json').write_text(json.dumps({'sdk': {'version': '10.0.201', 'rollForward': 'disable'}}))
    names = [f'P{i:04}' for i in range(projects)]
    for i, name in enumerate(names):
        project = root / name
        project.mkdir()
        # Bounded-depth star: every consumer calls P0000; final consumer is a leaf.
        reference = '' if i == 0 else '<ItemGroup><ProjectReference Include="../P0000/P0000.csproj" /></ItemGroup>'
        (project / f'{name}.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable></PropertyGroup>' + reference + '</Project>\n')
        for c in range(classes):
            body = []
            for m in range(methods):
                expression = str(m) if i == 0 else f'P0000.C0.M0() + {m}'
                body.append(f'    public static int M{m}() => {expression};')
            (project / f'C{c}.cs').write_text(f'namespace {name};\npublic class C{c}\n{{\n' + '\n'.join(body) + '\n}\n')
    (root / 'Synthetic.slnx').write_text('<Solution>\n' + ''.join(f'  <Project Path="{n}/{n}.csproj" />\n' for n in names) + '</Solution>\n')
    manifest = {'projects': projects, 'classesPerProject': classes, 'methodsPerClass': methods,
                'sourceFiles': projects * classes, 'methods': projects * classes * methods,
                'topology': 'star; P0000 dependency, last project leaf', 'seed': 'deterministic-v1',
                'sourceBytes': sum(p.stat().st_size for p in root.rglob('*.cs'))}
    (root / 'dataset.json').write_text(json.dumps(manifest, indent=2) + '\n')
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('destination', type=Path)
    parser.add_argument('--projects', type=int, choices=(10, 100, 1000), default=10)
    parser.add_argument('--classes', type=int, default=4)
    parser.add_argument('--methods', type=int, default=8)
    args = parser.parse_args()
    print(json.dumps(generate(args.destination, args.projects, args.classes, args.methods), indent=2))
