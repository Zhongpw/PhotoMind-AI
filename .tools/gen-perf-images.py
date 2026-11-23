import os, sys
from PIL import Image

out = sys.argv[1] if len(sys.argv) > 1 else r"TestData\perf10k"
count = int(sys.argv[2]) if len(sys.argv) > 2 else 10000
os.makedirs(out, exist_ok=True)

# Distribute across a few subfolders to exercise recursive scan.
subs = 10
for i in range(count):
    d = os.path.join(out, f"batch{i % subs}")
    os.makedirs(d, exist_ok=True)
    # 32x32 solid color, varied by index so files differ.
    r = (i * 7) % 256
    g = (i * 13) % 256
    b = (i * 29) % 256
    img = Image.new("RGB", (32, 32), (r, g, b))
    img.save(os.path.join(d, f"img{i:05d}.jpg"), quality=80)
    if (i + 1) % 2000 == 0:
        print(f"generated {i + 1}/{count}")

print("DONE", count)
