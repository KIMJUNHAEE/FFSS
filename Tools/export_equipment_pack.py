import argparse
import csv
import re
import shutil
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CATALOG_PATH = ROOT / "Assets" / "Scripts" / "Equipment" / "EquipmentCatalog.cs"
ICON_DIR = ROOT / "Assets" / "Resources" / "Equipment"
EQUIPMENT_COUNT = 96
PREVIEW_PATH = ROOT / "Assets" / "Art" / "Equipment" / f"preview_equipment_{EQUIPMENT_COUNT}.png"

ENTRY_PATTERN = re.compile(
    r'new\("(?P<id>[^"]+)",\s*"(?P<name>[^"]+)",\s*'
    r'EquipmentSlotType\.(?P<slot>\w+),\s*EquipmentRarity\.(?P<rarity>\w+),\s*'
    r'"(?P<lore>[^"]*)",\s*"(?P<effect>[^"]*)",',
    re.MULTILINE,
)

SLOT_LABELS = {
    "Weapon": "무기",
    "Garment": "의복",
    "Talisman": "부적",
    "Keepsake": "기념품",
}

RARITY_LABELS = {
    "Common": "일반",
    "Rare": "희귀",
    "Legendary": "전설",
}

SCRIPT_PATHS = [
    ROOT / "Assets" / "Scripts" / "Equipment" / "EquipmentCatalog.cs",
    ROOT / "Assets" / "Scripts" / "Equipment" / "EquipmentLoadout.cs",
    ROOT / "Assets" / "Scripts" / "UI" / "EquipmentInventoryView.cs",
    ROOT / "Assets" / "Editor" / "EquipmentIconPostprocessor.cs",
]


def parse_catalog() -> list[dict[str, str]]:
    source = CATALOG_PATH.read_text(encoding="utf-8")
    items = []
    for match in ENTRY_PATTERN.finditer(source):
        item = match.groupdict()
        item["slot_label"] = SLOT_LABELS[item["slot"]]
        item["rarity_label"] = RARITY_LABELS[item["rarity"]]
        item["file"] = f'{item["id"]}.png'
        items.append(item)
    if len(items) != EQUIPMENT_COUNT:
        raise RuntimeError(f"Expected {EQUIPMENT_COUNT} equipment entries, found {len(items)}")
    return items


def write_csv(items: list[dict[str, str]], output_path: Path) -> None:
    fields = ["file", "id", "name", "slot_label", "rarity_label", "lore", "effect"]
    with output_path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(items)


def write_readme(items: list[dict[str, str]], output_path: Path) -> None:
    lines = [
        f"# FFSS 장비 {EQUIPMENT_COUNT}종",
        "",
        "FFSS의 포커 족보, 카드 색상, 약점 관통, 격파 게이지에 연결된 장비 묶음이다.",
        "모든 아이콘은 512x512 투명 PNG이며 무기, 의복, 부적, 기념품을 한 칸씩 장착한다.",
        "",
        "## 구성",
        "",
        f"- `icons`: 장비 아이콘 {EQUIPMENT_COUNT}개",
        "- `Unity_Scripts`: 장비 목록, 장착 저장, 인벤토리 표시, 아이콘 임포터",
        f"- `equipment_effects_{EQUIPMENT_COUNT}.csv`: 이름, 등급, 설명, 효과 전체 목록",
        f"- `00_장비_{EQUIPMENT_COUNT}종_미리보기.png`: 전체 아이콘 미리보기",
        "",
        "## Unity 적용",
        "",
        f"1. `icons`의 PNG {EQUIPMENT_COUNT}개를 `Assets/Resources/Equipment`에 넣는다.",
        "2. `Unity_Scripts/EquipmentCatalog.cs`와 `EquipmentLoadout.cs`를 `Assets/Scripts/Equipment`에 넣는다.",
        "3. `EquipmentInventoryView.cs`를 `Assets/Scripts/UI`에 넣는다.",
        "4. `EquipmentIconPostprocessor.cs`를 `Assets/Editor`에 넣는다.",
        "5. 플레이어 오브젝트에 `EquipmentLoadout`을 붙이고 인벤토리 화면에 `EquipmentInventoryView`를 연결한다.",
        "",
        "장착 정보는 `PlayerPrefs`에 저장되며 장비 효과는 `EquipmentCatalog`의 정의를 통해 전투 계산에 적용된다.",
        "",
        "## 장비 목록",
        "",
    ]

    for slot in ("Weapon", "Garment", "Talisman", "Keepsake"):
        lines.extend(
            [
                f'### {SLOT_LABELS[slot]}',
                "",
                "| 파일 | 이름 | 등급 | 효과 |",
                "| --- | --- | --- | --- |",
            ]
        )
        for item in (entry for entry in items if entry["slot"] == slot):
            lines.append(
                f'| `{item["file"]}` | {item["name"]} | {item["rarity_label"]} | {item["effect"]} |'
            )
        lines.append("")

    output_path.write_text("\n".join(lines), encoding="utf-8")


def build_pack(output_dir: Path, zip_path: Path) -> None:
    items = parse_catalog()
    icon_output = output_dir / "icons"
    script_output = output_dir / "Unity_Scripts"
    icon_output.mkdir(parents=True, exist_ok=True)
    script_output.mkdir(parents=True, exist_ok=True)

    for item in items:
        source = ICON_DIR / item["file"]
        if not source.exists():
            raise FileNotFoundError(source)
        shutil.copy2(source, icon_output / source.name)

    for source in SCRIPT_PATHS:
        shutil.copy2(source, script_output / source.name)

    shutil.copy2(PREVIEW_PATH, output_dir / f"00_장비_{EQUIPMENT_COUNT}종_미리보기.png")
    write_csv(items, output_dir / f"equipment_effects_{EQUIPMENT_COUNT}.csv")
    write_readme(items, output_dir / "README_장비_효과_적용법.md")

    zip_path.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(zip_path, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for file_path in sorted(path for path in output_dir.rglob("*") if path.is_file()):
            archive.write(file_path, file_path.relative_to(output_dir.parent))

    print(f"Pack: {output_dir}")
    print(f"ZIP: {zip_path} ({zip_path.stat().st_size} bytes)")
    print(f"Equipment: {len(items)}")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--zip", required=True, type=Path)
    args = parser.parse_args()
    build_pack(args.output_dir.resolve(), args.zip.resolve())


if __name__ == "__main__":
    main()
