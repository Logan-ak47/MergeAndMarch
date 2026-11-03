"""Merge & March bean sprite generator.

This script is the vector source for the "Bold hybrid-casual beans" art
direction (design/art/art-bible.md). Every sprite is built from simple shapes
following the bible's rules -- one ink outline weight (4.7% of canvas height),
one flat shadow crescent on the right, one gloss, no texture -- drawn at 4x and
downsampled so edges stay clean. Change a colour or proportion here and re-run
to regenerate the whole set consistently.

Usage:
    python art-source/beans/generate_beans.py Assets/_MergeAndMarch/Sprites/Beans

Exports follow art bible Section 8: 256 px canvas = 1 world unit (PPU 256),
feet on an 8 px bottom margin, PNG-32 straight alpha, lowercase_underscore names.
"""
import math
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

SS = 4  # supersampling factor

# ---------------------------------------------------------------- palette (bible §4)
INK = "#1d1442"
WHITE = "#ffffff"
GOLD = "#ffc21a"
HORN = "#ffe680"
METAL = "#dfe5ee"
WOOD = "#8a4b1f"
ENEMY = "#f0283f"
TANK = "#b3123f"
BOSS = "#e01e3c"
FLYER_WING = "#9c1630"
ARMOUR = "#8b93a1"

TROOP_COLORS = {
    "knight": "#2f6bff",
    "archer": "#18c964",
    "mage": "#a347ff",
    "healer": "#ff6fd1",
    "bomber": "#ff7a00",
}
BADGE_COLORS = {1: "#9aa3b5", 2: "#2fd0ff", 3: GOLD}

OUTLINE = 12.0       # 4.7% of a 256 canvas
EYE_OUTLINE = 6.0
EYE_L = (108.0, 114.0)
EYE_R = (148.0, 114.0)


def rgba(hex_color, alpha=255):
    hex_color = hex_color.lstrip("#")
    return tuple(int(hex_color[i:i + 2], 16) for i in (0, 2, 4)) + (alpha,)


def darken(hex_color, factor=0.78):
    r, g, b, _ = rgba(hex_color)
    return "#%02x%02x%02x" % (int(r * factor), int(g * factor), int(b * factor))


# ---------------------------------------------------------------- shapes
class Shape:
    """A shape that can rasterise itself as a mask, optionally grown by `grow` px."""

    def draw(self, d, k, grow):
        raise NotImplementedError

    def mask(self, canvas, grow=0.0, k=1.0):
        m = Image.new("L", (canvas * SS, canvas * SS), 0)
        self.draw(ImageDraw.Draw(m), k, grow)
        return m


class Ellipse(Shape):
    def __init__(self, cx, cy, rx, ry=None):
        self.cx, self.cy, self.rx, self.ry = cx, cy, rx, rx if ry is None else ry

    def draw(self, d, k, grow):
        rx, ry = self.rx * k + grow, self.ry * k + grow
        if rx <= 0 or ry <= 0:
            return
        cx, cy = self.cx * k, self.cy * k
        d.ellipse([(cx - rx) * SS, (cy - ry) * SS, (cx + rx) * SS, (cy + ry) * SS], fill=255)


class RRect(Shape):
    def __init__(self, x0, y0, x1, y1, r):
        self.box, self.r = (x0, y0, x1, y1), r

    def draw(self, d, k, grow):
        x0, y0, x1, y1 = (v * k for v in self.box)
        r = max(0.0, self.r * k + grow)
        box = [(x0 - grow) * SS, (y0 - grow) * SS, (x1 + grow) * SS, (y1 + grow) * SS]
        if box[2] <= box[0] or box[3] <= box[1]:
            return
        d.rounded_rectangle(box, radius=r * SS, fill=255)


class Poly(Shape):
    """Filled polygon; growing it is a Minkowski sum with a disc (round joins)."""

    def __init__(self, points):
        self.points = points

    def draw(self, d, k, grow):
        pts = [(x * k * SS, y * k * SS) for x, y in self.points]
        d.polygon(pts, fill=255)
        if grow > 0:
            g = grow * SS
            for i, a in enumerate(pts):
                b = pts[(i + 1) % len(pts)]
                d.line([a, b], fill=255, width=int(round(2 * g)))
                d.ellipse([a[0] - g, a[1] - g, a[0] + g, a[1] + g], fill=255)


