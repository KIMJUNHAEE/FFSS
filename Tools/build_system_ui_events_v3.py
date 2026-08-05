from __future__ import annotations

import csv
import json
import shutil
import zipfile
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont


DESKTOP = Path(r"C:\Users\kirby\OneDrive\바탕 화면\구구가가")
UI_ROOT = DESKTOP / "웹게임_UI_신규"
OUTPUT = UI_ROOT / "즉시사용_v3_시스템전체UI_이벤트18종"

GENERATED = Path(r"C:\Users\kirby\.codex\generated_images\019fd035-e339-7860-93b2-b1cd104a6ff5")
START_SOURCE = GENERATED / "exec-485838e5-ec4a-4a48-ab66-75b999081442.png"
ATLAS_SOURCES = {
    1: GENERATED / "exec-40947604-90a5-49cf-bb7c-a11857fa4b85.png",
    2: GENERATED / "exec-e9ab3fc8-1da5-40c8-a0dc-af6491500459.png",
    3: GENERATED / "exec-ff4b5797-19f1-4a1d-9ce0-b1ad001a9249.png",
    4: GENERATED / "exec-c8e473bd-e901-449d-b034-b537eee0f174.png",
}
EVENT_SOURCES = {
    1: GENERATED / "exec-16dbb83e-2a16-46f8-86aa-3d97a0788219.png",
    2: GENERATED / "exec-73bdccf7-11ee-4294-b38f-e30a68d53cf3.png",
    3: GENERATED / "exec-0ed2dd2d-23e3-4e17-85ce-b49a2c82dc21.png",
    4: GENERATED / "exec-b74b13dd-fdbc-48a3-b2a6-cef7dd7ac1e9.png",
    5: GENERATED / "exec-24e869d1-1931-4072-9370-0d1a56ee4fb7.png",
    6: GENERATED / "exec-fa9b5dd8-15f1-400e-8b54-4b518dc59e85.png",
}

GROUND_ROOT = (
    UI_ROOT
    / "즉시사용_v2_독립지면_캐릭터전용섯다덱_최종본"
    / "파괴된포커마을_독립보행지면_29종"
)

DIR_EXISTING = OUTPUT / "00_기존UI_25종_동봉"
DIR_SCREENS = OUTPUT / "01_시스템디자인_전체화면_UI_17종"
DIR_COMPONENTS = OUTPUT / "02_잘라쓰기_UI패널_48종"
DIR_PROPS = OUTPUT / "03_이벤트_건물오브젝트_누끼_18종"
DIR_TILES = OUTPUT / "04_이벤트_건물육각타일_18종"
DIR_SOURCES = OUTPUT / "05_AI원화_시트"
DIR_PREVIEWS = OUTPUT / "06_미리보기_카탈로그"
DIR_ZIPS = OUTPUT / "07_ZIP_묶음"

FONT_REGULAR = Path(r"C:\Windows\Fonts\malgun.ttf")
FONT_BOLD = Path(r"C:\Windows\Fonts\malgunbd.ttf")

GOLD = (236, 192, 99, 255)
IVORY = (248, 239, 214, 255)
RED = (225, 83, 70, 255)
MINT = (105, 225, 213, 255)
BLUE = (104, 173, 255, 255)


@dataclass(frozen=True)
class Label:
    text: str
    x: int
    y: int
    size: int = 32
    color: tuple[int, int, int, int] = IVORY
    anchor: str = "mm"


@dataclass(frozen=True)
class ScreenSpec:
    file_name: str
    title: str
    atlas: int
    quadrant: int
    labels: tuple[Label, ...]


