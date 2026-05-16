using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;
using Reach.Framework.Core;

namespace Reach.Framework.Editor
{
    /// <summary>
    /// Imports a pack from a ZIP file produced by the Lovable Pack Builder.
    ///
    /// ZIP structure expected:
    ///   pack.json
    ///   images/characters/{id}.png
    ///   images/interactables/{id}.png
    ///   audio/{object_id}_{character_id}.mp3
    ///
    /// Behavior: In-place update. Existing pack folder is reused, SOs are
    /// updated (not replaced) so scene references survive.
    /// </summary>
    public static class PackImporter
    {
        [MenuItem("Tools/Reach/Import Pack from ZIP")]
        public static void ImportZipMenuItem()
        {
            string zipPath = EditorUtility.OpenFilePanel("Choose pack ZIP", "", "zip");
            if (string.IsNullOrEmpty(zipPath)) return;

            try
            {
                ImportZip(zipPath);
                EditorUtility.DisplayDialog("Pack Import", "Pack imported successfully.", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PackImporter] Failed: {ex}");
                EditorUtility.DisplayDialog("Pack Import — ERROR", ex.Message, "OK");
            }
        }

        public static void ImportZip(string zipPath)
        {
            // Extract to temp folder
            string tempDir = Path.Combine(Path.GetTempPath(), "ReachPackImport_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                ZipFile.ExtractToDirectory(zipPath, tempDir);
                Debug.Log($"[PackImporter] Extracted to: {tempDir}");

                string jsonPath = Path.Combine(tempDir, "pack.json");
                if (!File.Exists(jsonPath))
                    throw new Exception("pack.json not found in ZIP root");

                string jsonText = File.ReadAllText(jsonPath);
                var packData = JsonUtility.FromJson<PackJson>(jsonText);
                if (packData == null || string.IsNullOrEmpty(packData.packName))
                    throw new Exception("Could not parse pack.json or packName missing");

                ImportPackFromData(packData, tempDir);
            }
            finally
            {
                // Cleanup temp
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        static void ImportPackFromData(PackJson data, string tempDir)
        {
            // Target folder under Assets/_Packs/{packName}/
            string safeName = SafeName(data.packName);
            string packRoot = $"Assets/_Packs/{safeName}";
            EnsureFolder(packRoot);
            EnsureFolder($"{packRoot}/Characters");
            EnsureFolder($"{packRoot}/Interactables");
            EnsureFolder($"{packRoot}/Sprites");
            EnsureFolder($"{packRoot}/Sprites/Characters");
            EnsureFolder($"{packRoot}/Sprites/Interactables");
            EnsureFolder($"{packRoot}/Audio");

            // ----- 1. Copy images + audio into Assets, set Sprite type on images -----
            var charSprites = new Dictionary<string, Sprite>();
            var objSprites = new Dictionary<string, Sprite>();
            var audioClips = new Dictionary<string, AudioClip>(); // key: "objId_charId"

            foreach (var c in data.characters ?? new List<CharacterJson>())
            {
                if (string.IsNullOrEmpty(c.transitionImage)) continue;
                string srcPath = Path.Combine(tempDir, c.transitionImage.Replace("/", Path.DirectorySeparatorChar.ToString()));
                if (!File.Exists(srcPath)) { Debug.LogWarning($"[PackImporter] Missing char image: {srcPath}"); continue; }
                string ext = Path.GetExtension(srcPath);
                string destAsset = $"{packRoot}/Sprites/Characters/{c.id}{ext}";
                CopyAsset(srcPath, destAsset);
                ApplySpriteImportSettings(destAsset);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(destAsset);
                if (sprite != null) charSprites[c.id] = sprite;
            }

            foreach (var o in data.interactables ?? new List<InteractableJson>())
            {
                if (!string.IsNullOrEmpty(o.objectImage))
                {
                    string srcPath = Path.Combine(tempDir, o.objectImage.Replace("/", Path.DirectorySeparatorChar.ToString()));
                    if (File.Exists(srcPath))
                    {
                        string ext = Path.GetExtension(srcPath);
                        string destAsset = $"{packRoot}/Sprites/Interactables/{o.id}{ext}";
                        CopyAsset(srcPath, destAsset);
                        ApplySpriteImportSettings(destAsset);
                        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(destAsset);
                        if (sprite != null) objSprites[o.id] = sprite;
                    }
                }

                // Per-character audio
                foreach (var r in o.responsesPerCharacter ?? new List<ResponseJson>())
                {
                    if (string.IsNullOrEmpty(r.audioFile)) continue;
                    string srcPath = Path.Combine(tempDir, r.audioFile.Replace("/", Path.DirectorySeparatorChar.ToString()));
                    if (!File.Exists(srcPath)) { Debug.LogWarning($"[PackImporter] Missing audio: {srcPath}"); continue; }
                    string ext = Path.GetExtension(srcPath);
                    string destAsset = $"{packRoot}/Audio/{o.id}_{r.characterId}{ext}";
                    CopyAsset(srcPath, destAsset);
                    AssetDatabase.ImportAsset(destAsset);
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(destAsset);
                    if (clip != null) audioClips[$"{o.id}_{r.characterId}"] = clip;
                }
            }

            // ----- 2. Create / update Character SOs -----
            var charDefs = new Dictionary<string, CharacterDefinition>();
            foreach (var c in data.characters ?? new List<CharacterJson>())
            {
                string assetPath = $"{packRoot}/Characters/{c.id}.asset";
                var def = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(assetPath);
                if (def == null)
                {
                    def = ScriptableObject.CreateInstance<CharacterDefinition>();
                    AssetDatabase.CreateAsset(def, assetPath);
                }

                def.displayName = c.displayName ?? c.id;
                def.characterId = c.id;
                def.gateTtsLine = c.gateTtsLine ?? def.gateTtsLine;
                def.gatePassphrase = c.gatePassphrase ?? def.gatePassphrase;
                if (c.gateSimilarityThreshold > 0f) def.gateSimilarityThreshold = c.gateSimilarityThreshold;
                if (!string.IsNullOrEmpty(c.chatSystemPrompt)) def.chatSystemPrompt = c.chatSystemPrompt;
                if (!string.IsNullOrEmpty(c.voiceMac)) def.voiceMacOS = c.voiceMac;
                if (!string.IsNullOrEmpty(c.voiceWindows)) def.voiceWindows = c.voiceWindows;
                if (c.ambientVolume > 0f) def.ambientVolume = c.ambientVolume;
                if (c.ambientPitch > 0f) def.ambientPitch = c.ambientPitch;

                if (charSprites.TryGetValue(c.id, out var sp)) def.transitionImage = sp;

                EditorUtility.SetDirty(def);
                charDefs[c.id] = def;
            }

            // ----- 3. Create / update Interactable SOs -----
            var iodefs = new List<InteractableObjectDefinition>();
            foreach (var o in data.interactables ?? new List<InteractableJson>())
            {
                string assetPath = $"{packRoot}/Interactables/{o.id}.asset";
                var iod = AssetDatabase.LoadAssetAtPath<InteractableObjectDefinition>(assetPath);
                if (iod == null)
                {
                    iod = ScriptableObject.CreateInstance<InteractableObjectDefinition>();
                    AssetDatabase.CreateAsset(iod, assetPath);
                }

                if (!string.IsNullOrEmpty(o.mode) && Enum.TryParse<InteractActionMode>(o.mode, out var mode))
                    iod.mode = mode;
                if (o.interactRadius > 0f) iod.interactRadius = o.interactRadius;
                if (!string.IsNullOrEmpty(o.promptText)) iod.promptText = o.promptText;
                if (!string.IsNullOrEmpty(o.secondStepPromptText)) iod.secondStepPromptText = o.secondStepPromptText;
                iod.unlocksOutreach = o.unlocksOutreach;

                if (objSprites.TryGetValue(o.id, out var sp)) iod.objectImage = sp;

                // Per-character responses
                iod.responsesPerCharacter = new List<CharacterResponse>();
                foreach (var r in o.responsesPerCharacter ?? new List<ResponseJson>())
                {
                    var resp = new CharacterResponse();
                    if (charDefs.TryGetValue(r.characterId, out var charDef)) resp.character = charDef;
                    resp.responseText = r.responseText ?? "";
                    if (r.responseDurationSeconds > 0f) resp.responseDurationSeconds = r.responseDurationSeconds;
                    if (r.audioVolume > 0f) resp.audioVolume = r.audioVolume;
                    if (audioClips.TryGetValue($"{o.id}_{r.characterId}", out var clip)) resp.audioClip = clip;
                    iod.responsesPerCharacter.Add(resp);
                }

                // Default response
                if (o.defaultResponse != null)
                {
                    iod.defaultResponse.responseText = o.defaultResponse.responseText ?? "";
                    if (o.defaultResponse.responseDurationSeconds > 0f)
                        iod.defaultResponse.responseDurationSeconds = o.defaultResponse.responseDurationSeconds;
                }

                EditorUtility.SetDirty(iod);
                iodefs.Add(iod);
            }

            // ----- 4. Create / update Pack SO -----
            string packAssetPath = $"{packRoot}/{data.packName}.asset";
            var pack = AssetDatabase.LoadAssetAtPath<StoryPack>(packAssetPath);
            if (pack == null)
            {
                pack = ScriptableObject.CreateInstance<StoryPack>();
                AssetDatabase.CreateAsset(pack, packAssetPath);
            }
            pack.packName = data.packName;
            pack.description = data.description ?? "";
            pack.language = string.IsNullOrEmpty(data.language) ? "en" : data.language;
            pack.maxPerspectives = data.maxPerspectives;
            pack.characters = new List<CharacterDefinition>(charDefs.Values);
            EditorUtility.SetDirty(pack);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PackImporter] Pack '{data.packName}' imported: " +
                      $"{charDefs.Count} characters, {iodefs.Count} interactables, " +
                      $"{audioClips.Count} audio clips.");
        }