class Stroke(Shape):
    """A thick polyline with round caps and joins."""

    def __init__(self, points, width):
        self.points, self.width = points, width

    def draw(self, d, k, grow):
        half = (self.width * k) / 2.0 + grow
        if half <= 0:
            return
        pts = [(x * k * SS, y * k * SS) for x, y in self.points]
        h = half * SS
        if len(pts) > 1:
            d.line(pts, fill=255, width=int(round(2 * h)), joint="curve")
        for x, y in pts:
            d.ellipse([x - h, y - h, x + h, y + h], fill=255)


class Union(Shape):
    def __init__(self, *shapes):
        self.shapes = shapes

    def draw(self, d, k, grow):
        for s in self.shapes:
            s.draw(d, k, grow)


def arc_points(cx, cy, r, a0, a1, steps=24):
    return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / steps)),
             cy + r * math.sin(math.radians(a0 + (a1 - a0) * i / steps))) for i in range(steps + 1)]


def star_points(cx, cy, outer, inner, n=5, rot=-90):
    pts = []
    for i in range(n * 2):
        r = outer if i % 2 == 0 else inner
        a = math.radians(rot + i * 180.0 / n)
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def ellipse_points(cx, cy, rx, ry, rot_deg, steps=40):
    rot = math.radians(rot_deg)
    pts = []
    for i in range(steps):
        t = 2 * math.pi * i / steps
        x, y = rx * math.cos(t), ry * math.sin(t)
        pts.append((cx + x * math.cos(rot) - y * math.sin(rot), cy + x * math.sin(rot) + y * math.cos(rot)))
    return pts


# ---------------------------------------------------------------- painting
class Canvas:
    def __init__(self, size, k=1.0):
        self.size = size
        self.k = k
        self.img = Image.new("RGBA", (size * SS, size * SS), (0, 0, 0, 0))
        self.silhouette = Image.new("L", self.img.size, 0)

    def _paste(self, color, mask, alpha=1.0):
        if alpha < 1.0:
            mask = mask.point(lambda v: int(v * alpha))
        self.img.paste(rgba(color) if isinstance(color, str) else color, (0, 0) + self.img.size, mask)

    def paint(self, shape, fill, outline=OUTLINE, shade=0.0, gloss=None):
        """Ink outline, flat fill, optional right-side shadow crescent and gloss oval."""
        k = self.k
        if outline > 0:
            ink = shape.mask(self.size, outline * k, k)
            self._paste(INK, ink)
            self.silhouette = ImageChops.lighter(self.silhouette, ink)
        body = shape.mask(self.size, 0, k)
        self._paste(fill, body)
        self.silhouette = ImageChops.lighter(self.silhouette, body)
        if shade > 0:
            shifted = Image.new("L", body.size, 0)
            shifted.paste(body, (-int(shade * k * SS), 0))
            self._paste(darken(fill), ImageChops.subtract(body, shifted))
        if gloss is not None:
            g = ImageChops.multiply(gloss.mask(self.size, 0, k), body)
            self._paste(WHITE, g, alpha=0.6)
        return body

    def fill_only(self, shape, fill, alpha=1.0):
        m = shape.mask(self.size, 0, self.k)
        self._paste(fill, m, alpha)
        return m

    def export(self, path):
        save(self.img, self.size, path)


def save(img, size, path):
    path.parent.mkdir(parents=True, exist_ok=True)
    small = img.convert("RGBa").resize((size, size), Image.LANCZOS).convert("RGBA")
    small.save(path)
    print("wrote", path)


def save_mask(mask, size, path, color=WHITE):
    img = Image.new("RGBA", mask.size, rgba(color, 0))
    img.paste(rgba(color), (0, 0) + mask.size, mask)
    save(img, size, path)


# ---------------------------------------------------------------- bean kit (bible §3)
def bean_body(x0=62, x1=194, y0=74, y1=238):
    return RRect(x0, y0, x1, y1, (x1 - x0) / 2.0)


FEET = Union(Ellipse(106, 235, 22, 13), Ellipse(150, 235, 22, 13))


def gloss_for(x0, y0):
    return Ellipse(x0 + 20, y0 + 74, 8, 14)


