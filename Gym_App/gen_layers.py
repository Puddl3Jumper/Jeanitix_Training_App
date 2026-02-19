from PIL import Image

logo = Image.open('Gym_App/Resources/drawable/logo.png').convert('RGBA')
sizes = {'mdpi': 108, 'hdpi': 162, 'xhdpi': 216, 'xxhdpi': 324, 'xxxhdpi': 432}
for density, size in sizes.items():
    resized = logo.resize((size, size), Image.Resampling.LANCZOS)
    resized.save(f'Gym_App/Resources/mipmap-{density}/appicon_foreground.png', 'PNG', optimize=False)
    print(f'{density}: {size}x{size}')
