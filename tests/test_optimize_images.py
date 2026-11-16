"""Verify lossless PNG compression and rejection of damaged input."""
import importlib.util
from pathlib import Path
import struct
import subprocess
import sys
import tempfile
import unittest
import zlib

OPTIMIZER_PATH = Path(__file__).resolve().parents[1] / 'tools/optimize-images.py'
spec = importlib.util.spec_from_file_location('optimize_images', OPTIMIZER_PATH)
if spec is None or spec.loader is None:
    raise ImportError(f'Cannot load image optimizer: {OPTIMIZER_PATH}')
optimizer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(optimizer)


def chunk(kind, payload):
    return struct.pack('>I', len(payload)) + kind + payload + struct.pack('>I', zlib.crc32(kind + payload))


def fixture(color=6):
    channels = 4 if color == 6 else 3
    scanlines = (b'\0' + bytes(range(channels)) * 64) * 64
    header = struct.pack('>IIBBBBB', 64, 64, 8, color, 0, 0, 0)
    compressed = zlib.compress(scanlines, 0)
    png = optimizer.SIGNATURE + chunk(b'IHDR', header) + chunk(b'gAMA', struct.pack('>I', 45455))
    png += chunk(b'tEXt', b'Author\0K4RMA')
    png += chunk(b'IDAT', compressed[:100]) + chunk(b'IDAT', compressed[100:]) + chunk(b'IEND', b'')
    return png, scanlines


class PNGOptimizationTests(unittest.TestCase):
    def test_rgb_and_rgba_preserve_exact_scanlines_and_metadata(self):
        for color in (2, 6):
            with self.subTest(color=color):
                original, scanlines = fixture(color)
                result = optimizer.optimize_png(original)
                self.assertLess(len(result), len(original))
                entries = optimizer.chunks(result)
                self.assertEqual(zlib.decompress(b''.join(p for k, p, _ in entries if k == b'IDAT')), scanlines)
                self.assertEqual([(k, p) for k, p, _ in entries if k != b'IDAT'],
                                 [(k, p) for k, p, _ in optimizer.chunks(original) if k != b'IDAT'])
                self.assertEqual(optimizer.optimize_png(result), result)

    def test_damaged_chunk_is_rejected(self):
        data, _ = fixture()
        damaged = bytearray(data)
        damaged[20] ^= 1
        with self.assertRaises(ValueError):
            optimizer.optimize_png(bytes(damaged))
        with self.assertRaises(ValueError):
            optimizer.optimize_png(data[:-2])

    def test_animated_png_is_preserved(self):
        data, _ = fixture()
        animated = data[:33] + chunk(b'acTL', struct.pack('>II', 1, 0)) + data[33:]
        self.assertEqual(optimizer.optimize_png(animated), animated)

    def test_cli_applies_checks_and_reports_duplicates_without_deleting(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / 'Pose.png'
            metadata = Path(folder) / 'Pose.png.meta'
            original, _ = fixture()
            path.write_bytes(original)
            metadata.write_text('guid: preserved\n')
            command = [sys.executable, '-B', str(OPTIMIZER_PATH), folder]
            result = subprocess.run(command + ['--check'], capture_output=True, text=True)
            self.assertEqual(result.returncode, 1, result.stderr)
            self.assertEqual(path.read_bytes(), original)
            result = subprocess.run(command + ['--apply'], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertLess(path.stat().st_size, len(original))
            self.assertEqual(metadata.read_text(), 'guid: preserved\n')
            result = subprocess.run(command + ['--check'], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            duplicate = Path(folder) / 'Duplicate.png'
            duplicate.write_bytes(path.read_bytes())
            result = subprocess.run(command + ['--check'], capture_output=True, text=True)
            self.assertEqual(result.returncode, 1, result.stderr)
            self.assertIn('DUPLICATE', result.stdout)
            self.assertTrue(path.exists() and duplicate.exists())

    def test_quick_duplicate_audit_skips_recompression(self):
        with tempfile.TemporaryDirectory() as folder:
            original, _ = fixture()
            for name in ('Pose.png', 'Copy.png'):
                (Path(folder) / name).write_bytes(original)
            command = [sys.executable, '-B', str(OPTIMIZER_PATH), folder, '--duplicates-only']
            result = subprocess.run(command + ['--check'], capture_output=True, text=True)
            self.assertEqual(result.returncode, 1, result.stderr)
            self.assertIn('1 byte-identical duplicate groups', result.stdout)
            self.assertIn('compression not checked', result.stdout)
            self.assertNotIn('CAN OPTIMIZE', result.stdout)
            self.assertEqual((Path(folder) / 'Pose.png').read_bytes(), original)
            result = subprocess.run(command + ['--apply'], capture_output=True, text=True)
            self.assertEqual(result.returncode, 2)


if __name__ == '__main__':
    unittest.main()
