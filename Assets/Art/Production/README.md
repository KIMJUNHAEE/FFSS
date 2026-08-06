# Production Art Sources

This folder is the curated art pool for the new production scenes. Importing an
asset here does not make it approved for every screen; prefabs and scene-specific
data decide where it is used.

## UI Atlas

- Source: `C:/Users/kirby/OneDrive/바탕 화면/구구가가/웹게임_UI_신규/누끼완료_전체/ready_sliced_ui`
- Contents: 404 transparent PNG pieces grouped by their source package folders.
- Unity policy: Sprite, transparent alpha, no mipmaps, clamp wrapping, high-quality compression.
- Usage: assemble reusable UI prefabs from the pieces. Do not flatten whole screens into code-generated rectangles.

## Project Art

- Source: `ProjectAssetGuide/public/project`
- Contents: title treatment, backgrounds, tables, map nodes, character references, skill art, resources, and panel references.
- Usage: production reference pool. Existing combat scenes keep their current art until their copied production scene is migrated deliberately.

## VFX

- Source: `ProjectAssetGuide/public/consistency/vfx`
- Contents: nine transparent project-style VFX source plates.
- Usage: source sprites for prefab-based effects. Timing, particle separation, animation, and pooling are applied in Unity rather than baked into the scene.

Before release, verify ownership and redistribution rights for every source pack
against its original license record. This repository path records provenance but
is not a substitute for a release license audit.
