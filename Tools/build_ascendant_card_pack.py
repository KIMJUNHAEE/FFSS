from __future__ import annotations

import csv
import json
import shutil
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps


ROOT = Path(r"C:\git\FFSS")
GENERATED = Path(r"C:\Users\kirby\.codex\generated_images\019fd035-e339-7860-93b2-b1cd104a6ff5")
SOURCE_DIR = ROOT / "Assets" / "Art" / "Cards" / "SourceAI"
SIGNATURE_DIR = ROOT / "Assets" / "Resources" / "Cards" / "SignatureSeotda"
POKER_DIR = ROOT / "Assets" / "Resources" / "Cards" / "AscendantPoker"
PREVIEW_DIR = ROOT / "Assets" / "Art" / "Cards"

CARD_SIZE = (486, 758)
GOLD = (226, 181, 86)
BLACK = (9, 11, 16)
IVORY = (239, 232, 213)
FONT_KR = Path(r"C:\Windows\Fonts\malgunbd.ttf")
FONT_SYMBOL = Path(r"C:\Windows\Fonts\seguisym.ttf")
FONT_LATIN = Path(r"C:\Windows\Fonts\georgiab.ttf")


SIGNATURE_CARDS = [
    ("1땡", "sig_01_ddang", "솔잎 관통패", 1, False, "exec-35eba211-3d16-417b-aa84-3a9405148b6a.png", "1월과 만나면 위력 +1, 얇은 게이지 +2"),
    ("2땡", "sig_02_ddang", "설매 반격패", 2, False, "exec-feaa9359-47bc-4410-b4f5-8427e7fa941b.png", "2월과 만나면 위력 +2, 얇은 게이지 +1"),
    ("3땡", "sig_03_ddang", "삼연화 낙화패", 3, True, "exec-c5aba51f-b0a0-49b8-aeb6-44f7f1c4488c.png", "3월과 만나면 위력 +2, HP 추가 1, 얇은 게이지 +1"),
    ("4땡", "sig_04_ddang", "흑싸리 사신패", 4, False, "exec-f4eca874-cd09-41ce-b5d8-751bc6bea34f.png", "4월과 만나면 위력 +1, HP 추가 1, 얇은 게이지 +3"),
    ("5땡", "sig_05_ddang", "창포 수경패", 5, False, "exec-7e1f12b5-9494-4da3-abd1-5adf9037a37f.png", "5월과 만나면 위력 +2, 얇은 게이지 +2"),
    ("6땡", "sig_06_ddang", "육화 독무패", 6, False, "exec-ab4b0d72-5e79-459e-b803-b27937b7cead.png", "6월과 만나면 위력 +2, HP 추가 2"),
    ("7땡", "sig_07_ddang", "홍싸리 철퇴패", 7, False, "exec-27f42ed2-2132-4c6d-9839-b74fbf881637.png", "7월과 만나면 위력 +3, 얇은 게이지 +2"),
    ("8땡", "sig_08_ddang", "공산 봉인패", 8, True, "exec-25e86118-58b1-4546-b423-1daa6e408e58.png", "8월과 만나면 위력 +1, 얇은 게이지 +4"),
    ("9땡", "sig_09_ddang", "국화 취월패", 9, False, "exec-2cc7c116-b35f-4cad-9cf9-3858a3013175.png", "9월과 만나면 위력 +2, HP 추가 2, 얇은 게이지 +1"),
    ("10땡", "sig_10_ddang", "단풍 십연패", 10, False, "exec-a18dc026-56a0-44b6-ba73-665ddee05047.png", "10월과 만나면 족보 +2, 위력 +3, HP 추가 2, 얇은 게이지 +2"),
    ("13", "sig_13_gwang", "일삼 멸광패", 3, True, "exec-994a92ed-c277-4234-b6dc-207fca8840b0.png", "1월과 만나 일삼광땡이면 위력 +3, HP 추가 2"),
    ("18", "sig_18_gwang", "일팔 금륜패", 8, True, "exec-1816729b-1408-462f-998d-4b66a8b3ecf6.png", "1월과 만나 일팔광땡이면 위력 +3, 얇은 게이지 +3"),
    ("38", "sig_38_gwang", "삼팔 광천패", 8, True, "exec-a86086ab-254a-4498-8025-edc40f087719.png", "3월과 만나 삼팔광땡이면 위력 +4, HP 추가 3, 얇은 게이지 +3"),
    ("구사", "sig_94_reset", "구사 판뒤집기패", 4, False, "exec-514cf947-353c-485b-80bd-62f80c8b89f1.png", "9월과 만나 구사가 되면 족보 +2, 위력 +2, 얇은 게이지 +4"),
    ("땡잡이", "sig_37_hunter", "청쇄 땡사냥패", 3, False, "exec-5cd2e10a-7799-4e8b-88c5-b9af56f51b7e.png", "7월과 만나 땡잡이가 되면 위력 +3, HP 추가 1, 얇은 게이지 +3"),
    ("멍구사", "sig_49_assassin", "멍구사 절명패", 9, False, "exec-a99642fd-a025-476e-b1fb-e811c4b93da8.png", "4월과 만나 멍구사가 되면 위력 +4, HP 추가 2"),
    ("암행어사", "sig_47_inspector", "마패 암행출두", 4, False, "exec-02241366-ef02-4ff6-bffa-32c000555bd4.png", "7월과 만나 암행어사가 되면 위력 +3, HP 추가 2, 얇은 게이지 +3"),
]

