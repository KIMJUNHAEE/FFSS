using System;
using UnityEditor;
using UnityEngine;

namespace CardBattle.EditorTools
{
    public sealed class ClockworkTimekeeperAssetPostprocessor : AssetPostprocessor
    {
        private const string CharacterRoot = "Assets/Characters/ClockworkTimekeeper/";

        private void OnPreprocessTexture()
        {
            if (!IsClockworkAsset(assetPath))
                return;

            var textureImporter = (TextureImporter)assetImporter;
            string fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);

            if (ContainsIgnoreCase(fileName, "Normal"))
            {
                textureImporter.textureType = TextureImporterType.NormalMap;
                textureImporter.sRGBTexture = false;
            }
            else if (ContainsIgnoreCase(fileName, "Metallic") ||
                     ContainsIgnoreCase(fileName, "Roughness"))
            {
                textureImporter.textureType = TextureImporterType.Default;
                textureImporter.sRGBTexture = false;
            }
            else
            {
                textureImporter.textureType = TextureImporterType.Default;
                textureImporter.sRGBTexture = true;
            }
        }

        private void OnPreprocessModel()
        {
            if (!IsClockworkAsset(assetPath))
                return;

            var modelImporter = (ModelImporter)assetImporter;
            modelImporter.animationType = ModelImporterAnimationType.Generic;
            modelImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            modelImporter.importAnimation = true;
            modelImporter.importCameras = false;
            modelImporter.importLights = false;
            modelImporter.materialImportMode = ModelImporterMaterialImportMode.None;
            modelImporter.optimizeGameObjects = false;
            modelImporter.animationCompression = ModelImporterAnimationCompression.Optimal;
            modelImporter.resampleCurves = true;
            modelImporter.removeConstantScaleCurves = true;

            ConfigureAnimationClips(modelImporter);
        }

        private static bool IsClockworkAsset(string path)
        {
            return path.StartsWith(CharacterRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static void ConfigureAnimationClips(ModelImporter modelImporter)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(modelImporter.assetPath);
            string clipName = null;

            if (ContainsIgnoreCase(fileName, "idle"))
                clipName = "Idle";
            else if (ContainsIgnoreCase(fileName, "walk"))
                clipName = "Walk";

            if (clipName == null)
                return;

            ModelImporterClipAnimation[] clips = modelImporter.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].name = clips.Length == 1 ? clipName : $"{clipName}_{i + 1}";
                clips[i].loopTime = true;
                clips[i].loopPose = true;
                clips[i].wrapMode = WrapMode.Loop;
                clips[i].lockRootRotation = true;
                clips[i].lockRootHeightY = true;
                clips[i].lockRootPositionXZ = true;
                clips[i].keepOriginalOrientation = true;
                clips[i].keepOriginalPositionY = true;
                clips[i].keepOriginalPositionXZ = true;
                clips[i].heightFromFeet = true;
            }

            modelImporter.clipAnimations = clips;
        }

        private static bool ContainsIgnoreCase(string value, string part)
        {
            return value?.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
