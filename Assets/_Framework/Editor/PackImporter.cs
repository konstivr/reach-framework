using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Reach.Framework.Core;

namespace Reach.Framework.EditorTools
{
    /// <summary>
    /// Imports a Pack defined as JSON into Unity assets.
    /// Creates/updates the StoryPack SO + all CharacterDefinition SOs + all InteractableObjectDefinition SOs.
    ///
    /// JSON schema is documented in Docs/pack-schema.md.
    ///
    /// Workflow:
    ///   1) Tools -> Reach -> Import Pack from JSON
    ///   2) Pick a .json file
    ///   3) Pick the target Asset folder (e.g. Assets/_Packs/DDR/)
    ///   4) Importer creates / updates assets
    /// </summary>
    public static class PackImporter
    {
        // ============================================================
        // DTOs that mirror the JSON schema
        // ============================================================

        [Serializable] class PackJson
        {
            public string packName;
            public string description;
            public string language;
            public int maxPerspectives;
            public List<CharacterJson> characters = new List<CharacterJson>();
            public List<InteractableJson> interactables = new List<InteractableJson>();
            public List<string> musicLayerPaths = new List<string>();
        }

        [Serializable] class CharacterJson
        {
            public string id;
            public string displayName;

            public string gateTtsLine;
            public string gatePassphrase;
            public float gateSimilarityThreshold = 0.82f;

            public string chatSystemPrompt;

            public string voiceMac = "Samantha";
            public string voiceWindows = "Zira";

            public string ambientLoopPath; // relative to pack folder, e.g. "Audio/stasi_ambient.wav"
            public float ambientVolume = 0.1f;
            public float ambientPitch = 1.0f;
        }

        [Serializable] class InteractableJson
        {
            public string id;
            public string mode = "OneShot"; // OneShot | TwoStep
            public float interactRadius = 2.5f;

            public string promptText = "Press Interact";
            public string secondStepPromptText = "Press again";

            public bool unlocksOutreach = true;

            public List<ResponseJson> responsesPerCharacter = new List<ResponseJson>();
            public ResponseJson defaultResponse;
        }

        [Serializable] class ResponseJson
        {
            public string characterId; // matches CharacterJson.id (empty = default fallback)
            public string responseText;
            public float responseDurationSeconds = 2.5f;
            public string audioClipPath;          // relative to pack folder
            public string firstStepAudioClipPath; // relative to pack folder
            public float audioVolume = 1.0f;
        }

        // ============================================================
        // Menu entry
        // ============================================================

        [MenuItem("Tools/Reach/Import Pack from JSON...")]
        public static void ImportFromMenu()
        {
            string jsonPath = EditorUtility.OpenFilePanel("Select pack JSON", "", "json");
            if (string.IsNullOrEmpty(jsonPath)) return;

            string targetAbsFolder = EditorUtility.OpenFolderPanel(
                "Select target asset folder (under Assets/_Packs/)",
                Path.Combine(Application.dataPath, "_Packs"),
                "");
            if (string.IsNullOrEmpty(targetAbsFolder)) return;

            // Convert to project-relative
            if (!targetAbsFolder.StartsWith(Application.dataPath))
            {
                EditorUtility.DisplayDialog("Invalid folder",
                    "Target folder must be inside the Assets/ folder.", "OK");
                return;
            }

            string targetAssetFolder = "Assets" + targetAbsFolder.Substring(Application.dataPath.Length);
            Import(jsonPath, targetAssetFolder);
        }

        // ============================================================
        // Importer core
        // ============================================================

        public static void Import(string jsonPath, string targetAssetFolder)
        {
            if (!File.Exists(jsonPath))
            {
                Debug.LogError($"[PackImporter] JSON file not found: {jsonPath}");
                return;
            }

            string jsonText;
            try { jsonText = File.ReadAllText(jsonPath); }
            catch (Exception ex) { Debug.LogError($"[PackImporter] Read failed: {ex.Message}"); return; }

            PackJson data;
            try { data = JsonUtility.FromJson<PackJson>(jsonText); }
            catch (Exception ex) { Debug.LogError($"[PackImporter] JSON parse failed: {ex.Message}"); return; }

            if (data == null)
            {
                Debug.LogError("[PackImporter] JSON parsed to null.");
                return;
            }

            // The folder containing the JSON file. Audio paths are relative to this folder.
            string jsonFolder = Path.GetDirectoryName(jsonPath);

            // Make sure target folder + subfolders exist
            EnsureFolder(targetAssetFolder);
            EnsureFolder(targetAssetFolder + "/Characters");
            EnsureFolder(targetAssetFolder + "/Interactables");

            // ----- Characters -----
            var charLookup = new Dictionary<string, CharacterDefinition>();

            foreach (var cj in data.characters)
            {
                if (string.IsNullOrEmpty(cj.id))
                {
                    Debug.LogWarning("[PackImporter] Character without id, skipped.");
                    continue;
                }

                string assetPath = $"{targetAssetFolder}/Characters/{cj.id}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(assetPath);

                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<CharacterDefinition>();
                    AssetDatabase.CreateAsset(asset, assetPath);
                }

                asset.characterId = cj.id;
                asset.displayName = cj.displayName ?? cj.id;
                asset.gateTtsLine = cj.gateTtsLine ?? "";
                asset.gatePassphrase = cj.gatePassphrase ?? "";
                asset.gateSimilarityThreshold = cj.gateSimilarityThreshold;
                asset.chatSystemPrompt = cj.chatSystemPrompt ?? "";
                asset.voiceMacOS = cj.voiceMac ?? "Samantha";
                asset.voiceWindows = cj.voiceWindows ?? "Zira";
                asset.ambientLoop = ResolveAudio(cj.ambientLoopPath, jsonFolder, targetAssetFolder);
                asset.ambientVolume = cj.ambientVolume;
                asset.ambientPitch = cj.ambientPitch;

                EditorUtility.SetDirty(asset);
                charLookup[cj.id] = asset;
            }