POKER_SOURCES = {
    "X-R": "exec-b6fc3c75-ad86-4d99-9da3-24770bb787b1.png",
    "X-B": "exec-64a57ef8-55eb-4e03-b75a-be71aeb5ba89.png",
    "Back-A": "exec-d9b75423-88bd-419b-a48e-8c4e00146102.png",
    "S-court": "exec-b12adffe-3b09-4d85-8b74-05acce8d1eec.png",
    "C-court": "exec-2790d9d6-3c8c-4a8e-91a9-ba578a0e1383.png",
    "H-court": "exec-d1c90c2c-874c-4332-9a60-2f9a8076bfc5.png",
    "D-court": "exec-5ba2e3b1-d3f3-4bf5-874d-e3d35949c908.png",
}

SUITS = {
    "S": ("스페이드", "♠", (214, 224, 235), (28, 34, 44)),
    "C": ("클로버", "♣", (105, 179, 128), (20, 48, 34)),
    "H": ("하트", "♥", (232, 77, 96), (66, 20, 29)),
    "D": ("다이아", "♦", (235, 177, 70), (64, 42, 18)),
}

PIP_LAYOUTS = {
    2: [(0.5, 0.25), (0.5, 0.75)],
    3: [(0.5, 0.22), (0.5, 0.5), (0.5, 0.78)],
    4: [(0.32, 0.28), (0.68, 0.28), (0.32, 0.72), (0.68, 0.72)],
    5: [(0.32, 0.25), (0.68, 0.25), (0.5, 0.5), (0.32, 0.75), (0.68, 0.75)],
    6: [(0.32, 0.22), (0.68, 0.22), (0.32, 0.5), (0.68, 0.5), (0.32, 0.78), (0.68, 0.78)],
    7: [(0.32, 0.19), (0.68, 0.19), (0.5, 0.36), (0.32, 0.5), (0.68, 0.5), (0.32, 0.81), (0.68, 0.81)],
    8: [(0.32, 0.17), (0.68, 0.17), (0.5, 0.34), (0.32, 0.43), (0.68, 0.43), (0.5, 0.66), (0.32, 0.83), (0.68, 0.83)],
    9: [(0.32, 0.16), (0.68, 0.16), (0.32, 0.36), (0.68, 0.36), (0.5, 0.5), (0.32, 0.64), (0.68, 0.64), (0.32, 0.84), (0.68, 0.84)],
    10: [(0.32, 0.12), (0.68, 0.12), (0.32, 0.31), (0.68, 0.31), (0.32, 0.5), (0.68, 0.5), (0.32, 0.69), (0.68, 0.69), (0.32, 0.88), (0.68, 0.88)],
}