def paint_bean(c, color, body=None, feet=True):
    body = body or bean_body()
    x0, y0 = body.box[0], body.box[1]
    if feet:
        c.paint(FEET, darken(color, 0.7))
    width = body.box[2] - body.box[0]
    c.paint(body, color, shade=width * 0.25, gloss=gloss_for(x0, y0))


# ---------------------------------------------------------------- troops
def troop_parts(kind):
    """Returns (paint_hat(canvas) -> accent mask or None, paint_prop(canvas) -> accent mask or None)."""
    color = TROOP_COLORS[kind]

    if kind == "knight":
        plume = Poly(ellipse_points(142, 30, 13, 24, 25))
        helmet = Poly([(128 + 76 * math.cos(math.radians(a)), 96 + 50 * math.sin(math.radians(a)))
                       for a in range(180, 361, 6)])

        def hat(c):
            c.paint(plume, "#8fb0ff")
            c.paint(helmet, METAL, shade=36)
            visible_plume = ImageChops.subtract(plume.mask(c.size), helmet.mask(c.size, OUTLINE))
            return visible_plume

        shield = Ellipse(72, 186, 40)

        def prop(c):
            c.paint(shield, METAL, shade=18, gloss=Ellipse(58, 170, 7, 10))
            c.fill_only(Ellipse(72, 186, 21), color)
            return None

        return hat, prop

    if kind == "archer":
        tails = Union(Poly([(66, 84), (34, 74), (42, 98)]), Poly([(66, 92), (30, 104), (46, 112)]))
        band = RRect(56, 78, 200, 96, 8)

        def hat(c):
            c.paint(tails, "#0b8f5a")
            c.paint(band, "#0b8f5a")
            return ImageChops.lighter(tails.mask(c.size), band.mask(c.size))

        bow = Stroke(arc_points(100, 182, 54, 125, 235, 20), 12)
        string = Stroke([(69, 138), (69, 226)], 4)

        def prop(c):
            c.fill_only(string, "#fff3d6")
            c.paint(bow, WOOD, outline=8)
            return None

        return hat, prop

    if kind == "mage":
        cone = Poly([(76, 88), (180, 88), (146, 6)])
        brim = Ellipse(128, 90, 78, 13)

        def band_x(y):
            t = (88 - y) / 82.0
            return 76 + t * 70, 180 - t * 34

        l1, r1 = band_x(66)
        l0, r0 = band_x(84)
        band = Poly([(l1, 66), (r1, 66), (r0, 84), (l0, 84)])

        def hat(c):
            c.paint(cone, "#6f22d6", shade=20)
            c.fill_only(band, "#c79bff")
            c.paint(brim, "#6f22d6")
            return ImageChops.subtract(band.mask(c.size), brim.mask(c.size, OUTLINE))

        staff = Stroke([(54, 246), (54, 128)], 12)
        orb = Ellipse(54, 116, 17)

        def prop(c):
            c.paint(staff, WOOD, outline=8)
            c.paint(orb, "#7ef0ff", gloss=Ellipse(48, 110, 5, 6))
            return None

        return hat, prop

    if kind == "healer":
        cross = Union(RRect(114, 136, 142, 212, 6), RRect(90, 160, 166, 188, 6))

        def hat(c):
            return None

        def prop(c):
            c.paint(cross, WHITE, outline=8)
            inner = Union(RRect(121, 143, 135, 205, 3), RRect(97, 167, 159, 181, 3))
            return ImageChops.subtract(cross.mask(c.size), inner.mask(c.size))

        return hat, prop

    if kind == "bomber":
        bomb = Ellipse(74, 196, 34)
        fuse = Stroke([(62, 164), (52, 150), (46, 138)], 6)
        spark = Poly(star_points(44, 128, 15, 6, 4, -90))

        def hat(c):
            goggles = RRect(64, 72, 192, 86, 7)
            c.paint(goggles, "#5a4a3a", outline=8)
            for ex in (110, 146):
                c.paint(Ellipse(ex, 76, 9), "#9fe3ff", outline=6)
            return None

        def prop(c):
            c.paint(fuse, WOOD, outline=5)
            c.paint(bomb, "#2a2440", shade=14, gloss=Ellipse(62, 182, 7, 9))
            c.paint(spark, "#ffe14d", outline=5)
            return spark.mask(c.size)

        return hat, prop

    raise ValueError(kind)


