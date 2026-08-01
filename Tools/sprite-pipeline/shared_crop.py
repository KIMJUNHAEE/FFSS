import os
import warnings
from PIL import Image

warnings.filterwarnings("ignore")
Image.MAX_IMAGE_PIXELS = None

from grid_slice import grid_slice

ASSETS_ENEMY = r"C:\FFSS\Assets\Enemy"


def regenerate_raw(sheet_names, cell_size=1920, cols=5, rows=8):
    """Re-slice each sheet fresh from its source (uncropped cells)."""
    for name in sheet_names:
        folder = os.path.join(ASSETS_ENEMY, name)
        if os.path.isdir(folder):
            for f in os.listdir(folder):
                os.remove(os.path.join(folder, f))
        grid_slice(name, cell_size=cell_size, cols=cols, rows=rows)


def shared_crop(sheet_names, margin=20):
    """Compute ONE union bbox across every frame of every given animation,
    then crop every frame of every animation to that identical rect, so
    switching animations doesn't change the character's apparent scale."""
    all_frames = {}
    min_x = min_y = 10 ** 9
    max_x = max_y = -1

    for name in sheet_names:
        folder = os.path.join(ASSETS_ENEMY, name)
        files = sorted(f for f in os.listdir(folder) if f.lower().endswith(".png"))
        imgs = [Image.open(os.path.join(folder, f)) for f in files]
        all_frames[name] = (files, imgs)

        for img in imgs:
            bbox = img.split()[-1].getbbox()
            if bbox is None:
                continue
            l, t, r, b = bbox
            min_x, min_y = min(min_x, l), min(min_y, t)
            max_x, max_y = max(max_x, r), max(max_y, b)

    w, h = next(iter(all_frames.values()))[1][0].size
    min_x = max(0, min_x - margin)
    min_y = max(0, min_y - margin)
    max_x = min(w, max_x + margin)
    max_y = min(h, max_y + margin)

    print(f"shared union bbox = ({min_x},{min_y})-({max_x},{max_y}) "
          f"-> {max_x - min_x}x{max_y - min_y} (cell was {w}x{h})", flush=True)

    for name, (files, imgs) in all_frames.items():
        folder = os.path.join(ASSETS_ENEMY, name)
        for fname, img in zip(files, imgs):
            cropped = img.crop((min_x, min_y, max_x, max_y))
            cropped.save(os.path.join(folder, fname))
        print(f"{name}: {len(files)} frames cropped to shared rect", flush=True)


if __name__ == "__main__":
    sheets = ["38_Idle", "38_Death"]
    regenerate_raw(sheets)
    shared_crop(sheets)
