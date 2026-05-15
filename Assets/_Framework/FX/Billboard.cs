using UnityEngine;

namespace Reach.Framework.FX
{
    /// <summary>
    /// Rotates this transform every LateUpdate so it always faces the main camera.
    /// Use on quads with a sprite/texture for poster-style 2D objects in 3D space.
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        [Tooltip("If true, only rotate around Y axis (sprite stays upright, only turns horizontally).")]
        public bool yAxisOnly = true;

        Camera _cam;

        void LateUpdate()
        {
            if (_cam == null)
            {
                _cam = Camera.main;
                if (_cam == null) return;
            }

            Vector3 dir = _cam.transform.position - transform.position;

            if (yAxisOnly)
                dir.y = 0;

            if (dir.sqrMagnitude < 0.0001f) return;

            // Face TOWARD camera: flip direction so sprite-front points at camera
            transform.rotation = Quaternion.LookRotation(-dir.normalized);
        }
    }
}
