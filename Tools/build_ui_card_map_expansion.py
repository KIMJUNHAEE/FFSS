from __future__ import annotations

import csv
import json
import math
import shutil
import zipfile
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont, ImageOps


ROOT = Path(r"C:\git\FFSS")
GENERATED = Path(r"C:\Users\kirby\.codex\generated_images\019fd035-e339-7860-93b2-b1cd104a6ff5")
OUTPUT = Path(r"C:\Users\kirby\OneDrive\바탕 화면\구구가가\웹게임_UI_신규\즉시사용_강화카드_파괴된포커마을")
BUILD = ROOT / "Builds" / "UIPacks"
SITE = ROOT / "ProjectAssetGuide" / "public" / "ui-expansion"

CHRONO_DIR = OUTPUT / "주인공_시간각성포커_54장_뒷면1장"
REVERSE_DIR = OUTPUT / "리버스포커_54장_뒷면1장"
SEOTDA_DIR = OUTPUT / "상대전용_각성섯다_17장"
MAP_DIR = OUTPUT / "파괴된포커마을_육각맵_29종"
CATALOG_DIR = OUTPUT / "미리보기_카탈로그"
SOURCE_DIR = OUTPUT / "AI_원화소스"
ZIP_DIR = OUTPUT / "ZIP_묶음"
DOWNLOAD_MIRROR = Path(r"C:\Users\kirby\OneDrive\바탕 화면\구구가가\웹게임_UI_신규\사이트_다운로드_ZIP")

CARD_SIZE = (486, 758)
MAP_SIZE = (1024, 1024)
GOLD = (224, 180, 84)
IVORY = (244, 237, 215)
INK = (6, 9, 14)
TEAL = (66, 224, 218)
SILVER = (216, 225, 232)

FONT_KR = Path(r"C:\Windows\Fonts\malgunbd.ttf")
FONT_KR_REG = Path(r"C:\Windows\Fonts\malgun.ttf")
FONT_SYMBOL = Path(r"C:\Windows\Fonts\seguisym.ttf")
FONT_LATIN = Path(r"C:\Windows\Fonts\georgiab.ttf")

CARD_SOURCES = {
    "S-court": "exec-dd7cbc6d-765c-4486-bcb2-4a6c7f005eab.png",
    "C-court": "exec-e99c8f1a-f1bb-4ffe-bd29-e11e2f3e909b.png",
    "H-court": "exec-8c94f721-062d-4f81-8271-c3ed0e103ab6.png",
    "D-court": "exec-4b578da2-401a-4c2f-aef9-2e728ad5cd64.png",
    "chrono-joker": "exec-ad0452a3-147a-4287-9df4-e22c23625846.png",
    "reverse-joker": "exec-51e76b0a-d59c-41d7-9934-c76d70a1241a.png",
    "chrono-back": "exec-47a463be-03bb-44b3-8dc2-8b85e0692df2.png",
    "reverse-back": "exec-8c214125-5419-4e2d-a3e4-9d1fd13293db.png",
    "S-number": "exec-0af655d0-b5a2-4b44-bb3a-b11e223c958f.png",
    "C-number": "exec-dcbeb4d4-f9a7-4233-8591-7d9468130786.png",
    "H-number": "exec-cc40a54b-c93c-4dde-9d05-6baf4e2d2043.png",
    "D-number": "exec-0ddc708a-bbef-4a35-b025-6ff4a14eec1f.png",
}

MAP_SOURCES = {
    "plaza_heart": "exec-ecd22cc9-9298-4a37-a715-8f9df0aee62f.png",
    "plaza_spade": "exec-a306d02f-1daf-4b68-930e-a79155540a25.png",
    "plaza_diamond": "exec-7a7848f9-d5dd-4708-b259-94a4e351a3dc.png",
    "plaza_club": "exec-6ce34a44-c228-4f26-a08b-f98e8a37e03a.png",
    "road_hub": "exec-e17f0451-ff4d-4940-9312-fc282064aa19.png",
    "neutral_ground": "exec-f5ac1789-0c30-4926-90b1-3bc902c1c765.png",
    "landmark_clocktower": "exec-11046456-18d8-4cfa-aec6-f0171ee390c5.png",
    "landmark_tea_salon": "exec-f974b4b3-06c3-49a2-abdb-ed19926c782a.png",
    "landmark_casino_gate": "exec-07a72a69-1f94-4a73-b035-ccdbc1a2c4fd.png",
    "hazard_time_bridge": "exec-37188625-b826-4ed0-abbb-3d42adeb5a1f.png",
    "arena_time_crater": "exec-f34eec67-1e54-4b16-944a-f2c56a0436ea.png",
}

SUITS = {
    "S": {"ko": "스페이드", "symbol": "♠", "color": (225, 234, 240), "reverse": (56, 226, 236)},
    "C": {"ko": "클럽", "symbol": "♣", "color": (86, 187, 128), "reverse": (92, 246, 194)},
    "H": {"ko": "하트", "symbol": "♥", "color": (239, 75, 93), "reverse": (225, 225, 219)},
    "D": {"ko": "다이아", "symbol": "♦", "color": (242, 184, 73), "reverse": (238, 238, 229)},
}

