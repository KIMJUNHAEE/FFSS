from __future__ import annotations

import shutil
import zipfile
from pathlib import Path


ROOT = Path(r"C:\git\FFSS")
BUILD = ROOT / "Builds" / "CardPacks"
DESKTOP = Path(r"C:\Users\kirby\OneDrive\바탕 화면\구구가가\웹게임_UI_신규\누끼완료_전체\카드_상위포커_섯다전용_71")
SITE_PUBLIC = ROOT / "ProjectAssetGuide" / "public" / "cards"

POKER = ROOT / "Assets" / "Resources" / "Cards" / "AscendantPoker"
SIGNATURE = ROOT / "Assets" / "Resources" / "Cards" / "SignatureSeotda"
ART = ROOT / "Assets" / "Art" / "Cards"
RESOURCE_ROOT = ROOT / "Assets" / "Resources" / "Cards"


def add_file(archive: zipfile.ZipFile, source: Path, destination: str) -> None:
    archive.write(source, destination)


def add_directory(archive: zipfile.ZipFile, source: Path, destination: str) -> None:
    for path in sorted(source.rglob("*")):
        if path.is_file() and not path.name.endswith(".meta"):
            archive.write(path, str(Path(destination) / path.relative_to(source)))


def poker_zip(path: Path) -> None:
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        add_directory(archive, POKER, "AscendantPoker")
        add_file(archive, RESOURCE_ROOT / "ascendant_poker_catalog.json", "ascendant_poker_catalog.json")
        add_file(archive, ART / "ascendant_poker_catalog.csv", "ascendant_poker_catalog.csv")
        add_file(archive, ART / "preview_ascendant_poker_54.png", "preview_ascendant_poker_54.png")
        add_file(archive, ROOT / "Assets" / "Scripts" / "UI" / "PokerHandEvaluator.cs", "Unity/PokerHandEvaluator.cs")
        add_file(archive, ROOT / "Assets" / "Scripts" / "UI" / "PokerHandController.cs", "Unity/PokerHandController.cs")
        add_file(archive, ROOT / "Assets" / "Scripts" / "Battle" / "RpsCombatController.cs", "Unity/RpsCombatController.cs")
        add_file(archive, ART / "README_CARDS_KO.md", "README_CARDS_KO.md")


def signature_zip(path: Path) -> None:
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        add_directory(archive, SIGNATURE, "SignatureSeotda")
        add_file(archive, RESOURCE_ROOT / "signature_seotda_catalog.json", "signature_seotda_catalog.json")
        add_file(archive, ART / "signature_seotda_catalog.csv", "signature_seotda_catalog.csv")
        add_file(archive, ART / "preview_signature_seotda_17.png", "preview_signature_seotda_17.png")
        add_file(archive, ROOT / "Assets" / "Scripts" / "Battle" / "OpponentSeotdaCardCatalog.cs", "Unity/OpponentSeotdaCardCatalog.cs")
        add_file(archive, ROOT / "Assets" / "Scripts" / "UI" / "SeotdaHandEvaluator.cs", "Unity/SeotdaHandEvaluator.cs")
        add_file(archive, ROOT / "Assets" / "Scripts" / "UI" / "SeotdaTableController.cs", "Unity/SeotdaTableController.cs")
        add_file(archive, ROOT / "Assets" / "Scripts" / "Battle" / "RpsCombatController.cs", "Unity/RpsCombatController.cs")
        add_file(archive, ART / "README_CARDS_KO.md", "README_CARDS_KO.md")


def complete_zip(path: Path) -> None:
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        add_directory(archive, POKER, "Cards/AscendantPoker")
        add_directory(archive, SIGNATURE, "Cards/SignatureSeotda")
        for file_name in [
            "ascendant_poker_catalog.json",
            "signature_seotda_catalog.json",
        ]:
            add_file(archive, RESOURCE_ROOT / file_name, f"Catalog/{file_name}")
        for file_name in [
            "ascendant_poker_catalog.csv",
            "signature_seotda_catalog.csv",
            "preview_ascendant_poker_54.png",
            "preview_signature_seotda_17.png",
            "README_CARDS_KO.md",
        ]:
            add_file(archive, ART / file_name, f"Catalog/{file_name}")
        for source in [
            ROOT / "Assets" / "Scripts" / "UI" / "PokerHandEvaluator.cs",
            ROOT / "Assets" / "Scripts" / "UI" / "PokerHandController.cs",
            ROOT / "Assets" / "Scripts" / "UI" / "SeotdaHandEvaluator.cs",
            ROOT / "Assets" / "Scripts" / "UI" / "SeotdaTableController.cs",
            ROOT / "Assets" / "Scripts" / "Battle" / "OpponentSeotdaCardCatalog.cs",
            ROOT / "Assets" / "Scripts" / "Battle" / "RpsCombatController.cs",
        ]:
            add_file(archive, source, f"Unity/{source.name}")


def copy_ready_files() -> None:
    (DESKTOP / "상위포커_54장_뒷면1장").mkdir(parents=True, exist_ok=True)
    (DESKTOP / "상대전용섯다_17장").mkdir(parents=True, exist_ok=True)
    (DESKTOP / "카탈로그와미리보기").mkdir(parents=True, exist_ok=True)
    shutil.copytree(POKER, DESKTOP / "상위포커_54장_뒷면1장", dirs_exist_ok=True)
    shutil.copytree(SIGNATURE, DESKTOP / "상대전용섯다_17장", dirs_exist_ok=True)
    for source in [
        ART / "ascendant_poker_catalog.csv",
        ART / "signature_seotda_catalog.csv",
        ART / "preview_ascendant_poker_54.png",
        ART / "preview_signature_seotda_17.png",
        ART / "README_CARDS_KO.md",
    ]:
        shutil.copy2(source, DESKTOP / "카탈로그와미리보기" / source.name)


def copy_site_assets() -> None:
    shutil.copytree(POKER, SITE_PUBLIC / "poker", dirs_exist_ok=True)
    shutil.copytree(SIGNATURE, SITE_PUBLIC / "signature", dirs_exist_ok=True)
    shutil.copy2(ART / "preview_ascendant_poker_54.png", SITE_PUBLIC / "preview_ascendant_poker_54.png")
    shutil.copy2(ART / "preview_signature_seotda_17.png", SITE_PUBLIC / "preview_signature_seotda_17.png")


def main() -> None:
    BUILD.mkdir(parents=True, exist_ok=True)
    poker_path = BUILD / "ffss-ascendant-poker-54.zip"
    signature_path = BUILD / "ffss-signature-seotda-17.zip"
    complete_path = BUILD / "ffss-card-pack-71.zip"
    poker_zip(poker_path)
    signature_zip(signature_path)
    complete_zip(complete_path)
    copy_ready_files()
    copy_site_assets()
    for path in [poker_path, signature_path, complete_path]:
        print(f"{path.name}={path.stat().st_size}")


if __name__ == "__main__":
    main()
