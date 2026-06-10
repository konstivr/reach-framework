using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

namespace Reach.Framework.Core
{
    /// <summary>
    /// MainMenu logic: title screen + character selection.
    /// Two panels managed: TitlePanel (Spielen / Beenden buttons)
    /// and CharSelectPanel (one button per character).
    ///
    /// Selected character is stored in PlayerPrefs and read by the game scene.
    /// Resets to "default A" (first char in pack) every time the menu loads.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        public const string PREF_KEY_SELECTED_CHAR = "ReachSelectedCharId";

        [Header("Story Pack (drag pack SO here)")]
        public StoryPack pack;

        [Header("Game Scene to load")]
        public string gameSceneName = "Outreach_SmokeTest";

        [Header("Panels")]
        public GameObject titlePanel;
        public GameObject charSelectPanel;

        [Header("Title Panel")]
        public TMPro.TMP_Text titleText;
        public TMPro.TMP_Text subtitleText;
        public Button playButton;
        public Button quitButton;

        [Header("Char Select Panel")]
        [Tooltip("Parent transform that will hold dynamically-spawned char buttons.")]
        public Transform charButtonContainer;
        [Tooltip("Prefab for a single character button. Should have an Image + TMP_Text inside.")]
        public GameObject charButtonPrefab;
        public Button charSelectBackButton;

        [Header("Debug")]
        public bool debugLogs = true;

        void Start()
        {
            // Safety net: reset audio + time state in case we returned from a paused game
            AudioListener.pause = false;
            Time.timeScale = 1f;

            // Reset to default on every menu load
            PlayerPrefs.DeleteKey(PREF_KEY_SELECTED_CHAR);

            // Wire up title panel
            if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
            if (charSelectBackButton != null) charSelectBackButton.onClick.AddListener(OnBackClicked);

            // Set title from pack
            if (pack != null)
            {
                if (titleText != null) titleText.text = "REACH";
                if (subtitleText != null) subtitleText.text = pack.packName;
            }

            ShowTitlePanel();
        }

        public void ShowTitlePanel()
        {
            if (titlePanel != null) titlePanel.SetActive(true);
            if (charSelectPanel != null) charSelectPanel.SetActive(false);

            // Delay one frame to ensure EventSystem is ready
            if (playButton != null)
                StartCoroutine(SelectNextFrame(playButton.gameObject));
        }

        System.Collections.IEnumerator SelectNextFrame(GameObject go)
        {
            yield return null;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(go);
        }

        public void ShowCharSelectPanel()
        {
            if (titlePanel != null) titlePanel.SetActive(false);
            if (charSelectPanel != null) charSelectPanel.SetActive(true);

            BuildCharButtons();
        }

        void BuildCharButtons()
        {
            if (charButtonContainer == null || charButtonPrefab == null || pack == null) return;

            // Clear existing
            for (int i = charButtonContainer.childCount - 1; i >= 0; i--)
                Destroy(charButtonContainer.GetChild(i).gameObject);

            // Spawn one per character
            GameObject firstButton = null;
            foreach (var c in pack.characters)
            {
                if (c == null) continue;

                var go = Instantiate(charButtonPrefab, charButtonContainer);
                go.name = $"CharBtn_{c.characterId}";

                // Set image — find child explicitly by name (not via GetComponentInChildren
                // which would return the root Button image first)
                Transform imgT = go.transform.Find("CharImage");
                if (imgT == null)
                {
                    // Fallback: find any Image that isn't the root one
                    foreach (var i in go.GetComponentsInChildren<Image>(true))
                    {
                        if (i.transform != go.transform) { imgT = i.transform; break; }
                    }
                }
                if (imgT != null)
                {
                    var img = imgT.GetComponent<Image>();
                    if (img != null && c.transitionImage != null)
                    {
                        img.sprite = c.transitionImage;
                        img.preserveAspect = true;
                    }
                }

                // Set label
                var label = go.GetComponentInChildren<TMPro.TMP_Text>();
                if (label != null) label.text = c.displayName;

                // Wire button
                var btn = go.GetComponent<Button>();
                if (btn != null)
                {
                    string charId = c.characterId;
                    btn.onClick.AddListener(() => OnCharSelected(charId));
                    if (firstButton == null) firstButton = go;
                }
            }

            // Focus first button for controller nav
            if (firstButton != null)
                StartCoroutine(SelectNextFrame(firstButton));
        }

        void OnPlayClicked()
        {
            if (debugLogs) Debug.Log("[MainMenu] Play clicked -> loading game scene directly (Char 0 starts)");
            // Bypass CharSelect: PREF_KEY remains unset -> PerspectiveManager falls back to StoryPack.StartCharacter (= Char 0, the neutral observer).
            // CharSelect is now only accessible via Pause-Menu in-game.
            PlayerPrefs.DeleteKey(PREF_KEY_SELECTED_CHAR);
            PlayerPrefs.Save();
            SceneManager.LoadScene(gameSceneName);
        }

        void OnQuitClicked()
        {
            if (debugLogs) Debug.Log("[MainMenu] Quit clicked");
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }

        void OnBackClicked()
        {
            ShowTitlePanel();
        }

        void OnCharSelected(string charId)
        {
            if (debugLogs) Debug.Log($"[MainMenu] Char selected: {charId}");
            PlayerPrefs.SetString(PREF_KEY_SELECTED_CHAR, charId);
            PlayerPrefs.Save();

            SceneManager.LoadScene(gameSceneName);
        }
    }
}
