import os
import re
import sys
import uuid

import torch
from PIL import Image

from rrdbnet import RRDBNet

ASSETS_ENEMY = r"C:\FFSS\Assets\Enemy"
MODEL_PATH = os.path.join(os.path.dirname(__file__), "RealESRGAN_x4plus_anime_6B.pth")

SHEETS = ["38_Idle", "38_Attack", "38_Hurt", "38_Death"]

FRAME_RE = re.compile(
    r"name:\s*(\S+)\s*\n"
    r"(?:.*\n)*?\s*x:\s*([\d.]+)\s*\n"
    r"\s*y:\s*([\d.]+)\s*\n"
    r"(?:.*\n)*?\s*width:\s*([\d.]+)\s*\n"
    r"\s*height:\s*([\d.]+)"
)


def parse_frames(meta_path):
    with open(meta_path, "r", encoding="utf-8") as f:
        text = f.read()

    # Only look inside the spriteSheet.sprites block (skip the outline/customData noise)
    frames = []
    for m in FRAME_RE.finditer(text):
        name, x, y, w, h = m.groups()
        if not re.match(r".+_\d+$", name):
            continue
        frames.append({
            "name": name,
            "x": float(x),
            "y": float(y),
            "w": float(w),
            "h": float(h),
        })

    # de-dupe by name (regex can match nested/loose text), keep first occurrence in file order
    seen = set()
    unique = []
    for fr in frames:
        if fr["name"] in seen:
            continue
        seen.add(fr["name"])
        unique.append(fr)

    unique.sort(key=lambda fr: int(fr["name"].rsplit("_", 1)[1]))

    # Unity's "Automatic" slicing fragments some sheets into tiny noise specks
    # (anti-aliasing crumbs, a few px) and/or a full-canvas catch-all rect.
    # Real character frames on these sheets land well inside [MIN_SIZE, MAX_SIZE].
    MIN_SIZE = 50
    MAX_SIZE = 1200
    before = len(unique)
    unique = [fr for fr in unique if MIN_SIZE <= fr["w"] <= MAX_SIZE and MIN_SIZE <= fr["h"] <= MAX_SIZE]
    if len(unique) != before:
        print(f"    filtered {before - len(unique)} junk/outlier frame(s) out of {before}", flush=True)

    return unique


def load_model():
    device = torch.device("cpu")
    model = RRDBNet(num_in_ch=3, num_out_ch=3, scale=4, num_feat=64, num_block=6, num_grow_ch=32)
    state = torch.load(MODEL_PATH, map_location=device)
    state = state.get("params_ema", state.get("params", state))
    model.load_state_dict(state, strict=True)
    model.eval()
    return model


@torch.no_grad()
def upscale(model, img: Image.Image) -> Image.Image:
    if img.mode == "RGBA":
        alpha = img.split()[-1]
        # Flatten onto white first: raw RGB under semi-transparent pixels is often garbage,
        # which the model has no way to know is meant to be invisible and hallucinates on.
        flattened = Image.new("RGB", img.size, (255, 255, 255))
        flattened.paste(img, mask=alpha)
        rgb = flattened
    else:
        alpha = None
        rgb = img.convert("RGB")

    t = torch.from_numpy(
        __import__("numpy").array(rgb)
    ).float().permute(2, 0, 1).unsqueeze(0) / 255.0
    out = model(t).clamp(0, 1)
    out_img = (out.squeeze(0).permute(1, 2, 0).numpy() * 255.0).round().astype("uint8")
    result = Image.fromarray(out_img, mode="RGB")

    if alpha is not None:
        alpha_up = alpha.resize(result.size, Image.LANCZOS)
        result = result.convert("RGBA")
        result.putalpha(alpha_up)

    return result


def make_meta(guid: str) -> str:
    return f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 4096
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 4096
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def main():
    print("Loading model...", flush=True)
    model = load_model()
    print("Model loaded.", flush=True)

    for sheet in SHEETS:
        png_path = os.path.join(ASSETS_ENEMY, f"{sheet}.png")
        meta_path = png_path + ".meta"
        frames = parse_frames(meta_path)
        print(f"{sheet}: {len(frames)} frames parsed", flush=True)

        src = Image.open(png_path)
        src_h = src.height

        out_dir = os.path.join(ASSETS_ENEMY, sheet)
        os.makedirs(out_dir, exist_ok=True)

        for i, fr in enumerate(frames):
            x, y, w, h = fr["x"], fr["y"], fr["w"], fr["h"]
            # Unity sprite rect Y is measured from the BOTTOM of the texture; PIL crop uses top-left origin.
            top = src_h - (y + h)
            crop = src.crop((int(x), int(top), int(x + w), int(top + h)))

            longest = max(crop.size)
            if longest >= 350:
                # Already large enough to look reasonably sharp; the AI model gets very slow (minutes/frame on CPU)
                # past this size for little visible gain, and plain resize causes ringing artifacts on this flat-color art.
                upscaled = crop
            else:
                upscaled = upscale(model, crop)

            idx = int(fr["name"].rsplit("_", 1)[1])
            out_name = f"{sheet}_{idx:03d}.png"
            out_path = os.path.join(out_dir, out_name)
            upscaled.save(out_path)

            guid = uuid.uuid4().hex
            with open(out_path + ".meta", "w", encoding="utf-8") as mf:
                mf.write(make_meta(guid))

            print(f"  [{i + 1}/{len(frames)}] {out_name} -> {upscaled.size}", flush=True)

    print("Done.", flush=True)


if __name__ == "__main__":
    main()
