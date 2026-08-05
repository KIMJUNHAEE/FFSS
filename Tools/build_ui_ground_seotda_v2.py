from __future__ import annotations

import csv
import json
import math
import random
import shutil
import zipfile
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter, ImageFont, ImageOps


ROOT = Path(r"C:\git\FFSS")
GENERATED = Path(r"C:\Users\kirby\.codex\generated_images\019fd035-e339-7860-93b2-b1cd104a6ff5")
OUTPUT = Path(
    r"C:\Users\kirby\OneDrive\바탕 화면\구구가가\웹게임_UI_신규"
    r"\즉시사용_v2_독립지면_캐릭터전용섯다덱_최종본"
)

DECK_ROOT = OUTPUT / "상대전용_섯다전체덱_17인_340장"
GROUND_ROOT = OUTPUT / "파괴된포커마을_독립보행지면_29종"
ROAD_ROOT = OUTPUT / "망가진포커포커마을_전면길지면_36종"
PREVIEW_ROOT = OUTPUT / "미리보기_카탈로그"
SOURCE_ROOT = OUTPUT / "AI_원화소스_17덱프레임_9지면"
ZIP_ROOT = OUTPUT / "ZIP_묶음"
DOWNLOAD_MIRROR = Path(
    r"C:\Users\kirby\OneDrive\바탕 화면\구구가가\웹게임_UI_신규\사이트_다운로드_ZIP"
)
BUILD_ROOT = ROOT / "Builds" / "UIPacks"

CARD_SIZE = (486, 758)
GROUND_SIZE = (1024, 1024)
HEX_POINTS = [(512, 18), (974, 256), (974, 768), (512, 1006), (50, 768), (50, 256)]

FONT_BOLD = Path(r"C:\Windows\Fonts\malgunbd.ttf")
FONT_REGULAR = Path(r"C:\Windows\Fonts\malgun.ttf")
FONT_LATIN = Path(r"C:\Windows\Fonts\georgiab.ttf")

IVORY = (245, 239, 218)
GOLD = (226, 183, 86)
INK = (6, 8, 12)


DECK_SPECS = [
    {
        "order": 1,
        "id": "one-ddeng",
        "name": "1땡",
        "folder": "01_1땡_수비의소나무_20장",
        "identity": "수비의 소나무",
        "motif": "창 방진 · 인내 · 반격 준비",
        "accent": (71, 142, 103),
        "frame": "exec-02255bbf-3ea8-4fad-bc79-6c77a27c246c.png",
        "character": "one-ddeng.png",
    },
    {
        "order": 2,
        "id": "two-ddeng",
        "name": "2땡",
        "folder": "02_2땡_매화반격_20장",
        "identity": "매화 반격",
        "motif": "쌍월도 · 거울 초승달 · 즉시 반격",
        "accent": (240, 126, 169),
        "frame": "exec-82683b6b-1e61-4644-a127-5bfcd4042408.png",
        "character": "two-ddeng.png",
    },
    {
        "order": 3,
        "id": "three-ddeng",
        "name": "3땡",
        "folder": "03_3땡_삼연화_20장",
        "identity": "삼연화",
        "motif": "세 번 누적 · 연속 베기 · 벚꽃 폭발",
        "accent": (221, 52, 91),
        "frame": "exec-cbde4f9f-c4d9-45f5-9f8d-f37f4886fdb3.png",
        "character": "three-ddeng.png",
    },
    {
        "order": 4,
        "id": "four-ddeng",
        "name": "4땡",
        "folder": "04_4땡_월영회피_20장",
        "identity": "월영 회피",
        "motif": "흑월 낫 · 등나무 그림자 · 잔상",
        "accent": (138, 91, 198),
        "frame": "exec-af2f60c6-2399-45dd-8662-0c21e6b066f3.png",
        "character": "four-ddeng.png",
    },
    {
        "order": 5,
        "id": "five-ddeng",
        "name": "5땡",
        "folder": "05_5땡_청류복제_20장",
        "identity": "청류 복제",
        "motif": "쌍부채 · 거울 수면 · 직전 행동 복사",
        "accent": (55, 174, 200),
        "frame": "exec-8b75b8d9-32a9-4ef0-8c57-1f32ad283d05.png",
        "character": "five-ddeng.png",
    },
    {
        "order": 6,
        "id": "six-ddeng",
        "name": "6땡",
        "folder": "06_6땡_모란독접_20장",
        "identity": "모란 독접",
        "motif": "독나비 · 쌍단검 · 중첩 독",
        "accent": (204, 42, 126),
        "frame": "exec-2e20ef39-be57-4507-8482-a462cdf54b70.png",
        "character": "six-ddeng.png",
    },
    {
        "order": 7,
        "id": "seven-ddeng",
        "name": "7땡",
        "folder": "07_7땡_철퇴진동_20장",
        "identity": "철퇴 진동",
        "motif": "거대 망치 · 철판 · 브레이크 충격",
        "accent": (196, 55, 45),
        "frame": "exec-b604dcb0-ee24-4b8b-a17f-a4a7c9357b88.png",
        "character": "seven-ddeng.png",
    },
    {
        "order": 8,
        "id": "eight-ddeng",
        "name": "8땡",
        "folder": "08_8땡_팔문봉인_20장",
        "identity": "팔문 봉인",
        "motif": "금부 · 기러기 · 여덟 겹 속박",
        "accent": (163, 143, 75),
        "frame": "exec-628ec659-2ce1-4eb2-96de-b67c311c5ec0.png",
        "character": "eight-ddeng.png",
    },
    {
        "order": 9,
        "id": "nine-ddeng",
        "name": "9땡",
        "folder": "09_9땡_국화취월_20장",
        "identity": "국화 취월",
        "motif": "술독 · 호박빛 안개 · 정보 교란",
        "accent": (201, 145, 42),
        "frame": "exec-3a58d1d6-65d8-4a92-a261-57f943e8da6f.png",
        "character": "nine-ddeng.png",
    },
    {
        "order": 10,
        "id": "ten-ddeng",
        "name": "10땡",
        "folder": "10_10땡_단풍종언_20장",
        "identity": "단풍 종언",
        "motif": "거대 단풍 부채 · 돌풍 · 삼박 카운트",
        "accent": (201, 55, 41),
        "frame": "exec-e1c86874-576d-4afa-aa05-8ebfe54aa625.png",
        "character": "ten-ddeng.png",
    },
    {
        "order": 11,
        "id": "gwang-13",
        "name": "13광땡",
        "folder": "11_13광땡_일삼천궁_20장",
        "identity": "일삼 천궁",
        "motif": "장궁 · 약점 표식 · 정직한 저격",
        "accent": (205, 60, 62),
        "frame": "exec-47b047cf-7905-4afd-92f2-8f2dc4e4d5d1.png",
        "character": "gwang-13.png",
    },
    {
        "order": 12,
        "id": "gwang-18",
        "name": "18광땡",
        "folder": "12_18광땡_팔문일광_20장",
        "identity": "팔문 일광",
        "motif": "의장 지팡이 · 문양 봉쇄 · 태양 인장",
        "accent": (201, 144, 63),
        "frame": "exec-77061351-1b66-447b-9524-bfa686b7056f.png",
        "character": "gwang-18.png",
    },
    {
        "order": 13,
        "id": "gwang-38",
        "name": "38광땡",
        "folder": "13_38광땡_광열군림_20장",
        "identity": "광열 군림",
        "motif": "삼팔 이중 인장 · 흑염 · 최종 광열",
        "accent": (227, 48, 39),
        "frame": "exec-401ca172-4eee-4793-9846-9db84c76d20d.png",
        "character": "gwang-38.png",
    },
    {
        "order": 14,
        "id": "gusa",
        "name": "구사",
        "folder": "14_구사_저패역천_20장",
        "identity": "저패 역천",
        "motif": "뒤집힌 매듭 · 반월 톱날 · 낮은 패 반전",
        "accent": (121, 177, 164),
        "frame": "exec-19d3243c-8b29-426a-b5ff-0df7db07cf07.png",
        "character": "gusa.png",
    },
    {
        "order": 15,
        "id": "ddengjabi",
        "name": "땡잡이",
        "folder": "15_땡잡이_청쇄추적_20장",
        "identity": "청쇄 추적",
        "motif": "푸른 혼불 · 사슬 · 반복 패 파괴",
        "accent": (80, 158, 222),
        "frame": "exec-3c2687f3-646b-4cf2-befe-496ecaff1b51.png",
        "character": "ddengjabi.png",
    },
    {
        "order": 16,
        "id": "meonggusa",
        "name": "멍구사",
        "folder": "16_멍구사_무음미끼_20장",
        "identity": "무음 미끼",
        "motif": "쌍단검 · 안개 잔상 · 버린 패 기습",
        "accent": (140, 181, 171),
        "frame": "exec-c82ea6ec-361e-4dcd-872c-43be98be125f.png",
        "character": "meonggusa.png",
    },
    {
        "order": 17,
        "id": "amhaeng",
        "name": "암행어사",
        "folder": "17_암행어사_장부심판_20장",
        "identity": "장부 심판",
        "motif": "마패 · 죄목 장부 · 반복 행동 판결",
        "accent": (184, 60, 84),
        "frame": "exec-245f286c-dfff-424f-bf55-93a02af9a4bc.png",
        "character": "amhaeng.png",
    },
]


