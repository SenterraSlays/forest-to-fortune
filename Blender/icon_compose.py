"""Frames the icon render: rounded square with a forest-green radial gradient, a warm glow
behind the diorama and a thin gold rim.  python Blender/icon_compose.py <render.png> <out.png>"""
import sys
from PIL import Image, ImageDraw, ImageFilter

render, out = sys.argv[1], sys.argv[2]
S = 1024
# Background: radial gradient from a lighter pine green in the middle to a deep green edge.
bg = Image.new("RGBA", (S, S))
px = bg.load()
cx, cy = S * 0.5, S * 0.46
for y in range(S):
    for x in range(S):
        d = min(1.0, (((x - cx) ** 2 + (y - cy) ** 2) ** 0.5) / (S * 0.72))
        t = d * d
        r = int(58 * (1 - t) + 14 * t); g = int(98 * (1 - t) + 34 * t); b = int(64 * (1 - t) + 22 * t)
        px[x, y] = (r, g, b, 255)
# Warm lantern glow behind the scene.
glow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
ImageDraw.Draw(glow).ellipse((S * 0.18, S * 0.2, S * 0.82, S * 0.78), fill=(255, 196, 96, 110))
glow = glow.filter(ImageFilter.GaussianBlur(110))
bg = Image.alpha_composite(bg, glow)
# The render, slightly smaller and with a soft contact shadow.
art = Image.open(render).convert("RGBA").resize((int(S * 0.94), int(S * 0.94)), Image.LANCZOS)
shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
ImageDraw.Draw(shadow).ellipse((S * 0.16, S * 0.78, S * 0.86, S * 0.95), fill=(0, 0, 0, 120))
shadow = shadow.filter(ImageFilter.GaussianBlur(28))
bg = Image.alpha_composite(bg, shadow)
layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
layer.paste(art, ((S - art.width) // 2, int(S * 0.04)), art)
bg = Image.alpha_composite(bg, layer)
# Rounded-square mask and a thin gold rim.
mask = Image.new("L", (S, S), 0)
ImageDraw.Draw(mask).rounded_rectangle((0, 0, S - 1, S - 1), radius=int(S * 0.2), fill=255)
icon = Image.new("RGBA", (S, S), (0, 0, 0, 0))
icon.paste(bg, (0, 0), mask)
rim = Image.new("RGBA", (S, S), (0, 0, 0, 0))
ImageDraw.Draw(rim).rounded_rectangle((6, 6, S - 7, S - 7), radius=int(S * 0.19), outline=(245, 190, 90, 200), width=10)
icon = Image.alpha_composite(icon, rim)
icon.save(out)
print("ICON", out)
