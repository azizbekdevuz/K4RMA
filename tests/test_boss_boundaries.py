"""Keep concrete boss behavior dependencies inside their ownership folders."""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
RUNTIME = ROOT / 'Assets/Scripts'
BOSSES = RUNTIME
OWNERS = ('SwordWave', 'RisingSlash', 'Guard')
SHARED_PARTIALS = {'KarmaEnemy', 'KarmaCharacterAnimator', 'KarmaBossTuning'}


def code(path):
    text = path.read_text()
    return re.sub(r'//[^\n]*|/\*.*?\*/|@?"(?:""|\\.|[^"\\])*"', '', text, flags=re.S)


def owned_type_names(text):
    """Names reachable without first referring to another boss-owned type.

    A helper such as KarmaSW05VFX.Clip requires its owning type (or a using
    static directive naming that owner). Checking the owner protects that
    boundary without confusing unrelated Clip methods or Kind fields.
    Members of shared partial hosts still need their own boundary checks.
    """
    result, scopes = set(), []
    pending = None
    for token in re.finditer(r'\b(?:class|struct|enum|interface)\s+(\w+)|[{}]', text):
        if token.group(1):
            pending = token.group(1)
            if all(scope is None or scope in SHARED_PARTIALS for scope in scopes):
                result.add(pending)
        elif token.group() == '{':
            scopes.append(pending)
            pending = None
        elif scopes:
            scopes.pop()
    return result - SHARED_PARTIALS


class BossTypeDiscovery(unittest.TestCase):
    def test_nested_helpers_do_not_claim_unrelated_identifiers(self):
        owned = owned_type_names('''namespace Game {
            class KarmaSW05VFX { public enum Clip { Charge } enum Kind { Glow } }
            class KarmaSwordWaveAnimation { struct Mote { } }
        }''')
        common = 'target.Kind; AudioClip Clip() { return null; } struct Mote {}'
        self.assertFalse(set(re.findall(r'\b\w+\b', common)) & owned)
        for reference in ('KarmaSW05VFX.Clip', 'using static KarmaSW05VFX;',
                          'new KarmaSwordWaveAnimation()'):
            self.assertTrue(set(re.findall(r'\b\w+\b', reference)) & owned)

    def test_types_in_shared_partial_hosts_remain_protected(self):
        owned = owned_type_names('''namespace Game {
            partial class KarmaEnemy {
                class GD05Pattern { struct Mote {} }
            }
            partial class KarmaCharacterAnimator { class SwordMotion {} }
            class KarmaGuardDome {}
        }''')
        self.assertEqual(owned, {'GD05Pattern', 'SwordMotion', 'KarmaGuardDome'})
        for reference in ('new GD05Pattern()', 'KarmaEnemy.GD05Pattern',
                          'typeof(KarmaGuardDome)', 'SwordMotion motion;'):
            self.assertTrue(set(re.findall(r'\b\w+\b', reference)) & owned)


