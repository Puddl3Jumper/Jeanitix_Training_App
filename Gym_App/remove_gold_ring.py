from PIL import Image, ImageDraw
import math

# Load foreground
fg = Image.open('Gym_App/Resources/drawable/foreground.png').convert('RGBA')
w, h = fg.size
pixels = fg.load()

print(f'Original: {fg.size}')

# Create new image
new_fg = Image.new('RGBA', (w, h), (0, 0, 0, 0))
new_pixels = new_fg.load()

cx, cy = w // 2, h // 2

# Copy only the central icon, skip the outer gold ring
# The gold ring is at radius 0.25-0.40, so keep only 0.0-0.22
for y in range(h):
    for x in range(w):
        dx = x - cx
        dy = y - cy
        distance = math.sqrt(dx*dx + dy*dy)
        radius_ratio = distance / cx  # Normalized radius
        
        # Keep only pixels in the center (the arm and dumbbell)
        # Skip the gold circular border at 0.25-0.40
        if radius_ratio < 0.24:  # Keep central icon only
            color = pixels[x, y]
            if color[3] > 10:  # Non-transparent
                new_pixels[x, y] = color

# Save
new_fg.save('Gym_App/Resources/drawable/foreground.png', 'PNG')
print('✓ Gold circular border removed - kept only central arm icon')
