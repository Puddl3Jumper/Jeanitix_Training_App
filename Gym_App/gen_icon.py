#!/usr/bin/env python3
from PIL import Image, ImageDraw

# Create dumbbell icon
size = 256
img = Image.new('RGB', (size, size), color=(25, 25, 25))
draw = ImageDraw.Draw(img)
cx, cy = size // 2, size // 2

# Left weight
draw.ellipse([cx - 115, cy - 45, cx - 25, cy + 45], fill=(255, 200, 0))
# Right weight
draw.ellipse([cx + 25, cy - 45, cx + 115, cy + 45], fill=(255, 200, 0))
# Center bar
draw.rectangle([cx - 40, cy - 12, cx + 40, cy + 12], fill=(255, 200, 0))

img.save('Gym_App/Resources/drawable/logo.png', 'PNG')
print('✓ Icon created')