GROUND_SOURCES = [
    ("dark_stone", "검은 자갈 보행지면", "exec-84a0844c-058d-4ae6-87fc-6ec254cee4ad.png"),
    ("heart_mosaic", "파손된 하트 구역 지면", "exec-523967b3-518f-4bcc-ace5-e78907d8b49b.png"),
    ("diamond_marble", "다이아 균열 대리석", "exec-4132b31d-c2b3-4d3d-8353-776d3bc44d06.png"),
    ("club_moss", "클럽 이끼 석판", "exec-ef52a62e-31e3-4b7b-9a4e-f5ea5c98869c.png"),
    ("time_crack", "시간 균열 자갈지면", "exec-ba9fa94f-6634-4598-80b1-ac57ee8acb46.png"),
    ("burned", "그을린 폐허 지면", "exec-0d7bc67c-d580-4803-9a70-570f7c920d4a.png"),
    ("rain", "빗물 고인 보행지면", "exec-86263ad8-c9fd-4bd0-b035-26631d2b4c50.png"),
    ("market", "무너진 시장 지면", "exec-0ca365d8-09b0-4110-87b9-c1bd92b60e6f.png"),
    ("seal_shrine", "금부 사당 지면", "exec-bbcffc6b-dd76-4521-8068-d228dc3e0a12.png"),
]


ROAD_FAMILIES = [
    ("main", "왕도 검은 자갈길", "dark_stone"),
    ("heart", "하트 구역 붉은 벽돌길", "heart_mosaic"),
    ("spade", "스페이드 구역 빗물길", "rain"),
    ("diamond", "다이아 구역 대리석길", "diamond_marble"),
    ("club", "클럽 구역 이끼길", "club_moss"),
    ("time", "시간붕괴 청록 균열길", "time_crack"),
]

ROAD_STATES = [
    ("clean", "정돈", None, 0.0),
    ("cracked", "균열", "time_crack", 0.34),
    ("wet", "침수", "rain", 0.36),
    ("scorched", "그을림", "burned", 0.34),
    ("cards", "카드 잔해", "market", 0.32),
    ("collapsed", "붕괴 흔적", "seal_shrine", 0.38),
]


