using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Reach.Framework.Core
{
    /// <summary>
    /// In-game character select overlay. Opened from PauseMenuCanvas via the
    /// "Change Character" button. Shows all non-neutral characters from the
    /// active StoryPack. Selection triggers a direct switch (with transition)
    /// to the chosen character and resumes the game.
    ///
    /// Dynamic button generation, reusing the same prefab/container pattern
    /// as MainMenuController.BuildCharButtons.
    /// </summary>
    public class CharSelectOverlay : MonoBehaviour
    {
        [Header("UI")]
        public GameObject overlay;
        [Tooltip("Parent transform that will hold dynamically-spawned char buttons.")]
        public Transform charButtonContainer;
        [Tooltip("Prefab for a single character button. Should have an Image + TMP_Text inside.")]
        public GameObject charButtonPrefab;
        public Button backButton;

        [Header("Behavior")]
        [Tooltip("If true: also include the current active character in the list (greyed out). If false: hide it.")]
        public bool showCurrentCharacter = false;

        [Header("Debug")]
        public bool debugLogs = true;

        bool _isOpen;
        bool _isSwitching;

        void Awake()
        {
            if (overlay != null) overlay.SetActive(false);
            if (backButton != null) backButton.onClick.AddListener(OnBack);
        }

        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;

            if (overlay != null) overlay.SetActive(true);

            // Time.timeScale stays at 0 (passed from PauseMenu)
            BuildCharButtons();

            if (debugLogs) Debug.Log("[CharSelectOverlay] Opened");
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;

            if (overlay != null) overlay.SetActive(false);

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            if (debugLogs) Debug.Log("[CharSelectOverlay] Closed");
        }

        void OnBack()
        {
            // Cancel and return to pause menu (game stays paused)
            Close();
            var pause = FindObjectOfType<PauseMenuCanvas>();
            if (pause != null) pause.Open();
        }

        void BuildCharButtons()
        {
            if (charButtonContainer == null || charButtonPrefab == null) return;

            var ctx = GameContext.Instance;
            var pack = ctx?.pack;
            if (pack == null)
            {
                Debug.LogWarning("[CharSelectOverlay] No active pack.");
                return;
            }

            // Clear existing
            for (int i = charButtonContainer.childCount - 1; i >= 0; i--)
                Destroy(charButtonContainer.GetChild(i).gameObject);

            var current = ctx.Perspective?.Current;
            GameObject firstButton = null;

            foreach (var c in pack.characters)
            {
                if (c == null) continue;
                // Filter neutral starter (Char 0 cannot be returned to)
                if (c.isNeutralStarter) continue;
                // Optionally filter current
                if (!showCurrentCharacter && current != null && current.Definition == c) continue;

                var go = Instantiate(charButtonPrefab, charButtonContainer);
                go.name = $"CharBtn_{c.characterId}";

                // Set image (same lookup as MainMenuController)
                Transform imgT = go.transform.Find("CharImage");
                if (imgT == null)
                {
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

                // Wire button — direct switch
                var btn = go.GetComponent<Button>();
                if (btn != null)
                {
                    CharacterDefinition defRef = c;
                    btn.onClick.AddListener(() => OnCharSelected(defRef));
                    if (firstButton == null) firstButton = go;
                }
            }

            if (firstButton != null)
                StartCoroutine(SelectNextFrame(firstButton));
        }

        IEnumerator SelectNextFrame(GameObject go)
        {
            yield return null;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(go);
        }

        void OnCharSelected(CharacterDefinition def)
        {
            if (_isSwitching) return;
            if (def == null) return;
            _isSwitching = true;

            if (debugLogs) Debug.Log($"[CharSelectOverlay] Selected '{def.characterId}' -> direct switch");

            var ctx = GameContext.Instance;
            var target = ctx?.Characters?.FindByDefinition(def);
            if (target == null)
            {
                Debug.LogWarning("[CharSelectOverlay] Selected char not found in scene.");
                _isSwitching = false;
                return;
            }

            // Resume time scale BEFORE starting transition (transition uses real time, but other systems need normal flow)
            Time.timeScale = 1f;

            // Hide overlay before transition starts
            Close();

            StartCoroutine(DoSwitchWithTransition(target));
        }

        IEnumerator DoSwitchWithTransition(PossessableCharacter target)
        {
            var transition = FindObjectOfType<Reach.Framework.FX.ReachTransitionFX>();
            if (transition != null)
            {
                var task = transition.PlayAndSwitchAsync(target);
                yield return new WaitUntil(() => task.IsCompleted);
                if (task.Exception != null)
                    Debug.LogError($"[CharSelectOverlay] Transition error: {task.Exception}");
            }
            else
            {
                var ctx = GameContext.Instance;
                ctx?.Perspective?.TrySwitchTo(target);
            }

            _isSwitching = false;
        }
    }
}