SCREENS = [
    ScreenSpec("26_불러오기_세이브슬롯.png", "불러오기", 1, 0, (
        Label("슬롯 1", 320, 170, 34, GOLD), Label("1막 · 북문 장터", 320, 215, 23),
        Label("슬롯 2", 836, 170, 34, (180, 180, 180, 255)), Label("비어 있음", 836, 215, 23, (170, 170, 170, 255)),
        Label("슬롯 3", 1350, 170, 34, RED), Label("3막 · 폐궁", 1350, 215, 23),
    )),
    ScreenSpec("27_필드상점_거래.png", "떠돌이 상점", 1, 1, (
        Label("구매", 1348, 822, 31, IVORY), Label("판매", 1530, 822, 31, IVORY),
        Label("보유 128 엽전", 1440, 715, 23, GOLD),
    )),
    ScreenSpec("28_휴식캠프_회복강화안정도.png", "휴식 캠프", 1, 2, (
        Label("HP +22 회복", 275, 827, 29), Label("카드 1장 강화", 836, 827, 29), Label("안정도 +18", 1390, 827, 29),
    )),
    ScreenSpec("29_필드이벤트_무너진장터수레.png", "무너진 장터 수레", 1, 3, (
        Label("HP 5 · 수레를 밀고 상자를 연다", 285, 822, 24),
        Label("20엽전 · 안전하게 치운다", 836, 822, 24),
        Label("그냥 지나간다", 1385, 822, 24),
    )),
    ScreenSpec("30_보스문_정보예고.png", "38광땡 · 보스 정보 예고", 2, 0, (
        Label("최근 5전투 행동 비율", 310, 215, 25, GOLD), Label("시작 광열", 235, 680, 24, RED),
        Label("규칙 교체 · 40엽전", 530, 858, 28), Label("입장", 1420, 858, 36, IVORY),
    )),
    ScreenSpec("31_장비보상_격파전리품.png", "격파 전리품 · 1개 선택", 2, 1, (
        Label("적월 단검", 580, 275, 28), Label("시간 추", 930, 275, 28), Label("광패 등불", 1280, 275, 28),
        Label("선택 확정", 836, 865, 30, GOLD),
    )),
    ScreenSpec("32_적기술각인_보상.png", "적 기술 각인", 2, 2, (
        Label("균열 찌르기", 280, 540, 28), Label("시간 봉쇄", 1390, 540, 28), Label("각인 확정", 836, 855, 31, GOLD),
    )),
    ScreenSpec("33_전투조건_선택.png", "전투 조건 선택", 2, 3, (
        Label("검은 패 방어", 410, 210, 27), Label("손패 6장 시작", 820, 210, 27), Label("첫 필살 강화", 1230, 210, 27),
        Label("현재 보유 조건", 1490, 220, 22, GOLD), Label("선택 확정", 1455, 835, 29),
    )),
    ScreenSpec("34_막전환_다음막준비.png", "2막 준비", 3, 0, (
        Label("HP 25% 회복", 836, 280, 29, MINT), Label("카드 제거", 310, 510, 28), Label("카드 강화", 690, 510, 28),
        Label("광땡 전리품 · 1개 선택", 836, 675, 28, GOLD),
    )),
    ScreenSpec("35_일시정지_런상태.png", "런 상태", 3, 1, (
        Label("계속", 1450, 530, 28), Label("옵션", 1450, 635, 28), Label("런 포기", 1450, 740, 28, RED), Label("시작화면", 1450, 845, 28),
        Label("자동 저장 완료", 1395, 400, 22, MINT),
    )),
    ScreenSpec("36_사망정산.png", "런 종료", 3, 2, (
        Label("최근 5전투 행동 비율", 1190, 180, 24, GOLD), Label("가장 많이 맞은 기믹", 345, 525, 24),
        Label("획득 기록", 370, 790, 24), Label("새 런 시작", 1320, 850, 32, IVORY),
    )),
    ScreenSpec("37_클리어정산.png", "클리어", 3, 3, (
        Label("다음 런 계승 선택", 1410, 580, 25, GOLD), Label("새 게임", 1430, 790, 29), Label("시작화면", 1430, 875, 27),
    )),
    ScreenSpec("38_카드강화_제거.png", "카드 정비", 4, 0, (
        Label("강화 전", 720, 180, 25), Label("강화 후", 1180, 180, 25, GOLD), Label("강화", 345, 845, 31, BLUE), Label("제거", 1165, 845, 31, RED),
    )),
    ScreenSpec("39_장비상세_판매.png", "장비 상세", 4, 1, (
        Label("잠금", 875, 850, 26), Label("판매", 1085, 850, 26, GOLD), Label("장착", 1300, 850, 26, BLUE), Label("분해", 1510, 850, 26),
    )),
    ScreenSpec("40_런안정도_상세.png", "런 안정도", 4, 2, (
        Label("안정", 245, 270, 22), Label("흔들림", 590, 270, 22), Label("위험", 945, 270, 22), Label("붕괴", 1300, 270, 22, RED),
        Label("전투 조건", 760, 430, 23, GOLD), Label("장비 과열", 1280, 430, 23, RED), Label("기술 기억", 1280, 650, 23),
    )),
    ScreenSpec("41_런기록_도감.png", "런 기록", 4, 3, (
        Label("최근 20런", 835, 130, 25, GOLD), Label("보스 기록", 320, 410, 24), Label("격파 기록", 1320, 410, 24),
    )),
]


