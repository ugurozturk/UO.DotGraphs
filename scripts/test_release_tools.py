#!/usr/bin/env python3
import json
import tempfile
import unittest
import sys
from pathlib import Path
from safe_export import export
from synthetic_solution import generate


class ReleaseToolsTests(unittest.TestCase):
    def test_dimensions_references_and_no_overwrite(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder).resolve() / 'dataset'
            manifest = generate(root, 10, 2, 3)
            self.assertEqual(60, manifest['methods'])
            self.assertEqual(20, len(list(root.rglob('*.cs'))))
            self.assertIn('P0000.csproj', (root / 'P0009/P0009.csproj').read_text())
            with self.assertRaises(FileExistsError): generate(root)

    def test_export_is_exclusive_and_literal(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder).resolve() / 'literal;$(data).json'
            self.assertEqual(0, export(path, [sys.executable, '-c', 'print("{}")']))
            self.assertEqual({}, json.loads(path.read_text()))
            with self.assertRaises(FileExistsError): export(path, [sys.executable, '-c', 'print("lost")'])
            self.assertEqual({}, json.loads(path.read_text()))
            link = Path(folder).resolve() / 'link'
            link.symlink_to(path)
            with self.assertRaises(ValueError): export(link, [sys.executable, '-c', 'print("lost")'])


if __name__ == '__main__': unittest.main()
