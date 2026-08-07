using UnityEngine;

namespace CardBattle.Exploration
{
    [DisallowMultipleComponent]
    public sealed class QuarterViewCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new(0f, 5.15f, -8.7f);
        [SerializeField] private Vector3 lookAtOffset = new(0f, 0.55f, 0f);
        [SerializeField] private float positionDamping = 10f;
        [SerializeField] private float rotationDamping = 12f;
        [SerializeField] private bool followTargetVertical = false;
        [SerializeField] private float targetGroundY = 0f;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            SnapToTarget();
        }

        private void Start()
        {
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            Vector3 followBase = GetFollowBasePosition();
            Vector3 desiredPosition = followBase + offset;
            float positionBlend = 1f - Mathf.Exp(-positionDamping * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, positionBlend);

            Quaternion desiredRotation = GetFixedQuarterViewRotation();
            float rotationBlend = 1f - Mathf.Exp(-rotationDamping * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationBlend);
        }

        private void SnapToTarget()
        {
            if (target == null)
                return;

            transform.position = GetFollowBasePosition() + offset;
            transform.rotation = GetFixedQuarterViewRotation();
        }

        private Vector3 GetFollowBasePosition()
        {
            Vector3 followBase = target.position;
            if (!followTargetVertical)
                followBase.y = targetGroundY;

            return followBase;
        }

        private Quaternion GetFixedQuarterViewRotation()
        {
            Vector3 direction = lookAtOffset - offset;
            return direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(direction.normalized, Vector3.up)
                : transform.rotation;
        }
    }
}
