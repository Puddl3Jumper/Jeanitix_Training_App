from PIL import Image

# Load foreground with white background
fg = Image.open('Gym_App/Resources/drawable/foreground.png').convert('RGBA')
pixels = fg.load()

print(f'Original foreground: {fg.size}')

# Create new image with transparency
new_fg = Image.new('RGBA', fg.size, (0, 0, 0, 0))

# Remove white/light background, keep only the colored icon
for y in range(fg.height):
    for x in range(fg.width):
        r, g, b, a = pixels[x, y]
        
        # If pixel is nearly white, make it transparent
        if r > 240 and g > 240 and b > 240:
            continue  # Skip white pixels (make transparent)
        else:
            # Keep colored pixels (the icon)
            new_fg.putpixel((x, y), (r, g, b, 255))

# Save transparent foreground
new_fg.save('Gym_App/Resources/drawable/foreground.png', 'PNG')

# Count results
new_pixels = new_fg.load()
transparent = sum(1 for x in range(new_fg.width) for y in range(new_fg.height) if new_pixels[x,y][3] < 10)
total = new_fg.width * new_fg.height
print(f'Transparent pixels: {transparent}/{total} ({transparent*100//total}%)')
print('✓ White background removed - foreground is now transparent')
