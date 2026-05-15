using UnityEngine;
using Unity.Cinemachine;

namespace Reach.Framework.Core
{
    /// <summary>
    /// Tells a Cinemachine 3.x CinemachineCamera to track the currently controlled character.
    /// Listens to PerspectiveManager.Switched events and re-targets on each switch.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("The Cinemachine camera that should follow.")]
        public CinemachineCamera vcam;

        [Header("Fallback")]
        public bool fallbackToCharacterTransform = true;

        [Header("Debug")]
        public bool debugLogs = true;

        bool _subscribed;

        void Awake()
        {
            if (vcam == null) vcam = GetComponent<CinemachineCamera>();
        }

        void Start()
        {
            TrySubscribe();
            // Apply current character on start (in case PM already has one)
            var pm = GameContext.Instance?.Perspective;
            if (pm?.Current != null) Retarget(pm.Current);
        }

        void OnEnable()
        {
            TrySubscribe();
        }

        void OnDisable()
        {
            var pm = GameContext.Instance?.Perspective;
            if (pm != null && _subscribed)
            {
                pm.Switched -= OnSwitched;
                _subscribed = false;
            }
        }

        void TrySubscribe()
        {
            if (_subscribed) return;
            var pm = GameContext.Instance?.Perspective;
            if (pm == null) return;

            pm.Switched += OnSwitched;
            _subscribed = true;

            if (debugLogs) Debug.Log("[CameraFollow] Subscribed to PerspectiveManager.Switched");
        }

        void OnSwitched(PossessableCharacter from, PossessableCharacter to)
        {
            if (debugLogs) Debug.Log($"[CameraFollow] OnSwitched: from='{from?.name}' to='{to?.name}'");
            Retarget(to);
        }

        void Retarget(PossessableCharacter character)
        {
            if (vcam == null || character == null) return;
            if (!fallbackToCharacterTransform) return;

            vcam.Follow = character.transform;
            vcam.LookAt = character.transform;

            if (debugLogs) Debug.Log($"[CameraFollow] Retargeted to '{character.name}' (Follow + LookAt)");
        }
    }
}