EVENTS = [
    (1, "무너진 장터 수레", "1막", "ground_market_01.png"),
    (2, "잠긴 약방", "1막", "ground_market_02.png"),
    (3, "땡잡이의 발자국", "1막", "ground_market_03.png"),
    (4, "부서진 시계탑 종", "1막", "ground_burned_01.png"),
    (5, "피난민의 부탁", "1막", "ground_dark_stone_01.png"),
    (6, "독 물길 밸브", "2막", "ground_rain_01.png"),
    (7, "젖은 장부", "2막", "ground_rain_02.png"),
    (8, "대장간 화로", "2막", "ground_burned_02.png"),
    (9, "숨은 다리", "2막", "ground_rain_03.png"),
    (10, "멍구사 미끼패", "2막", "ground_club_moss_02.png"),
    (11, "사당 등불", "3막", "ground_seal_shrine_01.png"),
    (12, "관아 검문", "3막", "ground_seal_shrine_02.png"),
    (13, "주막 취객", "3막", "ground_dark_stone_03.png"),
    (14, "광패 균열", "3막", "ground_time_crack_02.png"),
    (15, "마지막 보급 마차", "3막", "ground_time_crack_01.png"),
    (16, "시계공의 찻집", "공통", "ground_mixed_teal_01.png"),
    (17, "무너진 시간다리", "공통", "ground_time_crack_03.png"),
    (18, "카지노 정문", "공통", "ground_mixed_gold_01.png"),
]


def font(size: int, bold: bool = True) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(FONT_BOLD if bold else FONT_REGULAR), size)


def ensure_dirs() -> None:
    for path in (
        DIR_EXISTING,
        DIR_SCREENS,
        DIR_COMPONENTS,
        DIR_PROPS,
        DIR_TILES,
        DIR_SOURCES,
        DIR_PREVIEWS,
        DIR_ZIPS,
    ):
        path.mkdir(parents=True, exist_ok=True)


def draw_text(draw: ImageDraw.ImageDraw, label: Label) -> None:
    fnt = font(label.size)
    shadow = (0, 0, 0, 230)
    draw.text((label.x + 2, label.y + 3), label.text, font=fnt, fill=shadow, anchor=label.anchor, stroke_width=2, stroke_fill=shadow)
    draw.text((label.x, label.y), label.text, font=fnt, fill=label.color, anchor=label.anchor, stroke_width=1, stroke_fill=(52, 33, 16, 255))


