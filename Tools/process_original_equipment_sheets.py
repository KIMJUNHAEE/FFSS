from pathlib import Path
import secrets
import uuid

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
ART_DIR = ROOT / "Assets" / "Art" / "Equipment"
SHEET_DIR = ART_DIR / "TransparentSheets"
ICON_DIR = ROOT / "Assets" / "Resources" / "Equipment"
META_TEMPLATE_PATH = ICON_DIR / "weapon_red_moon_hwando.png.meta"

SHEETS = [
    (
        "weapons_original_a_alpha.png",
        [
            "weapon_celestial_chakrams",
            "weapon_snow_bamboo_staff",
            "weapon_ghostfire_chain_sickle",
            "weapon_white_serpent_whip",
            "weapon_sun_crow_longbow",
            "weapon_jade_tiger_gauntlets",
        ],
    ),
    (
        "weapons_original_b_alpha.png",
        [
            "weapon_lotus_parasol_spear",
            "weapon_waveglass_rapier",
            "weapon_heaven_bell_mace",
            "weapon_geomungo_greatsword",
            "weapon_crescent_needle_case",
            "weapon_cloud_rune_hand_cannon",
        ],
    ),
    (
        "garments_original_a_alpha.png",
        [
            "garment_dawn_phoenix_hanbok",
            "garment_snow_bamboo_cloak",
            "garment_jade_scale_robe",
            "garment_thunder_magistrate_coat",
            "garment_moon_rabbit_scout_vest",
            "garment_raven_feather_mantle",
        ],
    ),
    (
        "garments_original_b_alpha.png",
        [
            "garment_lotus_guardian_dress",
            "garment_bronze_tiger_coat",
            "garment_star_shaman_veil",
            "garment_sea_dragon_overcoat",
            "garment_paper_ward_coat",
            "garment_cherry_shadow_armor",
        ],
    ),
    (
        "talismans_original_a_alpha.png",
        [
            "talisman_nine_tail_fox_seal",
            "talisman_north_star_compass",
            "talisman_dragon_tooth_knot",
            "talisman_moon_rabbit_stamp",
            "talisman_thunder_drum",
            "talisman_three_jade_necklace",
        ],
    ),
    (
        "talismans_original_b_alpha.png",
        [
            "talisman_broken_moon_mirror",
            "talisman_five_color_fate_thread",
            "talisman_lotus_coin",
            "talisman_black_sun_seal",
            "talisman_mountain_spirit_bell",
            "talisman_dream_norigae",
        ],
    ),
    (
        "keepsakes_original_a_alpha.png",
        [
            "keepsake_celestial_map_scroll",
            "keepsake_brass_wayfinder_compass",
            "keepsake_porcelain_ink_bottle",
            "keepsake_bamboo_dice_case",
            "keepsake_moon_rabbit_pouch",
            "keepsake_foxfire_lantern",
        ],
    ),
    (
        "keepsakes_original_b_alpha.png",
        [
            "keepsake_seaglass_hairpin",
            "keepsake_brass_pocket_watch",
            "keepsake_lacquer_music_box",
            "keepsake_cracked_jade_comb",
            "keepsake_red_fate_spool",
            "keepsake_mini_folding_screen",
        ],
    ),
    (
        "weapons_poker_alpha.png",
        [
            "weapon_poker_twin_ace_daggers",
            "weapon_poker_five_step_straight_sword",
            "weapon_poker_flush_suit_rapier",
            "weapon_poker_full_house_hand_cannon",
            "weapon_poker_four_kind_gauntlets",
            "weapon_poker_royal_flush_spear",
        ],
    ),
    (
        "garments_poker_alpha.png",
        [
            "garment_poker_high_card_dealer_coat",
            "garment_poker_two_pair_vest",
            "garment_poker_three_kind_guard_robe",
            "garment_poker_straight_runner_mantle",
            "garment_poker_flush_robe",
            "garment_poker_royal_flush_regalia",
        ],
    ),
    (
        "talismans_poker_alpha.png",
        [
            "talisman_poker_dealer_button_seal",
            "talisman_poker_paired_ace_clasp",
            "talisman_poker_full_house_gate",
            "talisman_poker_four_kind_knot",
            "talisman_poker_straight_flush_ribbon",
            "talisman_poker_joker_reversal_tag",
        ],
    ),
    (
        "keepsakes_poker_alpha.png",
        [
            "keepsake_poker_ace_card_case",
            "keepsake_poker_five_chip_stack",
            "keepsake_poker_four_suit_compass",
            "keepsake_poker_royal_crown_watch",
            "keepsake_poker_discard_lacquer_tray",
            "keepsake_poker_lucky_cut_box",
        ],
    ),
]

EXISTING_IDS = [
    "weapon_red_moon_hwando",
    "weapon_plum_spear",
    "weapon_ink_twin_blades",
    "weapon_gold_war_hammer",
    "weapon_cloud_fan",
    "weapon_sealed_scythe",
    "garment_tiger_durumagi",
    "garment_plum_silk_armor",
    "garment_black_brigandine",
    "garment_cloud_robe",
    "garment_oni_hide_coat",
    "garment_white_crane_mantle",
    "talisman_twin_crimson_cards",
    "talisman_royal_gwang",
    "talisman_hunters_eye",
    "talisman_ink_cloud",
    "talisman_red_thunder",
    "talisman_reversal_guardian",
    "keepsake_red_sand_hourglass",
    "keepsake_yeopjeon_bundle",
    "keepsake_plum_ring",
    "keepsake_lacquer_gourd",
    "keepsake_cracked_mask",
    "keepsake_blue_jade_tablet",
]