class BossBoundaries(unittest.TestCase):
    def test_common_code_has_no_concrete_boss_dependencies(self):
        owned_types = set()
        for owner in OWNERS:
            paths = list((BOSSES / owner).rglob('*.cs'))
            self.assertTrue(paths, f'Missing owner sources: {owner}')
            for path in paths:
                owned_types.update(owned_type_names(code(path)))
        for path in RUNTIME.rglob('*.cs'):
            if any(BOSSES / owner in path.parents for owner in OWNERS):
                continue
            text = code(path)
            with self.subTest(path=path.relative_to(ROOT)):
                self.assertIsNone(re.search(r'\bEnemyAttack\.(?:SW|UP|GD)\w*', text))
                self.assertFalse(set(re.findall(r'\b\w+\b', text)) & owned_types)
                self.assertIsNone(re.search(r'\b(?:swordGuard|frontalGuard|shieldUntil|shieldWarningUntil|nextShield|wavePhase|waveSwingAt)\b', text))

    def test_bosses_do_not_reference_other_boss_types(self):
        types = {}
        for owner in OWNERS:
            types[owner] = set()
            for path in (BOSSES / owner).rglob('*.cs'):
                types[owner].update(owned_type_names(code(path)))
        for owner in OWNERS:
            foreign = set().union(*(types[o] for o in OWNERS if o != owner)) - types[owner]
            for path in (BOSSES / owner).rglob('*.cs'):
                with self.subTest(path=path.relative_to(ROOT)):
                    self.assertFalse(set(re.findall(r'\b\w+\b', code(path))) & foreign)

    def test_shared_enemy_host_does_not_choose_a_boss_effect(self):
        paths = list((RUNTIME / 'Combat').glob('KarmaEnemy*.cs')) + [RUNTIME / 'Combat/KarmaBossBehavior.cs']
        self.assertGreater(len(paths), 1)
        for path in paths:
            with self.subTest(path=path.relative_to(ROOT)):
                self.assertNotRegex(code(path), r'\bEssence\.(?:Guard|SwordWave|RisingSlash)\b')

    def test_owned_state_and_presentation_stay_with_the_owner(self):
        members = {
            'SwordWave': {'waveSwingAt', 'wavePhase', 'wavePhaseSwingAt', 'waveReleasedUntil',
                          'WaveMotionRevision', 'PrepareWaveAttack', 'PlayWavePhase', 'ReleaseWavePhase',
                          'ShowSwordRetreat', 'ShowGuardianSpacing', 'ConfigureSwordPresentation',
                          'GuardianDamageSprite', 'GuardianDamageScale', 'GuardianPhaseOverridesPose'},
            'Guard': {'shieldUntil', 'nextShield', 'shieldWarningUntil', 'frontalGuard',
                      'TryGuardCircleHit', 'guardStagger', 'heavyStrike', 'EndHeavyStrike', 'ConfigureHeavyStrike',
                      'PrepareHeavyStrike', 'SwingHeavyStrike', 'HeavyHandPosition'},
            'RisingSlash': {'ConfigureRisingPresentation'},
        }
        for owner, names in members.items():
            for path in RUNTIME.rglob('*.cs'):
                if BOSSES / owner in path.parents:
                    continue
                with self.subTest(owner=owner, path=path.relative_to(ROOT)):
                    self.assertFalse(set(re.findall(r'\b\w+\b', code(path))) & names)

    def test_boss_owned_tests_are_in_owner_folders(self):
        for path in (ROOT / 'tests/Unity').glob('*.cs'):
            self.assertNotRegex(path.name, r'Karma(?:Sword|SW05|Guard|Rising)')

    def test_boss_art_and_tools_use_owned_paths(self):
        self.assertFalse((ROOT / 'Assets/Art').exists())
        self.assertFalse((ROOT / 'Assets/Art.meta').exists())
        for path in (ROOT / 'tools').glob('*sword*'):
            self.fail(f'SwordWave tools belong in tools/SwordWave: {path.name}')

    def test_sprite_library_sources_exist_and_stay_with_the_owner(self):
        libraries = [ROOT / 'Assets/Data/Resources/KarmaSpriteLibrary.asset',
                     *(ROOT / 'Assets/Data/Resources/BossArtwork').glob('*BossSprites.asset')]
        self.assertEqual(len(libraries), 4)
        owners = {'KarmaSwordBossSprites': 'SwordWave', 'KarmaRisingBossSprites': 'RisingSlash',
                  'KarmaGuardBossSprites': 'Guard'}
        for library in libraries:
            owner = owners.get(library.stem)
            for source in re.findall(r'^\s*source: (.+)$', library.read_text(), flags=re.M):
                with self.subTest(library=library.relative_to(ROOT), source=source):
                    self.assertTrue((ROOT / source).exists(), source)
                    if owner is None:
                        for other in OWNERS:
                            self.assertNotIn('Arts/Resources/' + other + '/', source)
                    else:
                        for other in OWNERS:
                            if other != owner:
                                self.assertNotIn('Arts/Resources/' + other + '/', source)

    def test_boss_registrations_are_local_and_preserved(self):
        for owner, name in [('SwordWave', 'Sword'), ('RisingSlash', 'Rising'), ('Guard', 'Guard')]:
            text = (BOSSES / owner / f'Karma{name}BossBehavior.cs').read_text()
            self.assertIn(f'[BossKind(Essence.{owner})]', text)
            self.assertIn('[UnityEngine.Scripting.Preserve]', text)
            self.assertTrue((BOSSES / owner / f'KarmaBossTuning.{name}.cs').is_file())


if __name__ == '__main__':
    unittest.main()
