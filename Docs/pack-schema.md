# Pack JSON Schema

A Reach pack is defined as a single JSON file plus a folder of audio assets.
Audio paths in the JSON are **relative to the JSON file's location**.

## Folder layout (recommended)

my-pack-export/
- pack.json
- Audio/
  - stasi_ambient.wav
  - stasi_reaction_phone.wav

## Top-level structure

See pack-example-ddr.json for a complete example.

| Field             | Type     | Notes                                                      |
|-------------------|----------|------------------------------------------------------------|
| packName          | string   | Display name of the pack.                                  |
| description       | string   | Optional description.                                      |
| language          | string   | Language code for STT/LLM (e.g. "de", "en").               |
| maxPerspectives   | int      | 0 = use characters.Count (recommended).                    |
| characters        | array    | See "Character".                                           |
| interactables     | array    | See "Interactable".                                        |
| musicLayerPaths   | array    | Relative paths to audio files. Added one per switch.       |

## Character fields

| Field                      | Type   | Notes                                                       |
|----------------------------|--------|-------------------------------------------------------------|
| id                         | string | Unique within pack. Used for asset filename + lookup.       |
| displayName                | string | Human-readable name.                                        |
| gateTtsLine                | string | What this character says when the player tries to reach out.|
| gatePassphrase             | string | What the player must say to switch into this character.     |
| gateSimilarityThreshold    | float  | 0.5-1.0. Higher = stricter match.                           |
| chatSystemPrompt           | string | LLM system prompt, defines character voice/personality.     |
| voiceMac                   | string | macOS say voice name (e.g. "Anna", "Samantha").             |
| voiceWindows               | string | Windows SAPI voice name.                                    |
| ambientLoopPath            | string | Optional relative path to ambient audio.                    |
| ambientVolume              | float  | 0.0-1.0.                                                    |
| ambientPitch               | float  | 0.5-1.5.                                                    |

## Interactable fields

| Field                       | Type           | Notes                                                    |
|-----------------------------|----------------|----------------------------------------------------------|
| id                          | string         | Unique within pack.                                      |
| mode                        | string         | "OneShot" or "TwoStep".                                  |
| interactRadius              | float          | Meters.                                                  |
| promptText                  | string         | HUD prompt when in range.                                |
| secondStepPromptText        | string         | TwoStep mode only.                                       |
| unlocksOutreach             | bool           | Interacting at least once unlocks outreach.              |
| responsesPerCharacter       | array          | One Response per character (by id).                      |
| defaultResponse             | object/null    | Fallback when character not in list.                     |

## Response fields

| Field                     | Type   | Notes                                                |
|---------------------------|--------|------------------------------------------------------|
| characterId               | string | Matches a Character.id. Empty = default fallback.    |
| responseText              | string | Shown in HUD on interaction.                         |
| responseDurationSeconds   | float  | How long the text stays.                             |
| audioClipPath             | string | Optional relative path to audio.                     |
| firstStepAudioClipPath    | string | TwoStep mode only - audio for the first press.       |
| audioVolume               | float  | 0.0-1.0.                                             |