def font(path: Path, size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(path), size)


def fit_text(draw: ImageDraw.ImageDraw, text: str, max_width: int, initial: int, path: Path = FONT_KR):
    size = initial
    while size > 14:
        selected = font(path, size)
        if draw.textbbox((0, 0), text, font=selected)[2] <= max_width:
            return selected
        size -= 1
    return font(path, size)


def fit_cover(image: Image.Image, size=CARD_SIZE) -> Image.Image:
    return ImageOps.fit(image.convert("RGB"), size, Image.Resampling.LANCZOS, centering=(0.5, 0.5))


def copy_sources() -> None:
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    for _, card_id, _, _, _, source, _ in SIGNATURE_CARDS:
        shutil.copy2(GENERATED / source, SOURCE_DIR / f"{card_id}_source.png")
    for name, source in POKER_SOURCES.items():
        shutil.copy2(GENERATED / source, SOURCE_DIR / f"{name}_source.png")


def overlay_corner_rank(card: Image.Image, rank: str, symbol: str, color: tuple[int, int, int]) -> None:
    def corner() -> Image.Image:
        layer = Image.new("RGBA", (88, 124), (0, 0, 0, 0))
        draw = ImageDraw.Draw(layer)
        draw.rounded_rectangle((3, 3, 84, 120), radius=12, fill=(4, 6, 10, 220), outline=GOLD + (255,), width=2)
        rank_font = fit_text(draw, rank, 68, 48, FONT_LATIN)
        box = draw.textbbox((0, 0), rank, font=rank_font)
        draw.text(((88 - (box[2] - box[0])) / 2, 5), rank, font=rank_font, fill=IVORY + (255,))
        suit_font = font(FONT_SYMBOL, 45)
        box = draw.textbbox((0, 0), symbol, font=suit_font)
        draw.text(((88 - (box[2] - box[0])) / 2, 61), symbol, font=suit_font, fill=color + (255,))
        return layer

    marker = corner()
    card.paste(marker, (15, 14), marker)
    rotated = marker.rotate(180)
    card.paste(rotated, (CARD_SIZE[0] - 103, CARD_SIZE[1] - 138), rotated)


