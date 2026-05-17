using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Reach.Framework.Dialogue
{
    /// <summary>
    /// Neural TTS using Piper (https://github.com/rhasspy/piper).
    /// Runs piper CLI as a subprocess: text → wav → AudioClip.
    ///
    /// Setup:
    ///   1. pip3 install piper-tts --break-system-packages
    ///   2. Download voice models to ~/piper-voices/de/  (.onnx + .onnx.json)
    ///   3. CharacterDefinition.voiceMacOS = model name without extension
    ///      (e.g. "thorsten-medium" → loads ~/piper-voices/de/thorsten-medium.onnx)
    /// </summary>
    public class PiperTTS : MonoBehaviour, ITextToSpeech
    {
        [Header("Piper Binary")]
        [Tooltip("Path to piper executable. Leave empty for auto-detect.")]
        public string piperBinaryPath = "";

        [Header("Voice Models")]
        [Tooltip("Folder containing voice model files (.onnx + .onnx.json). ~ is expanded.")]
        public string voicesFolder = "~/piper-voices/de";

        [Tooltip("Default voice model name (without .onnx extension) if char does not specify one.")]
        public string defaultVoice = "thorsten-medium";

        [Header("Debug")]
        public bool debugLogs = true;

        // ============================================================
        // State
        // ============================================================

        string _resolvedPiperPath;
        string _resolvedVoicesFolder;

        public bool IsReady
        {
            get
            {
                if (_resolvedPiperPath == null) ResolvePaths();
                return !string.IsNullOrEmpty(_resolvedPiperPath) && File.Exists(_resolvedPiperPath);
            }
        }

        // ============================================================
        // Lifecycle
        // ============================================================

        void Awake()
        {
            ResolvePaths();
        }

        void ResolvePaths()
        {
            // Resolve piper binary
            if (!string.IsNullOrEmpty(piperBinaryPath) && File.Exists(piperBinaryPath))
            {
                _resolvedPiperPath = piperBinaryPath;
            }
            else
            {
                string[] candidates = new[]
                {
                    "/opt/homebrew/bin/piper",
                    "/usr/local/bin/piper",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/Python/3.11/bin/piper"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/Python/3.12/bin/piper"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/Python/3.13/bin/piper"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/Python/3.14/bin/piper"),
                    @"C:\\piper\\piper.exe",
                };
                foreach (var c in candidates)
                {
                    if (File.Exists(c)) { _resolvedPiperPath = c; break; }
                }
            }

            if (string.IsNullOrEmpty(_resolvedPiperPath))
            {
                Debug.LogWarning("[PiperTTS] Piper binary not found. Set piperBinaryPath in inspector.");
            }
            else if (debugLogs)
            {
                Debug.Log($"[PiperTTS] Binary: {_resolvedPiperPath}");
            }

            // Resolve voices folder (expand ~)
            _resolvedVoicesFolder = voicesFolder.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            if (!Directory.Exists(_resolvedVoicesFolder))
            {
                Debug.LogWarning($"[PiperTTS] Voices folder not found: {_resolvedVoicesFolder}");
            }
            else if (debugLogs)
            {
                Debug.Log($"[PiperTTS] Voices folder: {_resolvedVoicesFolder}");
            }
        }

        // ============================================================
        // ITextToSpeech
        // ============================================================

        public async Task<AudioClip> SynthesizeAsync(string text, string voiceName)
        {
            if (!IsReady)
            {
                Debug.LogWarning("[PiperTTS] Not ready (piper binary missing).");
                return null;
            }

            text = (text ?? "").Trim();
            if (string.IsNullOrEmpty(text)) return null;

            string voice = string.IsNullOrEmpty(voiceName) ? defaultVoice : voiceName;
            string modelPath = Path.Combine(_resolvedVoicesFolder, $"{voice}.onnx");

            if (!File.Exists(modelPath))
            {
                Debug.LogWarning($"[PiperTTS] Voice model not found: {modelPath}, falling back to '{defaultVoice}'");
                modelPath = Path.Combine(_resolvedVoicesFolder, $"{defaultVoice}.onnx");
                if (!File.Exists(modelPath))
                {
                    Debug.LogError($"[PiperTTS] Default voice not found either: {modelPath}");
                    return null;
                }
            }

            string outDir = Path.Combine(Application.persistentDataPath, "tts");
            Directory.CreateDirectory(outDir);
            string wavPath = Path.Combine(outDir, $"piper_{DateTime.Now:HHmmssfff}_{Guid.NewGuid():N}.wav");

            try
            {
                if (debugLogs) Debug.Log($"[PiperTTS] Synthesizing voice='{voice}' text='{text.Substring(0, Mathf.Min(60, text.Length))}'");

                int exit = await RunPiperProcess(modelPath, text, wavPath);
                if (exit != 0 || !File.Exists(wavPath))
                {
                    Debug.LogWarning($"[PiperTTS] piper failed exit={exit}");
                    return null;
                }

                var clip = await LoadWav(wavPath);
                if (debugLogs && clip != null)
                    Debug.Log($"[PiperTTS] OK len={clip.length:0.00}s");

                TryDelete(wavPath);
                return clip;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PiperTTS] Exception: {ex}");
                TryDelete(wavPath);
                return null;
            }
        }

        // ============================================================
        // Process & WAV loading
        // ============================================================

        async Task<int> RunPiperProcess(string modelPath, string text, string outWav)
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = _resolvedPiperPath,
                Arguments = $"--model \"{modelPath}\" --output_file \"{outWav}\"",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = new System.Diagnostics.Process { StartInfo = psi };
            proc.Start();

            // Write text to stdin and close
            await proc.StandardInput.WriteAsync(text);
            proc.StandardInput.Close();

            _ = await proc.StandardOutput.ReadToEndAsync();
            string stderr = await proc.StandardError.ReadToEndAsync();
            await Task.Run(() => proc.WaitForExit());

            if (proc.ExitCode != 0 && !string.IsNullOrEmpty(stderr) && debugLogs)
                Debug.LogWarning($"[PiperTTS] stderr: {stderr}");

            return proc.ExitCode;
        }

        static async Task<AudioClip> LoadWav(string path)
        {
            string url = "file://" + path;
            using var req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[PiperTTS] Load WAV failed: {req.error}");
                return null;
            }
            return DownloadHandlerAudioClip.GetContent(req);
        }

        static void TryDelete(string p) { try { if (File.Exists(p)) File.Delete(p); } catch { } }
    }
}
