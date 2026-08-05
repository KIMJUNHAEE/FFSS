from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter, ImageFont, ImageOps


ROOT = Path(__file__).resolve().parents[1]
BASE_PATH = ROOT / "Assets/UI/38Battle/CombatSkin/gwangddaeng_boss_hud.png"
SOURCE_DIR = ROOT / "Temp/BossHudSources"
OUTPUT_DIR = ROOT / "Assets/UI/BossCombatSkins/HUD"
COMPARISON_PATH = ROOT / "Temp/BossHudComparison.png"

CANVAS_SIZE = (1906, 825)
SOURCE_NAMES = {
    "18": "18_keyed.png",
    "13": "13_keyed.png",
    "amhaeng": "암행어사_keyed.png",
    "ddengjabi": "땡잡이_keyed.png",
    "meonggusa": "멍구사_keyed.png",
    "gusa": "구사_keyed.png",
}
DISPLAY_NAMES = {
    "38": "38광땡",
    "18": "18광땡",
    "13": "13광땡",
    "amhaeng": "암행어사",
    "ddengjabi": "땡잡이",
    "meonggusa": "멍구사",
    "gusa": "구사",
}


def rgba(path: Path) -> Image.Image:
    return Image.open(path).convert("RGBA")


def make_empty_master(source: Image.Image) -> Image.Image:
    master = source.copy()

    # Replace only the red interior of the HP lane. The illustrated bevel and
    # end caps stay untouched and become the shared functional grid.
    dark_texture = source.crop((575, 615, 1485, 646)).resize((1040, 90), Image.Resampling.LANCZOS)
    dark_texture = ImageEnhance.Brightness(dark_texture).enhance(0.68)
    dark_texture = ImageEnhance.Color(dark_texture).enhance(0.25)

    interior = Image.new("L", CANVAS_SIZE, 0)
    draw = ImageDraw.Draw(interior)
    draw.polygon(
        [(542, 480), (1510, 480), (1540, 518), (1508, 558), (544, 558), (514, 518)],
        fill=255,
    )
    texture_layer = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
    texture_layer.paste(dark_texture, (514, 474))
    master.paste(texture_layer, (0, 0), interior)
    return master


def side_art_mask() -> Image.Image:
    # Keep every functional center pixel from one master. Generated artwork is
    # admitted only around the sides, with a soft transition behind the trim.
    mask = Image.new("L", CANVAS_SIZE, 0)
    pixels = mask.load()
    for x in range(CANVAS_SIZE[0]):
        if x <= 600:
            value = 255
        elif x < 730:
            value = round(255 * (730 - x) / 130)
        elif x <= 1175:
            value = 0
        elif x < 1305:
            value = round(255 * (x - 1175) / 130)
        else:
            value = 255
        for y in range(CANVAS_SIZE[1]):
            pixels[x, y] = value

    protected = Image.new("L", CANVAS_SIZE, 0)
    draw = ImageDraw.Draw(protected)
    draw.polygon([(500, 252), (1465, 252), (1485, 458), (475, 458)], fill=255)
    draw.rectangle((420, 425, 1620, 615), fill=255)
    draw.rectangle((420, 580, 1620, 695), fill=255)
    protected = protected.filter(ImageFilter.GaussianBlur(5))
    return ImageChops.subtract(mask, protected)


def fit_source(source: Image.Image) -> Image.Image:
    # Generated variants were requested at the same ultra-wide ratio. Fit
    # without stretching and keep their complete side ornaments.
    fitted = ImageOps.contain(source, CANVAS_SIZE, Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
    x = (CANVAS_SIZE[0] - fitted.width) // 2
    y = (CANVAS_SIZE[1] - fitted.height) // 2
    canvas.alpha_composite(fitted, (x, y))
    return canvas


def create_fill_textures(original: Image.Image) -> None:
    hp = original.crop((550, 485, 1502, 552)).resize((1024, 64), Image.Resampling.LANCZOS)
    hp.save(OUTPUT_DIR / "ornate_hp_fill.png")

    gray = ImageOps.grayscale(hp)
    break_fill = ImageOps.colorize(gray, black="#66552d", white="#ffe469").convert("RGBA")
    break_fill.putalpha(hp.getchannel("A"))
    break_fill.save(OUTPUT_DIR / "ornate_break_fill.png")


def create_comparison(outputs: dict[str, Image.Image]) -> None:
    thumb_size = (953, 413)
    label_height = 48
    sheet = Image.new("RGB", (thumb_size[0] * 2, (thumb_size[1] + label_height) * 4), "#16171b")
    draw = ImageDraw.Draw(sheet)
    font_path = Path("C:/Windows/Fonts/malgunbd.ttf")
    font = ImageFont.truetype(str(font_path), 28) if font_path.exists() else ImageFont.load_default()

    for index, boss_id in enumerate(("38", "18", "13", "amhaeng", "ddengjabi", "meonggusa", "gusa")):
        col = index % 2
        row = index // 2
        x = col * thumb_size[0]
        y = row * (thumb_size[1] + label_height)
        tile = Image.new("RGBA", thumb_size, (28, 29, 34, 255))
        hud = outputs[boss_id].resize(thumb_size, Image.Resampling.LANCZOS)
        tile.alpha_composite(hud)
        sheet.paste(tile.convert("RGB"), (x, y))
        draw.text((x + 20, y + thumb_size[1] + 7), DISPLAY_NAMES[boss_id], font=font, fill="#f6e5b7")

    sheet.save(COMPARISON_PATH)


def main() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    original = rgba(BASE_PATH)
    if original.size != CANVAS_SIZE:
        raise RuntimeError(f"Unexpected master size: {original.size}")

    master = make_empty_master(original)
    decoration_mask = side_art_mask()
    outputs = {"38": master}

    for boss_id, source_name in SOURCE_NAMES.items():
        source = fit_source(rgba(SOURCE_DIR / source_name))
        source.putalpha(ImageChops.multiply(source.getchannel("A"), decoration_mask))
        final = master.copy()
        final.putalpha(ImageChops.multiply(master.getchannel("A"), ImageOps.invert(decoration_mask)))
        final.alpha_composite(source)
        outputs[boss_id] = final

    for boss_id, image in outputs.items():
        image.save(OUTPUT_DIR / f"boss_{boss_id}_hud.png")

    create_fill_textures(original)
    create_comparison(outputs)
    print(f"Wrote {len(outputs)} HUD skins to {OUTPUT_DIR}")
    print(f"Comparison: {COMPARISON_PATH}")


if __name__ == "__main__":
    main()