RANK_NAMES = {
    1: ("A", "에이스"),
    2: ("2", "2"),
    3: ("3", "3"),
    4: ("4", "4"),
    5: ("5", "5"),
    6: ("6", "6"),
    7: ("7", "7"),
    8: ("8", "8"),
    9: ("9", "9"),
    10: ("10", "10"),
    11: ("J", "잭"),
    12: ("Q", "퀸"),
    13: ("K", "킹"),
}

PIP_LAYOUTS = {
    2: [(0.5, 0.26), (0.5, 0.74)],
    3: [(0.5, 0.22), (0.5, 0.5), (0.5, 0.78)],
    4: [(0.32, 0.26), (0.68, 0.26), (0.32, 0.74), (0.68, 0.74)],
    5: [(0.32, 0.24), (0.68, 0.24), (0.5, 0.5), (0.32, 0.76), (0.68, 0.76)],
    6: [(0.32, 0.2), (0.68, 0.2), (0.32, 0.5), (0.68, 0.5), (0.32, 0.8), (0.68, 0.8)],
    7: [(0.32, 0.17), (0.68, 0.17), (0.5, 0.34), (0.32, 0.5), (0.68, 0.5), (0.32, 0.83), (0.68, 0.83)],
    8: [(0.32, 0.16), (0.68, 0.16), (0.5, 0.33), (0.32, 0.43), (0.68, 0.43), (0.5, 0.67), (0.32, 0.84), (0.68, 0.84)],
    9: [(0.32, 0.14), (0.68, 0.14), (0.32, 0.35), (0.68, 0.35), (0.5, 0.5), (0.32, 0.65), (0.68, 0.65), (0.32, 0.86), (0.68, 0.86)],
    10: [(0.32, 0.1), (0.68, 0.1), (0.32, 0.3), (0.68, 0.3), (0.32, 0.5), (0.68, 0.5), (0.32, 0.7), (0.68, 0.7), (0.32, 0.9), (0.68, 0.9)],
}

SEOTDA_SPECS = [
    ("1땡", "sig_01_ddang", "1월", "각성 솔잎 철벽패", "수비 결계", "솔잎과 목검의 방어막이 겹쳐지고, 막아낸 충격을 관통 반격으로 돌린다.", (86, 179, 116)),
    ("2땡", "sig_02_ddang", "2월", "각성 설매 반격패", "매화 반격", "두 번째 공격을 읽어 설매 꽃잎으로 궤도를 바꾸고 즉시 반격한다.", (239, 137, 166)),
    ("3땡", "sig_03_ddang", "3월", "각성 삼연화 만개패", "삼연참", "세 장의 꽃잎 인장이 차례로 피며 마지막 베기에 광채가 폭발한다.", (244, 103, 139)),
    ("4땡", "sig_04_ddang", "4월", "각성 흑싸리 사신패", "회피 사신", "낫의 검은 잔상이 보존한 패를 베고 빈 슬롯으로 미끄러진다.", (171, 130, 218)),
    ("5땡", "sig_05_ddang", "5월", "각성 창포 회귀패", "행동 복제", "수면 거울이 직전 행동을 복제하고 반대 행동에 창포 파문을 남긴다.", (74, 181, 209)),
    ("6땡", "sig_06_ddang", "6월", "각성 육화 독접패", "독나비", "모란과 독나비가 상처에 달라붙고 검은 문양의 방어만이 이를 태운다.", (177, 83, 203)),
    ("7땡", "sig_07_ddang", "7월", "각성 홍싸리 진철패", "철퇴 반사", "대철퇴가 방어 충격을 축적한 뒤 압박 게이지째 되돌려친다.", (221, 84, 69)),
    ("8땡", "sig_08_ddang", "8월", "각성 팔문 봉천패", "팔문 봉인", "여덟 문이 붉은 문양과 검은 문양을 번갈아 잠그며 탈출로를 좁힌다.", (198, 174, 82)),
    ("9땡", "sig_09_ddang", "9월", "각성 국화 취월패", "취월 안개", "국화 술안개가 의도를 감추고 달빛 잔상만 남겨 다음 수를 흐린다.", (207, 153, 74)),
    ("10땡", "sig_10_ddang", "10월", "각성 단풍 종언패", "십연 카운트", "열 장의 단풍이 한 장씩 타들어가며 마지막 잎에서 종언의 일격이 열린다.", (232, 77, 61)),
    ("13광땡", "sig_13_gwang", "1·3광", "각성 일삼 천궁패", "약점 저격", "붉은 조준실이 약점을 묶고 삼중 화살이 시간차로 같은 틈을 꿰뚫는다.", (225, 102, 83)),
    ("18광땡", "sig_18_gwang", "1·8광", "각성 일팔 팔문패", "문양 봉쇄", "금륜의 여덟 문이 선택한 색을 봉인하고 반대 문양을 시험한다.", (218, 178, 64)),
    ("38광땡", "sig_38_gwang", "3·8광", "각성 삼팔 광겁패", "최종 광열", "두 광패가 합쳐질수록 광열이 차오르고 세 번째 합일에서 전장을 태운다.", (244, 72, 46)),
    ("구사", "sig_94_reset", "4·9", "각성 구사 역천패", "규칙 반전", "뒤집힌 판이 낮은 패를 왕좌로 올리고 높은 패의 광채를 깎아낸다.", (88, 196, 188)),
    ("땡잡이", "sig_37_hunter", "3·7", "각성 청쇄 땡멸패", "땡 사냥", "청색 사슬과 조준선이 같은 숫자의 쌍을 묶어 땡의 방어를 찢는다.", (71, 154, 224)),
    ("멍구사", "sig_49_assassin", "4·9", "각성 멍구사 무음패", "미끼 잠행", "버린 카드가 미끼 잔상으로 남고 네 장 교체의 순간 그림자가 칼을 뽑는다.", (126, 108, 192)),
    ("암행어사", "sig_47_inspector", "4·7", "각성 마패 심판패", "죄목 심판", "마패와 장부가 반복 행동을 기록하고 세 번째 같은 선택에 붉은 판결을 내린다.", (229, 165, 56)),
]

