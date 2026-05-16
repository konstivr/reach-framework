using UnityEngine;

namespace Reach.Framework.InputSys
{
    /// <summary>
    /// Filters a noisy Vector2 input (e.g. analog stick) into a clean signal.
    ///
    /// Solves three real-world stick problems:
    ///   1) Stick drift: tiny non-zero values when the stick is centered.
    ///      - Initial calibration on enable (first ~0.3s)
    ///      - Optional: continuous re-learning, but only after stick rests stably.
    ///   2) Deadzone: ignores tiny inputs entirely, scales the rest to [0..1].
    ///   3) Forward-snap: when the stick is almost-but-not-quite forward,
    ///      kill tiny x bias so the character walks straight.
    /// </summary>
    [System.Serializable]
    public class Vector2Filter
    {
        [Header("Deadzone")]
        [Tooltip("Magnitudes below this are treated as zero. Above, the input is rescaled to [0..1].")]
        [Range(0f, 0.5f)] public float deadzone = 0.22f;

        [Header("Drift Calibration")]
        [Tooltip("Learn bias automatically. Initial calibration always happens; continuous only if enabled.")]
        public bool autoCalibrate = true;

        [Tooltip("Also continuously re-learn bias while the stick rests stably. " +
                 "Off by default — initial calibration is usually enough and prevents drift accumulation.")]
        public bool continuousCalibrate = false;

        [Tooltip("Stick is considered 'resting' when raw magnitude is below this.")]
        [Range(0f, 0.2f)] public float calibrateWhenBelow = 0.06f;

        [Tooltip("Stick must rest below threshold for this many seconds before continuous calibration kicks in.")]
        [Range(0f, 2f)] public float restStableSeconds = 0.4f;

        [Tooltip("How fast the bias is learned during calibration windows.")]
        [Range(0.01f, 20f)] public float calibrateSpeed = 6f;

        [Tooltip("Duration of initial calibration on enable/reset.")]
        [Range(0.1f, 2f)] public float initialCalibrateSeconds = 0.3f;

        [Header("Forward Snap (optional)")]
        [Tooltip("When the input is within this angle (deg) of straight forward/back, " +
                 "kill the x-component to prevent tiny sideways drift while walking forward. " +
                 "Set to 0 to disable.")]
        [Range(0f, 20f)] public float forwardSnapAngleDeg = 8f;

        // Internal state
        Vector2 _bias;
        float _restTimer;          // How long the stick has been resting near zero
        float _initialTimer = -1f; // Counts up during initial calibration window
        bool _initialDone;

        public Vector2 Process(Vector2 raw, float deltaTime)
        {
            // ---- Initial calibration window (first 0.3s after enable) ----
            if (autoCalibrate && !_initialDone)
            {
                if (_initialTimer < 0f) _initialTimer = 0f;
                _initialTimer += deltaTime;

                if (raw.magnitude < calibrateWhenBelow)
                {
                    // Lock onto current resting value
                    float t = 1f - Mathf.Exp(-calibrateSpeed * 2f * deltaTime);
                    _bias = Vector2.Lerp(_bias, raw, t);
                }

                if (_initialTimer >= initialCalibrateSeconds)
                    _initialDone = true;
            }

            // ---- Continuous calibration (only when stick has rested stably) ----
            if (autoCalibrate && continuousCalibrate && _initialDone)
            {
                if (raw.magnitude < calibrateWhenBelow)
                {
                    _restTimer += deltaTime;
                    if (_restTimer >= restStableSeconds)
                    {
                        float t = 1f - Mathf.Exp(-calibrateSpeed * deltaTime);
                        _bias = Vector2.Lerp(_bias, raw, t);
                    }
                }
                else
                {
                    _restTimer = 0f;
                }
            }

            // ---- Subtract learned bias ----
            Vector2 v = raw - _bias;

            // ---- Deadzone + rescale ----
            float mag = v.magnitude;
            if (mag < deadzone) return Vector2.zero;

            float scaled = Mathf.InverseLerp(deadzone, 1f, mag);
            v = v.normalized * scaled;

            // ---- Forward snap ----
            if (forwardSnapAngleDeg > 0f)
            {
                float angle = Mathf.Abs(Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg);
                if (angle < forwardSnapAngleDeg) v.x = 0f;
            }

            return v;
        }

        /// <summary>Reset the learned bias and re-enter initial calibration window.</summary>
        public void ResetBias()
        {
            _bias = Vector2.zero;
            _restTimer = 0f;
            _initialTimer = -1f;
            _initialDone = false;
        }
    }
}
