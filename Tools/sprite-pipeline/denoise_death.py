import os
from PIL import Image

from upscale_frames import load_model, upscale

ASSETS_ENEMY = r"C:\FFSS\Assets\Enemy"
TARGET_LONGEST = 350


def denoise_and_upscale(sheet_name):
    folder = os.path.join(ASSETS_ENEMY, sheet_name)
    files = sorted(f for f in os.listdir(folder) if f.lower().endswith(".png"))

    model = load_model()
    print(f"{sheet_name}: {len(files)} frames", flush=True)

    for i, name in enumerate(files):
        path = os.path.join(folder, name)
        img = Image.open(path)

        scale = TARGET_LONGEST / max(img.size)
        small = img.resize((max(1, round(img.width * scale)), max(1, round(img.height * scale))), Image.LANCZOS)

        result = upscale(model, small)
        result.save(path)
        print(f"  [{i + 1}/{len(files)}] {name}: {img.size} -> down {small.size} -> up {result.size}", flush=True)

    print("Done.", flush=True)


if __name__ == "__main__":
    denoise_and_upscale("38_Death")
