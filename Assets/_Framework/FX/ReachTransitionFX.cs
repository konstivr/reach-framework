using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Reach.Framework.Core;
using Reach.Framework.HUD;

namespace Reach.Framework.FX
{
    /// <summary>
    /// Simple fade transition for perspective switches.
    /// Black overlay fades in → centered character image appears →
    /// hold (switch happens) → image fades out → black fades out.
    /// </summary>
    public class ReachTransitionFX : MonoBehaviour, IReachTransition
    {
        [Header("Audio")]
        public AudioSource sfxSource;
        public AudioClip transitionSfx;
        [Range(0f, 1f)] public float sfxVolume = 0.9f;

        [Header("UI References")]
        [Tooltip("CanvasGroup that holds the fullscreen black overlay.")]
        public CanvasGroup blackOverlay;

        [Tooltip("CanvasGroup that holds the centered character image.")]
        public CanvasGroup imageGroup;

        [Tooltip("Character image (centered, small in middle).")]
        public Image characterImage;

        [Header("Timing")]
        public float blackFadeInSeconds = 1.5f;
        public float imageFadeInSeconds = 0.5f;
        public float holdImageSeconds = 2.0f;
        public float imageFadeOutSeconds = 0.5f;
        public float blackFadeOutSeconds = 1.5f;
        public float settleAfterSeconds = 0.1f;

        [Header("Debug")]
        public bool debugLogs = true;

        // ============================================================
        // State
        // ============================================================

        bool _isTransitioning;
        public bool IsTransitioning => _isTransitioning;

        TaskCompletionSource<bool> _completionTcs;

        void Awake()
        {
            if (blackOverlay != null) blackOverlay.alpha = 0f;
            if (imageGroup != null) imageGroup.alpha = 0f;
        }

        void OnEnable()
        {
            if (blackOverlay != null) blackOverlay.alpha = 0f;
            if (imageGroup != null) imageGroup.alpha = 0f;
        }

        // ============================================================
        // Public API
        // ============================================================

        public Task<bool> PlayAndSwitchAsync(PossessableCharacter target)
        {
            if (_isTransitioning) return Task.FromResult(false);
            _completionTcs = new TaskCompletionSource<bool>();
            StartCoroutine(PlayAndSwitchRoutine(target));
            return _completionTcs.Task;
        }

        public Task PlayCloseOnlyAsync()
        {
            if (_isTransitioning) return Task.CompletedTask;
            var tcs = new TaskCompletionSource<bool>();
            StartCoroutine(PlayCloseOnlyRoutine(tcs));
            return tcs.Task;
        }

        // ============================================================
        // Coroutines
        // ============================================================

        IEnumerator PlayAndSwitchRoutine(PossessableCharacter target)
        {
            _isTransitioning = true;

            var ctx = GameContext.Instance;
            if (ctx == null || ctx.Perspective == null)
            {
                _isTransitioning = false;
                _completionTcs?.SetResult(false);
                yield break;
            }

            if (debugLogs) Debug.Log($"[ReachFX] Transition INTO '{target?.name}'");
            ctx.Hud?.SetFXOverride("");

            if (sfxSource != null && transitionSfx != null)
                sfxSource.PlayOneShot(transitionSfx, sfxVolume);

            // Set character image
            bool hasImage = false;
            if (characterImage != null)
            {
                if (target != null && target.Definition != null && target.Definition.transitionImage != null)
                {
                    characterImage.sprite = target.Definition.transitionImage;
                    characterImage.enabled = true;
                    characterImage.preserveAspect = true;
                    hasImage = true;
                }
                else
                {
                    characterImage.enabled = false;
                }
            }

            // Phase 1: Black fade in
            yield return FadeCanvasGroup(blackOverlay, 0f, 1f, blackFadeInSeconds);

            // Switch under cover of black
            bool switched = ctx.Perspective.TrySwitchTo(target);

            // Phase 2: Image fade in
            if (hasImage && imageGroup != null)
            {
                yield return FadeCanvasGroup(imageGroup, 0f, 1f, imageFadeInSeconds);

                // Phase 3: Hold
                yield return new WaitForSeconds(holdImageSeconds);

                // Phase 4: Image fade out
                yield return FadeCanvasGroup(imageGroup, 1f, 0f, imageFadeOutSeconds);
            }
            else
            {
                yield return new WaitForSeconds(holdImageSeconds * 0.3f);
            }

            // Phase 5: Black fade out
            yield return FadeCanvasGroup(blackOverlay, 1f, 0f, blackFadeOutSeconds);

            yield return new WaitForSeconds(settleAfterSeconds);

            ctx.Hud?.ClearFXOverride();

            _isTransitioning = false;
            if (debugLogs) Debug.Log($"[ReachFX] Done (switch={switched})");
            _completionTcs?.SetResult(switched);
            _completionTcs = null;
        }

        IEnumerator PlayCloseOnlyRoutine(TaskCompletionSource<bool> tcs)
        {
            _isTransitioning = true;
            yield return FadeCanvasGroup(blackOverlay, 0f, 1f, blackFadeInSeconds);
            _isTransitioning = false;
            tcs.SetResult(true);
        }

        IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
        {
            if (group == null) yield break;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float eased = EaseInOutCubic(k);
                group.alpha = Mathf.Lerp(from, to, eased);
                yield return null;
            }
            group.alpha = to;
        }

        static float EaseInOutCubic(float x) =>
            x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
    }
}
