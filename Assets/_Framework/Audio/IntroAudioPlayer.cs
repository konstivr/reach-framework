using System.Collections;
using UnityEngine;
using Reach.Framework.Core;

namespace Reach.Framework.Audio
{
    /// <summary>
    /// Plays the pack's introAudio clip once at scene start.
    /// Skippable via Interact-button press.
    /// Pack-bound: clip comes from StoryPack.introAudio.
    /// </summary>
    public class IntroAudioPlayer : MonoBehaviour
    {
        [Header("Debug")]
        public bool debugLogs = true;

        [Header("Audio")]
        [Range(0f, 1f)]
        public float volume = 1f;

        AudioSource _source;
        bool _isPlaying;
        bool _hasStarted;

        public bool IsActive => _hasStarted && (_isPlaying || (_source != null && _source.isPlaying));
        public bool IsConsumingInput => _isPlaying;

        void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.spatialBlend = 0f;
            _source.playOnAwake = false;
            _source.loop = false;
            _source.volume = volume;
        }

        IEnumerator Start()
        {
            var ctx = GameContext.Instance;
            if (ctx == null)
            {
                if (debugLogs) Debug.LogWarning("[IntroAudio] No GameContext, skipping intro.");
                yield break;
            }

            while (ctx.pack == null) yield return null;

            var clip = ctx.pack.introAudio;
            if (clip == null)
            {
                if (debugLogs) Debug.Log("[IntroAudio] Pack has no introAudio, skipping.");
                yield break;
            }

            float delay = ctx.pack.introDelaySeconds;
            if (delay > 0f)
            {
                if (debugLogs) Debug.Log($"[IntroAudio] Waiting {delay:0.0}s before playing intro");
                yield return new WaitForSeconds(delay);
            }

            _source.clip = clip;
            _source.volume = volume;
            _source.Play();
            _isPlaying = true;
            _hasStarted = true;

            if (debugLogs) Debug.Log($"[IntroAudio] Playing '{clip.name}' ({clip.length:0.0}s)");
        }

        public void Stop()
        {
            if (!_isPlaying) return;
            if (_source != null && _source.isPlaying) _source.Stop();
            _isPlaying = false;
            if (debugLogs) Debug.Log("[IntroAudio] Stopped externally.");
        }

        void Update()
        {
            if (!_isPlaying) return;

            if (_source != null && !_source.isPlaying)
            {
                _isPlaying = false;
                if (debugLogs) Debug.Log("[IntroAudio] Finished naturally.");
                return;
            }

            var ctx = GameContext.Instance;
            if (ctx == null) return;

            var input = ctx.Input;
            if (input != null && input.InteractDown)
            {
                if (_source != null) _source.Stop();
                _isPlaying = false;
                if (debugLogs) Debug.Log("[IntroAudio] Skipped by player Interact press.");
            }
        }
    }
}
