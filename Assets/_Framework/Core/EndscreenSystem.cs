using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Reach.Framework.Interaction;

namespace Reach.Framework.Core
{
    /// <summary>
    /// Tracks completion state: each character must visit, and each character
    /// must interact with at least one object. When both conditions are met,
    /// the Endscreen fires (transition + outro video → reload).
    ///
    /// Listens to:
    ///   - PerspectiveManager.Switched (track visited)
    ///   - InteractableObject completion (track per-character object usage)
    /// </summary>
    public class EndscreenSystem : MonoBehaviour
    {
        [Header("References (auto-resolved)")]
        public EndscreenCanvas endscreenCanvas;
        public Reach.Framework.FX.ReachTransitionFX transition;

        [Header("Debug")]
        public bool debugLogs = true;

        // Track which characters have interacted with at least one object
        readonly HashSet<CharacterDefinition> _charsThatInteracted = new HashSet<CharacterDefinition>();

        bool _endscreenFired;

        public bool IsEndscreenFired => _endscreenFired;

        void Awake()
        {
            var ctx = GameContext.Instance;
            if (ctx != null) ctx.Endscreen = this;

            if (endscreenCanvas == null) endscreenCanvas = FindObjectOfType<EndscreenCanvas>(true);
            if (transition == null) transition = FindObjectOfType<Reach.Framework.FX.ReachTransitionFX>();
        }

        /// <summary>
        /// Called by InteractableObject when a character successfully interacts.
        /// </summary>
        public void NotifyCharacterInteracted(CharacterDefinition character)
        {
            if (character == null || _endscreenFired) return;
            if (_charsThatInteracted.Add(character))
            {
                if (debugLogs) Debug.Log($"[Endscreen] Char interacted: {character.name} ({_charsThatInteracted.Count} total)");
            }
            CheckCompletion();
        }

        void CheckCompletion()
        {
            if (_endscreenFired) return;

            var ctx = GameContext.Instance;
            if (ctx == null || ctx.Perspective == null || ctx.pack == null) return;

            int maxChars = ctx.pack.EffectiveMaxPerspectives;
            if (maxChars <= 0) return;

            int visited = ctx.Perspective.VisitedCount;
            int interacted = _charsThatInteracted.Count;

            if (debugLogs) Debug.Log($"[Endscreen] Check: visited={visited}/{maxChars}, interacted={interacted}/{maxChars}");

            if (visited >= maxChars && interacted >= maxChars)
            {
                FireEndscreen();
            }
        }

        async void FireEndscreen()
        {
            if (_endscreenFired) return;
            _endscreenFired = true;

            if (debugLogs) Debug.Log("[Endscreen] FIRING — all conditions met");

            // Play transition first (eye-close effect) WITHOUT switching characters
            if (transition != null)
            {
                // Reuse transition: we pass null target → just plays close/open without switch
                // Or: we trigger a dummy close + show endscreen during the closed phase
                await transition.PlayCloseOnlyAsync();
            }

            // Show endscreen
            if (endscreenCanvas != null)
            {
                var ctx = GameContext.Instance;
                var clip = ctx?.pack != null ? ctx.pack.outroVideo : null;
                endscreenCanvas.Show(clip, OnEndscreenFinished);
            }
            else
            {
                Debug.LogWarning("[Endscreen] No EndscreenCanvas in scene — reloading immediately.");
                OnEndscreenFinished();
            }
        }

        void OnEndscreenFinished()
        {
            if (debugLogs) Debug.Log("[Endscreen] Finished — loading MainMenu");
            SceneManager.LoadScene("MainMenu");
        }
    }
}
