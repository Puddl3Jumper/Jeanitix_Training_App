from PIL import Image
import os

# Load the images
foreground = Image.open('Gym_App/Resources/drawable/foreground.png').convert('RGBA')
background = Image.open('Gym_App/Resources/drawable/background.png').convert('RGBA')

print(f'Original foreground size: {foreground.size}')
print(f'Original background size: {background.size}')

# Android adaptive icon densities (all 108dp in different pixel densities)
densities = {
    'mdpi': 108,
    'hdpi': 162,
    'xhdpi': 216,
    'xxhdpi': 324,
    'xxxhdpi': 432
}

# Generate all density sizes
for density, size in densities.items():
    # Resize foreground
    fg_resized = foreground.resize((size, size), Image.Resampling.LANCZOS)
    fg_path = f'Gym_App/Resources/mipmap-{density}/appicon_foreground.png'
    fg_resized.save(fg_path, 'PNG', optimize=False)
    
    # Resize background
    bg_resized = background.resize((size, size), Image.Resampling.LANCZOS)
    bg_path = f'Gym_App/Resources/mipmap-{density}/appicon_background.png'
    bg_resized.save(bg_path, 'PNG', optimize=False)
    
    print(f'{density}: {size}x{size} - foreground & background created')

print('\n✓ All adaptive icon layers generated successfully')
