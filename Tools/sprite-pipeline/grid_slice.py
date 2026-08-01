import os
import uuid
from PIL import Image

from upscale_frames import make_meta

ASSETS_ENEMY = r"C:\FFSS\Assets\Enemy"


def grid_slice(sheet_name, cell_size=1024, cols=5, rows=8):
    src_path = os.path.join(ASSETS_ENEMY, f"{sheet_name}.png")
    src = Image.open(src_path)
    assert src.width == cell_size * cols and src.height == cell_size * rows, src.size

    out_dir = os.path.join(ASSETS_ENEMY, sheet_name)
    os.makedirs(out_dir, exist_ok=True)

    idx = 0
    kept = 0
    # Unity texture Y is bottom-up in world terms, but PIL crop is top-left based on the
    # raw pixel buffer, and rows in the sheet read top-to-bottom visually either way, so we
    # just walk the raw image top-to-bottom, left-to-right, which matches how these grid
    # sheets are laid out (row 0 = top of image).
    for row in range(rows):
        for col in range(cols):
            left = col * cell_size
            top = row * cell_size
            cell = src.crop((left, top, left + cell_size, top + cell_size))

            alpha = cell.split()[-1]
            if alpha.getbbox() is None:
                idx += 1
                continue  # fully transparent cell, skip

            out_name = f"{sheet_name}_{kept:03d}.png"
            out_path = os.path.join(out_dir, out_name)
            cell.save(out_path)

            guid = uuid.uuid4().hex
            with open(out_path + ".meta", "w", encoding="utf-8") as mf:
                mf.write(make_meta(guid))

            print(f"  {out_name} (sheet cell {idx})", flush=True)
            idx += 1
            kept += 1

    print(f"{sheet_name}: {kept} frames kept out of {cols * rows} cells", flush=True)


if __name__ == "__main__":
    grid_slice("38_Attack")
