using UnityEngine;

namespace Reach.Framework.Core
{
    /// <summary>
    /// When this character is uncontrolled and the controlled player gets close,
    /// stop wandering, turn to face the player, and force Idle animation.
    /// </summary>
    public class CharacterProximityFreeze : MonoBehaviour
    {
        [Header("Proximity")]
        public float freezeRadius = 3.0f;
        public float turnSpeed = 360f;
        public float lookHeight = 1.5f;

        [Header("Refs (auto)")]
        public PossessableCharacter character;
        public CharacterWander wander;
        public Animator animator;

        int _animIDSpeed;
        int _animIDMotionSpeed;

        bool _isFrozen;

        void Awake()
        {
            if (character == null) character = GetComponent<PossessableCharacter>();
            if (wander == null) wander = GetComponent<CharacterWander>();
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            _animIDSpeed       = Animator.StringToHash("Speed");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }

        void Update()
        {
            if (character == null) return;

            if (character.IsControlled)
            {
                _isFrozen = false;
                return;
            }

            var pm = GameContext.Instance?.Perspective;
            if (pm == null || pm.Current == null) return;

            var current = pm.Current;
            if (current == character) return;

            float dist = Vector3.Distance(transform.position, current.transform.position);
            bool shouldFreeze = dist <= freezeRadius;

            SetFrozen(shouldFreeze);

            if (_isFrozen)
            {
                RotateTowards(current);
                ForceIdleAnimation();
            }
        }

        void SetFrozen(bool frozen)
        {
            if (_isFrozen == frozen) return;
            _isFrozen = frozen;

            if (wander != null)
                wander.enabled = !frozen;
        }

        void ForceIdleAnimation()
        {
            if (animator == null) return;
            animator.SetFloat(_animIDSpeed, 0f);
            animator.SetFloat(_animIDMotionSpeed, 0f);
        }

        void RotateTowards(PossessableCharacter target)
        {
            Vector3 targetPos = target.transform.position + Vector3.up * lookHeight;
            Vector3 dir = targetPos - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude < 0.0001f) return;

            Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        }
    }
}
