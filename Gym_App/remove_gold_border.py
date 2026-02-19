from PIL import Image
import math

fg = Image.open('Gym_App/Resources/drawable/foreground.png').convert('RGBA')
pixels = fg.load()
w, h = fg.size
cx, cy = w // 2, h // 2

print(f'Original: {fg.size}')

# Create new image
new_fg = Image.new('RGBA', (w, h), (0, 0, 0, 0))
new_pixels = new_fg.load()

# Copy pixels but skip the gold circular ring
for y in range(h):
    for x in range(w):
        r, g, b, a = pixels[x, y]
        
        # Calculate distance from center
        dx = x - cx
        dy = y - cy
        distance = math.sqrt(dx*dx + dy*dy)
        
        # Skip gold ring pixels (radius ~150-160 at 1024 size, scales to 100-106 at 432)
        # At 1024 size, that's roughly 231-245px radius
        is_gold_ring = (230 <= distance <= 250 and 
                       r > 150 and g > 100 and b < 150 and a > 100)
        
        if not is_gold_ring:
            new_pixels[x, y] = (r, g, b, a)

# Save
new_fg.save('Gym_App/Resources/drawable/foreground.png', 'PNG')
print('✓ Gold circular border removed from foreground')
