"""Check Unity asset identities and physical loading paths after repository moves."""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'Assets'
GUID = re.compile(r'\bguid: ([0-9a-f]{32})')
TEXT_ASSETS = {'.asset', '.unity', '.prefab', '.mat', '.anim', '.controller', '.meta'}


class AssetIntegrity(unittest.TestCase):
    def test_meta_pairs_and_unique_guids(self):
        identities = {}
        for meta in ASSETS.rglob('*.meta'):
            with self.subTest(meta=meta.relative_to(ROOT)):
                self.assertTrue(Path(str(meta)[:-5]).exists(), 'Orphan meta')
                match = re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text(), re.M)
                self.assertIsNotNone(match)
                self.assertNotIn(match[1], identities, f'Duplicate GUID: {identities.get(match[1])}')
                identities[match[1]] = meta
        for path in ASSETS.rglob('*'):
            if path.name.startswith('.') or path.suffix == '.meta':
                continue
            self.assertTrue(Path(str(path) + '.meta').is_file(), f'Missing meta: {path}')

    def test_case_sensitive_paths_and_resource_keys(self):
        physical, resources = {}, {}
        for path in ASSETS.rglob('*'):
            if path.name.startswith('.') or path.suffix == '.meta':
                continue
            relative = path.relative_to(ASSETS).as_posix()
            self.assertNotIn(relative.casefold(), physical, f'Case collision: {relative}')
            physical[relative.casefold()] = relative
            if path.is_file() and 'Resources' in path.parts:
                # Different Unity types may intentionally share a resource basename.
                key = '/'.join(path.parts[path.parts.index('Resources') + 1:]).casefold()
                self.assertNotIn(key, resources, f'Duplicate resource: {path}, {resources.get(key)}')
                resources[key] = path

    def test_serialized_guids_resolve(self):
        known = set()
        for folder in (ASSETS, ROOT / 'Packages', ROOT / 'Library/PackageCache'):
            for meta in folder.rglob('*.meta'):
                match = re.search(r'^guid: ([0-9a-f]{32})', meta.read_text(errors='ignore'), re.M)
                if match:
                    known.add(match[1])
        if not (ROOT / 'Library/PackageCache').exists():
            self.skipTest('Import with Unity first to resolve package-owned GUIDs')
        missing = []
        for folder in (ASSETS, ROOT / 'ProjectSettings'):
            for path in folder.rglob('*'):
                if path.suffix not in TEXT_ASSETS:
                    continue
                for guid in set(GUID.findall(path.read_text(errors='ignore'))):
                    if guid not in known and not guid.startswith('00000000'):
                        missing.append((str(path.relative_to(ROOT)), guid))
        self.assertEqual(missing, [])

    def test_artwork_sources_and_shader_includes_exist(self):
        for path in ASSETS.rglob('*.asset'):
            for source in re.findall(r'^\s*source: (Assets/.+)$', path.read_text(), re.M):
                self.assertTrue((ROOT / source).exists(), f'{path}: {source}')
        for path in ASSETS.rglob('*.shader'):
            for include in re.findall(r'#include\s+"([^\"]+)"', path.read_text()):
                if include.startswith('Packages/'):
                    continue  # Unity compiler resolves versioned package includes.
                candidate = ROOT / include if include.startswith('Assets/') else path.parent / include
                self.assertTrue(candidate.is_file(), f'{path}: {include}')

    def test_editor_and_tool_asset_paths_resolve(self):
        # Authoring destinations matter even when existing assets still load.
        paths = [*(ASSETS / 'Scripts').rglob('*.cs'), *(ROOT / 'tools').rglob('*.py')]
        self.assertTrue(paths)
        for path in paths:
            for target in re.findall(r'''["'](Assets/[^"'\n]+)["']''', path.read_text()):
                self.assertTrue((ROOT / target).exists(), f'{path}: {target}')

    def test_build_scenes_preserve_path_and_identity(self):
        settings = (ROOT / 'ProjectSettings/EditorBuildSettings.asset').read_text()
        scenes = re.findall(r'path: (Assets/[^\n]+)\n\s+guid: ([0-9a-f]{32})', settings)
        self.assertTrue(scenes)
        for path, guid in scenes:
            self.assertTrue((ROOT / path).is_file(), path)
            self.assertIn('guid: ' + guid, (ROOT / (path + '.meta')).read_text())


if __name__ == '__main__':
    unittest.main()
