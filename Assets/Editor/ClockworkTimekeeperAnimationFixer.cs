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
        private const string IdleSourcePath = CharacterDir + "/Player_idle_2.fbx";
        private const string WalkSourcePath = CharacterDir + "/Player_walk.fbx";
        private const string IdleClipPath = CharacterDir + "/ClockworkTimekeeper_Idle.anim";
        private const string WalkClipPath = CharacterDir + "/ClockworkTimekeeper_Walk_InPlace.anim";
        private const string ControllerPath = CharacterDir + "/ClockworkTimekeeper.controller";
        private const string PrefabPath = "Assets/Prefabs/ClockworkTimekeeperPlayer.prefab";
        private const string FixerScriptPath = "Assets/Editor/ClockworkTimekeeperAnimationFixer.cs";

        private const string SpeedParameter = "Speed";
        private const string IdleStateName = "Idle";
        private const string WalkStateName = "Walk";
        private const string ModelSkeletonRootPath = "Armature";
        private const string AnimationRootPath = "";
        private const float WalkSpeedThreshold = 0.05f;
        private const float TransitionDuration = 0.12f;

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

            AnimationClip fixedWalk = CreateFixedClip(
                WalkSourcePath,
                WalkClipPath,
                "Walk",
                true,
                null);

            AnimationClip fixedIdle = CreateFixedClip(
                IdleSourcePath,
                IdleClipPath,
                "Idle",
                true,
                ModelSkeletonRootPath);

            CopyTransformCurves(fixedWalk, fixedIdle, ModelSkeletonRootPath);
            ConvertTransformToReferenceParentSpace(fixedIdle, ModelSkeletonRootPath + "/Hips", fixedWalk, ModelSkeletonRootPath);
            MatchInitialTransformRotation(fixedIdle, fixedWalk, ModelSkeletonRootPath + "/Hips");
            MatchInitialTransformPosition(fixedIdle, fixedWalk, ModelSkeletonRootPath + "/Hips");
            UpdateAnimatorController(fixedIdle, fixedWalk);
            UpdatePlayerPrefabDefaults();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ClockworkTimekeeperAnimationFixer] Created looping in-place idle/walk clips, aligned Hips, and updated the explicit Animator state machine.");
        }

        private static bool CanFixAssets()
        {
            return File.Exists(ToProjectAbsolutePath(IdleSourcePath)) &&
                   File.Exists(ToProjectAbsolutePath(WalkSourcePath));
        }

        private static bool NeedsFix()
        {
            if (!File.Exists(ToProjectAbsolutePath(IdleClipPath)) ||
                !File.Exists(ToProjectAbsolutePath(WalkClipPath)))
                return true;

            if (!File.Exists(ToProjectAbsolutePath(ControllerPath)))
                return true;

            return File.GetLastWriteTimeUtc(ToProjectAbsolutePath(IdleSourcePath)) > File.GetLastWriteTimeUtc(ToProjectAbsolutePath(IdleClipPath)) ||
                   File.GetLastWriteTimeUtc(ToProjectAbsolutePath(WalkSourcePath)) > File.GetLastWriteTimeUtc(ToProjectAbsolutePath(WalkClipPath)) ||
                   File.GetLastWriteTimeUtc(ToProjectAbsolutePath(FixerScriptPath)) > File.GetLastWriteTimeUtc(ToProjectAbsolutePath(IdleClipPath)) ||
                   File.GetLastWriteTimeUtc(ToProjectAbsolutePath(FixerScriptPath)) > File.GetLastWriteTimeUtc(ToProjectAbsolutePath(WalkClipPath));
        }

        private static AnimationClip CreateFixedClip(
            string sourcePath,
            string outputPath,
            string clipName,
            bool stripRootPosition,
            string transformPathPrefix)
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
            fixedClip.name = Path.GetFileNameWithoutExtension(outputPath);
            fixedClip.wrapMode = WrapMode.Loop;

            ConfigureClipLooping(fixedClip);

            if (stripRootPosition)
                StripRootPositionCurves(fixedClip);

            PrefixExistingTransformCurvePaths(fixedClip, transformPathPrefix);
            fixedClip.EnsureQuaternionContinuity();

            if (createAsset)
                AssetDatabase.CreateAsset(fixedClip, outputPath);

            EditorUtility.SetDirty(fixedClip);
            return fixedClip;
        }

        private static void CopyCurves(
            AnimationClip sourceClip,
            AnimationClip targetClip,
            bool stripRootPosition,
            string transformPathPrefix)
        {
            foreach (EditorCurveBinding sourceBinding in AnimationUtility.GetCurveBindings(sourceClip))
            {
                if (!ShouldCopyCurve(sourceBinding, stripRootPosition))
                    continue;

                EditorCurveBinding targetBinding = RemapBindingPath(sourceBinding, transformPathPrefix);
                AnimationCurve curve = AnimationUtility.GetEditorCurve(sourceClip, sourceBinding);
                AnimationUtility.SetEditorCurve(targetClip, targetBinding, curve);
            }

            foreach (EditorCurveBinding sourceBinding in AnimationUtility.GetObjectReferenceCurveBindings(sourceClip))
            {
                if (!ShouldCopyCurve(sourceBinding, stripRootPosition))
                    continue;

                EditorCurveBinding targetBinding = RemapBindingPath(sourceBinding, transformPathPrefix);
                ObjectReferenceKeyframe[] curve = AnimationUtility.GetObjectReferenceCurve(sourceClip, sourceBinding);
                AnimationUtility.SetObjectReferenceCurve(targetClip, targetBinding, curve);
            }
        }

        private static bool ShouldCopyCurve(EditorCurveBinding binding, bool stripRootPosition)
        {
            if (binding.type != typeof(Transform))
                return true;

            if (binding.path == AnimationRootPath)
                return false;

            return !stripRootPosition || !IsRootMotionPositionBinding(binding);
        }

        private static EditorCurveBinding RemapBindingPath(EditorCurveBinding binding, string transformPathPrefix)
        {
            if (binding.type != typeof(Transform) || !NeedsPathPrefix(binding.path, transformPathPrefix))
                return binding;

            EditorCurveBinding remappedBinding = binding;
            remappedBinding.path = transformPathPrefix + "/" + binding.path;
            return remappedBinding;
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

        private static void StripRootPositionCurves(AnimationClip clip)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type == typeof(Transform) && IsRootMotionPositionBinding(binding))
                    AnimationUtility.SetEditorCurve(clip, binding, null);
            }
        }

        private static void PrefixExistingTransformCurvePaths(AnimationClip clip, string pathPrefix)
        {
            if (string.IsNullOrWhiteSpace(pathPrefix))
                return;

            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type != typeof(Transform) || !NeedsPathPrefix(binding.path, pathPrefix))
                    continue;

                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                EditorCurveBinding remappedBinding = binding;
                remappedBinding.path = pathPrefix + "/" + binding.path;
                AnimationUtility.SetEditorCurve(clip, binding, null);
                AnimationUtility.SetEditorCurve(clip, remappedBinding, curve);
            }

            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (binding.type != typeof(Transform) || !NeedsPathPrefix(binding.path, pathPrefix))
                    continue;

                ObjectReferenceKeyframe[] curve = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                EditorCurveBinding remappedBinding = binding;
                remappedBinding.path = pathPrefix + "/" + binding.path;
                AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
                AnimationUtility.SetObjectReferenceCurve(clip, remappedBinding, curve);
            }
        }

        private static bool IsRotationBinding(string propertyName)
        {
            return propertyName.StartsWith("m_LocalRotation.", System.StringComparison.Ordinal) ||
                   propertyName.StartsWith("localEulerAngles.", System.StringComparison.Ordinal);
        }

        private static bool NeedsPathPrefix(string path, string pathPrefix)
        {
            if (string.IsNullOrWhiteSpace(pathPrefix))
                return false;

            return !string.IsNullOrWhiteSpace(path) &&
                   !path.Equals(pathPrefix, System.StringComparison.OrdinalIgnoreCase) &&
                   !path.StartsWith(pathPrefix + "/", System.StringComparison.OrdinalIgnoreCase);
        }

        private static void CopyTransformCurves(AnimationClip sourceClip, AnimationClip targetClip, string transformPath)
        {
            if (sourceClip == null || targetClip == null || string.IsNullOrWhiteSpace(transformPath))
                return;

            foreach (EditorCurveBinding sourceBinding in AnimationUtility.GetCurveBindings(sourceClip))
            {
                if (sourceBinding.type != typeof(Transform) || sourceBinding.path != transformPath)
                    continue;

                AnimationCurve curve = AnimationUtility.GetEditorCurve(sourceClip, sourceBinding);
                AnimationUtility.SetEditorCurve(targetClip, sourceBinding, curve);
            }
        }

        private static void ConvertTransformToReferenceParentSpace(
            AnimationClip targetClip,
            string transformPath,
            AnimationClip parentReferenceClip,
            string parentPath)
        {
            if (targetClip == null || parentReferenceClip == null)
                return;

            if (!TryGetRotationCurves(parentReferenceClip, parentPath, out AnimationCurve parentX, out AnimationCurve parentY, out AnimationCurve parentZ, out AnimationCurve parentW))
                return;

            Quaternion inverseParentRotation = Quaternion.Inverse(EvaluateQuaternion(parentX, parentY, parentZ, parentW, 0f));
            ConvertPositionToParentSpace(targetClip, transformPath, inverseParentRotation);
            ConvertRotationToParentSpace(targetClip, transformPath, inverseParentRotation);
            EditorUtility.SetDirty(targetClip);
        }

        private static void ConvertPositionToParentSpace(AnimationClip targetClip, string transformPath, Quaternion inverseParentRotation)
        {
            if (!TryGetPositionCurves(targetClip, transformPath, out AnimationCurve xCurve, out AnimationCurve yCurve, out AnimationCurve zCurve))
                return;

            AnimationCurve convertedX = new();
            AnimationCurve convertedY = new();
            AnimationCurve convertedZ = new();

            foreach (float time in GetPositionSampleTimes(xCurve, yCurve, zCurve))
            {
                Vector3 converted = inverseParentRotation * EvaluateVector3(xCurve, yCurve, zCurve, time);
                convertedX.AddKey(time, converted.x);
                convertedY.AddKey(time, converted.y);
                convertedZ.AddKey(time, converted.z);
            }

            SetPositionCurve(targetClip, transformPath, "x", convertedX);
            SetPositionCurve(targetClip, transformPath, "y", convertedY);
            SetPositionCurve(targetClip, transformPath, "z", convertedZ);
        }

        private static void ConvertRotationToParentSpace(AnimationClip targetClip, string transformPath, Quaternion inverseParentRotation)
        {
            if (!TryGetRotationCurves(targetClip, transformPath, out AnimationCurve xCurve, out AnimationCurve yCurve, out AnimationCurve zCurve, out AnimationCurve wCurve))
                return;

            AnimationCurve convertedX = new();
            AnimationCurve convertedY = new();
            AnimationCurve convertedZ = new();
            AnimationCurve convertedW = new();

            foreach (float time in GetRotationSampleTimes(xCurve, yCurve, zCurve, wCurve))
            {
                Quaternion converted = Normalize(inverseParentRotation * EvaluateQuaternion(xCurve, yCurve, zCurve, wCurve, time));
                convertedX.AddKey(time, converted.x);
                convertedY.AddKey(time, converted.y);
                convertedZ.AddKey(time, converted.z);
                convertedW.AddKey(time, converted.w);
            }

            SetRotationCurve(targetClip, transformPath, "x", convertedX);
            SetRotationCurve(targetClip, transformPath, "y", convertedY);
            SetRotationCurve(targetClip, transformPath, "z", convertedZ);
            SetRotationCurve(targetClip, transformPath, "w", convertedW);
            RemoveEulerCurves(targetClip, transformPath);
        }

        private static void MatchInitialTransformRotation(
            AnimationClip targetClip,
            AnimationClip referenceClip,
            string transformPath)
        {
            if (targetClip == null || referenceClip == null)
                return;

            if (!TryGetRotationCurves(targetClip, transformPath, out AnimationCurve targetX, out AnimationCurve targetY, out AnimationCurve targetZ, out AnimationCurve targetW) ||
                !TryGetRotationCurves(referenceClip, transformPath, out AnimationCurve referenceX, out AnimationCurve referenceY, out AnimationCurve referenceZ, out AnimationCurve referenceW))
            {
                return;
            }

            Quaternion targetStart = EvaluateQuaternion(targetX, targetY, targetZ, targetW, 0f);
            Quaternion referenceStart = EvaluateQuaternion(referenceX, referenceY, referenceZ, referenceW, 0f);
            Quaternion correction = referenceStart * Quaternion.Inverse(targetStart);

            AnimationCurve correctedX = new();
            AnimationCurve correctedY = new();
            AnimationCurve correctedZ = new();
            AnimationCurve correctedW = new();

            foreach (float time in GetRotationSampleTimes(targetX, targetY, targetZ, targetW))
            {
                Quaternion source = EvaluateQuaternion(targetX, targetY, targetZ, targetW, time);
                Quaternion corrected = Normalize(correction * source);
                correctedX.AddKey(time, corrected.x);
                correctedY.AddKey(time, corrected.y);
                correctedZ.AddKey(time, corrected.z);
                correctedW.AddKey(time, corrected.w);
            }

            SetRotationCurve(targetClip, transformPath, "x", correctedX);
            SetRotationCurve(targetClip, transformPath, "y", correctedY);
            SetRotationCurve(targetClip, transformPath, "z", correctedZ);
            SetRotationCurve(targetClip, transformPath, "w", correctedW);
            RemoveEulerCurves(targetClip, transformPath);
            EditorUtility.SetDirty(targetClip);
        }

        private static void MatchInitialTransformPosition(
            AnimationClip targetClip,
            AnimationClip referenceClip,
            string transformPath)
        {
            if (targetClip == null || referenceClip == null)
                return;

            if (!TryGetPositionCurves(targetClip, transformPath, out AnimationCurve targetX, out AnimationCurve targetY, out AnimationCurve targetZ) ||
                !TryGetPositionCurves(referenceClip, transformPath, out AnimationCurve referenceX, out AnimationCurve referenceY, out AnimationCurve referenceZ))
            {
                return;
            }

            Vector3 targetStart = EvaluateVector3(targetX, targetY, targetZ, 0f);
            Vector3 referenceStart = EvaluateVector3(referenceX, referenceY, referenceZ, 0f);
            Vector3 offset = referenceStart - targetStart;

            AnimationCurve correctedX = new();
            AnimationCurve correctedY = new();
            AnimationCurve correctedZ = new();

            foreach (float time in GetPositionSampleTimes(targetX, targetY, targetZ))
            {
                Vector3 corrected = EvaluateVector3(targetX, targetY, targetZ, time) + offset;
                correctedX.AddKey(time, corrected.x);
                correctedY.AddKey(time, corrected.y);
                correctedZ.AddKey(time, corrected.z);
            }

            SetPositionCurve(targetClip, transformPath, "x", correctedX);
            SetPositionCurve(targetClip, transformPath, "y", correctedY);
            SetPositionCurve(targetClip, transformPath, "z", correctedZ);
            EditorUtility.SetDirty(targetClip);
        }

        private static bool TryGetRotationCurves(
            AnimationClip clip,
            string transformPath,
            out AnimationCurve xCurve,
            out AnimationCurve yCurve,
            out AnimationCurve zCurve,
            out AnimationCurve wCurve)
        {
            xCurve = GetRotationCurve(clip, transformPath, "x");
            yCurve = GetRotationCurve(clip, transformPath, "y");
            zCurve = GetRotationCurve(clip, transformPath, "z");
            wCurve = GetRotationCurve(clip, transformPath, "w");

            return xCurve != null && yCurve != null && zCurve != null && wCurve != null;
        }

        private static AnimationCurve GetRotationCurve(AnimationClip clip, string transformPath, string axis)
        {
            return AnimationUtility.GetEditorCurve(clip, CreateRotationBinding(transformPath, axis));
        }

        private static void SetRotationCurve(AnimationClip clip, string transformPath, string axis, AnimationCurve curve)
        {
            AnimationUtility.SetEditorCurve(clip, CreateRotationBinding(transformPath, axis), curve);
        }

        private static EditorCurveBinding CreateRotationBinding(string transformPath, string axis)
        {
            return new EditorCurveBinding
            {
                path = transformPath,
                type = typeof(Transform),
                propertyName = "m_LocalRotation." + axis
            };
        }

        private static bool TryGetPositionCurves(
            AnimationClip clip,
            string transformPath,
            out AnimationCurve xCurve,
            out AnimationCurve yCurve,
            out AnimationCurve zCurve)
        {
            xCurve = GetPositionCurve(clip, transformPath, "x");
            yCurve = GetPositionCurve(clip, transformPath, "y");
            zCurve = GetPositionCurve(clip, transformPath, "z");

            return xCurve != null && yCurve != null && zCurve != null;
        }

        private static AnimationCurve GetPositionCurve(AnimationClip clip, string transformPath, string axis)
        {
            return AnimationUtility.GetEditorCurve(clip, CreatePositionBinding(transformPath, axis));
        }

        private static void SetPositionCurve(AnimationClip clip, string transformPath, string axis, AnimationCurve curve)
        {
            AnimationUtility.SetEditorCurve(clip, CreatePositionBinding(transformPath, axis), curve);
        }

        private static EditorCurveBinding CreatePositionBinding(string transformPath, string axis)
        {
            return new EditorCurveBinding
            {
                path = transformPath,
                type = typeof(Transform),
                propertyName = "m_LocalPosition." + axis
            };
        }

        private static float[] GetRotationSampleTimes(params AnimationCurve[] curves)
        {
            List<float> times = new();
            foreach (AnimationCurve curve in curves)
            {
                foreach (Keyframe key in curve.keys)
                    times.Add(key.time);
            }

            return times.Distinct().OrderBy(time => time).ToArray();
        }

        private static float[] GetPositionSampleTimes(params AnimationCurve[] curves)
        {
            List<float> times = new();
            foreach (AnimationCurve curve in curves)
            {
                foreach (Keyframe key in curve.keys)
                    times.Add(key.time);
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
            return Normalize(new Quaternion(
                xCurve.Evaluate(time),
                yCurve.Evaluate(time),
                zCurve.Evaluate(time),
                wCurve.Evaluate(time)));
        }

        private static Vector3 EvaluateVector3(
            AnimationCurve xCurve,
            AnimationCurve yCurve,
            AnimationCurve zCurve,
            float time)
        {
            return new Vector3(
                xCurve.Evaluate(time),
                yCurve.Evaluate(time),
                zCurve.Evaluate(time));
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

        private static void RemoveEulerCurves(AnimationClip clip, string transformPath)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type == typeof(Transform) &&
                    binding.path == transformPath &&
                    binding.propertyName.StartsWith("localEulerAngles.", System.StringComparison.Ordinal))
                {
                    AnimationUtility.SetEditorCurve(clip, binding, null);
                }
            }
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
            ClearStateMachine(stateMachine);
            RemoveUnusedBlendTrees();

            AnimatorState idleState = stateMachine.AddState(IdleStateName, new Vector3(280f, 80f, 0f));
            idleState.motion = idleClip;
            idleState.writeDefaultValues = true;

            AnimatorState walkState = stateMachine.AddState(WalkStateName, new Vector3(280f, 190f, 0f));
            walkState.motion = walkClip;
            walkState.writeDefaultValues = true;

            AnimatorStateTransition idleToWalk = idleState.AddTransition(walkState);
            ConfigureSpeedTransition(idleToWalk, AnimatorConditionMode.Greater, WalkSpeedThreshold);

            AnimatorStateTransition walkToIdle = walkState.AddTransition(idleState);
            ConfigureSpeedTransition(walkToIdle, AnimatorConditionMode.Less, WalkSpeedThreshold);

            stateMachine.defaultState = idleState;
            EditorUtility.SetDirty(idleState);
            EditorUtility.SetDirty(walkState);
            EditorUtility.SetDirty(controller);
        }

        private static void ClearStateMachine(AnimatorStateMachine stateMachine)
        {
            foreach (ChildAnimatorState child in stateMachine.states.ToArray())
                stateMachine.RemoveState(child.state);

            foreach (ChildAnimatorStateMachine child in stateMachine.stateMachines.ToArray())
                stateMachine.RemoveStateMachine(child.stateMachine);

            foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions.ToArray())
                stateMachine.RemoveAnyStateTransition(transition);

            foreach (AnimatorTransition transition in stateMachine.entryTransitions.ToArray())
                stateMachine.RemoveEntryTransition(transition);
        }

        private static void RemoveUnusedBlendTrees()
        {
            foreach (Object subAsset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
            {
                if (subAsset is BlendTree blendTree)
                    Object.DestroyImmediate(blendTree, true);
            }
        }

        private static void ConfigureSpeedTransition(
            AnimatorStateTransition transition,
            AnimatorConditionMode conditionMode,
            float threshold)
        {
            transition.hasExitTime = false;
            transition.exitTime = 0f;
            transition.hasFixedDuration = true;
            transition.duration = TransitionDuration;
            transition.offset = 0f;
            transition.AddCondition(conditionMode, threshold, SpeedParameter);
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
                    ClockworkTimekeeperEditorUtils.SetFloat(mover, "walkStopGraceTime", 0.08f);
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
