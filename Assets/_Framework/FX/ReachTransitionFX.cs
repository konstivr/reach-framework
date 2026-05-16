using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Reach.Framework.Core;
using Reach.Framework.HUD;

namespace Reach.Framework.FX
{
    /// <summary>
    /// Eye-close transition for perspective switches.
    ///
    /// Choreography:
    ///   1) Close: bars slide to center (eyes close)
    ///   2) Open to image: bars slide back, revealing fullscreen char image
    ///   3) Hold image (with switch happening)
    ///   4) Close again: bars slide to center
    ///   5) Open final: bars slide back, revealing new perspective
    /// </summary>
    public class ReachTransitionFX : MonoBehaviour, IReachTransition
    {
        [Header("PostFX")]
        public Volume transitionVolume;
        [Range(0f, 1f)] public float transitionVolumeMax = 1f;

        [Header("Audio")]
        public AudioSource sfxSource;
        public AudioClip transitionSfx;
        [Range(0f, 1f)] public float sfxVolume = 0.9f;

        [Header("Bars (eye close/open)")]
        [Tooltip("Top bar that slides down. Anchor: top-stretch, Pivot Y=1.")]
        public RectTransform topBar;
        [Tooltip("Bottom bar that slides up. Anchor: bottom-stretch, Pivot Y=0.")]
        public RectTransform bottomBar;

        [Header("Character Image (shown fullscreen between phases)")]
        public CanvasGroup imageGroup;
        public Image characterImage;

        [Header("Timing")]
        public float closeSeconds = 0.5f;
        public float openToImageSeconds = 0.4f;
        public float holdImageSeconds = 1.5f;
        public float closeAgainSeconds = 0.5f;
        public float openFinalSeconds = 0.6f;
        public float settleAfterSeconds = 0.1f;

        [Header("Debug")]
        public bool debugLogs = true;

        // ============================================================
        // State
        // ============================================================

        bool _isTransitioning;
        public bool IsTransitioning => _isTransitioning;

        // ============================================================
        // Lifecycle
        // ============================================================

        void Awake()
        {
            // Force bars fully off-screen at rest, image hidden
            if (transitionVolume != null) transitionVolume.weight = 0f;
            if (imageGroup != null) imageGroup.alpha = 0f;
            SetBarOpenness(1f);
        }

        void OnEnable()
        {
            // Belt-and-suspenders: ensure bars hidden even after scene reloads
            SetBarOpenness(1f);
        }

        // ============================================================
        // Public API
        // ============================================================

        public async Task<bool> PlayAndSwitchAsync(PossessableCharacter target)
        {
            if (_isTransitioning) return false;

            var ctx = GameContext.Instance;
            if (ctx == null || ctx.Perspective == null) return false;

            _isTransitioning = true;
            if (debugLogs) Debug.Log($"[ReachFX] Transition INTO \'{target?.name}\'");

            ctx.Hud?.SetFXOverride("");

            if (sfxSource != null && transitionSfx != null)
                sfxSource.PlayOneShot(transitionSfx, sfxVolume);

            // Set image (hidden via CanvasGroup alpha until phase 2)
            bool hasImage = false;
            if (characterImage != null)
            {
                if (target != null && target.Definition != null && target.Definition.transitionImage != null)
                {
                    characterImage.sprite = target.Definition.transitionImage;
                    characterImage.enabled = true;
                    hasImage = true;
                }
                else
                {
                    characterImage.enabled = false;
                }
            }

            // ---- Phase 1: Close (eyes shut) ----
            await AnimateBars(1f, 0f, closeSeconds);

            // Switch happens NOW (under cover of closed bars)
            bool switched = ctx.Perspective.TrySwitchTo(target);

            // ---- Phase 2: Open to reveal fullscreen image ----
            if (hasImage && imageGroup != null)
            {
                imageGroup.alpha = 1f; // image visible while bars open
                await AnimateBars(0f, 1f, openToImageSeconds);

                // ---- Phase 3: Hold fullscreen image ----
                await Wait(holdImageSeconds);

                // ---- Phase 4: Close again ----
                await AnimateBars(1f, 0f, closeAgainSeconds);

                imageGroup.alpha = 0f; // hide image while bars are closed
            }
            else
            {
                // No image — just hold briefly while closed
                await Wait(holdImageSeconds * 0.3f);
            }

            // ---- Phase 5: Open final (reveal new perspective) ----
            await AnimateBars(0f, 1f, openFinalSeconds);

            await Wait(settleAfterSeconds);

            // Reset
            if (transitionVolume != null) transitionVolume.weight = 0f;
            ctx.Hud?.ClearFXOverride();

            _isTransitioning = false;
            if (debugLogs) Debug.Log($"[ReachFX] Done (switch={switched})");
            return switched;
        }

        // ============================================================
        // Bar animation
        // ============================================================

        /// <summary>
        /// openness: 1 = bars pushed fully off-screen (open eyes),
        ///           0 = bars meet at center (closed eyes).
        /// </summary>
        async Task AnimateBars(float fromOpenness, float toOpenness, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float eased = EaseInOutCubic(k);
                float openness = Mathf.Lerp(fromOpenness, toOpenness, eased);
                SetBarOpenness(openness);
                await Task.Yield();
            }
            SetBarOpenness(toOpenness);
        }

        /// <summary>
        /// openness 1 = bars off-screen (above top / below bottom of screen),
        /// openness 0 = bars cover their half of screen (meet at center).
        /// Each bar fills exactly HALF the screen when openness = 0.
        /// </summary>
        void SetBarOpenness(float openness)
        {
            float screenH = Screen.height;
            float halfH = screenH * 0.5f;

            // Set bar height to cover half the screen
            if (topBar != null)
            {
                Vector2 size = topBar.sizeDelta;
                size.y = halfH;
                topBar.sizeDelta = size;

                // Pivot Y = 1 (top), so anchoredPosition.y of 0 = bar's top edge at top of canvas
                // openness 0 → anchoredPosition.y = 0 (bar fully visible, hangs down from top)
                // openness 1 → anchoredPosition.y = +halfH (bar fully above screen)
                Vector2 pos = topBar.anchoredPosition;
                pos.y = openness * halfH;
                topBar.anchoredPosition = pos;
            }

            if (bottomBar != null)
            {
                Vector2 size = bottomBar.sizeDelta;
                size.y = halfH;
                bottomBar.sizeDelta = size;

                // Pivot Y = 0 (bottom), so anchoredPosition.y of 0 = bar's bottom edge at bottom of canvas
                // openness 0 → anchoredPosition.y = 0 (bar fully visible, rises up from bottom)
                // openness 1 → anchoredPosition.y = -halfH (bar fully below screen)
                Vector2 pos = bottomBar.anchoredPosition;
                pos.y = -openness * halfH;
                bottomBar.anchoredPosition = pos;
            }
        }

        static float EaseInOutCubic(float x) =>
            x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;

        static async Task Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                await Task.Yield();
            }
        }
    }
}
