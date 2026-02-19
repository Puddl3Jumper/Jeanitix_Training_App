from PIL import Image
import math

fg = Image.open('Gym_App/Resources/mipmap-xxxhdpi/appicon_foreground.png').convert('RGBA')
pixels = fg.load()
w = fg.width
cx = w // 2

# Find the maximum radius where there's content
max_r = 0
for angle in range(0, 360, 10):
    rad = math.radians(angle)
    for r in range(cx):
        x = int(cx + r * math.cos(rad))
        y = int(cx + r * math.sin(rad))
        if pixels[x, y][3] > 10:  # Non-transparent
            max_r = max(max_r, r)

print(f'Icon extends to radius: {max_r}px')
print(f'Canvas radius: {cx}px')
ratio = max_r / cx
print(f'Icon uses: {ratio:.1%} of canvas radius')
print(f'Safe zone: 61% of canvas radius')
print(f'To eliminate yellow ring, scale to: {0.61 / ratio * 100:.0f}%')
