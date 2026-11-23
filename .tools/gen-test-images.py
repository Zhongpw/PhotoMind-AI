# -*- coding: utf-8 -*-
"""Generate a small, deterministic set of test images for PhotoMind AI scan verification.
Writes into TestData/sample/ (never touches real user photos)."""
import os
from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), "..", "TestData", "sample")
os.makedirs(ROOT, exist_ok=True)
os.makedirs(os.path.join(ROOT, "sub", "nested"), exist_ok=True)

def save(img, rel, fmt, **kw):
    p = os.path.join(ROOT, rel.replace("/", os.sep))
    os.makedirs(os.path.dirname(p), exist_ok=True)
    img.save(p, fmt, **kw)
    print("wrote", p)

# Valid images in every Phase-1 supported format
save(Image.new("RGB", (160, 120), (255, 0, 0)),        "photo1.jpg",   "JPEG", quality=85)
save(Image.new("RGB", (200, 150), (0, 180, 0)),        "photo2.png",   "PNG")
save(Image.new("RGB", (128, 128), (0, 0, 220)),        "photo3.webp",  "WEBP")
save(Image.new("RGB", (100, 80),  (255, 220, 0)),      "photo4.bmp",   "BMP")
gif = Image.new("RGB", (90, 90), (160, 40, 200)).convert("P", palette=Image.Palette.ADAPTIVE)
save(gif, "photo5.gif", "GIF")

# TIFF: recognised by magic but NOT in the supported-for-AI set -> should be skipped
save(Image.new("RGB", (140, 100), (128, 128, 128)),    "photo6.tiff",  "TIFF")

# Nested directory (verifies recursion)
save(Image.new("RGB", (120, 90),  (0, 200, 200)),      "sub/nested/photo7.png", "PNG")

# Corrupt file with no image magic -> format Unknown -> skipped
with open(os.path.join(ROOT, "broken.jpg"), "wb") as f:
    f.write(b"\x01\x02\x03\x04not-an-image-at-all\x00\xff\x00" * 8)
print("wrote broken.jpg")

# JPEG magic header but corrupt body -> detected as Jpeg, decode fails -> Failed
with open(os.path.join(ROOT, "fakemagic.jpg"), "wb") as f:
    f.write(b"\xff\xd8\xff\xe0" + b"\x00" * 64 + b"garbage-not-a-real-jpeg")
print("wrote fakemagic.jpg")

# Non-image extension -> never enumerated by the candidate filter
with open(os.path.join(ROOT, "notanimage.txt"), "w") as f:
    f.write("hello, this is not an image")
print("wrote notanimage.txt")

print("DONE")
