# Production Audio Sources

The files in this folder are the current consistency and implementation baseline,
not the final soundtrack approval list.

## BGM

| File | Source | Intended role |
| --- | --- | --- |
| `roam-tyhosi-sparrow.ogg` | https://opengameart.org/content/tyhosi-sparrow | field exploration |
| `event-orien.ogg` | https://opengameart.org/content/orien | event and dialogue |
| `battle-oriented.ogg` | https://opengameart.org/content/oriented | normal battle and prototype boss battle |

The planning guide labels these as CC0 baseline tracks. Re-check the license and
author information on each linked source page before a public build, then add the
required credits to the shipped credits screen.

## SFX

- Source package: `ProjectAssetGuide/public/consistency/audio/sfx`
- Contents: 12 normalized card, combat, reward, navigation, and footstep cues.
- Planning references: Kenney RPG Audio, OpenGameArt, and the consistency guide's processed baseline pack.

The Unity importer streams BGM and keeps short SFX compressed in memory. Runtime
playback must go through `AudioManager` and cue assets so scene objects do not own
mixing policy independently.