def split_quadrant(sheet: Image.Image, quadrant: int) -> Image.Image:
    width, height = sheet.size
    half_w, half_h = width // 2, height // 2
    margin = max(3, width // 512)
    boxes = (
        (margin, margin, half_w - margin, half_h - margin),
        (half_w + margin, margin, width - margin, half_h - margin),
        (margin, half_h + margin, half_w - margin, height - margin),
        (half_w + margin, half_h + margin, width - margin, height - margin),
    )
    return sheet.crop(boxes[quadrant]).resize((1672, 941), Image.Resampling.LANCZOS)


def build_start_screen() -> Path:
    image = Image.open(START_SOURCE).convert("RGBA")
    width, height = image.size
    target_ratio = 16 / 9
    crop_h = int(width / target_ratio)
    top = max(0, min(height - crop_h, (height - crop_h) // 2))
    image = image.crop((0, top, width, top + crop_h)).resize((1920, 1080), Image.Resampling.LANCZOS)
    draw = ImageDraw.Draw(image)
    draw_text(draw, Label("POKER × SEOTDA", 425, 122, 25, GOLD))
    draw_text(draw, Label("포커포커", 425, 195, 62, IVORY))
    draw_text(draw, Label("섯다섯다", 425, 270, 52, RED))
    draw_text(draw, Label("이어하기", 360, 515, 39, IVORY))
    draw_text(draw, Label("새 게임", 360, 662, 39, IVORY))
    draw_text(draw, Label("슬롯 1 · 1막 북문 장터", 1730, 330, 22, IVORY))
    draw_text(draw, Label("슬롯 2 · 비어 있음", 1730, 500, 22, (190, 190, 190, 255)))
    draw_text(draw, Label("슬롯 3 · 기록", 1730, 670, 22, IVORY))
    draw_text(draw, Label("HP 74 / 90", 260, 845, 22, MINT))
    draw_text(draw, Label("덱 16", 520, 850, 20, GOLD))
    draw_text(draw, Label("v0.3.0", 540, 315, 18, (180, 165, 135, 255)))
    path = DIR_SCREENS / "01_시작화면_v3_주인공과파괴된마을.png"
    image.convert("RGB").save(path, quality=95)
    image.convert("RGB").save(UI_ROOT / "01_웹_타이틀화면_v3.png", quality=95)
    return path


def build_screens() -> list[Path]:
    atlas_cache = {key: Image.open(path).convert("RGBA") for key, path in ATLAS_SOURCES.items()}
    output_paths = [build_start_screen()]

    for spec in SCREENS:
        image = split_quadrant(atlas_cache[spec.atlas], spec.quadrant)
        draw = ImageDraw.Draw(image)
        draw_text(draw, Label(spec.title, 836, 63, 36, GOLD))
        for label in spec.labels:
            draw_text(draw, label)
        path = DIR_SCREENS / spec.file_name
        image.convert("RGB").save(path, quality=95)
        output_paths.append(path)

        # Three useful rectangular slices per screen: title bar, primary work area, and command strip.
        slices = {
            "title": (410, 12, 1262, 116),
            "main": (36, 116, 1636, 742),
            "commands": (36, 742, 1636, 930),
        }
        stem = Path(spec.file_name).stem
        for suffix, box in slices.items():
            image.crop(box).save(DIR_COMPONENTS / f"{stem}_{suffix}.png")

    return output_paths


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int]:
    alpha = image.getchannel("A")
    bbox = alpha.getbbox()
    if bbox is None:
        raise RuntimeError("Generated event prop has no opaque pixels")
    return bbox


def make_prop_canvas(crop: Image.Image) -> Image.Image:
    crop = crop.crop(alpha_bbox(crop))
    max_w, max_h = 860, 860
    scale = min(max_w / crop.width, max_h / crop.height)
    new_size = (max(1, int(crop.width * scale)), max(1, int(crop.height * scale)))
    crop = crop.resize(new_size, Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
    x = (1024 - crop.width) // 2
    y = min(1024 - crop.height - 42, 120)
    canvas.alpha_composite(crop, (x, max(20, y)))
    return canvas


def build_event_assets() -> tuple[list[Path], list[Path]]:
    alpha_sheets = {
        idx: Image.open(DIR_SOURCES / f"event_sheet_{idx:02d}_alpha.png").convert("RGBA")
        for idx in EVENT_SOURCES
    }
    prop_paths: list[Path] = []
    tile_paths: list[Path] = []

    for event_id, name, act, ground_name in EVENTS:
        sheet_idx = (event_id - 1) // 3 + 1
        column = (event_id - 1) % 3
        sheet = alpha_sheets[sheet_idx]
        left = round(sheet.width * column / 3)
        right = round(sheet.width * (column + 1) / 3)
        crop = sheet.crop((left, 0, right, sheet.height))
        prop = make_prop_canvas(crop)

        safe_name = name.replace(" ", "_")
        prop_path = DIR_PROPS / f"event_{event_id:02d}_{safe_name}_prop.png"
        prop.save(prop_path)
        prop_paths.append(prop_path)

        ground = Image.open(GROUND_ROOT / ground_name).convert("RGBA").resize((1024, 1024), Image.Resampling.LANCZOS)
        placed = prop.crop(alpha_bbox(prop))
        max_w, max_h = 620, 600
        scale = min(max_w / placed.width, max_h / placed.height)
        placed = placed.resize((max(1, int(placed.width * scale)), max(1, int(placed.height * scale))), Image.Resampling.LANCZOS)
        x = (1024 - placed.width) // 2
        y = max(120, 710 - placed.height)

        shadow_mask = placed.getchannel("A").resize((placed.width, max(24, placed.height // 5)), Image.Resampling.LANCZOS)
        shadow_mask = shadow_mask.filter(ImageFilter.GaussianBlur(18))
        shadow = Image.new("RGBA", (placed.width, shadow_mask.height), (0, 0, 0, 0))
        shadow.putalpha(shadow_mask.point(lambda value: min(100, value // 2)))
        ground.alpha_composite(shadow, (x, min(850, y + placed.height - shadow.height // 2)))
        ground.alpha_composite(placed, (x, y))

        draw = ImageDraw.Draw(ground)
        beacon_y = min(900, y + placed.height + 10)
        draw.ellipse((480, beacon_y - 12, 544, beacon_y + 12), outline=(236, 192, 99, 185), width=4)
        draw.polygon(((512, beacon_y - 18), (525, beacon_y), (512, beacon_y + 18), (499, beacon_y)), fill=(236, 192, 99, 220))

        tile_path = DIR_TILES / f"event_{event_id:02d}_{safe_name}_hex.png"
        ground.save(tile_path)
        tile_paths.append(tile_path)

    return prop_paths, tile_paths


def copy_sources() -> None:
    shutil.copy2(START_SOURCE, DIR_SOURCES / "start_screen_ai_source.png")
    for idx, path in ATLAS_SOURCES.items():
        shutil.copy2(path, DIR_SOURCES / f"system_ui_atlas_{idx:02d}.png")
    for idx, path in EVENT_SOURCES.items():
        shutil.copy2(path, DIR_SOURCES / f"event_sheet_{idx:02d}_chroma.png")


def copy_existing_ui() -> None:
    for path in UI_ROOT.glob("*.png"):
        if path.name == "01_웹_타이틀화면_v3.png":
            continue
        if path.name[:2].isdigit() and 1 <= int(path.name[:2]) <= 25:
            shutil.copy2(path, DIR_EXISTING / path.name)


def checker(size: tuple[int, int], step: int = 32) -> Image.Image:
    image = Image.new("RGBA", size, (39, 42, 48, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], step):
        for x in range(0, size[0], step):
            if (x // step + y // step) % 2:
                draw.rectangle((x, y, x + step - 1, y + step - 1), fill=(57, 61, 67, 255))
    return image


def make_contact_sheet(paths: list[Path], output: Path, columns: int, thumb_size: tuple[int, int], labels: list[str]) -> None:
    rows = (len(paths) + columns - 1) // columns
    cell_w, cell_h = thumb_size[0] + 28, thumb_size[1] + 74
    canvas = Image.new("RGB", (columns * cell_w + 28, rows * cell_h + 28), (8, 13, 20))
    draw = ImageDraw.Draw(canvas)
    for idx, path in enumerate(paths):
        col, row = idx % columns, idx // columns
        x, y = 28 + col * cell_w, 28 + row * cell_h
        source = Image.open(path).convert("RGBA")
        base = checker(thumb_size)
        fitted = source.copy()
        fitted.thumbnail(thumb_size, Image.Resampling.LANCZOS)
        px = (thumb_size[0] - fitted.width) // 2
        py = (thumb_size[1] - fitted.height) // 2
        base.alpha_composite(fitted, (px, py))
        canvas.paste(base.convert("RGB"), (x, y))
        draw.rectangle((x, y, x + thumb_size[0], y + thumb_size[1]), outline=(216, 169, 76), width=3)
        draw.text((x + thumb_size[0] // 2, y + thumb_size[1] + 30), labels[idx], font=font(20), fill=(246, 236, 209), anchor="mm")
    canvas.save(output, quality=94)


def write_catalog(screen_paths: list[Path], prop_paths: list[Path], tile_paths: list[Path]) -> None:
    catalog = {
        "version": 3,
        "createdFor": "FFSS Poker x Seotda roguelike",
        "unityModified": False,
        "screens": [path.name for path in screen_paths],
        "events": [
            {
                "id": event_id,
                "name": name,
                "act": act,
                "prop": prop_paths[event_id - 1].name,
                "hexTile": tile_paths[event_id - 1].name,
                "edgeStandard": "shared-stone-edge-v2",
            }
            for event_id, name, act, _ in EVENTS
        ],
    }
    (OUTPUT / "catalog_system_ui_events_v3.json").write_text(json.dumps(catalog, ensure_ascii=False, indent=2), encoding="utf-8")
    with (OUTPUT / "catalog_system_ui_events_v3.csv").open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(("type", "id", "name", "act", "file"))
        for idx, path in enumerate(screen_paths, start=1):
            writer.writerow(("screen", idx, path.stem, "", path.name))
        for event_id, name, act, _ in EVENTS:
            writer.writerow(("event_prop", event_id, name, act, prop_paths[event_id - 1].name))
            writer.writerow(("event_hex", event_id, name, act, tile_paths[event_id - 1].name))


def make_zip(output: Path, roots: list[Path]) -> None:
    with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for root in roots:
            if root.is_file():
                archive.write(root, root.name)
                continue
            for path in sorted(root.rglob("*")):
                if path.is_file():
                    archive.write(path, path.relative_to(OUTPUT))


def validate(screen_paths: list[Path], prop_paths: list[Path], tile_paths: list[Path]) -> None:
    assert len(screen_paths) == 17
    assert len(prop_paths) == 18
    assert len(tile_paths) == 18
    for path in screen_paths[1:]:
        assert Image.open(path).size == (1672, 941), path
    assert Image.open(screen_paths[0]).size == (1920, 1080)
    for path in prop_paths:
        image = Image.open(path)
        assert image.size == (1024, 1024) and image.mode == "RGBA"
        assert image.getpixel((0, 0))[3] == 0
    for path in tile_paths:
        image = Image.open(path)
        assert image.size == (1024, 1024) and image.mode == "RGBA"
        assert image.getpixel((512, 512))[3] > 0


def main() -> None:
    ensure_dirs()
    copy_sources()
    copy_existing_ui()
    screen_paths = build_screens()
    prop_paths, tile_paths = build_event_assets()

    screen_labels = ["01 시작화면 v3"] + [f"{idx + 26:02d} {spec.title}" for idx, spec in enumerate(SCREENS)]
    event_labels = [f"{event_id:02d} {name}" for event_id, name, _, _ in EVENTS]
    make_contact_sheet(screen_paths, DIR_PREVIEWS / "preview_시스템디자인_전체화면_UI_17종.png", 3, (520, 293), screen_labels)
    make_contact_sheet(prop_paths, DIR_PREVIEWS / "preview_이벤트_건물오브젝트_18종.png", 6, (260, 260), event_labels)
    make_contact_sheet(tile_paths, DIR_PREVIEWS / "preview_이벤트_건물육각타일_18종.png", 6, (260, 260), event_labels)
    write_catalog(screen_paths, prop_paths, tile_paths)

    make_zip(DIR_ZIPS / "ffss-system-design-ui-v3-17screens.zip", [DIR_SCREENS, DIR_COMPONENTS])
    make_zip(DIR_ZIPS / "ffss-field-events-v3-18-landmarks-and-hexes.zip", [DIR_PROPS, DIR_TILES, DIR_PREVIEWS / "preview_이벤트_건물육각타일_18종.png"])
    make_zip(
        DIR_ZIPS / "ffss-system-ui-events-v3-complete.zip",
        [DIR_EXISTING, DIR_SCREENS, DIR_COMPONENTS, DIR_PROPS, DIR_TILES, DIR_SOURCES, DIR_PREVIEWS, OUTPUT / "catalog_system_ui_events_v3.json", OUTPUT / "catalog_system_ui_events_v3.csv"],
    )
    validate(screen_paths, prop_paths, tile_paths)

    print(json.dumps({
        "output": str(OUTPUT),
        "existingUi": len(list(DIR_EXISTING.glob("*.png"))),
        "screens": len(screen_paths),
        "uiComponents": len(list(DIR_COMPONENTS.glob("*.png"))),
        "eventProps": len(prop_paths),
        "eventHexTiles": len(tile_paths),
        "zips": {path.name: path.stat().st_size for path in sorted(DIR_ZIPS.glob("*.zip"))},
    }, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
