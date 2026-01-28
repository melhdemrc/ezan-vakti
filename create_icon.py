from PIL import Image
import sys

# Load PNG
img = Image.open('Assets/icon.png')

# Resize to multiple sizes for .ico
sizes = [(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)]
icon_images = []

for size in sizes:
    resized = img.resize(size, Image.Resampling.LANCZOS)
    icon_images.append(resized)

# Save as .ico
icon_images[0].save('Assets/icon.ico', format='ICO', sizes=[(img.width, img.height) for img in icon_images])
print("Icon created successfully!")
