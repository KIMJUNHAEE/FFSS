import os
from PIL import Image

ASSETS_ENEMY = r"C:\FFSS\Assets\Enemy"


def tighten_crop(sheet_name, margin=8):
    folder = os.path.join(ASSETS_ENEMY, sheet_name)
    files = sorted(f for f in os.listdir(folder) if f.lower().endswith(".png"))

    frames = [Image.open(os.path.join(folder, f)) for f in files]

    # Union bounding box across every frame so the crop rect is identical for all of
    # them (no frame-to-frame jitter), but tight enough that unused transparent
    # margin (the same on every frame) doesn't shrink the character inside preserveAspect.
    min_x = min_y = 10 ** 9
    max_x = max_y = -1
    for img in frames:
        bbox = img.split()[-1].getbbox()
        if bbox is None:
            continue
        l, t, r, b = bbox
        min_x, min_y = min(min_x, l), min(min_y, t)
        max_x, max_y = max(max_x, r), max(max_y, b)

    w, h = frames[0].size
    min_x = max(0, min_x - margin)
    min_y = max(0, min_y - margin)
    max_x = min(w, max_x + margin)
    max_y = min(h, max_y + margin)

    print(f"{sheet_name}: union bbox = ({min_x},{min_y})-({max_x},{max_y}) "
          f"-> {max_x - min_x}x{max_y - min_y} (was {w}x{h})", flush=True)

    for name, img in zip(files, frames):
        cropped = img.crop((min_x, min_y, max_x, max_y))
        cropped.save(os.path.join(folder, name))

    print(f"{sheet_name}: {len(files)} frames re-cropped", flush=True)


if __name__ == "__main__":
    tighten_crop("38_Attack")