def font(path: Path, size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(path), size)


def centered(draw: ImageDraw.ImageDraw, center: tuple[float, float], text: str, selected, fill, stroke=0, stroke_fill=None):
    box = draw.textbbox((0, 0), text, font=selected, stroke_width=stroke)
    x = center[0] - (box[2] - box[0]) / 2
    y = center[1] - (box[3] - box[1]) / 2 - box[1]
    draw.text((x, y), text, font=selected, fill=fill, stroke_width=stroke, stroke_fill=stroke_fill)


def fit_font(draw: ImageDraw.ImageDraw, text: str, width: int, start: int, path: Path = FONT_BOLD):
    for size in range(start, 11, -1):
        selected = font(path, size)
        box = draw.textbbox((0, 0), text, font=selected)
        if box[2] - box[0] <= width:
            return selected
    return font(path, 12)


def clear_directories():
    for directory in (DECK_ROOT, GROUND_ROOT, ROAD_ROOT, PREVIEW_ROOT, SOURCE_ROOT, ZIP_ROOT):
        if directory.exists():
            shutil.rmtree(directory)
        directory.mkdir(parents=True, exist_ok=True)


def crop_generated_card(path: Path) -> Image.Image:
    image = Image.open(path).convert("RGB")
    corner = image.crop((0, 0, 48, 48)).resize((1, 1)).getpixel((0, 0))
    bg = Image.new("RGB", image.size, corner)
    diff = ImageChops.difference(image, bg).convert("L").point(lambda value: 255 if value > 24 else 0)
    diff = diff.filter(ImageFilter.MaxFilter(9))
    box = diff.getbbox()
    if box is None:
        return ImageOps.fit(image, CARD_SIZE, Image.Resampling.LANCZOS)
    left, top, right, bottom = box
    left = max(0, left - 8)
    top = max(0, top - 8)
    right = min(image.width, right + 8)
    bottom = min(image.height, bottom + 8)
    card = image.crop((left, top, right, bottom))
    return ImageOps.fit(card, CARD_SIZE, Image.Resampling.LANCZOS, centering=(0.5, 0.5))


def month_art(month: int, variant: str, accent: tuple[int, int, int]) -> Image.Image:
    suffix = "1" if variant == "A" else "3"
    path = ROOT / "Assets" / "섰다패" / f"{month:02d}_{['', '소나무', '매화', '벚꽃', '흑싸리', '난초', '모란', '홍싸리', '공산', '국화', '단풍'][month]}_{suffix}.png"
    source = Image.open(path).convert("RGBA")
    source = source.crop((22, 22, source.width - 22, source.height - 22))
    source.thumbnail((340, 505), Image.Resampling.LANCZOS)

    pixels = source.load()
    for y in range(source.height):
        for x in range(source.width):
            r, g, b, _ = pixels[x, y]
            whiteness = min(r, g, b)
            if whiteness > 244 and max(r, g, b) - min(r, g, b) < 15:
                pixels[x, y] = (255, 255, 255, 0)
            elif r > 175 and r > g * 1.5 and r > b * 1.5:
                mix = 0.72 if variant == "A" else 0.92
                pixels[x, y] = (
                    int(accent[0] * mix + r * (1 - mix)),
                    int(accent[1] * mix + g * (1 - mix)),
                    int(accent[2] * mix + b * (1 - mix)),
                    255,
                )
            elif r > 170 and g > 150 and b < 110:
                pixels[x, y] = (*GOLD, 255)
            else:
                pixels[x, y] = (r, g, b, 255)
    return source


