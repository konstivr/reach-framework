using UnityEngine;
using Unity.Cinemachine;

namespace Reach.Framework.Core
{
    /// <summary>
    /// Drives a CinemachineOrbitalFollow's Horizontal + Vertical axes from
    /// GameContext.Input.Look (right stick / mouse delta).
    /// </summary>
    [RequireComponent(typeof(CinemachineOrbitalFollow))]
    public class CameraOrbit : MonoBehaviour
    {
        [Header("Sensitivity")]
        [Tooltip("Degrees per second at full stick deflection.")]
        public float horizontalSensitivity = 180f;

        [Tooltip("Degrees per second at full stick deflection.")]
        public float verticalSensitivity = 90f;

        [Header("Invert")]
        public bool invertY = false;

        [Header("Debug")]
        public bool debugLogs = false;

        CinemachineOrbitalFollow _orbital;

        void Awake()
        {
            _orbital = GetComponent<CinemachineOrbitalFollow>();
        }

        void Update()
        {
            var input = GameContext.Instance?.Input;
            if (input == null || _orbital == null) return;

            Vector2 look = input.Look;
            if (look.sqrMagnitude < 0.0001f) return;

            float dt = Time.deltaTime;

            // Horizontal: stick right → camera orbits right around character
            float hDelta = look.x * horizontalSensitivity * dt;
            _orbital.HorizontalAxis.Value += hDelta;

            // Vertical: stick up → camera tilts up (looking from higher angle)
            float vSign = invertY ? -1f : 1f;
            float vDelta = look.y * verticalSensitivity * dt * vSign;
            _orbital.VerticalAxis.Value += vDelta;

            // Clamp vertical (range from inspector)
            _orbital.VerticalAxis.Value = Mathf.Clamp(
                _orbital.VerticalAxis.Value,
                _orbital.VerticalAxis.Range.x,
                _orbital.VerticalAxis.Range.y
            );

            if (debugLogs)
                Debug.Log($"[CameraOrbit] H={_orbital.HorizontalAxis.Value:F1} V={_orbital.VerticalAxis.Value:F1}");
        }
    }
}
