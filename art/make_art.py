"""Original art for Player Limit Mod v2: Thunderstore icon (256x256) and gold page arrows (579x261)."""
import math
import sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont

out = sys.argv[1]
SS = 4  # supersample for smooth edges

GOLD_HI = (255, 226, 140)
GOLD = (222, 172, 70)
GOLD_LO = (150, 98, 30)
OUTLINE = (40, 26, 12)


def vgradient(size, top, bottom):
    w, h = size
    g = Image.new("RGBA", size)
    px = g.load()
    for y in range(h):
        t = y / max(1, h - 1)
        c = tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)) + (255,)
        for x in range(w):
            px[x, y] = c
    return g


# ---------------------------------------------------------------- arrows
def arrow(direction):
    W, H = 579 * SS, 261 * SS
    pad = 18 * SS
    # chunky arrow: shaft + head, pointing right
    shaft_h = H * 0.40
    head_w = W * 0.42
    cy = H / 2
    pts = [
        (pad, cy - shaft_h / 2),
        (W - pad - head_w, cy - shaft_h / 2),
        (W - pad - head_w, pad),
        (W - pad, cy),
        (W - pad - head_w, H - pad),
        (W - pad - head_w, cy + shaft_h / 2),
        (pad, cy + shaft_h / 2),
    ]
    mask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask).polygon(pts, fill=255)

    # dark outline = dilated mask
    outline_mask = mask.filter(ImageFilter.MaxFilter(1 + 2 * (6 * SS // 2)))
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    img.paste(Image.new("RGBA", (W, H), OUTLINE + (255,)), (0, 0), outline_mask)

    fill = vgradient((W, H), GOLD_HI, GOLD_LO)
    img.paste(fill, (0, 0), mask)

    # bevel highlight on upper half
    hl = Image.new("L", (W, H), 0)
    hd = ImageDraw.Draw(hl)
    hd.rectangle([0, 0, W, cy - shaft_h * 0.05], fill=70)
    hl = Image.composite(hl, Image.new("L", (W, H), 0), mask.filter(ImageFilter.MinFilter(1 + 2 * (5 * SS // 2))))
    img.paste(Image.new("RGBA", (W, H), (255, 245, 210, 255)), (0, 0), hl)

    img = img.resize((579, 261), Image.LANCZOS)
    if direction == "left":
        img = img.transpose(Image.FLIP_LEFT_RIGHT)
    return img


# ---------------------------------------------------------------- icon
def icon():
    S = 256 * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))

    # background: deep night-blue radial
    bg = Image.new("RGBA", (S, S))
    px = bg.load()
    c = S / 2
    for y in range(S):
        for x in range(S):
            d = math.hypot(x - c, y - c * 0.85) / (S * 0.75)
            d = min(1, d)
            col = (int(46 - 30 * d), int(40 - 26 * d), int(70 - 44 * d), 255)
            px[x, y] = col
    rmask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(rmask).rounded_rectangle([0, 0, S - 1, S - 1], radius=36 * SS, fill=255)
    img.paste(bg, (0, 0), rmask)

    d = ImageDraw.Draw(img)
    # gold frame
    d.rounded_rectangle([6 * SS, 6 * SS, S - 6 * SS, S - 6 * SS], radius=32 * SS, outline=GOLD, width=6 * SS)
    d.rounded_rectangle([14 * SS, 14 * SS, S - 14 * SS, S - 14 * SS], radius=26 * SS, outline=GOLD_LO, width=2 * SS)

    # crowd of hero silhouettes: three rows, back rows smaller and darker
    def hero(cx, base_y, scale, col):
        hr = 15 * SS * scale
        d.ellipse([cx - hr, base_y - 4.1 * hr, cx + hr, base_y - 2.1 * hr], fill=col)
        d.pieslice([cx - 2.0 * hr, base_y - 2.0 * hr, cx + 2.0 * hr, base_y + 2.0 * hr], 180, 360, fill=col)

    rows = [
        (6, 118, 0.62, (120, 92, 46)),
        (5, 150, 0.80, (176, 132, 58)),
        (4, 190, 1.00, GOLD),
    ]
    for n, y, sc, col in rows:
        span = 196 * SS
        for i in range(n):
            cx = (S - span) / 2 + span * (i + 0.5) / n
            hero(cx, y * SS, sc, col)

    # "16+" banner
    banner_h = 52 * SS
    by = S - 26 * SS - banner_h
    d.rounded_rectangle([40 * SS, by, S - 40 * SS, by + banner_h], radius=12 * SS, fill=(24, 18, 12, 235), outline=GOLD, width=3 * SS)
    font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf", 40 * SS)
    text = "16+"
    tb = d.textbbox((0, 0), text, font=font)
    tw, th = tb[2] - tb[0], tb[3] - tb[1]
    d.text(((S - tw) / 2 - tb[0], by + (banner_h - th) / 2 - tb[1]), text, font=font, fill=GOLD_HI)

    # top label
    font2 = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf", 24 * SS)
    t2 = "PARTY"
    tb = d.textbbox((0, 0), t2, font=font2)
    d.text(((S - (tb[2] - tb[0])) / 2 - tb[0], 30 * SS - tb[1]), t2, font=font2, fill=GOLD)

    return img.resize((256, 256), Image.LANCZOS)


icon().save(f"{out}/icon.png")
arrow("right").save(f"{out}/arrow_right.png")
arrow("left").save(f"{out}/arrow_left.png")
print("ok")