HEX_POINTS = [(512, 22), (972, 256), (972, 768), (512, 1002), (52, 768), (52, 256)]
EDGE_POINTS = {
    "NE": (742, 139),
    "E": (972, 512),
    "SE": (742, 885),
    "SW": (282, 885),
    "W": (52, 512),
    "NW": (282, 139),
}

ROAD_PATTERNS = [
    ("road_straight_NE_SW", ["NE", "SW"], "직선 NE-SW"),
    ("road_straight_E_W", ["E", "W"], "직선 E-W"),
    ("road_straight_NW_SE", ["NW", "SE"], "직선 NW-SE"),
    ("road_curve_NE_E", ["NE", "E"], "곡선 NE-E"),
    ("road_curve_E_SE", ["E", "SE"], "곡선 E-SE"),
    ("road_curve_SE_SW", ["SE", "SW"], "곡선 SE-SW"),
    ("road_curve_SW_W", ["SW", "W"], "곡선 SW-W"),
    ("road_curve_W_NW", ["W", "NW"], "곡선 W-NW"),
    ("road_curve_NW_NE", ["NW", "NE"], "곡선 NW-NE"),
    ("road_T_NW_NE_E", ["NW", "NE", "E"], "T자 북동"),
    ("road_T_NE_E_SE", ["NE", "E", "SE"], "T자 동쪽"),
    ("road_T_SE_SW_W", ["SE", "SW", "W"], "T자 남서"),
    ("road_Y_NE_SE_W", ["NE", "SE", "W"], "Y자 A"),
    ("road_Y_NW_E_SW", ["NW", "E", "SW"], "Y자 B"),
    ("road_cross_NE_E_SW_W", ["NE", "E", "SW", "W"], "4방 교차"),
    ("road_cross_NW_NE_SE_SW", ["NW", "NE", "SE", "SW"], "4방 세로"),
    ("road_fiveway", ["NW", "NE", "E", "SE", "SW"], "5방 교차"),
    ("road_sixway", ["NW", "NE", "E", "SE", "SW", "W"], "6방 교차"),
]


def fnt(path: Path, size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(path), size)


def fit_text(draw: ImageDraw.ImageDraw, text: str, width: int, size: int, path: Path = FONT_KR):
    while size > 12:
        selected = fnt(path, size)
        box = draw.textbbox((0, 0), text, font=selected)
        if box[2] - box[0] <= width:
            return selected
        size -= 1
    return fnt(path, size)


def centered(draw: ImageDraw.ImageDraw, xy: tuple[float, float], text: str, font, fill, stroke_width=0, stroke_fill=None):
    box = draw.textbbox((0, 0), text, font=font, stroke_width=stroke_width)
    x = xy[0] - (box[2] - box[0]) / 2
    y = xy[1] - (box[3] - box[1]) / 2 - box[1]
    draw.text((x, y), text, font=font, fill=fill, stroke_width=stroke_width, stroke_fill=stroke_fill)


def fit_cover(path: Path, size: tuple[int, int]) -> Image.Image:
    return ImageOps.fit(Image.open(path).convert("RGB"), size, Image.Resampling.LANCZOS, centering=(0.5, 0.5))


def crop_triptych(path: Path) -> list[Image.Image]:
    image = Image.open(path).convert("RGB")
    width, height = image.size
    panels = []
    for index in range(3):
        left = round(index * width / 3)
        right = round((index + 1) * width / 3)
        panel = image.crop((left + 5, 5, right - 5, height - 5))
        panels.append(ImageOps.fit(panel, CARD_SIZE, Image.Resampling.LANCZOS))
    return panels