def make_troop(kind, out):
    color = TROOP_COLORS[kind]
    hat, prop = troop_parts(kind)

    body = Canvas(256)
    paint_bean(body, color)
    hat_accent = hat(body)
    body.export(out / "Units" / f"char_{kind}_body.png")

    prop_canvas = Canvas(256)
    prop_accent = prop(prop_canvas)
    prop_canvas.export(out / "Units" / f"char_{kind}_prop.png")

    accent = hat_accent if hat_accent is not None else prop_accent
    save_mask(accent, 256, out / "Units" / f"char_{kind}_t3accent.png")

    silhouette = ImageChops.lighter(body.silhouette, prop_canvas.silhouette)
    save_mask(silhouette, 256, out / "Units" / f"char_{kind}_flash.png")


# ---------------------------------------------------------------- enemies
def horns(c, left, right, outline=OUTLINE):
    c.paint(Poly(left), HORN, outline=outline)
    c.paint(Poly(right), HORN, outline=outline)


def make_enemy(kind, out):
    c = Canvas(256)
    if kind == "grunt":
        horns(c, [(96, 86), (78, 40), (114, 76)], [(160, 86), (178, 40), (142, 76)])
        paint_bean(c, ENEMY)
    elif kind == "rusher":
        for tri in ([(98, 84), (106, 40), (120, 78)], [(116, 78), (128, 28), (140, 78)], [(136, 78), (150, 40), (158, 84)]):
            c.paint(Poly(tri), HORN)
        paint_bean(c, ENEMY, body=bean_body(69, 187, 74, 238))
    elif kind == "tank":
        horns(c, [(62, 94), (44, 62), (80, 86)], [(194, 94), (212, 62), (176, 86)])
        c.paint(FEET, darken(TANK, 0.7))
        c.paint(RRect(43, 88, 213, 238, 56), TANK, shade=42, gloss=Ellipse(66, 150, 8, 14))
        c.paint(RRect(50, 82, 206, 104, 11), ARMOUR, shade=30)
    elif kind == "flyer":
        horns(c, [(98, 74), (82, 32), (114, 66)], [(158, 74), (174, 32), (142, 66)])
        paint_bean(c, ENEMY, body=bean_body(66, 190, 64, 206), feet=False)
        wings = Canvas(256)
        left = [(72, 118), (14, 78), (26, 116), (6, 140), (40, 148), (70, 150)]
        right = [(256 - x, y) for x, y in left]
        wings.paint(Poly(left), FLYER_WING, shade=0)
        wings.paint(Poly(right), FLYER_WING, shade=0)
        wings.export(out / "Enemies" / "char_enemy_flyer_wings.png")
    else:
        raise ValueError(kind)
    c.export(out / "Enemies" / f"char_enemy_{kind}_body.png")
    save_mask(c.silhouette, 256, out / "Enemies" / f"char_enemy_{kind}_flash.png")


def make_boss(out):
    c = Canvas(512, k=2.0)
    for tri in ([(96, 86), (74, 34), (114, 76)], [(160, 86), (182, 34), (142, 76)], [(118, 78), (128, 22), (138, 78)]):
        c.paint(Poly(tri), HORN)
    paint_bean(c, BOSS)
    c.paint(Stroke([(150, 172), (170, 198)], 6), INK, outline=0)
    c.export(out / "Enemies" / "char_enemy_boss_body.png")
    save_mask(c.silhouette, 512, out / "Enemies" / "char_enemy_boss_flash.png")


# ---------------------------------------------------------------- faces (bible §5)
def eye_open(c, ex, ey, pupil_dy, r=16.0, ry=None):
    c.paint(Ellipse(ex, ey, r, r if ry is None else ry), WHITE, outline=EYE_OUTLINE)
    c.fill_only(Ellipse(ex, ey + pupil_dy, 7), INK)
    c.fill_only(Ellipse(ex - 2.5, ey + pupil_dy - 2.5, 2.5), WHITE)


def ink(c, points, width=7):
    c.fill_only(Stroke(points, width), INK)


def brows(c):
    for (ex, ey), side in ((EYE_L, -1), (EYE_R, 1)):
        ink(c, [(ex + side * 16, ey - 24), (ex - side * 12, ey - 13)], 8)


