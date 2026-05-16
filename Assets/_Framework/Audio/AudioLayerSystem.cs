using System.Collections.Generic;
using UnityEngine;

namespace Reach.Framework.Audio
{
    /// <summary>
    /// Plays adaptive music layers from StoryPack.musicLayers.
    /// Layer 0 starts immediately. Each subsequent layer is added
    /// at the next character switch, sample-synchronized so all
    /// active layers play in lock-step.
    ///
    /// Assumes all layers are same length and mastered at the same tempo.
    /// </summary>
    public class AudioLayerSystem : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("AudioMixerGroup to route all layers to (optional).")]
        public UnityEngine.Audio.AudioMixerGroup mixerGroup;

        [Header("Volume")]
        [Range(0f, 1f)] public float masterVolume = 0.6f;

        [Header("Fade")]
        [Tooltip("Seconds to fade in newly added layers.")]
        public float fadeInSeconds = 1.5f;

        [Header("Debug")]
        public bool debugLogs = true;

        // ============================================================
        // State
        // ============================================================

        readonly List<AudioSource> _sources = new List<AudioSource>();
        int _nextLayerIndex = 0;
        double _startDspTime = 0d;
        List<AudioClip> _layers;

        // ============================================================
        // Lifecycle
        // ============================================================

        void Start()
        {
            var ctx = Core.GameContext.Instance;
            if (ctx == null || ctx.pack == null)
            {
                if (debugLogs) Debug.Log("[AudioLayer] No pack — skipping music init");
                return;
            }

            _layers = ctx.pack.musicLayers;
            if (_layers == null || _layers.Count == 0)
            {
                if (debugLogs) Debug.Log("[AudioLayer] No music layers in pack");
                return;
            }

            // Layer 0 starts immediately
            _startDspTime = AudioSettings.dspTime + 0.1d; // small buffer
            PlayLayer(0, _startDspTime);
            _nextLayerIndex = 1;

            // Listen for character switches
            if (ctx.Perspective != null)
            {
                ctx.Perspective.Switched += OnCharacterSwitched;
                if (debugLogs) Debug.Log("[AudioLayer] Subscribed to PerspectiveManager.Switched");
            }
        }

        void OnDestroy()
        {
            var ctx = Core.GameContext.Instance;
            if (ctx != null && ctx.Perspective != null)
                ctx.Perspective.Switched -= OnCharacterSwitched;
        }

        // ============================================================
        // Event handler
        // ============================================================

        void OnCharacterSwitched(Core.PossessableCharacter from, Core.PossessableCharacter to)
        {
            if (_layers == null) return;
            if (_nextLayerIndex >= _layers.Count)
            {
                if (debugLogs) Debug.Log("[AudioLayer] All layers already active");
                return;
            }

            PlayLayer(_nextLayerIndex, AudioSettings.dspTime + 0.05d);
            _nextLayerIndex++;
        }

        // ============================================================
        // Playback
        // ============================================================

        void PlayLayer(int index, double dspStartTime)
        {
            if (_layers == null || index < 0 || index >= _layers.Count) return;
            var clip = _layers[index];
            if (clip == null)
            {
                if (debugLogs) Debug.LogWarning($"[AudioLayer] Layer {index} has no clip");
                return;
            }

            var go = new GameObject($"AudioLayer_{index}_{clip.name}");
            go.transform.SetParent(transform, false);

            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f; // 2D
            src.outputAudioMixerGroup = mixerGroup;
            src.volume = 0f; // start silent, fade in

            // Sync to first layer: align to original startDspTime so loops match
            // If layer 0 already running, calculate offset to align with it
            if (index == 0)
            {
                src.PlayScheduled(dspStartTime);
            }
            else
            {
                // Start at the same point in the loop as layer 0 would currently be
                double elapsed = AudioSettings.dspTime - _startDspTime;
                if (elapsed < 0d) elapsed = 0d;

                float clipLen = clip.length;
                if (clipLen > 0f)
                {
                    float offsetSeconds = (float)(elapsed % clipLen);
                    src.time = offsetSeconds;
                }
                src.PlayScheduled(dspStartTime);
            }

            _sources.Add(src);

            // Fade in
            StartCoroutine(FadeIn(src, fadeInSeconds, masterVolume));

            if (debugLogs) Debug.Log($"[AudioLayer] Started layer {index} ('{clip.name}') at dsp={dspStartTime:F3}");
        }

        System.Collections.IEnumerator FadeIn(AudioSource src, float duration, float targetVolume)
        {
            if (src == null) yield break;
            float t = 0f;
            while (t < duration && src != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                src.volume = Mathf.Lerp(0f, targetVolume, k);
                yield return null;
            }
            if (src != null) src.volume = targetVolume;
        }
    }
}
