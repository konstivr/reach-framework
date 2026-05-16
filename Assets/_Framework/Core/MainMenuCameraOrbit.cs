using UnityEngine;

namespace Reach.Framework.Core
{
    /// <summary>
    /// Rotates a target object slowly around its own Y axis.
    /// Used to make the background mesh subtly animated on the main menu.
    /// Attach to the mesh GameObject (not the camera).
    /// </summary>
    public class MainMenuCameraOrbit : MonoBehaviour
    {
        [Header("Rotation")]
        [Tooltip("Degrees per second around Y axis. Positive = clockwise from above.")]
        public float speedDegPerSec = 8f;

        [Tooltip("Optional axis. Default Y (vertical).")]
        public Vector3 axis = Vector3.up;

        void Update()
        {
            transform.Rotate(axis, speedDegPerSec * Time.deltaTime, Space.World);
        }
    }
}
