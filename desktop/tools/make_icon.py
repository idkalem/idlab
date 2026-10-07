"""Draws the ID Lab app icon (an Erlenmeyer flask on a dark tile) and saves Assets/idlab.ico.
Run once: python3 desktop/tools/make_icon.py  (the .ico is committed, so this is only needed to change it)."""
import os
from PIL import Image, ImageDraw

S = 512
img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)
d.rounded_rectangle([16, 16, S - 16, S - 16], radius=110, fill=(17, 24, 39, 255), outline=(53, 196, 255, 255), width=14)
# flask outline
neck_l, neck_r, top, shoulder, base_y = 214, 298, 92, 228, 420
flask = [(neck_l, top), (neck_r, top), (neck_r, shoulder), (404, base_y), (108, base_y), (neck_l, shoulder)]
d.polygon(flask, fill=(205, 228, 255, 60), outline=(230, 237, 247, 255))
d.line(flask + [flask[0]], fill=(230, 237, 247, 255), width=16, joint="curve")
# liquid
liquid = [(150, 330), (362, 330), (404, base_y - 10), (108, base_y - 10)]
d.polygon(liquid, fill=(53, 196, 255, 255))
d.rectangle([196, 80, 316, 100], fill=(230, 237, 247, 255))
# bubbles
for (x, y, r) in [(220, 362, 14), (280, 372, 10), (250, 296, 9), (262, 250, 7)]:
    d.ellipse([x - r, y - r, x + r, y + r], fill=(230, 237, 247, 220))
out = os.path.join(os.path.dirname(__file__), "..", "IDLab.Desktop", "Assets", "idlab.ico")
img.save(out, sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
img.save(out.replace(".ico", "-512.png"))
print("wrote", os.path.abspath(out))
