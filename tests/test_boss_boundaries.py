"""Keep concrete boss behavior dependencies inside their ownership folders."""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
RUNTIME = ROOT / 'Assets/K4RMA/Runtime'
BOSSES = RUNTIME / 'Enemies/Bosses'
OWNERS = ('SwordWave', 'RisingSlash', 'Guard')
SHARED_PARTIALS = {'KarmaEnemy', 'KarmaCharacterAnimator', 'KarmaBossTuning'}


def code(path):
    text = path.read_text()
    return re.sub(r'//[^\n]*|/\*.*?\*/|@?"(?:""|\\.|[^"\\])*"', '', text, flags=re.S)


class BossBoundaries(unittest.TestCase):
    def test_common_code_has_no_concrete_boss_dependencies(self):
        owned_types = set()
        for path in BOSSES.rglob('*.cs'):
            owned_types.update(re.findall(r'\b(?:class|struct|enum)\s+(\w+)', code(path)))
        owned_types -= SHARED_PARTIALS
        for path in [*RUNTIME.rglob('*.cs'), *(ROOT / 'Assets/K4RMA/Core').rglob('*.cs')]:
            if BOSSES in path.parents:
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
                types[owner].update(re.findall(r'\b(?:class|struct|enum)\s+(\w+)', code(path)))
            types[owner] -= SHARED_PARTIALS
        for owner in OWNERS:
            foreign = set().union(*(types[o] for o in OWNERS if o != owner)) - types[owner]
            for path in (BOSSES / owner).rglob('*.cs'):
                with self.subTest(path=path.relative_to(ROOT)):
                    self.assertFalse(set(re.findall(r'\b\w+\b', code(path))) & foreign)

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
        self.assertFalse((ROOT / 'Assets/K4RMA/Art').exists())
        self.assertFalse((ROOT / 'Assets/K4RMA/Art.meta').exists())
        for path in (ROOT / 'tools').glob('*sword*'):
            self.fail(f'SwordWave tools belong in tools/SwordWave: {path.name}')

    def test_boss_registrations_are_local_and_preserved(self):
        for owner, name in [('SwordWave', 'Sword'), ('RisingSlash', 'Rising'), ('Guard', 'Guard')]:
            text = (BOSSES / owner / f'Karma{name}BossBehavior.cs').read_text()
            self.assertIn(f'[BossKind(Essence.{owner})]', text)
            self.assertIn('[UnityEngine.Scripting.Preserve]', text)
            self.assertTrue((BOSSES / owner / f'Settings/KarmaBossTuning.{name}.cs').is_file())


if __name__ == '__main__':
    unittest.main()
