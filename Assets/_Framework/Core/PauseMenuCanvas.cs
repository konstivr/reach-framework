using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

namespace Reach.Framework.Core
{
    /// <summary>
    /// Pause menu overlay. Toggles via input.Pause, shows buttons for
    /// Resume / Restart / Main Menu / Quit.
    /// </summary>
    public class PauseMenuCanvas : MonoBehaviour
    {
        [Header("UI")]
        public Canvas canvas;
        public GameObject overlay;
        public Button resumeButton;
        public Button restartButton;
        public Button mainMenuButton;
        public Button quitButton;

        [Header("Debug")]
        public bool debugLogs = true;

        bool _isOpen;

        void Awake()
        {
            if (overlay != null) overlay.SetActive(false);

            if (resumeButton != null)   resumeButton.onClick.AddListener(OnResume);
            if (restartButton != null)  restartButton.onClick.AddListener(OnRestart);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenu);
            if (quitButton != null)     quitButton.onClick.AddListener(OnQuit);
        }

        void Update()
        {
            var input = GameContext.Instance?.Input;
            if (input == null) return;

            if (input.PauseDown)
            {
                Toggle();
            }
        }

        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;

            if (overlay != null) overlay.SetActive(true);

            // Pause the game
            Time.timeScale = 0f;

            // Focus first button for controller nav
            if (EventSystem.current != null && resumeButton != null)
                EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);

            if (debugLogs) Debug.Log("[PauseMenu] Opened");
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;

            if (overlay != null) overlay.SetActive(false);
            Time.timeScale = 1f;

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            if (debugLogs) Debug.Log("[PauseMenu] Closed");
        }

        void OnResume()
        {
            Close();
        }

        void OnRestart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        void OnMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }

        void OnQuit()
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
    }
}
