from PIL import Image

# Load the current logo
logo = Image.open('Gym_App/Resources/drawable/logo.png').convert('RGBA')
size = logo.size[0]

# Create new image with same size
new_img = Image.new('RGBA', (size, size), (0, 0, 0, 0))

# Scale down to 60% to fit well within safe zone (66dp circle)
scale = 0.60
new_size = int(size * scale)
resized_logo = logo.resize((new_size, new_size), Image.Resampling.LANCZOS)

# Center it
offset = (size - new_size) // 2
new_img.paste(resized_logo, (offset, offset), resized_logo)

new_img.save('Gym_App/Resources/drawable/logo.png', 'PNG')
print(f'Icon resized to {int(scale*100)}% and centered - should eliminate yellow circle')
