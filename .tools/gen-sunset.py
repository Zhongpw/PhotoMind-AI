from PIL import Image, ImageDraw

w, h = 480, 320
img = Image.new("RGB", (w, h))
px = img.load()

# Vertical gradient: deep blue (top) -> warm orange (horizon).
for y in range(h):
    t = y / (h - 1)
    r = int(30 + (235 - 30) * t)
    g = int(40 + (120 - 40) * t)
    b = int(90 + (60 - 90) * t)
    for x in range(w):
        px[x, y] = (r, g, b)

draw = ImageDraw.Draw(img)
# Sun disc near the horizon.
draw.ellipse((w // 2 - 55, int(h * 0.62) - 55, w // 2 + 55, int(h * 0.62) + 55), fill=(255, 215, 90))
# Sea below the horizon.
draw.rectangle((0, int(h * 0.78), w, h), fill=(25, 55, 85))

out = r"D:\AI_Agent+\dsh_next\PhotoMindAI\TestData\sample\photo-sunset.jpg"
img.save(out, "JPEG", quality=90)
print("saved", out, img.size)