def make_face(name, out, enemy=False):
    c = Canvas(256)
    for (ex, ey), side in ((EYE_L, -1), (EYE_R, 1)):
        if name == "idle":
            eye_open(c, ex, ey, 4 if enemy else -4)
        elif name == "blink":
            ink(c, [(ex - 13, ey), (ex, ey + 5), (ex + 13, ey)], 6)
        elif name == "attack":
            eye_open(c, ex, ey + 2, -2, r=16, ry=10)
            ink(c, [(ex - 17, ey - 9), (ex + 17, ey - 6)], 6)
        elif name == "hurt":
            if side < 0:
                ink(c, [(ex - 12, ey - 10), (ex + 8, ey), (ex - 12, ey + 10)])
            else:
                ink(c, [(ex + 12, ey - 10), (ex - 8, ey), (ex + 12, ey + 10)])
        elif name == "dizzy":
            ink(c, [(ex - 11, ey - 11), (ex + 11, ey + 11)])
            ink(c, [(ex - 11, ey + 11), (ex + 11, ey - 11)])
        elif name == "happy":
            ink(c, [(ex - 13, ey + 5), (ex, ey - 8), (ex + 13, ey + 5)])
    if enemy and name != "dizzy":
        brows(c)
    prefix = "enemy" if enemy else "ally"
    c.export(out / "Faces" / f"char_face_{prefix}_{name}.png")


# ---------------------------------------------------------------- badges and FX
DIGITS = {
    1: [[(50, 44), (68, 32), (68, 96)]],
    2: [[(46, 46), (54, 35), (66, 32), (79, 37), (82, 50), (75, 63), (47, 95), (84, 95)]],
    3: [[(46, 39), (62, 32), (78, 37), (80, 52), (64, 62)], [(64, 62), (81, 70), (83, 85), (66, 96), (46, 90)]],
}


def make_badge(tier, out):
    c = Canvas(128)
    c.paint(Ellipse(64, 64, 50), BADGE_COLORS[tier], outline=7, shade=12)
    for stroke in DIGITS[tier]:
        c.fill_only(Stroke(stroke, 14), INK)
    c.export(out / "UI" / f"ui_badge_t{tier}.png")


def make_fx(out):
    fx = out / "FX"
    shapes = {
        "vfx_circle": (64, Ellipse(32, 32, 30)),
        "vfx_star": (64, Poly(star_points(32, 32, 30, 13))),
        "vfx_sparkle": (64, Poly(star_points(32, 32, 30, 8, 4, -90))),
        "vfx_confetti": (64, RRect(12, 22, 52, 42, 4)),
        "vfx_chunk": (32, Poly([(4, 10), (26, 4), (28, 24), (8, 28)])),
        "vfx_puff": (128, Union(Ellipse(44, 74, 30), Ellipse(84, 74, 30), Ellipse(64, 50, 32), Ellipse(64, 84, 30))),
        "vfx_shadow": (128, Ellipse(64, 64, 60, 22)),
    }
    for name, (size, shape) in shapes.items():
        save_mask(shape.mask(size), size, fx / f"{name}.png")

    ring = ImageChops.subtract(Ellipse(128, 128, 120).mask(256), Ellipse(128, 128, 100).mask(256))
    save_mask(ring, 256, fx / "vfx_ring.png")

    rays = Union(*[Poly([(128, 128),
                         (128 + 124 * math.cos(math.radians(a - 7)), 128 + 124 * math.sin(math.radians(a - 7))),
                         (128 + 124 * math.cos(math.radians(a + 7)), 128 + 124 * math.sin(math.radians(a + 7)))])
                   for a in range(0, 360, 45)])
    save_mask(rays.mask(256), 256, fx / "vfx_rays.png")

    glow = Image.new("L", (128 * SS, 128 * SS), 0)
    px = glow.load()
    centre = 64 * SS
    for y in range(glow.height):
        for x in range(glow.width):
            dist = math.hypot(x - centre, y - centre) / centre
            px[x, y] = int(max(0.0, 1.0 - dist) ** 1.6 * 255)
    save_mask(glow, 128, fx / "vfx_glow.png")

    coin = Canvas(64)
    coin.paint(Ellipse(32, 32, 24), GOLD, outline=5, shade=10)
    coin.fill_only(Ellipse(30, 30, 12), "#ffe08a")
    coin.export(fx / "vfx_coin.png")

    # Knight slash: a crescent, white so it can be tinted.
    slash = ImageChops.subtract(Ellipse(64, 64, 58).mask(128), Ellipse(84, 52, 54).mask(128))
    save_mask(slash, 128, fx / "vfx_slash.png")

    # Archer arrow, pointing right (+x): wooden shaft, white head, green fletching.
    arrow = Canvas(128)
    arrow.paint(Union(Poly([(20, 52), (6, 44), (14, 64)]), Poly([(20, 76), (6, 84), (14, 64)])), TROOP_COLORS["archer"], outline=5)
    arrow.paint(Stroke([(14, 64), (98, 64)], 9), WOOD, outline=5)
    arrow.paint(Poly([(92, 48), (124, 64), (92, 80)]), WHITE, outline=5)
    arrow.export(fx / "vfx_arrow.png")

    # Slow-mo vignette: clear centre, opaque edges (white, tinted ink at runtime).
    vignette = Image.new("L", (256 * SS, 256 * SS), 0)
    vpx = vignette.load()
    half = 128 * SS
    for y in range(vignette.height):
        for x in range(vignette.width):
            dist = math.hypot((x - half) / half, (y - half) / half) / math.sqrt(2)
            vpx[x, y] = int(min(1.0, max(0.0, (dist - 0.35) / 0.65)) ** 1.5 * 255)
    save_mask(vignette, 256, fx / "vfx_vignette.png")

    # Slow-mo hourglass icon (art bible Section 7).
    glass = Canvas(128)
    glass.paint(RRect(30, 14, 98, 28, 6), WOOD, outline=6)
    glass.paint(RRect(30, 100, 98, 114, 6), WOOD, outline=6)
    glass.paint(Union(Poly([(38, 28), (90, 28), (68, 64), (90, 100), (38, 100), (60, 64)])), WHITE, outline=6)
    glass.fill_only(Poly([(46, 34), (82, 34), (64, 58)]), GOLD)
    glass.fill_only(Poly([(64, 76), (84, 96), (44, 96)]), GOLD)
    glass.export(out / "UI" / "ui_icon_hourglass.png")


