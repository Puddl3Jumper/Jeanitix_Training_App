from PIL import Image

# Create a fully transparent foreground image
size = 1024
transparent = Image.new('RGBA', (size, size), (0, 0, 0, 0))

# Save it
transparent.save('Gym_App/Resources/drawable/foreground.png', 'PNG')
print(f'✓ Created fully transparent foreground ({size}x{size})')