def add_signature_labels(image: Image.Image, card_id: str, title: str, month: int, is_gwang: bool) -> Image.Image:
    card = fit_cover(image).convert("RGBA")
    overlay = Image.new("RGBA", CARD_SIZE, (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    draw.rounded_rectangle((18, 18, 122, 105), radius=16, fill=(3, 5, 9, 222), outline=GOLD + (255,), width=3)
    month_text = f"{month}월"
    month_font = fit_text(draw, month_text, 90, 37)
    box = draw.textbbox((0, 0), month_text, font=month_font)
    draw.text((70 - (box[2] - box[0]) / 2, 26), month_text, font=month_font, fill=IVORY + (255,))
    if is_gwang:
        draw.ellipse((93, 76, 129, 112), fill=(150, 24, 28, 245), outline=GOLD + (255,), width=2)
        gwang_font = font(FONT_KR, 20)
        draw.text((101, 79), "광", font=gwang_font, fill=(255, 231, 158, 255))

    draw.rounded_rectangle((25, 682, 461, 742), radius=9, fill=(3, 5, 9, 225), outline=GOLD + (255,), width=2)
    title_font = fit_text(draw, title, 402, 29)
    box = draw.textbbox((0, 0), title, font=title_font)
    draw.text(((486 - (box[2] - box[0])) / 2, 693), title, font=title_font, fill=(255, 235, 183, 255))
    card.alpha_composite(overlay)
    return card.convert("RGB")


def build_signature_cards() -> list[dict]:
    SIGNATURE_DIR.mkdir(parents=True, exist_ok=True)
    catalog = []
    for boss_id, card_id, title, month, is_gwang, source, effect in SIGNATURE_CARDS:
        image = Image.open(GENERATED / source)
        final = add_signature_labels(image, card_id, title, month, is_gwang)
        final.save(SIGNATURE_DIR / f"{card_id}.png", optimize=True)
        catalog.append({
            "bossId": boss_id,
            "cardId": card_id,
            "displayName": title,
            "month": month,
            "isGwang": is_gwang,
            "effect": effect,
            "resourcePath": f"Cards/SignatureSeotda/{card_id}",
        })
    (ROOT / "Assets" / "Resources" / "Cards" / "signature_seotda_catalog.json").write_text(
        json.dumps({"version": 1, "cards": catalog}, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    return catalog


def blank_number_card(suit: str) -> Image.Image:
    _, _, color, dark = SUITS[suit]
    card = Image.new("RGB", CARD_SIZE, BLACK)
    draw = ImageDraw.Draw(card)
    draw.rounded_rectangle((7, 7, 478, 750), radius=26, fill=BLACK, outline=GOLD, width=5)
    draw.rounded_rectangle((18, 18, 467, 739), radius=21, fill=dark, outline=(108, 83, 42), width=2)
    draw.rounded_rectangle((48, 55, 438, 703), radius=18, fill=(18, 21, 27), outline=color, width=2)
    for inset, alpha in ((0, 115), (12, 80), (24, 45)):
        draw.arc((72 + inset, 95 + inset, 414 - inset, 675 - inset), 195, 345, fill=color, width=1)
        draw.arc((72 + inset, 95 + inset, 414 - inset, 675 - inset), 15, 165, fill=GOLD, width=1)
    for y in range(92, 700, 42):
        draw.line((58, y, 428, y - 18), fill=(color[0] // 5, color[1] // 5, color[2] // 5), width=1)
    return card


def draw_centered(draw: ImageDraw.ImageDraw, position: tuple[float, float], text: str,
                  selected_font: ImageFont.FreeTypeFont, fill) -> None:
    box = draw.textbbox((0, 0), text, font=selected_font)
    draw.text((position[0] - (box[2] - box[0]) / 2, position[1] - (box[3] - box[1]) / 2 - box[1]),
              text, font=selected_font, fill=fill)


def build_number_card(suit: str, rank: int) -> Image.Image:
    _, symbol, color, _ = SUITS[suit]
    card = blank_number_card(suit)
    draw = ImageDraw.Draw(card)
    rank_text = "A" if rank == 1 else str(rank)
    overlay_corner_rank(card, rank_text, symbol, color)
    draw = ImageDraw.Draw(card)

    if rank == 1:
        suit_font = font(FONT_SYMBOL, 220)
        draw_centered(draw, (243, 385), symbol, suit_font, color)
        draw.ellipse((154, 290, 332, 468), outline=GOLD, width=3)
        draw.ellipse((168, 304, 318, 454), outline=(108, 83, 42), width=1)
    else:
        pip_size = 92 if rank <= 6 else 74
        pip_font = font(FONT_SYMBOL, pip_size)
        for x, y in PIP_LAYOUTS[rank]:
            draw_centered(draw, (84 + x * 318, 102 + y * 545), symbol, pip_font, color)
    return card


def crop_court_panels(source: Path) -> list[Image.Image]:
    image = Image.open(source).convert("RGB")
    width, height = image.size
    margin = max(8, width // 100)
    panel_width = width / 3
    panels = []
    for index in range(3):
        left = int(index * panel_width + margin)
        right = int((index + 1) * panel_width - margin)
        panels.append(image.crop((left, margin, right, height - margin)))
    return panels


def build_court_card(suit: str, rank: int, portrait: Image.Image) -> Image.Image:
    _, symbol, color, _ = SUITS[suit]
    card = fit_cover(portrait).convert("RGBA")
    shade = Image.new("RGBA", CARD_SIZE, (0, 0, 0, 0))
    draw = ImageDraw.Draw(shade)
    draw.rectangle((0, 0, 486, 155), fill=(3, 5, 9, 105))
    draw.rectangle((0, 604, 486, 758), fill=(3, 5, 9, 125))
    draw.rounded_rectangle((7, 7, 478, 750), radius=26, outline=GOLD + (255,), width=6)
    draw.rounded_rectangle((17, 17, 468, 740), radius=21, outline=color + (255,), width=2)
    card.alpha_composite(shade)
    overlay_corner_rank(card, {11: "J", 12: "Q", 13: "K"}[rank], symbol, color)
    return card.convert("RGB")


def build_joker(source: Path, red: bool) -> Image.Image:
    card = fit_cover(Image.open(source)).convert("RGBA")
    color = (236, 73, 70) if red else (215, 224, 238)
    overlay = Image.new("RGBA", CARD_SIZE, (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    draw.rounded_rectangle((7, 7, 478, 750), radius=26, outline=GOLD + (255,), width=5)
    label_font = font(FONT_LATIN, 31)
    draw.rounded_rectangle((14, 14, 103, 205), radius=14, fill=(3, 5, 9, 218), outline=color + (255,), width=2)
    letters = "JOKER"
    for index, letter in enumerate(letters):
        draw.text((45, 24 + index * 33), letter, font=label_font, fill=color + (255,))
    marker = overlay.crop((14, 14, 104, 206)).rotate(180)
    overlay.paste(marker, (382, 552), marker)
    card.alpha_composite(overlay)
    return card.convert("RGB")


def build_poker_deck() -> list[dict]:
    POKER_DIR.mkdir(parents=True, exist_ok=True)
    catalog = []
    court_panels = {
        suit: crop_court_panels(GENERATED / POKER_SOURCES[f"{suit}-court"])
        for suit in SUITS
    }

    for suit, (suit_name, _, _, _) in SUITS.items():
        for rank in range(1, 14):
            if rank <= 10:
                card = build_number_card(suit, rank)
            else:
                card = build_court_card(suit, rank, court_panels[suit][rank - 11])
            file_name = f"{suit}-{rank}"
            card.save(POKER_DIR / f"{file_name}.png", optimize=True)
            if rank == 1:
                tier, effect = "왕권", "에이스 1장마다 공격·방어 +2"
            elif rank >= 11:
                tier, effect = "궁정", "J·Q·K 1장마다 스킬 +1"
            elif rank >= 6:
                tier, effect = "정련", "족보와 높은 카드 보정에 사용"
            else:
                tier, effect = "각성", "족보 구성과 무늬 약점 계산에 사용"
            catalog.append({
                "cardId": file_name,
                "suit": suit_name,
                "rank": rank,
                "tier": tier,
                "effect": effect,
                "resourcePath": f"Cards/AscendantPoker/{file_name}",
            })

    red_joker = build_joker(GENERATED / POKER_SOURCES["X-R"], True)
    black_joker = build_joker(GENERATED / POKER_SOURCES["X-B"], False)
    red_joker.save(POKER_DIR / "X-R.png", optimize=True)
    black_joker.save(POKER_DIR / "X-B.png", optimize=True)
    catalog.extend([
        {"cardId": "X-R", "suit": "적 조커", "rank": 0, "tier": "와일드", "effect": "최적의 카드로 대체, 공격 +3, 스킬 +2", "resourcePath": "Cards/AscendantPoker/X-R"},
        {"cardId": "X-B", "suit": "흑 조커", "rank": 0, "tier": "와일드", "effect": "최적의 카드로 대체, 방어 +3, 스킬 +2", "resourcePath": "Cards/AscendantPoker/X-B"},
    ])

    back = fit_cover(Image.open(GENERATED / POKER_SOURCES["Back-A"]))
    back.save(POKER_DIR / "Back-A.png", optimize=True)
    (ROOT / "Assets" / "Resources" / "Cards" / "ascendant_poker_catalog.json").write_text(
        json.dumps({"version": 1, "cards": catalog, "back": "Cards/AscendantPoker/Back-A"}, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    return catalog


def contact_sheet(paths: list[Path], labels: list[str], output: Path, columns: int, thumb=(194, 303)) -> None:
    rows = (len(paths) + columns - 1) // columns
    cell_w, cell_h = thumb[0] + 24, thumb[1] + 64
    canvas = Image.new("RGB", (columns * cell_w + 24, rows * cell_h + 24), (17, 18, 23))
    draw = ImageDraw.Draw(canvas)
    label_font = font(FONT_KR, 20)
    for index, (path, label) in enumerate(zip(paths, labels)):
        image = Image.open(path).convert("RGB")
        image.thumbnail(thumb, Image.Resampling.LANCZOS)
        x = 24 + (index % columns) * cell_w + (thumb[0] - image.width) // 2
        y = 18 + (index // columns) * cell_h
        canvas.paste(image, (x, y))
        selected = fit_text(draw, label, thumb[0], 20)
        box = draw.textbbox((0, 0), label, font=selected)
        draw.text((x + (image.width - (box[2] - box[0])) / 2, y + thumb[1] + 8), label, font=selected, fill=(245, 226, 170))
    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output, optimize=True)


def write_docs(signature_catalog: list[dict], poker_catalog: list[dict]) -> None:
    with (PREVIEW_DIR / "signature_seotda_catalog.csv").open("w", newline="", encoding="utf-8-sig") as file:
        writer = csv.DictWriter(file, fieldnames=["bossId", "cardId", "displayName", "month", "isGwang", "effect", "resourcePath"])
        writer.writeheader()
        writer.writerows(signature_catalog)
    with (PREVIEW_DIR / "ascendant_poker_catalog.csv").open("w", newline="", encoding="utf-8-sig") as file:
        writer = csv.DictWriter(file, fieldnames=["cardId", "suit", "rank", "tier", "effect", "resourcePath"])
        writer.writeheader()
        writer.writerows(poker_catalog)

    readme = """# 상위 포커·상대 전용 섯다 카드 팩

- `Assets/Resources/Cards/AscendantPoker`: 상위 포커 52장, 적/흑 조커 2장, 뒷면 1장
- `Assets/Resources/Cards/SignatureSeotda`: 상대 17명 전용 섯다 카드
- `ascendant_poker_catalog.csv`: 포커 카드별 등급과 효과
- `signature_seotda_catalog.csv`: 상대별 월, 광 여부, 발동 효과
- 포커 조커는 `PokerHandEvaluator`가 가능한 52장 대체 조합을 전부 비교해 최선의 족보를 선택한다.
- 상대 전용 섯다 카드는 `OpponentSeotdaCardCatalog`의 조건과 `RpsCombatController`의 전투 보너스로 연결된다.
"""
    (PREVIEW_DIR / "README_CARDS_KO.md").write_text(readme, encoding="utf-8")


def main() -> None:
    copy_sources()
    signature_catalog = build_signature_cards()
    poker_catalog = build_poker_deck()
    contact_sheet(
        [SIGNATURE_DIR / f"{card_id}.png" for _, card_id, *_ in SIGNATURE_CARDS],
        [f"{boss_id} · {title}" for boss_id, _, title, *_ in SIGNATURE_CARDS],
        PREVIEW_DIR / "preview_signature_seotda_17.png",
        columns=6,
    )
    poker_paths = [POKER_DIR / f"{entry['cardId']}.png" for entry in poker_catalog]
    contact_sheet(poker_paths, [entry["cardId"] for entry in poker_catalog],
                  PREVIEW_DIR / "preview_ascendant_poker_54.png", columns=9, thumb=(146, 228))
    write_docs(signature_catalog, poker_catalog)
    print(f"signature={len(signature_catalog)} poker={len(poker_catalog)} back=1")


if __name__ == "__main__":
    main()
