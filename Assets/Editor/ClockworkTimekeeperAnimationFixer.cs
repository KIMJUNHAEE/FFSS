using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardBattle.Exploration;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CardBattle.EditorTools
{
    public static class ClockworkTimekeeperAnimationFixer
    {
        private const string CharacterDir = "Assets/Characters/ClockworkTimekeeper";
        private const string IdleSourcePath = CharacterDir + "/Player_idle.fbx";
        private const string WalkSourcePath = CharacterDir + "/Player_walk.fbx";
        private const string FixedIdlePath = CharacterDir + "/ClockworkTimekeeper_Idle_Fixed.anim";
        private const string FixedWalkPath = CharacterDir + "/ClockworkTimekeeper_Walk_InPlace.anim";
        private const string ControllerPath = CharacterDir + "/ClockworkTimekeeper.controller";
        private const string PrefabPath = "Assets/Prefabs/ClockworkTimekeeperPlayer.prefab";
        private const string FixerScriptPath = "Assets/Editor/ClockworkTimekeeperAnimationFixer.cs";

        private const string SpeedParameter = "Speed";
        private const string LocomotionStateName = "Locomotion";
        private const string BlendTreeName = "SpeedBlend";

        [InitializeOnLoadMethod]
        private static void FixAfterEditorRefresh()
        {
            EditorApplication.delayCall += () =>
            {
                if (CanFixAssets() && NeedsFix())
                    FixAnimationAssets();
            };
        }

        [MenuItem("Card Battle/Exploration/Fix Clockwork Timekeeper Animation Imports")]
        public static void FixAnimationAssets()
        {
            if (!CanFixAssets())
            {
                Debug.LogWarning("[ClockworkTimekeeperAnimationFixer] Clockwork animation source assets were not found.");
                return;
            }

            AssetDatabase.ImportAsset(IdleSourcePath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(WalkSourcePath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            AnimationClip fixedIdle = CreateFixedClip(
                IdleSourcePath,
                FixedIdlePath,
                "Idle",
                Quaternion.Euler(90f, 0f, 0f),
                true);

            AnimationClip fixedWalk = CreateFixedClip(
                WalkSourcePath,
                FixedWalkPath,
                "Walk",
                Quaternion.identity,
                true);

            UpdateAnimatorController(fixedIdle, fixedWalk);
            UpdatePlayerPrefabDefaults();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ClockworkTimekeeperAnimationFixer] Created fixed looping in-place animation clips and updated the controller.");
        }

        private static bool CanFixAssets()
        {
            return File.Exists(ToProjectAbsolutePath(IdleSourcePath)) &&
                   File.Exists(ToProjectAbsolutePath(WalkSourcePath));
        }

        private static bool NeedsFix()
        {
            if (!File.Exists(ToProjectAbsolutePath(FixedIdlePath)) ||
                !File.Exists(ToProjectAbsolutePath(FixedWalkPath)))
                return true;

            if (!File.Exists(ToProjectAbsolutePath(ControllerPath)))
                return true;

            return File.GetLastWriteTimeUtc(ToProjectAbsolutePath(IdleSourcePath)) > File.GetLastWriteTimeUtc(ToProjectAbsolutePath(FixedIdlePath)) ||
                   File.GetLastWriteTimeUtc(ToProjectAbsolutePath(WalkSourcePath)) > File.GetLastWriteTimeUtc(ToProjectAbsolutePath(FixedWalkPath)) ||
                   File.GetLastWriteTimeUtc(ToProjectAbsolutePath(FixerScriptPath)) > File.GetLastWriteTimeUtc(ToProjectAbsolutePath(FixedWalkPath));
        }

        private static AnimationClip CreateFixedClip(
            string sourcePath,
            string outputPath,
            string clipName,
            Quaternion rootRotationCorrection,
            bool stripRootPosition)
        {
            AnimationClip sourceClip = LoadSourceClip(sourcePath);
            if (sourceClip == null)
            {
                Debug.LogWarning($"[ClockworkTimekeeperAnimationFixer] No usable clip found at {sourcePath}.");
                return null;
            }

            AnimationClip fixedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
            bool createAsset = fixedClip == null;
            if (createAsset)
                fixedClip = new AnimationClip();

            EditorUtility.CopySerialized(sourceClip, fixedClip);
            fixedClip.name = clipName;
            fixedClip.wrapMode = WrapMode.Loop;

            ConfigureClipLooping(fixedClip);

            if (stripRootPosition)
                StripRootPositionCurves(fixedClip);

            ApplyRootRotationCorrection(fixedClip, rootRotationCorrection);

            fixedClip.EnsureQuaternionContinuity();

            if (createAsset)
                AssetDatabase.CreateAsset(fixedClip, outputPath);

            EditorUtility.SetDirty(fixedClip);
            return fixedClip;
        }

        private static AnimationClip LoadSourceClip(string sourcePath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(sourcePath)
                .OfType<AnimationClip>()
                .Where(ClockworkTimekeeperEditorUtils.IsUsableClip)
                .OrderBy(clip => clip.name.Contains("__") ? 1 : 0)
                .FirstOrDefault();
        }

        private static void ConfigureClipLooping(AnimationClip clip)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.loopBlend = true;
            settings.loopBlendOrientation = true;
            settings.loopBlendPositionY = true;
            settings.loopBlendPositionXZ = true;
            settings.keepOriginalOrientation = true;
            settings.keepOriginalPositionY = true;
            settings.keepOriginalPositionXZ = true;
            settings.heightFromFeet = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        private static void StripRootPositionCurves(AnimationClip clip)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type != typeof(Transform) ||
                    !IsRootMotionPositionBinding(binding))
                {
                    continue;
                }

                AnimationUtility.SetEditorCurve(clip, binding, null);
            }
        }

        private static bool IsRootMotionPositionBinding(EditorCurveBinding binding)
        {
            if (!binding.propertyName.StartsWith("m_LocalPosition.", System.StringComparison.Ordinal))
                return false;

            if (binding.path.Length == 0)
                return true;

            if (binding.path.Contains("/"))
                return false;

            return binding.path.IndexOf("armature", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   binding.path.IndexOf("root", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ApplyRootRotationCorrection(AnimationClip clip, Quaternion correction)
        {
            const string rootPath = "";
            AnimationCurve xCurve = GetRootRotationCurve(clip, rootPath, "x");
            AnimationCurve yCurve = GetRootRotationCurve(clip, rootPath, "y");
            AnimationCurve zCurve = GetRootRotationCurve(clip, rootPath, "z");
            AnimationCurve wCurve = GetRootRotationCurve(clip, rootPath, "w");

            float[] sampleTimes = GetRotationSampleTimes(clip, xCurve, yCurve, zCurve, wCurve);
            AnimationCurve newX = new AnimationCurve();
            AnimationCurve newY = new AnimationCurve();
            AnimationCurve newZ = new AnimationCurve();
            AnimationCurve newW = new AnimationCurve();

            foreach (float time in sampleTimes)
            {
                Quaternion source = EvaluateQuaternion(xCurve, yCurve, zCurve, wCurve, time);
                Quaternion fixedRotation = correction * source;
                newX.AddKey(time, fixedRotation.x);
                newY.AddKey(time, fixedRotation.y);
                newZ.AddKey(time, fixedRotation.z);
                newW.AddKey(time, fixedRotation.w);
            }

            SetRootRotationCurve(clip, rootPath, "x", newX);
            SetRootRotationCurve(clip, rootPath, "y", newY);
            SetRootRotationCurve(clip, rootPath, "z", newZ);
            SetRootRotationCurve(clip, rootPath, "w", newW);
        }

        private static AnimationCurve GetRootRotationCurve(AnimationClip clip, string path, string axis)
        {
            EditorCurveBinding binding = CreateRootRotationBinding(path, axis);
            return AnimationUtility.GetEditorCurve(clip, binding);
        }

        private static void SetRootRotationCurve(AnimationClip clip, string path, string axis, AnimationCurve curve)
        {
            EditorCurveBinding binding = CreateRootRotationBinding(path, axis);
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        private static EditorCurveBinding CreateRootRotationBinding(string path, string axis)
        {
            return new EditorCurveBinding
            {
                path = path,
                type = typeof(Transform),
                propertyName = "m_LocalRotation." + axis
            };
        }

        private static float[] GetRotationSampleTimes(
            AnimationClip clip,
            params AnimationCurve[] curves)
        {
            List<float> times = new List<float>();
            foreach (AnimationCurve curve in curves)
            {
                if (curve == null)
                    continue;

                foreach (Keyframe key in curve.keys)
                    times.Add(key.time);
            }

            if (times.Count == 0)
            {
                times.Add(0f);
                times.Add(Mathf.Max(clip.length, 0.0166667f));
            }

            return times.Distinct().OrderBy(time => time).ToArray();
        }

        private static Quaternion EvaluateQuaternion(
            AnimationCurve xCurve,
            AnimationCurve yCurve,
            AnimationCurve zCurve,
            AnimationCurve wCurve,
            float time)
        {
            Quaternion rotation = new Quaternion(
                xCurve != null ? xCurve.Evaluate(time) : 0f,
                yCurve != null ? yCurve.Evaluate(time) : 0f,
                zCurve != null ? zCurve.Evaluate(time) : 0f,
                wCurve != null ? wCurve.Evaluate(time) : 1f);

            return Normalize(rotation);
        }

        private static Quaternion Normalize(Quaternion rotation)
        {
            float length = Mathf.Sqrt(
                rotation.x * rotation.x +
                rotation.y * rotation.y +
                rotation.z * rotation.z +
                rotation.w * rotation.w);

            if (length <= 0.0001f)
                return Quaternion.identity;

            float inverseLength = 1f / length;
            return new Quaternion(
                rotation.x * inverseLength,
                rotation.y * inverseLength,
                rotation.z * inverseLength,
                rotation.w * inverseLength);
        }

        private static void UpdateAnimatorController(AnimationClip idleClip, AnimationClip walkClip)
        {
            if (idleClip == null || walkClip == null)
                return;

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            if (controller.parameters.All(parameter => parameter.name != SpeedParameter))
                controller.AddParameter(SpeedParameter, AnimatorControllerParameterType.Float);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState locomotionState = stateMachine.states
                .Select(child => child.state)
                .FirstOrDefault(state => state.name == LocomotionStateName);

            if (locomotionState == null)
            {
                locomotionState = stateMachine.AddState(LocomotionStateName);
                stateMachine.defaultState = locomotionState;
            }

            BlendTree blendTree = locomotionState.motion as BlendTree;
            if (blendTree == null)
            {
                blendTree = new BlendTree { name = BlendTreeName };
                AssetDatabase.AddObjectToAsset(blendTree, controller);
                locomotionState.motion = blendTree;
            }

            blendTree.name = BlendTreeName;
            blendTree.blendType = BlendTreeType.Simple1D;
            blendTree.blendParameter = SpeedParameter;
            blendTree.useAutomaticThresholds = false;
            blendTree.children = new[]
            {
                new ChildMotion { motion = idleClip, threshold = 0f, timeScale = 1f },
                new ChildMotion { motion = walkClip, threshold = 1f, timeScale = 1f }
            };

            locomotionState.writeDefaultValues = true;
            stateMachine.defaultState = locomotionState;
            EditorUtility.SetDirty(blendTree);
            EditorUtility.SetDirty(controller);
        }

        private static void UpdatePlayerPrefabDefaults()
        {
            if (!File.Exists(ToProjectAbsolutePath(PrefabPath)))
                return;

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                QuarterViewPlayerController mover = root.GetComponent<QuarterViewPlayerController>();
                if (mover != null)
                {
                    ClockworkTimekeeperEditorUtils.SetBool(mover, "lockToGroundPlane", true);
                    ClockworkTimekeeperEditorUtils.SetFloat(mover, "groundY", 0f);
                    ClockworkTimekeeperEditorUtils.SetVector3(mover, "visualEulerOffset", Vector3.zero);
                    ClockworkTimekeeperEditorUtils.SetFloat(mover, "visualYawOffset", 0f);
                    ClockworkTimekeeperEditorUtils.SetFloat(mover, "animatorDampTime", 0f);
                }

                RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
                Animator animator = root.GetComponentInChildren<Animator>(true);
                if (animator != null && controller != null)
                {
                    animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    EditorUtility.SetDirty(animator);
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string ToProjectAbsolutePath(string assetPath)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        }

    }
}
