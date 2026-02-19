from PIL import Image, ImageDraw

size = 256
img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
draw = ImageDraw.Draw(img)

cx, cy = size // 2, size // 2

gold_dark = (246, 184, 0)
gold_light = (255, 213, 79)
gold = (255, 200, 0)

# Left weight
draw.ellipse([cx - 65, cy - 35, cx - 20, cy + 35], fill=gold_dark, outline=gold_light, width=2)

# Right weight
draw.ellipse([cx + 20, cy - 35, cx + 65, cy + 35], fill=gold_dark, outline=gold_light, width=2)

# Center bar
draw.rectangle([cx - 18, cy - 12, cx + 18, cy + 12], fill=gold, outline=gold_light, width=2)

# Shine details
draw.arc([cx - 65, cy - 35, cx - 20, cy + 35], 45, 135, fill=gold_light, width=3)
draw.arc([cx + 20, cy - 35, cx + 65, cy + 35], 45, 135, fill=gold_light, width=3)

img.save('Gym_App/Resources/drawable/logo.png', 'PNG')
print('OK')
