#!/usr/bin/env python3
"""Audit duplicate images and losslessly recompress PNGs without changing Unity metadata."""
import argparse
from concurrent.futures import ThreadPoolExecutor
import hashlib
from itertools import repeat
from pathlib import Path
import struct
import zlib


ROOT = Path(__file__).resolve().parents[1]
SIGNATURE = b'\x89PNG\r\n\x1a\n'
IMAGE_SUFFIXES = {'.png', '.jpg', '.jpeg', '.webp', '.tga', '.psd', '.gif', '.bmp', '.tif', '.tiff'}


def chunks(data):
    if not data.startswith(SIGNATURE):
        raise ValueError('Invalid PNG signature')
    offset = len(SIGNATURE)
    result = []
    while offset < len(data):
        if offset + 12 > len(data):
            raise ValueError('Truncated PNG chunk')
        size = struct.unpack_from('>I', data, offset)[0]
        end = offset + size + 12
        if end > len(data):
            raise ValueError('Truncated PNG payload')
        kind = data[offset + 4:offset + 8]
        payload = data[offset + 8:end - 4]
        crc = struct.unpack_from('>I', data, end - 4)[0]
        if zlib.crc32(kind + payload) != crc:
            raise ValueError('Invalid PNG chunk CRC')
        result.append((kind, payload, data[offset:end]))
        offset = end
    if not result or result[0][0] != b'IHDR' or result[-1][0] != b'IEND':
        raise ValueError('Missing PNG header or end')
    return result


def optimize_png(data):
    entries = chunks(data)
    # Animated PNGs have additional compressed frame streams; leave them intact.
    if any(kind == b'acTL' for kind, _, _ in entries):
        return data
    positions = [i for i, entry in enumerate(entries) if entry[0] == b'IDAT']
    if not positions or positions != list(range(positions[0], positions[-1] + 1)):
        raise ValueError('Missing or nonconsecutive PNG image data')
    original = b''.join(entries[i][1] for i in positions)
    scanlines = zlib.decompress(original)
    best = original
    for strategy in (zlib.Z_DEFAULT_STRATEGY, zlib.Z_FILTERED, zlib.Z_RLE):
        compressor = zlib.compressobj(9, zlib.DEFLATED, 15, 9, strategy)
        candidate = compressor.compress(scanlines) + compressor.flush()
        if len(candidate) < len(best):
            best = candidate
    # Retain every non-IDAT chunk, including color profiles and author metadata.
    payload = b'IDAT' + best
    idat = struct.pack('>I', len(best)) + payload + struct.pack('>I', zlib.crc32(payload))
    result = SIGNATURE + b''.join(
        idat if i == positions[0] else raw
        for i, (kind, _, raw) in enumerate(entries)
        if kind != b'IDAT' or i == positions[0])
    if len(result) >= len(data):
        return data
    verified = chunks(result)
    if zlib.decompress(b''.join(p for k, p, _ in verified if k == b'IDAT')) != scanlines:
        raise ValueError('Recompression changed PNG scanlines')
    if [(k, p) for k, p, _ in entries if k != b'IDAT'] != [(k, p) for k, p, _ in verified if k != b'IDAT']:
        raise ValueError('Recompression changed PNG metadata')
    return result


def inspect_image(path, recompress=True):
    data = path.read_bytes()
    optimized = optimize_png(data) if recompress and path.suffix.lower() == '.png' else data
    return path, data, optimized


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('paths', nargs='*', type=Path, help='Image files or directories (default: all game art)')
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument('--apply', action='store_true', help='Write only smaller, verified PNGs; never delete duplicates')
    mode.add_argument('--check', action='store_true', help='Exit 1 if recompression or duplicate review is needed')
    parser.add_argument('--duplicates-only', action='store_true', help='Find byte-identical files without recompressing or validating image data')
    args = parser.parse_args()
    if args.duplicates_only and args.apply:
        parser.error('--duplicates-only cannot be combined with --apply')
    files = set()
    for path in args.paths or [ROOT / 'Assets/Arts']:
        if not path.exists():
            parser.error(f'Path does not exist: {path}')
        files.update(p.resolve() for p in (path.rglob('*') if path.is_dir() else [path])
                     if p.is_file() and p.suffix.lower() in IMAGE_SUFFIXES)
    groups = {}
    saved = changed = 0
    with ThreadPoolExecutor(max_workers=4) as pool:
        for path, data, optimized in pool.map(inspect_image, sorted(files), repeat(not args.duplicates_only)):
            groups.setdefault(hashlib.sha256(optimized).hexdigest(), []).append(path)
            if len(optimized) < len(data):
                reduction = len(data) - len(optimized)
                saved += reduction
                changed += 1
                print(f'{"OPTIMIZED" if args.apply else "CAN OPTIMIZE"}: {path} (-{reduction:,} bytes)', flush=True)
                if args.apply:
                    path.write_bytes(optimized)
    duplicates = [paths for paths in groups.values() if len(paths) > 1]
    for paths in duplicates:
        print('DUPLICATE (review GUID, Sprite IDs, importer and Resources paths before merging):')
        for path in paths:
            print(f'  {path}')
    if args.duplicates_only:
        print(f'{len(files)} images; {len(duplicates)} byte-identical duplicate groups (compression not checked)')
    else:
        print(f'{len(files)} images; {changed} {"optimized" if args.apply else "can optimize"}; '
              f'{saved:,} bytes {"saved" if args.apply else "savable"}; {len(duplicates)} duplicate groups')
    return 1 if args.check and (changed or duplicates) else 0


if __name__ == '__main__':
    raise SystemExit(main())
