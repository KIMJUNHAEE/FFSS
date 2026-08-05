from pathlib import Path
import colorsys

from PIL import Image, ImageChops, ImageDraw, ImageFont, ImageOps


ROOT = Path(__file__).resolve().parents[1]
BASE_PATH = ROOT / "Assets/UI/38Battle/CombatSkin/gwangddaeng_intent_badge.png"
HUD_DIR = ROOT / "Assets/UI/BossCombatSkins/HUD"
OUTPUT_DIR = ROOT / "Assets/UI/BossCombatSkins/Intent"
COMPARISON_PATH = ROOT / "Temp/BossIntentComparison.png"

ACCENTS = {
    "18": "#286044",
    "13": "#b83c63",
    "amhaeng": "#3c557c",
    "ddengjabi": "#177a9c",
    "meonggusa": "#477b6c",
    "gusa": "#647d43",
}
MEDALLION_ACCENTS = {"38": "#b52b33", **ACCENTS}
DISPLAY_NAMES = {
    "38": "38광땡",
    "18": "18광땡",
    "13": "13광땡",
    "amhaeng": "암행어사",
    "ddengjabi": "땡잡이",
    "meonggusa": "멍구사",
    "gusa": "구사",
}
MOTIF_CROPS = {
    "38": (0, 0, 670, 740),
    "18": (0, 0, 670, 740),
    "13": (1240, 0, 1906, 740),
    "amhaeng": (0, 0, 690, 780),
    "ddengjabi": (1210, 0, 1906, 780),
    "meonggusa": (0, 0, 690, 780),
    "gusa": (0, 0, 720, 800),
}
def recolor_red_accents(source: Image.Image, target_hex: str) -> Image.Image:
    target = tuple(int(target_hex[i : i + 2], 16) / 255 for i in (1, 3, 5))
    th, ts, tv = colorsys.rgb_to_hsv(*target)
    image = source.convert("RGBA")
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, a = pixels[x, y]
            if a == 0:
                continue
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            is_red_accent = (h < 0.06 or h > 0.95) and s > 0.5 and r > 70
            if not is_red_accent:
                continue
            nr, ng, nb = colorsys.hsv_to_rgb(th, max(ts, s * 0.75), min(1.0, v * 1.05))
            pixels[x, y] = (round(nr * 255), round(ng * 255), round(nb * 255), a)
    return image


def decorate_medallion(frame: Image.Image, boss_id: str) -> Image.Image:
    # Keep the center decorative and tone-on-tone. It must read as part of
    # the frame, never as a gameplay icon slot.
    center = (584, 242)
    radius = 166
    mask = Image.new("L", frame.size, 0)
    ImageDraw.Draw(mask).ellipse(
        (center[0] - radius, center[1] - radius, center[0] + radius, center[1] + radius),
        fill=255,
    )
    body = Image.new("RGBA", frame.size, (9, 11, 15, 255))
    frame.paste(body, (0, 0), mask)

    hud = Image.open(HUD_DIR / f"boss_{boss_id}_hud.png").convert("RGBA")
    motif = ImageOps.fit(hud.crop(MOTIF_CROPS[boss_id]), (286, 286), Image.Resampling.LANCZOS)
    alpha = motif.getchannel("A")
    luminance = ImageOps.autocontrast(ImageOps.grayscale(motif))
    detail = ImageChops.multiply(alpha, luminance).point(lambda value: min(124, int(value * 0.55)))
    silhouette = alpha.point(lambda value: int(value * 0.23))
    motif_alpha = ImageChops.lighter(detail, silhouette)

    local_circle = Image.new("L", motif.size, 0)
    ImageDraw.Draw(local_circle).ellipse((4, 4, 281, 281), fill=255)
    motif_alpha = ImageChops.multiply(motif_alpha, local_circle)
    base_color = tuple(int(MEDALLION_ACCENTS[boss_id][i : i + 2], 16) for i in (1, 3, 5))
    color = tuple(min(255, int(channel * 1.35 + 20)) for channel in base_color)
    ink = Image.new("RGBA", motif.size, color + (0,))
    ink.putalpha(motif_alpha)
    frame.alpha_composite(ink, (center[0] - 143, center[1] - 143))
    return frame


def comparison(outputs: dict[str, Image.Image]) -> None:
    tile_size = (292, 336)
    label_height = 42
    sheet = Image.new("RGB", (tile_size[0] * 4, (tile_size[1] + label_height) * 2), "#17181d")
    draw = ImageDraw.Draw(sheet)
    font_path = Path("C:/Windows/Fonts/malgunbd.ttf")
    font = ImageFont.truetype(str(font_path), 22) if font_path.exists() else ImageFont.load_default()
    for index, boss_id in enumerate(("38", "18", "13", "amhaeng", "ddengjabi", "meonggusa", "gusa")):
        x = (index % 4) * tile_size[0]
        y = (index // 4) * (tile_size[1] + label_height)
        tile = Image.new("RGBA", tile_size, (25, 26, 31, 255))
        tile.alpha_composite(outputs[boss_id].resize(tile_size, Image.Resampling.LANCZOS))
        sheet.paste(tile.convert("RGB"), (x, y))
        draw.text((x + 12, y + tile_size[1] + 6), DISPLAY_NAMES[boss_id], font=font, fill="#f2dfaa")
    sheet.save(COMPARISON_PATH)


def main() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    base = Image.open(BASE_PATH).convert("RGBA")
    outputs = {"38": decorate_medallion(base.copy(), "38")}
    for boss_id, color in ACCENTS.items():
        frame = recolor_red_accents(base, color)
        outputs[boss_id] = decorate_medallion(frame, boss_id)
    for boss_id, image in outputs.items():
        image.save(OUTPUT_DIR / f"boss_{boss_id}_intent.png")
    comparison(outputs)
    print(f"Wrote {len(outputs)} intent skins to {OUTPUT_DIR}")
    print(f"Comparison: {COMPARISON_PATH}")


if __name__ == "__main__":
    main()
