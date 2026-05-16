using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Reach.Framework.Core
{
    /// <summary>
    /// Endscreen overlay with a video player. Plays the pack's outro video,
    /// supports pause/skip via input, and fires a callback when finished.
    /// </summary>
    public class EndscreenCanvas : MonoBehaviour
    {
        [Header("UI")]
        public Canvas canvas;
        public VideoPlayer videoPlayer;
        public RawImage videoDisplay;
        public RenderTexture videoRenderTexture;

        [Header("Pause Overlay (optional)")]
        public GameObject pauseOverlay;
        public Button resumeButton;
        public Button quitButton;

        [Header("Fallback")]
        [Tooltip("If no video is set, wait this many seconds before finishing.")]
        public float fallbackHoldSeconds = 3f;

        [Header("Debug")]
        public bool debugLogs = true;

        Action _onFinished;
        bool _isShowing;
        bool _isPaused;

        void Awake()
        {
            if (canvas != null) canvas.gameObject.SetActive(false);
            if (pauseOverlay != null) pauseOverlay.SetActive(false);

            if (resumeButton != null) resumeButton.onClick.AddListener(OnResumeClicked);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
        }

        public void Show(VideoClip clip, Action onFinished)
        {
            _onFinished = onFinished;
            _isShowing = true;
            _isPaused = false;

            if (canvas != null) canvas.gameObject.SetActive(true);
            if (pauseOverlay != null) pauseOverlay.SetActive(false);

            if (clip != null && videoPlayer != null)
            {
                videoPlayer.clip = clip;
                videoPlayer.isLooping = false;
                videoPlayer.loopPointReached -= OnVideoEnd;
                videoPlayer.loopPointReached += OnVideoEnd;
                videoPlayer.Play();
                if (debugLogs) Debug.Log($"[EndscreenCanvas] Playing outro: {clip.name}");
            }
            else
            {
                if (debugLogs) Debug.Log("[EndscreenCanvas] No video — using fallback hold.");
                Invoke(nameof(Finish), fallbackHoldSeconds);
            }
        }

        void Update()
        {
            if (!_isShowing) return;

            var input = GameContext.Instance?.Input;
            if (input == null) return;

            // Pause toggle
            if (input.PauseDown)
            {
                TogglePause();
            }
        }

        void TogglePause()
        {
            _isPaused = !_isPaused;
            if (pauseOverlay != null) pauseOverlay.SetActive(_isPaused);

            if (videoPlayer != null)
            {
                if (_isPaused) videoPlayer.Pause();
                else videoPlayer.Play();
            }

            if (debugLogs) Debug.Log($"[EndscreenCanvas] Pause toggled: {_isPaused}");
        }

        void OnResumeClicked()
        {
            if (_isPaused) TogglePause();
        }

        void OnQuitClicked()
        {
            Finish();
        }

        void OnVideoEnd(VideoPlayer vp)
        {
            Finish();
        }

        void Finish()
        {
            if (!_isShowing) return;
            _isShowing = false;

            if (videoPlayer != null)
            {
                videoPlayer.Stop();
                videoPlayer.loopPointReached -= OnVideoEnd;
            }

            _onFinished?.Invoke();
            _onFinished = null;
        }
    }
}
