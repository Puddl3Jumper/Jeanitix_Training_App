from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


def shrink_png(path: Path, scale: float) -> None:
	image = Image.open(path).convert('RGBA')
	width, height = image.size
	print(f'{path.as_posix()}: original={image.size}')

	if not (0 < scale <= 1):
		raise ValueError(f'scale must be in (0, 1], got {scale}')

	new_width = max(1, int(round(width * scale)))
	new_height = max(1, int(round(height * scale)))

	canvas = Image.new('RGBA', (width, height), (0, 0, 0, 0))
	scaled = image.resize((new_width, new_height), Image.Resampling.LANCZOS)

	offset_x = (width - new_width) // 2
	offset_y = (height - new_height) // 2
	canvas.paste(scaled, (offset_x, offset_y), scaled)

	canvas.save(path, 'PNG')
	print(f'{path.as_posix()}: scaled_to={int(round(scale * 100))}% centered')


def main() -> int:
	parser = argparse.ArgumentParser(description='Shrink adaptive icon foreground assets in-place.')
	parser.add_argument('--scale', type=float, default=0.65, help='Scale factor in (0, 1]. Default: 0.65')
	args = parser.parse_args()

	root = Path('Gym_App/Resources')
	targets: list[Path] = []

	# Drawable fallback/source foreground.
	targets.append(root / 'drawable' / 'foreground.png')

	# Adaptive icon foregrounds used by @mipmap/appicon_foreground.
	targets.extend(sorted(root.glob('mipmap-*/appicon_foreground.png')))

	missing = [p for p in targets if not p.exists()]
	if missing:
		print('Warning: some expected assets were not found:')
		for p in missing:
			print(f'  - {p.as_posix()}')

	any_processed = False
	for path in targets:
		if path.exists():
			shrink_png(path, args.scale)
			any_processed = True

	if not any_processed:
		print('No assets processed.')
		return 2

	return 0


if __name__ == '__main__':
	raise SystemExit(main())