def add_deck_labels(card: Image.Image, spec: dict, month: int, variant: str) -> Image.Image:
    overlay = Image.new("RGBA", CARD_SIZE, (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    accent = spec["accent"]
    draw.rounded_rectangle((18, 18, 158, 78), radius=12, fill=INK + (232,), outline=GOLD + (255,), width=2)
    draw.rounded_rectangle((328, 18, 468, 78), radius=12, fill=INK + (232,), outline=accent + (255,), width=2)
    centered(draw, (88, 48), f"{month}월 {variant}", font(FONT_BOLD, 25), IVORY + (255,))
    centered(draw, (398, 48), spec["name"], fit_font(draw, spec["name"], 118, 24), IVORY + (255,))

    draw.rounded_rectangle((24, 660, 462, 740), radius=13, fill=(2, 4, 8, 242), outline=GOLD + (255,), width=2)
    centered(draw, (243, 686), spec["identity"], fit_font(draw, spec["identity"], 395, 29), IVORY + (255,))
    centered(draw, (243, 720), spec["motif"], fit_font(draw, spec["motif"], 405, 18, FONT_REGULAR), accent + (255,))
    card.alpha_composite(overlay)
    return card


def build_face_card(frame: Image.Image, spec: dict, month: int, variant: str) -> Image.Image:
    card = frame.convert("RGBA")
    accent = spec["accent"]

    veil = Image.new("RGBA", CARD_SIZE, (0, 0, 0, 0))
    veil_draw = ImageDraw.Draw(veil)
    veil_draw.rounded_rectangle((64, 94, 422, 638), radius=46, fill=(248, 241, 220, 62), outline=accent + (105,), width=3)
    card.alpha_composite(veil)

    art = month_art(month, variant, accent)
    if variant == "B":
        art = ImageOps.mirror(art)
    glow_alpha = art.getchannel("A").filter(ImageFilter.GaussianBlur(13))
    glow = Image.new("RGBA", art.size, accent + (0,))
    glow.putalpha(glow_alpha.point(lambda value: int(value * 0.42)))
    x = (CARD_SIZE[0] - art.width) // 2 + (-7 if variant == "A" else 7)
    y = 118 + (month % 3) * 4
    card.alpha_composite(glow, (x, y))
    card.alpha_composite(art, (x, y))

    marks = Image.new("RGBA", CARD_SIZE, (0, 0, 0, 0))
    md = ImageDraw.Draw(marks)
    for index in range(month % 5 + 1):
        angle = (index / max(1, month % 5 + 1)) * math.tau + (0.25 if variant == "B" else 0)
        px = 243 + math.cos(angle) * 174
        py = 376 + math.sin(angle) * 248
        md.ellipse((px - 4, py - 4, px + 4, py + 4), fill=accent + (210,), outline=GOLD + (235,), width=1)
    card.alpha_composite(marks)
    return add_deck_labels(card, spec, month, variant).convert("RGB")


def load_character(spec: dict) -> Image.Image:
    path = ROOT / "ProjectAssetGuide" / "public" / "enemies" / spec["character"]
    image = Image.open(path).convert("RGBA")
    image.thumbnail((360, 520), Image.Resampling.LANCZOS)
    return image


def build_card_back(frame: Image.Image, spec: dict) -> Image.Image:
    card = ImageEnhance.Brightness(frame.convert("RGB")).enhance(0.48).convert("RGBA")
    accent = spec["accent"]
    aura = Image.new("RGBA", CARD_SIZE, (0, 0, 0, 0))
    ad = ImageDraw.Draw(aura)
    for radius, alpha in ((168, 65), (142, 95), (116, 125)):
        ad.ellipse((243 - radius, 370 - radius, 243 + radius, 370 + radius), outline=accent + (alpha,), width=5)
    aura = aura.filter(ImageFilter.GaussianBlur(8))
    card.alpha_composite(aura)

    character = load_character(spec)
    char_alpha = character.getchannel("A")
    shadow = Image.new("RGBA", character.size, accent + (0,))
    shadow.putalpha(char_alpha.filter(ImageFilter.GaussianBlur(17)).point(lambda value: min(175, value)))
    x = (486 - character.width) // 2
    y = 122 + max(0, (500 - character.height) // 2)
    card.alpha_composite(shadow, (x, y))
    card.alpha_composite(character, (x, y))

    overlay = Image.new("RGBA", CARD_SIZE, (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    draw.rounded_rectangle((20, 18, 466, 84), radius=14, fill=(2, 4, 8, 238), outline=GOLD + (255,), width=3)
    centered(draw, (243, 51), f"{spec['name']} 전용 섯다", fit_font(draw, f"{spec['name']} 전용 섯다", 410, 31), IVORY + (255,))
    draw.rounded_rectangle((30, 662, 456, 738), radius=14, fill=(2, 4, 8, 242), outline=accent + (255,), width=3)
    centered(draw, (243, 687), spec["identity"], fit_font(draw, spec["identity"], 390, 28), IVORY + (255,))
    centered(draw, (243, 718), spec["motif"], fit_font(draw, spec["motif"], 395, 17, FONT_REGULAR), accent + (255,))
    card.alpha_composite(overlay)
    return card.convert("RGB")


def make_deck_preview(spec: dict, entries: list[dict], directory: Path) -> Path:
    columns = 7
    thumb = (118, 184)
    rows = math.ceil(len(entries) / columns)
    cell_w, cell_h = 142, 226
    canvas = Image.new("RGB", (columns * cell_w + 36, rows * cell_h + 112), (8, 10, 14))
    draw = ImageDraw.Draw(canvas)
    draw.text((24, 20), f"{spec['name']} 전용 섯다 전체 덱 · {spec['identity']}", font=font(FONT_BOLD, 34), fill=IVORY)
    draw.text((26, 64), spec["motif"], font=font(FONT_REGULAR, 20), fill=spec["accent"])
    for index, entry in enumerate(entries):
        image = Image.open(directory / entry["file"]).convert("RGB")
        image.thumbnail(thumb, Image.Resampling.LANCZOS)
        x = 18 + (index % columns) * cell_w + (cell_w - image.width) // 2
        y = 98 + (index // columns) * cell_h
        canvas.paste(image, (x, y))
        centered(draw, (18 + (index % columns) * cell_w + cell_w / 2, y + image.height + 17), entry["label"], font(FONT_REGULAR, 15), IVORY)
    path = PREVIEW_ROOT / f"preview_{spec['order']:02d}_{spec['name']}_전체덱21.png"
    canvas.save(path, optimize=True)
    return path


def build_decks() -> list[dict]:
    all_decks = []
    for spec in DECK_SPECS:
        directory = DECK_ROOT / spec["folder"]
        directory.mkdir(parents=True, exist_ok=True)
        frame = crop_generated_card(GENERATED / spec["frame"])
        entries = []
        for month in range(1, 11):
            for variant in ("A", "B"):
                filename = f"{month:02d}월_{variant}_{spec['name']}_{spec['identity']}.png"
                build_face_card(frame, spec, month, variant).save(directory / filename, optimize=True)
                entries.append({"file": filename, "label": f"{month}월 {variant}", "month": month, "variant": variant})
        back_name = f"Back_{spec['name']}_{spec['identity']}.png"
        build_card_back(frame, spec).save(directory / back_name, optimize=True)
        entries.append({"file": back_name, "label": "전용 뒷면", "month": None, "variant": "BACK"})
        preview = make_deck_preview(spec, entries, directory)
        all_decks.append(
            {
                "id": spec["id"],
                "name": spec["name"],
                "identity": spec["identity"],
                "motif": spec["motif"],
                "folder": spec["folder"],
                "faceCards": 20,
                "backCards": 1,
                "preview": preview.name,
                "cards": entries,
            }
        )
    return all_decks


def hex_mask() -> Image.Image:
    mask = Image.new("L", GROUND_SIZE, 0)
    ImageDraw.Draw(mask).polygon(HEX_POINTS, fill=255)
    return mask


def normalize_ground(path: Path) -> Image.Image:
    image = Image.open(path).convert("RGB")
    corner = image.crop((0, 0, 48, 48)).resize((1, 1)).getpixel((0, 0))
    background = Image.new("RGB", image.size, corner)
    difference = ImageChops.difference(image, background).convert("L").point(lambda value: 255 if value > 22 else 0)
    difference = difference.filter(ImageFilter.MaxFilter(11))
    box = difference.getbbox()
    if box is None:
        cropped = image
    else:
        left, top, right, bottom = box
        cropped = image.crop((max(0, left - 5), max(0, top - 5), min(image.width, right + 5), min(image.height, bottom + 5)))
    fitted = ImageOps.fit(cropped, (924, 988), Image.Resampling.LANCZOS, centering=(0.5, 0.5))
    canvas = Image.new("RGB", GROUND_SIZE, (8, 10, 13))
    canvas.paste(fitted, (50, 18))
    return canvas


def ground_variant(image: Image.Image, variant: int) -> Image.Image:
    if variant == 1:
        result = image
    elif variant == 2:
        result = ImageOps.mirror(image)
        result = ImageEnhance.Contrast(result).enhance(1.06)
        result = ImageEnhance.Brightness(result).enhance(0.94)
    else:
        result = image.rotate(120, resample=Image.Resampling.BICUBIC, expand=False)
        result = ImageEnhance.Color(result).enhance(0.86)
        result = ImageEnhance.Contrast(result).enhance(1.1)
    rgba = result.convert("RGBA")
    rgba.putalpha(hex_mask())
    return rgba


def build_grounds() -> list[dict]:
    entries = []
    normalized = {}
    for source_id, label, filename in GROUND_SOURCES:
        image = normalize_ground(GENERATED / filename)
        normalized[source_id] = image
        for variant in range(1, 4):
            out_name = f"ground_{source_id}_{variant:02d}.png"
            ground_variant(image, variant).save(GROUND_ROOT / out_name, optimize=True)
            entries.append(
                {
                    "file": out_name,
                    "name": f"{label} {variant}",
                    "type": "walkable-ground",
                    "walkable": True,
                    "roadConnections": None,
                    "source": source_id,
                }
            )

    blends = [
        ("ground_mixed_gold_01.png", "금장 혼합 보행지면", Image.blend(normalized["dark_stone"], normalized["diamond_marble"], 0.42)),
        ("ground_mixed_teal_01.png", "청록 균열 혼합 보행지면", Image.blend(normalized["club_moss"], normalized["time_crack"], 0.46)),
    ]
    for filename, name, image in blends:
        rgba = ImageEnhance.Contrast(image).enhance(1.08).convert("RGBA")
        rgba.putalpha(hex_mask())
        rgba.save(GROUND_ROOT / filename, optimize=True)
        entries.append(
            {
                "file": filename,
                "name": name,
                "type": "walkable-ground",
                "walkable": True,
                "roadConnections": None,
                "source": "mixed",
            }
        )
    return entries


def inset_hex_mask(inset: int) -> Image.Image:
    mask = Image.new("L", GROUND_SIZE, 0)
    cx, cy = 512, 512
    points = []
    for x, y in HEX_POINTS:
        dx, dy = x - cx, y - cy
        length = math.hypot(dx, dy)
        scale = max(0.0, (length - inset) / length)
        points.append((cx + dx * scale, cy + dy * scale))
    ImageDraw.Draw(mask).polygon(points, fill=255)
    return mask


def shared_edge_ring() -> Image.Image:
    outer = hex_mask()
    inner = inset_hex_mask(116).filter(ImageFilter.GaussianBlur(8))
    return ImageChops.subtract(outer, inner)


def add_road_state_details(image: Image.Image, state_id: str, accent: tuple[int, int, int], seed: int) -> Image.Image:
    rng = random.Random(seed)
    overlay = Image.new("RGBA", GROUND_SIZE, (0, 0, 0, 0))

    if state_id == "cracked":
        draw = ImageDraw.Draw(overlay)
        for _ in range(5):
            x = rng.randint(270, 760)
            y = rng.randint(270, 760)
            points = [(x, y)]
            for _ in range(rng.randint(3, 6)):
                x += rng.randint(-70, 70)
                y += rng.randint(-58, 58)
                points.append((x, y))
            draw.line(points, fill=accent + (190,), width=7, joint="curve")
            draw.line(points, fill=(18, 22, 25, 235), width=3, joint="curve")
    elif state_id == "wet":
        draw = ImageDraw.Draw(overlay)
        for _ in range(4):
            x = rng.randint(230, 720)
            y = rng.randint(260, 730)
            w = rng.randint(120, 240)
            h = rng.randint(70, 150)
            draw.ellipse((x, y, x + w, y + h), fill=(39, 78, 105, 88), outline=(141, 195, 213, 125), width=4)
            draw.arc((x + 22, y + 18, x + w - 18, y + h - 14), 205, 332, fill=(218, 236, 239, 115), width=4)
        overlay = overlay.filter(ImageFilter.GaussianBlur(3))
    elif state_id == "scorched":
        draw = ImageDraw.Draw(overlay)
        for _ in range(4):
            x = rng.randint(250, 720)
            y = rng.randint(250, 720)
            r = rng.randint(65, 135)
            draw.ellipse((x - r, y - r * 0.65, x + r, y + r * 0.65), fill=(4, 3, 4, rng.randint(80, 145)))
            draw.arc((x - r, y - r, x + r, y + r), rng.randint(0, 90), rng.randint(190, 330), fill=(112, 48, 25, 130), width=8)
        overlay = overlay.filter(ImageFilter.GaussianBlur(13))
    elif state_id == "cards":
        for index in range(7):
            scrap = Image.new("RGBA", (48, 70), (0, 0, 0, 0))
            draw = ImageDraw.Draw(scrap)
            draw.rounded_rectangle((3, 3, 44, 66), radius=5, fill=(223, 211, 180, 210), outline=GOLD + (230,), width=3)
            suit_color = (157, 44, 52, 230) if index % 2 else (22, 26, 32, 230)
            draw.ellipse((17, 24, 31, 38), fill=suit_color)
            scrap = scrap.rotate(rng.randint(-55, 55), resample=Image.Resampling.BICUBIC, expand=True)
            x = rng.randint(210, 780) - scrap.width // 2
            y = rng.randint(230, 770) - scrap.height // 2
            overlay.alpha_composite(scrap, (x, y))
    elif state_id == "collapsed":
        draw = ImageDraw.Draw(overlay)
        for _ in range(13):
            x = rng.randint(210, 810)
            y = rng.randint(220, 790)
            radius = rng.randint(16, 48)
            points = []
            for point_index in range(rng.randint(5, 8)):
                angle = math.tau * point_index / 7 + rng.random() * 0.25
                rr = radius * rng.uniform(0.65, 1.2)
                points.append((x + math.cos(angle) * rr, y + math.sin(angle) * rr))
            draw.polygon(points, fill=(50, 50, 48, 185), outline=GOLD + (115,))
        for _ in range(3):
            x = rng.randint(320, 700)
            y = rng.randint(320, 700)
            draw.ellipse((x - 45, y - 26, x + 45, y + 26), outline=(13, 15, 18, 205), width=10)

    return Image.alpha_composite(image.convert("RGBA"), overlay)


def build_road_floor(
    base: Image.Image,
    detail: Image.Image | None,
    amount: float,
    common_edge: Image.Image,
    state_id: str,
    accent: tuple[int, int, int],
    seed: int,
) -> Image.Image:
    result = base.copy()
    if detail is not None and amount > 0:
        mixed = Image.blend(base, detail, amount)
        center = inset_hex_mask(82).filter(ImageFilter.GaussianBlur(32))
        result = Image.composite(mixed, result, center)

    rgba = add_road_state_details(result, state_id, accent, seed)
    master = normalize_ground(GENERATED / GROUND_SOURCES[0][2]).convert("RGBA")
    rgba = Image.composite(master, rgba, common_edge)
    rgba = ImageEnhance.Contrast(rgba).enhance(1.04)
    rgba.putalpha(hex_mask())
    return rgba


def build_road_floors() -> list[dict]:
    source_images = {
        source_id: normalize_ground(GENERATED / filename)
        for source_id, _, filename in GROUND_SOURCES
    }
    common_edge = shared_edge_ring()
    entries = []
    family_accents = {
        "main": (190, 158, 88),
        "heart": (186, 52, 58),
        "spade": (71, 144, 172),
        "diamond": (172, 78, 89),
        "club": (86, 144, 91),
        "time": (44, 215, 209),
    }
    for family_index, (family_id, family_name, source_id) in enumerate(ROAD_FAMILIES):
        base = source_images[source_id]
        for state_index, (state_id, state_name, detail_id, amount) in enumerate(ROAD_STATES):
            detail = source_images[detail_id] if detail_id else None
            tile = build_road_floor(
                base,
                detail,
                amount,
                common_edge,
                state_id,
                family_accents[family_id],
                family_index * 100 + state_index,
            )
            filename = f"roadfloor_{family_id}_{state_id}.png"
            tile.save(ROAD_ROOT / filename, optimize=True)
            entries.append(
                {
                    "file": filename,
                    "name": f"{family_name} · {state_name}",
                    "family": family_id,
                    "state": state_id,
                    "type": "full-road-surface",
                    "walkable": True,
                    "connectorLines": False,
                    "edgeStandard": "shared-stone-edge-v2",
                }
            )
    return entries


def make_ground_preview(entries: list[dict]) -> Path:
    columns = 5
    thumb = (196, 196)
    rows = math.ceil(len(entries) / columns)
    cell_w, cell_h = 222, 248
    canvas = Image.new("RGB", (columns * cell_w + 32, rows * cell_h + 112), (8, 10, 14))
    draw = ImageDraw.Draw(canvas)
    draw.text((24, 20), "파괴된 포커 마을 · 독립 보행 지면 29종", font=font(FONT_BOLD, 34), fill=IVORY)
    draw.text((26, 64), "타일 하나가 한 칸 전체 · 도로 연결선 없음 · 모든 변 방향 중립", font=font(FONT_REGULAR, 20), fill=(84, 214, 204))
    for index, entry in enumerate(entries):
        image = Image.open(GROUND_ROOT / entry["file"]).convert("RGBA")
        image.thumbnail(thumb, Image.Resampling.LANCZOS)
        x = 16 + (index % columns) * cell_w + (cell_w - image.width) // 2
        y = 96 + (index // columns) * cell_h
        canvas.alpha_composite(image, (x, y)) if canvas.mode == "RGBA" else canvas.paste(image, (x, y), image)
        label = entry["name"]
        centered(draw, (16 + (index % columns) * cell_w + cell_w / 2, y + image.height + 18), label, fit_font(draw, label, 200, 15, FONT_REGULAR), IVORY)
    path = PREVIEW_ROOT / "preview_독립보행지면_29종.png"
    canvas.save(path, optimize=True)
    return path


def make_road_preview(entries: list[dict]) -> Path:
    columns = 6
    thumb = (182, 182)
    rows = math.ceil(len(entries) / columns)
    cell_w, cell_h = 204, 234
    canvas = Image.new("RGB", (columns * cell_w + 32, rows * cell_h + 112), (8, 10, 14))
    draw = ImageDraw.Draw(canvas)
    draw.text((24, 20), "망가진 포커포커 마을 · 전면 길지면 36종", font=font(FONT_BOLD, 34), fill=IVORY)
    draw.text((26, 64), "한 육각형 전체가 길바닥 · 공통 가장자리 규격 · 연결선과 방향 구분 없음", font=font(FONT_REGULAR, 20), fill=(84, 214, 204))
    for index, entry in enumerate(entries):
        image = Image.open(ROAD_ROOT / entry["file"]).convert("RGBA")
        image.thumbnail(thumb, Image.Resampling.LANCZOS)
        x = 16 + (index % columns) * cell_w + (cell_w - image.width) // 2
        y = 96 + (index // columns) * cell_h
        canvas.paste(image, (x, y), image)
        centered(
            draw,
            (16 + (index % columns) * cell_w + cell_w / 2, y + image.height + 17),
            entry["name"],
            fit_font(draw, entry["name"], 190, 14, FONT_REGULAR),
            IVORY,
        )
    path = PREVIEW_ROOT / "preview_전면길지면_36종.png"
    canvas.save(path, optimize=True)
    return path


def tile_for_assembly(path: Path, width: int = 260) -> Image.Image:
    tile = Image.open(path).convert("RGBA").crop((50, 18, 974, 1006))
    height = round(width * tile.height / tile.width)
    return tile.resize((width, height), Image.Resampling.LANCZOS)


def assemble_hex_layout(entries: list[dict], layout: list[list[int]], name: str) -> Path:
    tile_w = 260
    tile_h = round(tile_w * 988 / 924)
    row_step = round(tile_h * 0.75)
    max_cols = max(len(row) for row in layout)
    canvas_w = max_cols * tile_w + tile_w + 100
    canvas_h = (len(layout) - 1) * row_step + tile_h + 100
    canvas = Image.new("RGBA", (canvas_w, canvas_h), (7, 9, 13, 255))
    for row_index, row in enumerate(layout):
        row_width = len(row) * tile_w
        start_x = (canvas_w - row_width) // 2 + (tile_w // 2 if row_index % 2 else 0)
        y = 50 + row_index * row_step
        for col_index, entry_index in enumerate(row):
            tile = tile_for_assembly(ROAD_ROOT / entries[entry_index % len(entries)]["file"], tile_w)
            x = start_x + col_index * tile_w
            canvas.alpha_composite(tile, (x, y))
    path = PREVIEW_ROOT / name
    canvas.convert("RGB").save(path, optimize=True)
    return path


def make_assembled_maps(entries: list[dict]) -> list[str]:
    layouts = [
        (
            "assembled_01_무너진왕도.png",
            [
                [0, 1, 2, 3],
                [4, 5, 6, 7, 8],
                [9, 10, 11, 12, 13, 14],
                [15, 16, 17, 18, 19],
                [20, 21, 22, 23],
            ],
        ),
        (
            "assembled_02_네문양구역.png",
            [
                [6, 7, 12, 13],
                [8, 9, 14, 15, 18],
                [10, 11, 16, 17, 19, 20],
                [24, 25, 30, 31, 21],
                [26, 27, 32, 33],
            ],
        ),
        (
            "assembled_03_시간붕괴광장.png",
            [
                [30, 31, 32],
                [33, 34, 35, 28],
                [29, 24, 25, 26, 27],
                [18, 19, 20, 21],
                [0, 4, 5],
            ],
        ),
    ]
    return [assemble_hex_layout(entries, layout, filename).name for filename, layout in layouts]


def make_master_deck_preview(decks: list[dict]) -> Path:
    columns = 4
    thumb = (234, 154)
    rows = math.ceil(len(decks) / columns)
    cell_w, cell_h = 264, 206
    canvas = Image.new("RGB", (columns * cell_w + 32, rows * cell_h + 108), (8, 10, 14))
    draw = ImageDraw.Draw(canvas)
    draw.text((24, 20), "상대 전용 섯다 전체 덱 · 17인 × 20장", font=font(FONT_BOLD, 34), fill=IVORY)
    draw.text((26, 63), "각 상대의 성격·무기·전투 문법이 덱 전체 프레임과 월패 변형에 적용됨", font=font(FONT_REGULAR, 19), fill=(84, 214, 204))
    for index, deck in enumerate(decks):
        image = Image.open(PREVIEW_ROOT / deck["preview"]).convert("RGB")
        image.thumbnail(thumb, Image.Resampling.LANCZOS)
        x = 16 + (index % columns) * cell_w + (cell_w - image.width) // 2
        y = 94 + (index // columns) * cell_h
        canvas.paste(image, (x, y))
        label = f"{deck['name']} · {deck['identity']}"
        centered(draw, (16 + (index % columns) * cell_w + cell_w / 2, y + image.height + 18), label, fit_font(draw, label, 244, 18), IVORY)
    path = PREVIEW_ROOT / "preview_상대전용_전체덱17종.png"
    canvas.save(path, optimize=True)
    return path


def copy_sources():
    frames = SOURCE_ROOT / "섯다덱_AI프레임_17종"
    grounds = SOURCE_ROOT / "독립지면_AI원화_9종"
    frames.mkdir(parents=True, exist_ok=True)
    grounds.mkdir(parents=True, exist_ok=True)
    for spec in DECK_SPECS:
        shutil.copy2(GENERATED / spec["frame"], frames / f"{spec['order']:02d}_{spec['name']}_{spec['identity']}_frame.png")
    for index, (_, label, filename) in enumerate(GROUND_SOURCES, 1):
        shutil.copy2(GENERATED / filename, grounds / f"{index:02d}_{label}.png")


def write_catalogs(decks: list[dict], grounds: list[dict], roads: list[dict], assembled_maps: list[str]):
    payload = {
        "version": 2,
        "rules": {
            "hexTileMeaning": "육각 타일 하나가 캐릭터가 걷는 한 칸 전체",
            "roadConnections": False,
            "seotdaDeckMeaning": "상대마다 1월부터 10월까지 20장 전체가 동일한 인물 문법을 공유",
        },
        "summary": {
            "opponents": len(decks),
            "seotdaFaceCards": sum(deck["faceCards"] for deck in decks),
            "seotdaBackCards": sum(deck["backCards"] for deck in decks),
            "walkableGroundTiles": len(grounds),
            "fullRoadGroundTiles": len(roads),
            "assembledMapExamples": len(assembled_maps),
        },
        "decks": decks,
        "groundTiles": grounds,
        "roadGroundTiles": roads,
        "assembledMapExamples": assembled_maps,
    }
    (PREVIEW_ROOT / "ui_ground_seotda_v2_catalog.json").write_text(
        json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    with (PREVIEW_ROOT / "상대전용_섯다덱_목록.csv").open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.DictWriter(handle, fieldnames=["name", "identity", "motif", "folder", "faceCards", "backCards"])
        writer.writeheader()
        for deck in decks:
            writer.writerow({key: deck[key] for key in writer.fieldnames})
    with (PREVIEW_ROOT / "독립보행지면_목록.csv").open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.DictWriter(handle, fieldnames=["file", "name", "type", "walkable", "source"])
        writer.writeheader()
        for item in grounds:
            writer.writerow({key: item[key] for key in writer.fieldnames})
    with (PREVIEW_ROOT / "전면길지면_목록.csv").open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.DictWriter(handle, fieldnames=["file", "name", "family", "state", "type", "walkable", "connectorLines", "edgeStandard"])
        writer.writeheader()
        writer.writerows(roads)


def zip_paths(target: Path, sources: list[tuple[Path, str]]):
    target.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(target, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for source, prefix in sources:
            for item in sorted(source.rglob("*")):
                if item.is_file():
                    archive.write(item, str(Path(prefix) / item.relative_to(source)))


def package_outputs() -> dict[str, int]:
    archives = {
        "ffss-opponent-seotda-full-decks-v2-17x20.zip": [(DECK_ROOT, "OpponentSeotdaFullDecks"), (PREVIEW_ROOT, "Catalog")],
        "ffss-poker-village-walkable-hex-ground-v2-29.zip": [(GROUND_ROOT, "WalkableHexGround"), (PREVIEW_ROOT, "Catalog")],
        "ffss-poker-village-full-road-ground-v2-36.zip": [(ROAD_ROOT, "FullRoadGround"), (PREVIEW_ROOT, "Catalog")],
        "ffss-seotda-ground-correction-v2-complete.zip": [
            (DECK_ROOT, "OpponentSeotdaFullDecks"),
            (GROUND_ROOT, "WalkableHexGround"),
            (ROAD_ROOT, "FullRoadGround"),
            (PREVIEW_ROOT, "Catalog"),
            (SOURCE_ROOT, "AISources"),
        ],
    }
    BUILD_ROOT.mkdir(parents=True, exist_ok=True)
    DOWNLOAD_MIRROR.mkdir(parents=True, exist_ok=True)
    sizes = {}
    for filename, sources in archives.items():
        built = BUILD_ROOT / filename
        zip_paths(built, sources)
        shutil.copy2(built, ZIP_ROOT / filename)
        shutil.copy2(built, DOWNLOAD_MIRROR / filename)
        sizes[filename] = built.stat().st_size
    return sizes


def validate(decks: list[dict], grounds: list[dict], roads: list[dict]):
    assert len(decks) == 17
    assert sum(deck["faceCards"] for deck in decks) == 340
    assert sum(deck["backCards"] for deck in decks) == 17
    assert len(grounds) == 29
    assert len(roads) == 36
    for deck in decks:
        directory = DECK_ROOT / deck["folder"]
        images = list(directory.glob("*.png"))
        assert len(images) == 21, (deck["name"], len(images))
        for path in images:
            with Image.open(path) as image:
                assert image.size == CARD_SIZE, (path, image.size)
    for item in grounds:
        path = GROUND_ROOT / item["file"]
        with Image.open(path) as image:
            assert image.size == GROUND_SIZE
            assert image.mode == "RGBA"
            alpha = image.getchannel("A")
            assert alpha.getpixel((0, 0)) == 0
            assert alpha.getpixel((512, 512)) > 240
            assert item["roadConnections"] is None
    for item in roads:
        path = ROAD_ROOT / item["file"]
        with Image.open(path) as image:
            assert image.size == GROUND_SIZE
            assert image.mode == "RGBA"
            alpha = image.getchannel("A")
            assert alpha.getpixel((0, 0)) == 0
            assert alpha.getpixel((512, 512)) > 240
            assert item["connectorLines"] is False
            assert item["edgeStandard"] == "shared-stone-edge-v2"


def main():
    clear_directories()
    decks = build_decks()
    grounds = build_grounds()
    roads = build_road_floors()
    make_master_deck_preview(decks)
    make_ground_preview(grounds)
    make_road_preview(roads)
    assembled_maps = make_assembled_maps(roads)
    copy_sources()
    write_catalogs(decks, grounds, roads, assembled_maps)
    validate(decks, grounds, roads)
    archives = package_outputs()
    print(
        json.dumps(
            {
                "opponents": len(decks),
                "seotdaFaces": sum(deck["faceCards"] for deck in decks),
                "seotdaBacks": sum(deck["backCards"] for deck in decks),
                "walkableGrounds": len(grounds),
                "fullRoadGrounds": len(roads),
                "assembledMaps": assembled_maps,
                "output": str(OUTPUT),
                "archives": archives,
            },
            ensure_ascii=False,
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