            // ----- Interactables -----
            var interactableAssets = new List<InteractableObjectDefinition>();

            foreach (var ij in data.interactables)
            {
                if (string.IsNullOrEmpty(ij.id))
                {
                    Debug.LogWarning("[PackImporter] Interactable without id, skipped.");
                    continue;
                }

                string assetPath = $"{targetAssetFolder}/Interactables/{ij.id}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<InteractableObjectDefinition>(assetPath);

                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<InteractableObjectDefinition>();
                    AssetDatabase.CreateAsset(asset, assetPath);
                }

                asset.mode = ij.mode == "TwoStep" ? InteractActionMode.TwoStep : InteractActionMode.OneShot;
                asset.interactRadius = ij.interactRadius;
                asset.promptText = ij.promptText ?? "Press Interact";
                asset.secondStepPromptText = ij.secondStepPromptText ?? "Press again";
                asset.unlocksOutreach = ij.unlocksOutreach;

                // Per-character responses
                asset.responsesPerCharacter = new List<CharacterResponse>();
                foreach (var rj in ij.responsesPerCharacter)
                {
                    if (rj == null) continue;
                    asset.responsesPerCharacter.Add(BuildResponse(rj, charLookup, jsonFolder, targetAssetFolder));
                }

                // Default response (fallback)
                asset.defaultResponse = ij.defaultResponse != null
                    ? BuildResponse(ij.defaultResponse, charLookup, jsonFolder, targetAssetFolder)
                    : new CharacterResponse();

                EditorUtility.SetDirty(asset);
                interactableAssets.Add(asset);
            }

            // ----- Pack manifest -----
            string packAssetPath = $"{targetAssetFolder}/{SafeFileName(data.packName)}.asset";
            var pack = AssetDatabase.LoadAssetAtPath<StoryPack>(packAssetPath);

            if (pack == null)
            {
                pack = ScriptableObject.CreateInstance<StoryPack>();
                AssetDatabase.CreateAsset(pack, packAssetPath);
            }

            pack.packName = data.packName ?? "Untitled Pack";
            pack.description = data.description ?? "";
            pack.language = data.language ?? "en";
            pack.maxPerspectives = data.maxPerspectives;
            pack.characters = new List<CharacterDefinition>();
            foreach (var cj in data.characters)
                if (charLookup.TryGetValue(cj.id, out var charAsset))
                    pack.characters.Add(charAsset);

            pack.musicLayers = new List<AudioClip>();
            foreach (var path in data.musicLayerPaths)
            {
                var clip = ResolveAudio(path, jsonFolder, targetAssetFolder);
                if (clip != null) pack.musicLayers.Add(clip);
            }

            EditorUtility.SetDirty(pack);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PackImporter] Imported '{pack.packName}': " +
                      $"{pack.characters.Count} characters, {interactableAssets.Count} interactables. " +
                      $"-> {packAssetPath}");

            // Select the new pack in the project window
            Selection.activeObject = pack;
            EditorGUIUtility.PingObject(pack);
        }

        // ============================================================
        // Helpers
        // ============================================================

        static CharacterResponse BuildResponse(
            ResponseJson rj,
            Dictionary<string, CharacterDefinition> charLookup,
            string jsonFolder,
            string targetAssetFolder)
        {
            var r = new CharacterResponse
            {
                responseText = rj.responseText ?? "",
                responseDurationSeconds = rj.responseDurationSeconds,
                audioVolume = rj.audioVolume,
            };

            if (!string.IsNullOrEmpty(rj.characterId) && charLookup.TryGetValue(rj.characterId, out var character))
                r.character = character;

            r.audioClip = ResolveAudio(rj.audioClipPath, jsonFolder, targetAssetFolder);
            r.firstStepAudioClip = ResolveAudio(rj.firstStepAudioClipPath, jsonFolder, targetAssetFolder);

            return r;
        }

        /// <summary>
        /// Resolve an audio file path. The path in JSON is relative to the JSON's folder.
        /// We copy the audio into the target Asset folder (under /Audio/) if it isn't already there.
        /// </summary>
        static AudioClip ResolveAudio(string relativePath, string jsonFolder, string targetAssetFolder)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;

            string absSource = Path.GetFullPath(Path.Combine(jsonFolder, relativePath));
            if (!File.Exists(absSource))
            {
                Debug.LogWarning($"[PackImporter] Audio not found: {absSource}");
                return null;
            }

            // Destination path inside Assets/
            string audioFolder = targetAssetFolder + "/Audio";
            EnsureFolder(audioFolder);

            string fileName = Path.GetFileName(absSource);
            string destAssetPath = $"{audioFolder}/{fileName}";
            string destAbs = Path.Combine(Application.dataPath, "..", destAssetPath);

            // Copy if missing or stale
            try
            {
                if (!File.Exists(destAbs) || File.GetLastWriteTime(absSource) > File.GetLastWriteTime(destAbs))
                {
                    File.Copy(absSource, destAbs, true);
                    AssetDatabase.ImportAsset(destAssetPath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PackImporter] Copy failed for {absSource}: {ex.Message}");
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<AudioClip>(destAssetPath);
        }

        static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;

            string parent = Path.GetDirectoryName(assetFolder).Replace("\\", "/");
            string name = Path.GetFileName(assetFolder);

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, name);
        }

        static string SafeFileName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "Pack";
            foreach (char c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return s;
        }
    }
}