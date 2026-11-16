"""The disposable Unity runner must never overwrite another project or a live one."""
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


class ValidationRunnerSafety(unittest.TestCase):
    def run_rejected(self, project, reason):
        result = subprocess.run([sys.executable, str(ROOT / 'tools/run-boss-playchecks.py'),
                                 '--suite', 'isolation', '--work-project', str(project)],
                                capture_output=True, text=True, timeout=15)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn(reason, result.stderr)

    def test_unowned_project_is_untouched(self):
        with tempfile.TemporaryDirectory(prefix='karma-runner-safety-') as folder:
            project = Path(folder)
            (project / 'Assets').mkdir()
            canary = project / 'Assets/keep.txt'
            canary.write_text('existing work')
            self.run_rejected(project, 'not created by this validation runner')
            self.assertEqual(canary.read_text(), 'existing work')
            self.assertFalse((project / '.k4rma-validation-project').exists())

    def test_source_project_is_rejected(self):
        self.run_rejected(ROOT, 'outside the source repository')

    def test_live_validation_project_is_untouched(self):
        with tempfile.TemporaryDirectory(prefix='karma-runner-safety-') as folder:
            project = Path(folder)
            (project / '.k4rma-validation-project').write_text(str(ROOT))
            (project / 'Temp').mkdir()
            (project / 'Temp/UnityLockfile').write_text('fixture lock')
            (project / 'Assets').mkdir()
            canary = project / 'Assets/keep.txt'
            canary.write_text('live work')
            self.run_rejected(project, 'Close the validation Editor')
            self.assertEqual(canary.read_text(), 'live work')


if __name__ == '__main__':
    unittest.main()