def extract_icon(sheet: Image.Image, index: int) -> Image.Image:
    col = index % 3
    row = index // 3
    cell_w = sheet.width // 3
    cell_h = sheet.height // 2
    inset = 6
    tile = sheet.crop(
        (
            col * cell_w + inset,
            row * cell_h + inset,
            (col + 1) * cell_w - inset,
            (row + 1) * cell_h - inset,
        )
    ).convert("RGBA")

    alpha = tile.getchannel("A")
    bbox = alpha.getbbox()
    if bbox is None:
        raise RuntimeError(f"No visible pixels in cell {index}")

    cropped = tile.crop(bbox)
    target_extent = 440
    scale = min(target_extent / cropped.width, target_extent / cropped.height)
    size = (
        max(1, round(cropped.width * scale)),
        max(1, round(cropped.height * scale)),
    )
    cropped = cropped.resize(size, Image.Resampling.LANCZOS)

    canvas = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
    offset = ((512 - cropped.width) // 2, (512 - cropped.height) // 2)
    canvas.alpha_composite(cropped, offset)
    return canvas


def make_preview(item_ids: list[str]) -> Path:
    tile_size = 256
    columns = 9
    rows = (len(item_ids) + columns - 1) // columns
    preview = Image.new("RGBA", (columns * tile_size, rows * tile_size), (0, 0, 0, 0))
    checker = Image.new("RGBA", (tile_size, tile_size), "#edf0ef")
    checker_draw = ImageDraw.Draw(checker)
    check_size = 32
    for y in range(0, tile_size, check_size):
        for x in range(0, tile_size, check_size):
            if (x // check_size + y // check_size) % 2:
                checker_draw.rectangle(
                    (x, y, x + check_size - 1, y + check_size - 1),
                    fill="#dfe4e1",
                )

    for index in range(rows * columns):
        x = (index % columns) * tile_size
        y = (index // columns) * tile_size
        preview.alpha_composite(checker, (x, y))

    for index, item_id in enumerate(item_ids):
        icon_path = ICON_DIR / f"{item_id}.png"
        if not icon_path.exists():
            raise FileNotFoundError(icon_path)
        icon = Image.open(icon_path).convert("RGBA").resize(
            (tile_size, tile_size), Image.Resampling.LANCZOS
        )
        x = (index % columns) * tile_size
        y = (index // columns) * tile_size
        preview.alpha_composite(icon, (x, y))

    draw = ImageDraw.Draw(preview)
    for x in range(0, preview.width + 1, tile_size):
        draw.line((x, 0, x, preview.height), fill="#212529", width=2)
    for y in range(0, preview.height + 1, tile_size):
        draw.line((0, y, preview.width, y), fill="#212529", width=2)
    output_path = ART_DIR / f"preview_equipment_{len(item_ids)}.png"
    preview.save(output_path)
    return output_path


def write_unity_meta(item_id: str, icon: Image.Image) -> None:
    meta_path = ICON_DIR / f"{item_id}.png.meta"
    if meta_path.exists():
        return

    bbox = icon.getchannel("A").getbbox()
    if bbox is None:
        raise RuntimeError(f"Cannot create metadata for empty icon: {item_id}")
    left, top, right, bottom = bbox
    rect_y = icon.height - bottom
    internal_id = int.from_bytes(secrets.token_bytes(8), "big", signed=True)
    if internal_id == 0:
        internal_id = 1

    template = META_TEMPLATE_PATH.read_text(encoding="utf-8")
    replacements = {
        "12f92f990a563f445b21de570aaec518": uuid.uuid4().hex,
        "weapon_red_moon_hwando_0": f"{item_id}_0",
        "-4153647514239044974": str(internal_id),
        "292f89fb7974b56c0800000000000000": f"{uuid.uuid4().hex[:16]}0800000000000000",
        "5e97eb03825dee720800000000000000": f"{uuid.uuid4().hex[:16]}0800000000000000",
        "        x: 123\n        y: 36\n        width: 265\n        height: 440": (
            f"        x: {left}\n        y: {rect_y}\n"
            f"        width: {right - left}\n        height: {bottom - top}"
        ),
    }
    for old, new in replacements.items():
        template = template.replace(old, new)
    meta_path.write_text(template, encoding="utf-8")


def main() -> None:
    ICON_DIR.mkdir(parents=True, exist_ok=True)
    new_ids: list[str] = []

    for sheet_name, item_ids in SHEETS:
        sheet_path = SHEET_DIR / sheet_name
        sheet = Image.open(sheet_path).convert("RGBA")
        if sheet.size != (1536, 1024):
            raise RuntimeError(f"Unexpected sheet size: {sheet_path} {sheet.size}")

        for index, item_id in enumerate(item_ids):
            icon = extract_icon(sheet, index)
            icon.save(ICON_DIR / f"{item_id}.png")
            write_unity_meta(item_id, icon)
            new_ids.append(item_id)

    preview_path = make_preview(EXISTING_IDS + new_ids)
    print(f"Wrote {len(new_ids)} generated icons and {preview_path.name}")


if __name__ == "__main__":
    main()
