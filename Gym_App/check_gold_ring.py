from PIL import Image
import math

fg = Image.open('Gym_App/Resources/mipmap-xxxhdpi/appicon_foreground.png').convert('RGBA')
pixels = fg.load()
w = fg.width
cx, cy = w // 2, w // 2

# Check for gold/yellow pixels in a ring pattern
gold_ring_pixels = []
for angle in range(0, 360, 5):
    rad = math.radians(angle)
    for radius in range(100, 150):  # Check outer area
        x = int(cx + radius * math.cos(rad))
        y = int(cy + radius * math.sin(rad))
        if 0 <= x < w and 0 <= y < w:
            r, g, b, a = pixels[x, y]
            # Check if it's gold/yellow (high red, high green, low blue)
            if a > 100 and r > 150 and g > 100 and b < 150:
                gold_ring_pixels.append((radius, angle, (r, g, b, a)))

if gold_ring_pixels:
    print(f"Found {len(gold_ring_pixels)} gold/yellow pixels forming a ring!")
    radii = [p[0] for p in gold_ring_pixels]
    print(f"Ring at radius: {min(radii)}-{max(radii)}px")
    print(f"Sample colors: {gold_ring_pixels[:3]}")
else:
    print("No gold ring found in foreground image")
    
# Check if there's ANY gold in the image
any_gold = False
for y in range(w):
    for x in range(w):
        r, g, b, a = pixels[x, y]
        if a > 100 and r > 200 and g > 150 and b < 100:
            any_gold = True
            print(f"Gold pixel found at ({x}, {y}): RGB({r}, {g}, {b})")
            break
    if any_gold:
        break

if not any_gold:
    print("No gold/yellow pixels found in entire foreground image")