def glow_text(base: Image.Image, xy: tuple[int, int], text: str, font, color: tuple[int, int, int], radius: int = 8):
    glow = Image.new("RGBA", base.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(glow)
    centered(draw, xy, text, font, color + (220,))
    blur = glow.filter(ImageFilter.GaussianBlur(radius))
    base.alpha_composite(blur)
    base.alpha_composite(glow)


def add_card_labels(image: Image.Image, suit: str, rank: int, reverse: bool = False) -> Image.Image:
    card = image.convert("RGBA")
    spec = SUITS[suit]
    rank_text, rank_ko = RANK_NAMES[rank]
    color = spec["reverse"] if reverse else spec["color"]
    border = SILVER if reverse else GOLD

    overlay = Image.new("RGBA", CARD_SIZE, (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    draw.rounded_rectangle((7, 7, 478, 750), radius=25, outline=border + (255,), width=5)
    draw.rounded_rectangle((17, 17, 468, 740), radius=20, outline=color + (210,), width=2)

    for x, y, rotate in ((14, 14, False), (384, 620, True)):
        badge = Image.new("RGBA", (88, 124), (0, 0, 0, 0))
        bd = ImageDraw.Draw(badge)
        bd.rounded_rectangle((2, 2, 85, 121), radius=12, fill=(3, 6, 10, 230), outline=border + (255,), width=2)
        centered(bd, (44, 32), rank_text, fit_text(bd, rank_text, 65, 45, FONT_LATIN), IVORY + (255,))
        centered(bd, (44, 84), spec["symbol"], fnt(FONT_SYMBOL, 44), color + (255,), 1, (0, 0, 0, 255))
        if rotate:
            badge = badge.rotate(180)
        overlay.alpha_composite(badge, (x, y))

    draw.rounded_rectangle((40, 677, 446, 741), radius=10, fill=(2, 5, 9, 232), outline=border + (255,), width=2)
    prefix = "리버스 " if reverse else ""
    title = f"{prefix}{spec['ko']} {rank_ko}"
    centered(draw, (243, 707), title, fit_text(draw, title, 372, 28), IVORY + (255,))
    tier = "REVERSE" if reverse else "CHRONO ASCENDED"
    centered(draw, (243, 658), tier, fnt(FONT_LATIN, 16), color + (240,), 1, (0, 0, 0, 220))
    card.alpha_composite(overlay)
    return card.convert("RGB")


def number_card(suit: str, rank: int) -> Image.Image:
    source = fit_cover(GENERATED / CARD_SOURCES[f"{suit}-number"], CARD_SIZE).convert("RGBA")
    veil = Image.new("RGBA", CARD_SIZE, (0, 0, 0, 0))
    draw = ImageDraw.Draw(veil)
    draw.rounded_rectangle((104, 122, 382, 630), radius=116, fill=(2, 5, 9, 142), outline=GOLD + (150,), width=2)
    source.alpha_composite(veil)

    symbol = SUITS[suit]["symbol"]
    color = SUITS[suit]["color"]
    if rank == 1:
        glow_text(source, (243, 386), symbol, fnt(FONT_SYMBOL, 215), color, 16)
    else:
        pip_font = fnt(FONT_SYMBOL, 64 if rank >= 8 else 76)
        for x, y in PIP_LAYOUTS[rank]:
            glow_text(source, (122 + x * 242, 164 + y * 405), symbol, pip_font, color, 7)
    return add_card_labels(source, suit, rank)


def reverse_transform(image: Image.Image, suit: str) -> Image.Image:
    gray = ImageOps.grayscale(image.convert("RGB"))
    if suit in ("H", "D"):
        result = ImageOps.colorize(gray, black=(5, 8, 12), white=(233, 235, 226), mid=(108, 113, 118))
    else:
        result = ImageOps.colorize(gray, black=(3, 8, 12), white=(90, 244, 235), mid=(22, 101, 116))
    result = ImageEnhance.Contrast(result).enhance(1.08)
    return result


def build_card_decks() -> tuple[list[dict], list[dict]]:
    CHRONO_DIR.mkdir(parents=True, exist_ok=True)
    REVERSE_DIR.mkdir(parents=True, exist_ok=True)
    chrono_catalog = []
    reverse_catalog = []
    courts = {suit: crop_triptych(GENERATED / CARD_SOURCES[f"{suit}-court"]) for suit in SUITS}

    for suit, spec in SUITS.items():
        for rank in range(1, 14):
            base = number_card(suit, rank) if rank <= 10 else add_card_labels(courts[suit][rank - 11], suit, rank)
            rank_text, rank_ko = RANK_NAMES[rank]
            filename = f"{suit}-{rank:02d}_{spec['ko']}_{rank_ko}.png"
            base.save(CHRONO_DIR / filename, optimize=True)

            reverse = reverse_transform(base, suit)
            reverse = add_card_labels(reverse, suit, rank, reverse=True)
            reverse.save(REVERSE_DIR / filename.replace(".png", "_리버스.png"), optimize=True)

            chrono_catalog.append({"file": filename, "suit": spec["ko"], "suitCode": suit, "rank": rank_text, "name": f"{spec['ko']} {rank_ko}"})
            reverse_catalog.append({"file": filename.replace(".png", "_리버스.png"), "suit": spec["ko"], "suitCode": suit, "rank": rank_text, "name": f"리버스 {spec['ko']} {rank_ko}", "colorRule": "하트·다이아 무채색 / 스페이드·클럽 청록 발광"})

    chrono_joker = fit_cover(GENERATED / CARD_SOURCES["chrono-joker"], CARD_SIZE)
    chrono_joker = add_special_label(chrono_joker, "JOKER", "시간의 조커", TEAL, GOLD)
    chrono_joker.save(CHRONO_DIR / "X-C_시간의_조커.png", optimize=True)
    chrono_mirror_joker = fit_cover(GENERATED / CARD_SOURCES["reverse-joker"], CARD_SIZE)
    chrono_mirror_joker = add_special_label(chrono_mirror_joker, "JOKER", "거울의 조커", SILVER, GOLD)
    chrono_mirror_joker.save(CHRONO_DIR / "X-M_거울의_조커.png", optimize=True)
    chrono_back = fit_cover(GENERATED / CARD_SOURCES["chrono-back"], CARD_SIZE)
    chrono_back.save(CHRONO_DIR / "Back-C_시간각성_뒷면.png", optimize=True)

    reverse_joker = fit_cover(GENERATED / CARD_SOURCES["reverse-joker"], CARD_SIZE)
    reverse_joker = add_special_label(reverse_joker, "JOKER", "역행의 조커", TEAL, SILVER)
    reverse_joker.save(REVERSE_DIR / "X-RV_역행의_조커.png", optimize=True)
    erased_joker = fit_cover(GENERATED / CARD_SOURCES["chrono-joker"], CARD_SIZE)
    erased_joker = ImageOps.colorize(ImageOps.grayscale(erased_joker), black=(3, 7, 10), white=(102, 238, 235), mid=(25, 92, 106))
    erased_joker = add_special_label(erased_joker, "JOKER", "색채소거 조커", TEAL, SILVER)
    erased_joker.save(REVERSE_DIR / "X-E_색채소거_조커.png", optimize=True)
    reverse_back = fit_cover(GENERATED / CARD_SOURCES["reverse-back"], CARD_SIZE)
    reverse_back.save(REVERSE_DIR / "Back-RV_리버스_뒷면.png", optimize=True)

    chrono_catalog.append({"file": "X-C_시간의_조커.png", "suit": "조커", "rank": "JOKER", "name": "시간의 조커"})
    chrono_catalog.append({"file": "X-M_거울의_조커.png", "suit": "조커", "rank": "JOKER", "name": "거울의 조커"})
    reverse_catalog.append({"file": "X-RV_역행의_조커.png", "suit": "조커", "rank": "JOKER", "name": "역행의 조커"})
    reverse_catalog.append({"file": "X-E_색채소거_조커.png", "suit": "조커", "rank": "JOKER", "name": "색채소거 조커"})
    return chrono_catalog, reverse_catalog


def add_special_label(image: Image.Image, rank: str, title: str, color, border) -> Image.Image:
    card = image.convert("RGBA")
    overlay = Image.new("RGBA", CARD_SIZE, (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    draw.rounded_rectangle((7, 7, 478, 750), radius=25, outline=border + (255,), width=5)
    draw.rounded_rectangle((16, 16, 469, 741), radius=20, outline=color + (225,), width=2)
    draw.rounded_rectangle((22, 22, 128, 76), radius=10, fill=(3, 6, 10, 230), outline=border + (255,), width=2)
    centered(draw, (75, 49), rank, fnt(FONT_LATIN, 22), IVORY + (255,))
    draw.rounded_rectangle((48, 678, 438, 742), radius=10, fill=(2, 5, 9, 234), outline=border + (255,), width=2)
    centered(draw, (243, 708), title, fit_text(draw, title, 350, 31), IVORY + (255,))
    card.alpha_composite(overlay)
    return card.convert("RGB")


def build_awakened_seotda() -> list[dict]:
    SEOTDA_DIR.mkdir(parents=True, exist_ok=True)
    source_root = ROOT / "Assets" / "Resources" / "Cards" / "SignatureSeotda"
    catalog = []
    for index, (boss, card_id, month, title, motif, description, color) in enumerate(SEOTDA_SPECS):
        source = Image.open(source_root / f"{card_id}.png").convert("RGB")
        source = ImageEnhance.Color(source).enhance(1.12)
        source = ImageEnhance.Contrast(source).enhance(1.08).convert("RGBA")

        aura = Image.new("RGBA", CARD_SIZE, (0, 0, 0, 0))
        ad = ImageDraw.Draw(aura)
        for inset, alpha in ((8, 245), (17, 170), (27, 85)):
            ad.rounded_rectangle((inset, inset, 486 - inset, 758 - inset), radius=26, outline=color + (alpha,), width=3)
        for angle in range(0, 360, 45):
            radius = 178 + (index % 3) * 8
            cx, cy = 243, 353
            x = cx + math.cos(math.radians(angle + index * 7)) * radius
            y = cy + math.sin(math.radians(angle + index * 7)) * radius * 1.35
            ad.ellipse((x - 5, y - 5, x + 5, y + 5), fill=color + (220,), outline=GOLD + (255,), width=1)
        blurred = aura.filter(ImageFilter.GaussianBlur(10))
        source.alpha_composite(blurred)
        source.alpha_composite(aura)

        overlay = Image.new("RGBA", CARD_SIZE, (0, 0, 0, 0))
        draw = ImageDraw.Draw(overlay)
        draw.rounded_rectangle((18, 18, 142, 74), radius=11, fill=(3, 6, 10, 232), outline=GOLD + (255,), width=2)
        centered(draw, (80, 46), f"각성 {month}", fit_text(draw, f"각성 {month}", 108, 22), IVORY + (255,))
        draw.rounded_rectangle((344, 18, 468, 74), radius=11, fill=(3, 6, 10, 232), outline=color + (255,), width=2)
        centered(draw, (406, 46), boss, fit_text(draw, boss, 108, 22), IVORY + (255,))

        draw.rounded_rectangle((108, 91, 378, 136), radius=20, fill=(3, 6, 10, 210), outline=color + (255,), width=2)
        centered(draw, (243, 113), motif, fit_text(draw, motif, 238, 23), color + (255,))

        draw.rounded_rectangle((20, 661, 466, 742), radius=12, fill=(2, 5, 9, 255), outline=GOLD + (255,), width=2)
        centered(draw, (243, 686), title, fit_text(draw, title, 410, 27), IVORY + (255,))
        centered(draw, (243, 721), motif, fit_text(draw, motif, 380, 19), color + (255,))
        source.alpha_composite(overlay)

        filename = f"{index + 1:02d}_{boss}_{title}.png"
        source.convert("RGB").save(SEOTDA_DIR / filename, optimize=True)
        catalog.append({"file": filename, "boss": boss, "month": month, "name": title, "motif": motif, "visualConcept": description})
    return catalog


def hex_mask() -> Image.Image:
    mask = Image.new("L", MAP_SIZE, 0)
    ImageDraw.Draw(mask).polygon(HEX_POINTS, fill=255)
    return mask


def normalized_map_source(key: str) -> Image.Image:
    return ImageOps.fit(Image.open(GENERATED / MAP_SOURCES[key]).convert("RGB"), MAP_SIZE, Image.Resampling.LANCZOS)


def make_hex_alpha(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    rgba.putalpha(hex_mask())
    return rgba


def branch_polygon(end: tuple[int, int], half_width: float = 78) -> list[tuple[float, float]]:
    cx, cy = 512, 512
    dx, dy = end[0] - cx, end[1] - cy
    length = math.hypot(dx, dy)
    px, py = -dy / length * half_width, dx / length * half_width
    start_x, start_y = cx - dx / length * 55, cy - dy / length * 55
    end_x, end_y = end[0] + dx / length * 18, end[1] + dy / length * 18
    return [
        (start_x + px, start_y + py),
        (end_x + px, end_y + py),
        (end_x - px, end_y - py),
        (start_x - px, start_y - py),
    ]


def build_road_tile(base: Image.Image, texture: Image.Image, connections: list[str]) -> Image.Image:
    mask = Image.new("L", MAP_SIZE, 0)
    md = ImageDraw.Draw(mask)
    md.ellipse((400, 400, 624, 624), fill=255)
    for direction in connections:
        md.polygon(branch_polygon(EDGE_POINTS[direction]), fill=255)
    mask = Image.composite(mask, Image.new("L", MAP_SIZE, 0), hex_mask())
    out = Image.composite(texture, base, mask).convert("RGBA")
    draw = ImageDraw.Draw(out)
    for direction in connections:
        end = EDGE_POINTS[direction]
        cx, cy = 512, 512
        dx, dy = end[0] - cx, end[1] - cy
        length = math.hypot(dx, dy)
        px, py = -dy / length * 78, dx / length * 78
        for sign in (-1, 1):
            start = (cx + px * sign, cy + py * sign)
            finish = (end[0] + px * sign, end[1] + py * sign)
            draw.line((start, finish), fill=GOLD + (225,), width=5)
            draw.line(((start[0] - px / 12, start[1] - py / 12), (finish[0] - px / 12, finish[1] - py / 12)), fill=(28, 35, 39, 220), width=3)
    draw.ellipse((400, 400, 624, 624), outline=GOLD + (220,), width=5)
    out.putalpha(hex_mask())
    return out


def build_maps() -> list[dict]:
    MAP_DIR.mkdir(parents=True, exist_ok=True)
    catalog = []
    base = normalized_map_source("neutral_ground")
    hub = normalized_map_source("road_hub")
    road_texture = ImageEnhance.Contrast(hub).enhance(1.05)

    for file_id, connections, label in ROAD_PATTERNS:
        tile = build_road_tile(base, road_texture, connections)
        filename = f"{file_id}.png"
        tile.save(MAP_DIR / filename, optimize=True)
        catalog.append({"file": filename, "name": label, "type": "road", "connections": connections})

    specials = [
        ("ground_ruins_neutral", "중립 폐허 바닥", "neutral_ground", "ground", []),
        ("plaza_heart_destroyed", "파괴된 하트 광장", "plaza_heart", "plaza", ["NW", "NE", "E", "SE", "SW", "W"]),
        ("plaza_spade_destroyed", "파괴된 스페이드 광장", "plaza_spade", "plaza", ["NW", "NE", "E", "SE", "SW", "W"]),
        ("plaza_diamond_destroyed", "파괴된 다이아 광장", "plaza_diamond", "plaza", ["NW", "NE", "E", "SE", "SW", "W"]),
        ("plaza_club_destroyed", "파괴된 클럽 광장", "plaza_club", "plaza", ["NW", "NE", "E", "SE", "SW", "W"]),
        ("landmark_shattered_clocktower", "무너진 시계탑", "landmark_clocktower", "landmark", []),
        ("landmark_timekeeper_tea_salon", "시간지기 찻집", "landmark_tea_salon", "landmark", ["NW", "SE"]),
        ("landmark_grand_casino_gate", "대카지노 관문", "landmark_casino_gate", "landmark", ["NW", "SE"]),
        ("hazard_collapsed_time_bridge", "붕괴한 시간다리", "hazard_time_bridge", "hazard", ["E", "W"]),
        ("arena_temporal_crater", "시간 분화구 결전장", "arena_time_crater", "arena", ["NW", "NE", "E", "SE", "SW", "W"]),
        ("road_hub_ai_original", "AI 원화 6방향 허브", "road_hub", "road-source", ["NW", "NE", "E", "SE", "SW", "W"]),
    ]
    for file_id, label, source_key, tile_type, connections in specials:
        tile = make_hex_alpha(normalized_map_source(source_key))
        filename = f"{file_id}.png"
        tile.save(MAP_DIR / filename, optimize=True)
        catalog.append({"file": filename, "name": label, "type": tile_type, "connections": connections})
    return catalog


def preview_grid(entries: list[dict], directory: Path, output: Path, columns: int, thumb: tuple[int, int], title: str, bg=(8, 10, 14)):
    rows = math.ceil(len(entries) / columns)
    cell_w, cell_h = thumb[0] + 26, thumb[1] + 58
    canvas = Image.new("RGB", (columns * cell_w + 32, rows * cell_h + 94), bg)
    draw = ImageDraw.Draw(canvas)
    draw.text((24, 22), title, font=fnt(FONT_KR, 34), fill=IVORY)
    label_font = fnt(FONT_KR_REG, 16)
    for index, entry in enumerate(entries):
        x = 16 + (index % columns) * cell_w + 13
        y = 76 + (index // columns) * cell_h
        image = Image.open(directory / entry["file"]).convert("RGBA")
        image.thumbnail(thumb, Image.Resampling.LANCZOS)
        px = x + (thumb[0] - image.width) // 2
        py = y + (thumb[1] - image.height) // 2
        canvas.paste(image.convert("RGB"), (px, py), image.getchannel("A") if "A" in image.getbands() else None)
        label = entry.get("name", entry["file"])
        centered(draw, (x + thumb[0] / 2, y + thumb[1] + 23), label, fit_text(draw, label, thumb[0], 16, FONT_KR_REG), IVORY)
    canvas.save(output, optimize=True)


def write_catalogs(chrono, reverse, seotda, maps):
    CATALOG_DIR.mkdir(parents=True, exist_ok=True)
    all_data = {
        "version": 1,
        "summary": {"chronoPoker": len(chrono), "reversePoker": len(reverse), "awakenedSeotda": len(seotda), "mapTiles": len(maps)},
        "chronoPoker": chrono,
        "reversePoker": reverse,
        "awakenedSeotda": seotda,
        "mapTiles": maps,
    }
    (CATALOG_DIR / "ui_expansion_catalog.json").write_text(json.dumps(all_data, ensure_ascii=False, indent=2), encoding="utf-8")

    with (CATALOG_DIR / "포커카드_식별표.csv").open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.DictWriter(handle, fieldnames=["deck", "file", "suit", "rank", "name"])
        writer.writeheader()
        for item in chrono:
            writer.writerow({"deck": "시간각성", **{key: item.get(key, "") for key in ("file", "suit", "rank", "name")}})
        for item in reverse:
            writer.writerow({"deck": "리버스", **{key: item.get(key, "") for key in ("file", "suit", "rank", "name")}})

    with (CATALOG_DIR / "각성섯다_설정표.csv").open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.DictWriter(handle, fieldnames=["file", "boss", "month", "name", "motif", "visualConcept"])
        writer.writeheader()
        writer.writerows(seotda)

    with (CATALOG_DIR / "육각맵_연결표.csv").open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.DictWriter(handle, fieldnames=["file", "name", "type", "connections"])
        writer.writeheader()
        for item in maps:
            writer.writerow({**item, "connections": "+".join(item["connections"]) or "없음"})

    readme = """# FFSS UI 확장팩

## 폴더
- 주인공_시간각성포커_54장_뒷면1장: 52장 + 시간·거울 조커 2장 + 뒷면 1장
- 리버스포커_54장_뒷면1장: 52장 + 역행·색채소거 조커 2장 + 뒷면 1장
- 상대전용_각성섯다_17장: 상대 성격과 전투 방식이 보이는 각성 전용패
- 파괴된포커마을_육각맵_29종: 투명 바깥 영역을 가진 1024x1024 육각 타일

## 리버스 색 규칙
- 하트·다이아: 기존 색을 제거한 무채색·상아색 문양
- 스페이드·클럽: 검은 문양 대신 청록 발광 문양

## 맵 연결 코드
- NW, NE, E, SE, SW, W는 육각형 각 변 중앙의 연결 방향이다.
- 파일명과 육각맵_연결표.csv를 같이 보면 조립 방향을 바로 확인할 수 있다.
"""
    (CATALOG_DIR / "README_바로사용.txt").write_text(readme, encoding="utf-8")


def copy_ai_sources():
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    for key, name in {**CARD_SOURCES, **MAP_SOURCES}.items():
        shutil.copy2(GENERATED / name, SOURCE_DIR / f"{key}_source.png")


def zip_directory(path: Path, sources: list[tuple[Path, str]]):
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED, compresslevel=8) as archive:
        for source, prefix in sources:
            for item in sorted(source.rglob("*")):
                if item.is_file() and not item.name.endswith(".meta"):
                    archive.write(item, str(Path(prefix) / item.relative_to(source)))


def package_outputs():
    BUILD.mkdir(parents=True, exist_ok=True)
    ZIP_DIR.mkdir(parents=True, exist_ok=True)
    DOWNLOAD_MIRROR.mkdir(parents=True, exist_ok=True)
    archives = {
        "ffss-chrono-reverse-poker-ui-108.zip": [(CHRONO_DIR, "ChronoPoker"), (REVERSE_DIR, "ReversePoker"), (CATALOG_DIR, "Catalog")],
        "ffss-awakened-seotda-ui-17.zip": [(SEOTDA_DIR, "AwakenedSeotda"), (CATALOG_DIR, "Catalog")],
        "ffss-destroyed-poker-village-hex-29.zip": [(MAP_DIR, "DestroyedPokerVillage"), (CATALOG_DIR, "Catalog")],
        "ffss-ui-card-map-expansion-complete.zip": [(CHRONO_DIR, "ChronoPoker"), (REVERSE_DIR, "ReversePoker"), (SEOTDA_DIR, "AwakenedSeotda"), (MAP_DIR, "DestroyedPokerVillage"), (CATALOG_DIR, "Catalog")],
    }
    for filename, sources in archives.items():
        archive_path = BUILD / filename
        zip_directory(archive_path, sources)
        shutil.copy2(archive_path, ZIP_DIR / filename)
        shutil.copy2(archive_path, DOWNLOAD_MIRROR / filename)
    return archives


def copy_site(archives):
    if SITE.exists():
        shutil.rmtree(SITE)
    SITE.mkdir(parents=True, exist_ok=True)
    previews = {
        "preview_시간각성포커_54.png": "preview_chrono_poker_54.png",
        "preview_리버스포커_54.png": "preview_reverse_poker_54.png",
        "preview_각성섯다_17.png": "preview_awakened_seotda_17.png",
        "preview_파괴된포커마을_29.png": "preview_destroyed_poker_village_29.png",
    }
    for source_name, site_name in previews.items():
        shutil.copy2(CATALOG_DIR / source_name, SITE / site_name)
    shutil.copy2(CATALOG_DIR / "ui_expansion_catalog.json", SITE / "ui_expansion_catalog.json")


def main():
    for directory in (CHRONO_DIR, REVERSE_DIR, SEOTDA_DIR, MAP_DIR, CATALOG_DIR, SOURCE_DIR):
        if directory.exists():
            shutil.rmtree(directory)
        directory.mkdir(parents=True, exist_ok=True)

    chrono, reverse = build_card_decks()
    seotda = build_awakened_seotda()
    maps = build_maps()
    write_catalogs(chrono, reverse, seotda, maps)

    preview_grid(chrono, CHRONO_DIR, CATALOG_DIR / "preview_시간각성포커_54.png", 9, (118, 184), "주인공 시간각성 포커 54장")
    preview_grid(reverse, REVERSE_DIR, CATALOG_DIR / "preview_리버스포커_54.png", 9, (118, 184), "리버스 포커 54장")
    preview_grid(seotda, SEOTDA_DIR, CATALOG_DIR / "preview_각성섯다_17.png", 5, (160, 250), "상대 전용 각성 섯다 17장")
    preview_grid(maps, MAP_DIR, CATALOG_DIR / "preview_파괴된포커마을_29.png", 5, (196, 196), "파괴된 포커 마을 육각맵 29종")
    copy_ai_sources()
    archives = package_outputs()
    copy_site(archives)

    print(json.dumps({
        "chrono": len(chrono),
        "reverse": len(reverse),
        "seotda": len(seotda),
        "maps": len(maps),
        "output": str(OUTPUT),
        "archives": {name: (BUILD / name).stat().st_size for name in archives},
    }, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