        // ============================================================
        // Helpers
        // ============================================================

        static void CopyAsset(string srcAbs, string destAssetPath)
        {
            string destAbs = Path.Combine(Application.dataPath, "..", destAssetPath);
            string destDir = Path.GetDirectoryName(destAbs);
            if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
            File.Copy(srcAbs, destAbs, true);
            AssetDatabase.ImportAsset(destAssetPath);
        }

        static void ApplySpriteImportSettings(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        static string SafeName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "Pack";
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Trim();
        }

        // ============================================================
        // JSON DTOs (matching Lovable export)
        // ============================================================

        [Serializable]
        class PackJson
        {
            public string packName;
            public string description;
            public string language;
            public int maxPerspectives;
            public List<CharacterJson> characters;
            public List<InteractableJson> interactables;
            public List<string> musicLayerPaths;
        }

        [Serializable]
        class CharacterJson
        {
            public string id;
            public string displayName;
            public string transitionImage;
            public string gateTtsLine;
            public string gatePassphrase;
            public float gateSimilarityThreshold;
            public string chatSystemPrompt;
            public string voiceMac;
            public string voiceWindows;
            public string ambientLoopPath;
            public float ambientVolume;
            public float ambientPitch;
        }

        [Serializable]
        class InteractableJson
        {
            public string id;
            public string mode;
            public string objectImage;
            public float interactRadius;
            public string promptText;
            public string secondStepPromptText;
            public bool unlocksOutreach;
            public List<ResponseJson> responsesPerCharacter;
            public ResponseJson defaultResponse;
        }

        [Serializable]
        class ResponseJson
        {
            public string characterId;
            public string responseText;
            public float responseDurationSeconds;
            public string audioClipPath;
            public string firstStepAudioClipPath;
            public float audioVolume;
            public string audioFile;
        }
    }
}
