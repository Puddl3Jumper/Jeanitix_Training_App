from PIL import Image, ImageChops
import numpy as np

# Load foreground
fg = Image.open('Gym_App/Resources/drawable/foreground.png').convert('RGBA')
pixels = fg.load()

print(f'Original foreground: {fg.size}')

# Create a new image with transparency
new_fg = Image.new('RGBA', fg.size, (0, 0, 0, 0))

# Find the icon by looking for non-white/non-black areas
# Threshold: keep pixels that are significantly colored (likely the gold/yellow icon)
for y in range(fg.height):
    for x in range(fg.width):
        r, g, b, a = pixels[x, y]
        
        # Keep pixels that are:
        # - Not pure white (background)
        # - Not very dark (also likely background)
        # - Have significant color (the icon)
        brightness = (r + g + b) / 3
        
        if brightness > 30 and brightness < 250:  # Not too dark, not too bright
            # This is likely part of the icon
            new_fg.putpixel((x, y), (r, g, b, a))
        elif r > 150 and g > 100 and b < 150:  # Yellow/gold tones
            # Definitely part of the icon
            new_fg.putpixel((x, y), (r, g, b, a))

# Save the transparent foreground
new_fg.save('Gym_App/Resources/drawable/foreground.png', 'PNG')

# Count transparent pixels
new_pixels = new_fg.load()
transparent = sum(1 for x in range(new_fg.width) for y in range(new_fg.height) if new_pixels[x,y][3] < 10)
total = new_fg.width * new_fg.height
print(f'New foreground: {transparent}/{total} ({transparent*100//total}%) transparent')
print('✓ Foreground updated with transparency')
