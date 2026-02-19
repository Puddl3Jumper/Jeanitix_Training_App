from PIL import Image, ImageDraw

size = 256
img = Image.new('RGBA', (size, size), (30, 30, 35, 255))
draw = ImageDraw.Draw(img)

gold = (255, 200, 0)
dark_gold = (220, 160, 0)
black = (20, 20, 25)

cx, cy = size // 2, size // 2

# Upper arm (bicep)
draw.ellipse([cx - 30, cy - 50, cx + 40, cy + 10], fill=gold, outline=dark_gold, width=3)

# Forearm
points = [(cx - 25, cy - 10), (cx - 40, cy - 60), (cx - 25, cy - 65), (cx - 10, cy - 15)]
draw.polygon(points, fill=gold, outline=dark_gold)

# Dumbbell - left weight
draw.ellipse([cx - 70, cy - 75, cx - 45, cy - 55], fill=gold, outline=dark_gold, width=2)
# Dumbbell - right weight
draw.ellipse([cx - 40, cy - 75, cx - 15, cy - 55], fill=gold, outline=dark_gold, width=2)
# Dumbbell bar
draw.rectangle([cx - 55, cy - 68, cx - 30, cy - 62], fill=dark_gold)

# Bicep definition
draw.arc([cx - 20, cy - 40, cx + 30, cy], 180, 360, fill=dark_gold, width=2)

img.save('Gym_App/Resources/drawable/logo.png', 'PNG')
print('OK')