# ---------------------------------------------------------------- protected egg (direction 2026-11-03)
EGG = "#fff4d6"
EGG_SPOT = "#2fd0ff"
NEST = "#c77d3a"


def make_egg(out):
    """The base the beans protect: a speckled egg in a straw nest, three crack states (0 = intact)."""
    cracks = [
        [],
        [[(128, 70), (118, 92), (134, 108), (122, 128)]],
        [[(128, 70), (118, 92), (134, 108), (122, 128), (138, 150)],
         [(92, 120), (108, 132), (98, 150), (112, 166)],
         [(166, 112), (152, 128), (164, 144)]],
    ]
    for state, lines in enumerate(cracks):
        c = Canvas(256)
        egg = Union(Ellipse(128, 136, 70, 86), Ellipse(128, 168, 76, 56))
        c.paint(egg, EGG, shade=34, gloss=Ellipse(94, 104, 10, 20))
        for sx, sy, r in ((150, 96, 9), (104, 150, 7), (160, 176, 11), (122, 196, 6), (176, 140, 6)):
            c.fill_only(Ellipse(sx, sy, r), EGG_SPOT, alpha=0.85)
        for line in lines:
            c.paint(Stroke(line, 7), INK, outline=0)
        nest = Union(Ellipse(128, 214, 104, 30), RRect(30, 200, 226, 232, 16))
        c.paint(nest, NEST, shade=30)
        for x in range(48, 214, 22):
            c.paint(Stroke([(x, 208), (x + 16, 224)], 5), darken(NEST, 0.7), outline=0)
        c.export(out / "UI" / f"ui_egg_{state}.png")


def main():
    out = Path(sys.argv[1] if len(sys.argv) > 1 else "Assets/_MergeAndMarch/Sprites/Beans")
    for kind in TROOP_COLORS:
        make_troop(kind, out)
    for kind in ("grunt", "rusher", "tank", "flyer"):
        make_enemy(kind, out)
    make_boss(out)
    for name in ("idle", "blink", "attack", "hurt", "dizzy", "happy"):
        make_face(name, out)
    for name in ("idle", "blink", "hurt", "dizzy"):
        make_face(name, out, enemy=True)
    for tier in (1, 2, 3):
        make_badge(tier, out)
    make_fx(out)
    make_egg(out)


if __name__ == "__main__":
    main()
