using UnityEngine;
using UnityEngine.InputSystem;

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

        public const string HeadingRootName = "VisualRoot";
        public const string AxisCorrectionRootName = "AxisCorrectionRoot";
        private static readonly int LocomotionStateHash = Animator.StringToHash("Locomotion");

        private CharacterController controller;
        private int speedParameterHash;
        private Vector3 facingDirection = Vector3.forward;
        private Transform axisCorrectionRoot;

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
                if (animator.HasState(0, LocomotionStateHash))
                    animator.Play(LocomotionStateHash, 0, 0f);
            }

            EnsureVisualWrapper();

            if (animator != null)
                animator.Rebind();
        }

        private void Start()
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            if (animator != null)
            {
                if (animator.HasState(0, LocomotionStateHash))
                    animator.Play(LocomotionStateHash, 0, 0f);
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

            Vector3 planarDirection = ReadCameraRelativeDirection();
            float inputAmount = Mathf.Clamp01(planarDirection.magnitude);

            Move(planarDirection);
            TurnToward(planarDirection);
            UpdateAnimator(inputAmount);
            KeepNonLoopingLocomotionAlive(inputAmount);
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
            Vector3 motion = planarDirection * moveSpeed;
            controller.Move(motion * Time.deltaTime);

            if (lockToGroundPlane)
                SnapToGroundPlane();
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

        private void UpdateAnimator(float inputAmount)
        {
            if (animator == null || string.IsNullOrWhiteSpace(speedParameter))
                return;

            if (animatorDampTime > 0f)
                animator.SetFloat(speedParameterHash, inputAmount, animatorDampTime, Time.deltaTime);
            else
                animator.SetFloat(speedParameterHash, inputAmount);
        }

        private void KeepNonLoopingLocomotionAlive(float inputAmount)
        {
            if (animator == null || inputAmount <= 0.05f)
                return;

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.loop || stateInfo.normalizedTime < 0.98f)
                return;

            animator.Play(LocomotionStateHash, 0, stateInfo.normalizedTime % 1f);
        }
    }
}
