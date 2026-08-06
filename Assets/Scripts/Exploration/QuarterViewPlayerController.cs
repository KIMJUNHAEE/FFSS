using UnityEngine;
using UnityEngine.InputSystem;
using FFSS.Framework.Core;
using FFSS.Framework.Presentation.Audio;
using FFSS.Framework.UI;

namespace CardBattle.Exploration
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class QuarterViewPlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 4.5f;
        [SerializeField] private float turnSpeed = 720f;
        [SerializeField] private bool lockToGroundPlane = true;
        [SerializeField] private float groundY = 0f;
        [SerializeField] private bool constrainToWorldBounds = false;
        [SerializeField] private Vector2 worldBoundsMin = new(-34f, -20f);
        [SerializeField] private Vector2 worldBoundsMax = new(34f, 20f);
        [SerializeField] private HexTileMapGenerator walkableMap = null;

        [Header("View")]
        [SerializeField] private Transform cameraTransform = null;
        [SerializeField] private Transform visualRoot = null;
        [SerializeField] private Vector3 visualEulerOffset = Vector3.zero;
        [SerializeField] private float visualYawOffset = 0f;
        [SerializeField] private bool buildVisualWrapperOnAwake = true;

        [Header("Animation")]
        [SerializeField] private Animator animator = null;
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField] private float animatorDampTime = 0f;
        [SerializeField] private float walkStopGraceTime = 0.08f;

        [Header("Footstep audio")]
        [SerializeField] private bool playFootsteps = true;
        [SerializeField] private string[] footstepCueIds =
        {
            "sfx.footstep.stone.01",
            "sfx.footstep.stone.02"
        };
        [SerializeField, Min(0.1f)] private float footstepInterval = 0.42f;

        public const string HeadingRootName = "VisualRoot";
        public const string AxisCorrectionRootName = "AxisCorrectionRoot";
        private static readonly int IdleStateHash = Animator.StringToHash("Idle");
        private static readonly int WalkStateHash = Animator.StringToHash("Walk");

        private CharacterController controller;
        private int speedParameterHash;
        private Vector3 facingDirection = Vector3.forward;
        private Transform axisCorrectionRoot;
        private float lastMovementInputTime = -999f;
        private float nextFootstepAt;
        private int footstepCueIndex;

        public bool IsMoving { get; private set; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            speedParameterHash = Animator.StringToHash(speedParameter);

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (animator.HasState(0, IdleStateHash))
                    animator.Play(IdleStateHash, 0, 0f);
            }

            EnsureVisualWrapper();

            if (animator != null)
                animator.Rebind();
        }

        private void Start()
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            if (walkableMap == null)
                walkableMap = FindAnyObjectByType<HexTileMapGenerator>();

            if (animator != null)
            {
                if (animator.HasState(0, IdleStateHash))
                    animator.Play(IdleStateHash, 0, 0f);
                animator.Update(0f);
            }

            SnapToGroundPlane();
            AlignVisualToController();
            facingDirection = GetPlanarForward();
            ApplyVisualRotation(facingDirection, true);
        }

        private void Update()
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            Vector3 planarDirection = IsMovementBlocked() ? Vector3.zero : ReadCameraRelativeDirection();
            float inputAmount = Mathf.Clamp01(planarDirection.magnitude);
            float animationAmount = GetAnimationAmount(inputAmount);
            IsMoving = inputAmount > 0.05f;

            Move(planarDirection);
            TurnToward(planarDirection);
            UpdateAnimator(animationAmount);
            KeepNonLoopingWalkAlive(animationAmount);
            UpdateFootsteps();
        }

        private Vector3 ReadCameraRelativeDirection()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return Vector3.zero;

            Vector2 input = Vector2.zero;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;

            input = Vector2.ClampMagnitude(input, 1f);
            if (input.sqrMagnitude <= 0.0001f)
                return Vector3.zero;

            Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;

            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            return Vector3.ClampMagnitude((right * input.x) + (forward * input.y), 1f);
        }

        private void Move(Vector3 planarDirection)
        {
            Vector3 current = transform.position;
            Vector3 desired = current + planarDirection * moveSpeed * Time.deltaTime;
            Vector3 constrained = walkableMap != null
                ? walkableMap.ConstrainMovement(current, desired)
                : desired;
            controller.Move(constrained - current);

            if (constrainToWorldBounds)
                ClampToWorldBounds();

            if (lockToGroundPlane)
                SnapToGroundPlane();
        }

        private static bool IsMovementBlocked()
        {
            return GameKernel.IsReady &&
                   GameKernel.Services.TryGet(out UIManager ui) &&
                   ui.HasVisibleModal;
        }

        private void ClampToWorldBounds()
        {
            float minX = Mathf.Min(worldBoundsMin.x, worldBoundsMax.x);
            float maxX = Mathf.Max(worldBoundsMin.x, worldBoundsMax.x);
            float minZ = Mathf.Min(worldBoundsMin.y, worldBoundsMax.y);
            float maxZ = Mathf.Max(worldBoundsMin.y, worldBoundsMax.y);

            Vector3 position = transform.position;
            position.x = Mathf.Clamp(position.x, minX, maxX);
            position.z = Mathf.Clamp(position.z, minZ, maxZ);
            transform.position = position;
        }

        private void TurnToward(Vector3 planarDirection)
        {
            bool hasMovementInput = planarDirection.sqrMagnitude > 0.0001f;
            if (hasMovementInput)
                facingDirection = planarDirection.normalized;

            ApplyVisualRotation(facingDirection, !hasMovementInput);
        }

        private void ApplyVisualRotation(Vector3 planarDirection, bool snap)
        {
            Transform turnTarget = visualRoot != null ? visualRoot : transform;
            Quaternion targetRotation = Quaternion.LookRotation(planarDirection, Vector3.up) *
                                        Quaternion.Euler(visualEulerOffset + new Vector3(0f, visualYawOffset, 0f));

            if (snap)
            {
                turnTarget.rotation = targetRotation;
                return;
            }

            turnTarget.rotation = Quaternion.RotateTowards(
                turnTarget.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime);
        }

        private void EnsureVisualWrapper()
        {
            if (!buildVisualWrapperOnAwake || animator == null)
                return;

            Transform modelRoot = animator.transform;
            if (modelRoot == transform)
                return;

            if (TryUseExistingWrapper(modelRoot))
                return;

            Vector3 originalLocalPosition = modelRoot.localPosition;
            Quaternion originalLocalRotation = modelRoot.localRotation;
            Vector3 originalLocalScale = modelRoot.localScale;

            GameObject headingObject = new(HeadingRootName);
            Transform headingRoot = headingObject.transform;
            headingRoot.SetParent(transform, false);
            headingRoot.localPosition = Vector3.zero;
            headingRoot.localRotation = Quaternion.identity;
            headingRoot.localScale = Vector3.one;

            GameObject axisObject = new(AxisCorrectionRootName);
            axisCorrectionRoot = axisObject.transform;
            axisCorrectionRoot.SetParent(headingRoot, false);
            axisCorrectionRoot.localPosition = Vector3.zero;
            axisCorrectionRoot.localRotation = Quaternion.identity;
            axisCorrectionRoot.localScale = Vector3.one;

            modelRoot.SetParent(axisCorrectionRoot, false);
            modelRoot.localPosition = originalLocalPosition;
            modelRoot.localRotation = originalLocalRotation;
            modelRoot.localScale = originalLocalScale;

            visualRoot = headingRoot;
            visualEulerOffset = Vector3.zero;
            visualYawOffset = 0f;
        }

        private bool TryUseExistingWrapper(Transform modelRoot)
        {
            Transform parent = modelRoot.parent;
            if (parent == null || parent.name != AxisCorrectionRootName)
                return false;

            Transform headingRoot = parent.parent;
            if (headingRoot == null || headingRoot.name != HeadingRootName)
                return false;

            axisCorrectionRoot = parent;
            axisCorrectionRoot.localRotation = Quaternion.identity;
            visualRoot = headingRoot;
            visualEulerOffset = Vector3.zero;
            visualYawOffset = 0f;
            return true;
        }

        private void SnapToGroundPlane()
        {
            if (!lockToGroundPlane)
                return;

            Vector3 position = transform.position;
            if (Mathf.Abs(position.y - groundY) <= 0.001f)
                return;

            transform.position = new Vector3(position.x, groundY, position.z);
        }

        private void AlignVisualToController()
        {
            if (axisCorrectionRoot == null || animator == null)
                return;

            if (!ExplorationGeometryUtility.TryGetRendererBounds(animator.gameObject, out Bounds bounds))
                return;

            Vector3 correction = new(
                transform.position.x - bounds.center.x,
                transform.position.y - bounds.min.y,
                transform.position.z - bounds.center.z);

            axisCorrectionRoot.position += correction;
        }

        private Vector3 GetPlanarForward()
        {
            Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        private float GetAnimationAmount(float inputAmount)
        {
            if (inputAmount > 0.05f)
            {
                lastMovementInputTime = Time.time;
                return inputAmount;
            }

            return Time.time - lastMovementInputTime <= walkStopGraceTime ? 1f : 0f;
        }

        private void UpdateAnimator(float inputAmount)
        {
            if (animator == null || string.IsNullOrWhiteSpace(speedParameter))
                return;

            if (animatorDampTime > 0f)
                animator.SetFloat(speedParameterHash, inputAmount, animatorDampTime, Time.deltaTime);
            else
                animator.SetFloat(speedParameterHash, inputAmount);
        }

        private void KeepNonLoopingWalkAlive(float inputAmount)
        {
            if (animator == null || inputAmount <= 0.05f || !animator.HasState(0, WalkStateHash))
                return;

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.shortNameHash != WalkStateHash)
                return;

            if (stateInfo.loop || stateInfo.normalizedTime < 0.98f)
                return;

            animator.Play(WalkStateHash, 0, stateInfo.normalizedTime % 1f);
        }

        private void UpdateFootsteps()
        {
            if (!playFootsteps || !IsMoving || footstepCueIds == null || footstepCueIds.Length == 0)
            {
                if (!IsMoving)
                    nextFootstepAt = Time.unscaledTime;
                return;
            }

            if (Time.unscaledTime < nextFootstepAt || !GameKernel.IsReady ||
                !GameKernel.Services.TryGet(out AudioManager audio))
            {
                return;
            }

            string cueId = footstepCueIds[footstepCueIndex % footstepCueIds.Length];
            footstepCueIndex = (footstepCueIndex + 1) % footstepCueIds.Length;
            nextFootstepAt = Time.unscaledTime + footstepInterval;
            if (!string.IsNullOrWhiteSpace(cueId))
                audio.Play(cueId, transform.position);
        }
    }
}
