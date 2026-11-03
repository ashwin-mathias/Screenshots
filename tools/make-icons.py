#!/usr/bin/env python3
"""Generates every Screenshot icon from the values in docs/DESIGN.md §10.

Run once after changing the design, then commit the output:
    python3 tools/make-icons.py
Needs Pillow (pip install pillow). Output goes to shared/assets/icon/.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageFont

OUT = Path(__file__).resolve().parent.parent / "shared" / "assets" / "icon"
TOP_LEFT = (0x2F, 0x6F, 0xE4)      # blue
BOTTOM_RIGHT = (0x17, 0xB3, 0xA8)  # teal
SS = 4  # supersampling for smooth edges


def gradient(size):
    """Diagonal gradient, top-left to bottom-right."""
    g = Image.new("RGB", (size, size))
    px = g.load()
    for y in range(size):
        for x in range(size):
            t = (x + y) / (2 * (size - 1))
            px[x, y] = tuple(round(a + (b - a) * t) for a, b in zip(TOP_LEFT, BOTTOM_RIGHT))
    return g


def glyph(draw, box, color, stroke, crosshair=True, check=False):
    """Four capture corners, plus a crosshair (or a checkmark) in the middle."""
    x0, y0, x1, y1 = box
    w = x1 - x0
    arm = w * 0.30
    s = stroke
    r = s / 2
    for cx, cy, dx, dy in ((x0, y0, 1, 1), (x1, y0, -1, 1), (x0, y1, 1, -1), (x1, y1, -1, -1)):
        # one L per corner: a single stroke with a rounded joint and round end caps
        pts = [(cx + dx * arm, cy), (cx, cy), (cx, cy + dy * arm)]
        draw.line(pts, fill=color, width=round(s), joint="curve")
        for px_, py_ in (pts[0], pts[2]):
            draw.ellipse([px_ - r, py_ - r, px_ + r, py_ + r], fill=color)
    mx, my = (x0 + x1) / 2, (y0 + y1) / 2
    if check:
        k = w * 0.20
        pts = [(mx - k, my), (mx - k * 0.3, my + k * 0.7), (mx + k, my - k * 0.75)]
        draw.line(pts, fill=color, width=round(s * 1.1), joint="curve")
        for p in (pts[0], pts[2]):
            draw.ellipse([p[0] - s * 0.55, p[1] - s * 0.55, p[0] + s * 0.55, p[1] + s * 0.55], fill=color)
    elif crosshair:
        c = w * 0.14
        t = s * 0.7
        draw.rounded_rectangle([mx - c, my - t / 2, mx + c, my + t / 2], radius=t / 2, fill=color)
        draw.rounded_rectangle([mx - t / 2, my - c, mx + t / 2, my + c], radius=t / 2, fill=color)


def app_icon(px, margin_ratio, shadow=True):
    """The coloured app icon at px × px. margin_ratio leaves room around the tile (macOS grid)."""
    n = px * SS
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    m = round(n * margin_ratio)
    tile = n - 2 * m
    radius = round(tile * 0.225)
    mask = Image.new("L", (n, n), 0)
    ImageDraw.Draw(mask).rounded_rectangle([m, m, m + tile - 1, m + tile - 1], radius=radius, fill=255)
    if shadow and m > 0:
        sh = Image.new("RGBA", (n, n), (0, 0, 0, 0))
        sm = Image.new("L", (n, n), 0)
        off = round(tile * 0.02)
        ImageDraw.Draw(sm).rounded_rectangle([m, m + off, m + tile - 1, m + tile - 1 + off], radius=radius, fill=90)
        sm = sm.filter(ImageFilter.GaussianBlur(tile * 0.025))
        sh.putalpha(sm)
        img = Image.alpha_composite(img, sh)
    grad = gradient(max(px, 64)).resize((n, n), Image.BICUBIC).convert("RGBA")
    layer = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    layer.paste(grad, (0, 0), mask)
    img = Image.alpha_composite(img, layer)
    d = ImageDraw.Draw(img)
    inset = tile * 0.22
    stroke = max(tile * 0.065, 1.6 * SS) if px > 32 else max(tile * 0.10, 1.6 * SS)
    glyph(d, (m + inset, m + inset, m + tile - inset, m + tile - inset), (255, 255, 255, 255), stroke,
          crosshair=px >= 24)
    return img.resize((px, px), Image.LANCZOS)


def tray_icon(px, color, check=False):
    """Monochrome glyph for the Windows tray (no tile)."""
    n = px * SS
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    pad = n * 0.08
    stroke = max(n * 0.11, 1.4 * SS)
    glyph(ImageDraw.Draw(img), (pad, pad, n - pad, n - pad), color, stroke, crosshair=True, check=check)
    return img.resize((px, px), Image.LANCZOS)


def store_art(w, h, wordmark=False):
    """Full-bleed artwork for the Store listing (box art, poster art): gradient, glyph, optional name."""
    n_w, n_h = w * 2, h * 2  # 2x supersampling is enough at these sizes
    img = gradient(256).resize((n_w, n_h), Image.BICUBIC).convert("RGBA")
    d = ImageDraw.Draw(img)
    side = min(n_w, n_h) * (0.42 if not wordmark else 0.46)
    cx, cy = n_w / 2, (n_h * 0.42 if wordmark else n_h / 2)
    glyph(d, (cx - side / 2, cy - side / 2, cx + side / 2, cy + side / 2), (255, 255, 255, 255), side * 0.085 / 0.56)
    if wordmark:
        font = None
        for path in ("/usr/share/fonts/opentype/inter/Inter-SemiBold.otf", "/Library/Fonts/Inter-SemiBold.otf",
                     "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"):
            if Path(path).exists():
                font = ImageFont.truetype(path, int(n_w * 0.115))
                break
        font = font or ImageFont.load_default()
        text = "Screenshotz"
        box = d.textbbox((0, 0), text, font=font)
        d.text(((n_w - (box[2] - box[0])) / 2 - box[0], cy + side / 2 + n_h * 0.09), text, font=font, fill=(255, 255, 255, 255))
    return img.convert("RGB").resize((w, h), Image.LANCZOS)


def main():
    mac = OUT / "macos"
    win = OUT / "windows"
    tray = win / "tray"
    for d in (mac, win, tray):
        d.mkdir(parents=True, exist_ok=True)

    # macOS: 1024 grid with a 100 px margin, like Apple's template.
    master = app_icon(1024, 100 / 1024)
    master.save(OUT / "AppIcon-1024.png")
    iconset = {}
    for pt in (16, 32, 128, 256, 512):
        for scale in (1, 2):
            px = pt * scale
            im = app_icon(px, 100 / 1024) if px >= 64 else app_icon(px, 100 / 1024, shadow=False)
            name = f"icon_{pt}x{pt}{'@2x' if scale == 2 else ''}.png"
            im.save(mac / name)
            iconset[px] = im
    master.save(mac / "AppIcon.icns", sizes=[(16, 16), (32, 32), (64, 64), (128, 128),
                                              (256, 256), (512, 512), (1024, 1024)])

    # Windows: the tile fills more of the square (Windows icons have little padding).
    sizes = (16, 20, 24, 32, 40, 48, 64, 256)
    ico = {s: app_icon(s, 0.04, shadow=False) for s in sizes}
    ico[256].save(win / "AppIcon.ico", sizes=[(s, s) for s in sizes],
                  append_images=[ico[s] for s in sizes if s != 256])
    for name, w, h in (("Square44x44Logo", 44, 44), ("Square150x150Logo", 150, 150),
                       ("StoreLogo", 50, 50)):
        for scale in (100, 200):
            px = w * scale // 100
            app_icon(px, 0.04, shadow=False).save(win / f"{name}.scale-{scale}.png")
    for scale in (100, 200):
        w, h = 310 * scale // 100, 150 * scale // 100
        wide = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        tile = app_icon(h, 0.12, shadow=False)
        wide.paste(tile, ((w - h) // 2, 0), tile)
        wide.save(win / f"Wide310x150Logo.scale-{scale}.png")
    # Windows 11 also uses an unplated 44 px logo for the taskbar and Start.
    for s in (16, 24, 32, 48, 256):
        app_icon(s, 0.04, shadow=False).save(win / f"Square44x44Logo.targetsize-{s}_altform-unplated.png")

    # Tray glyphs: light-on-dark and dark-on-light, normal and success (checkmark).
    for theme, color in (("light", (255, 255, 255, 255)), ("dark", (0, 0, 0, 255))):
        for check in (False, True):
            frames = [tray_icon(s, color, check) for s in (16, 20, 24, 32)]
            name = f"tray-{'check' if check else 'viewfinder'}-{'onDark' if theme == 'light' else 'onLight'}.ico"
            frames[-1].save(tray / name, sizes=[(16, 16), (20, 20), (24, 24), (32, 32)],
                            append_images=frames[:-1])
    # Microsoft Store listing images (docs/STORE.md): tile icon, 1:1 box art, 2:3 poster art.
    store = OUT / "store"
    store.mkdir(parents=True, exist_ok=True)
    app_icon(300, 0.04, shadow=False).save(store / "Screenshotz-tile-icon-300x300.png")
    store_art(2160, 2160).save(store / "Screenshotz-box-art-2160x2160.png", optimize=True)
    store_art(1440, 2160, wordmark=True).save(store / "Screenshotz-poster-art-1440x2160.png", optimize=True)
    print(f"Wrote icons to {OUT}")


if __name__ == "__main__":
    main()
