using UnityEngine;
using Unity.Cinemachine;

namespace Reach.Framework.Core
{
    /// <summary>
    /// Tells a Cinemachine Virtual Camera to follow the currently controlled character.
    /// Listens to PerspectiveManager.Switched events and re-targets on each switch.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("The Cinemachine virtual camera that should follow.")]
        public CinemachineCamera vcam;

        [Header("Fallback")]
        [Tooltip("If true, sets the vcam's Follow target to the current character's transform; " +
                 "if false, does nothing (use this if you control the follow target manually elsewhere).")]
        public bool fallbackToCharacterTransform = true;

        void Awake()
        {
            if (vcam == null) vcam = GetComponent<CinemachineCamera>();
        }

        void OnEnable()
        {
            var pm = GameContext.Instance?.Perspective;
            if (pm != null)
                pm.Switched += OnSwitched;

            // Apply current character if it's already set when this enables
            var current = pm?.Current;
            if (current != null) Retarget(current);
        }

        void OnDisable()
        {
            var pm = GameContext.Instance?.Perspective;
            if (pm != null)
                pm.Switched -= OnSwitched;
        }

        void OnSwitched(PossessableCharacter from, PossessableCharacter to)
        {
            Retarget(to);
        }

        void Retarget(PossessableCharacter character)
        {
            if (vcam == null || character == null) return;
            if (!fallbackToCharacterTransform) return;

            vcam.Follow = character.transform;
            vcam.LookAt = character.transform;
        }
    }
}