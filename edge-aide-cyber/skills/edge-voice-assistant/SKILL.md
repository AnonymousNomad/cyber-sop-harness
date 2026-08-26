# Edge Voice Assistant

## What To Do
Implement a voice interaction layer with three components: wake word detection ("Hey Cipher"), speech-to-text via whisper.cpp, and text-to-speech via Android's built-in TTS. The voice path feeds into the same governed pipeline as text input — no bypass.

## Why
On a tablet, voice is faster than typing for quick queries. "Hey Cipher, scan this domain" is more natural than typing `/tool dns.reverse example.com`. The governance layer ensures voice commands follow the same authorization rules.

## Architecture
```
Microphone → [Wake Word "Hey Cipher"] → [whisper.cpp STT]
  → [Context Manager] → [Model] → [Response]
  → [termux-tts-speak] → Speaker
```

## Code Guidance
- `src/model/voice-assistant.mjs`: Detects capabilities at boot via `Promise.allSettled` (parallel, 2s timeout per check). Checks for `whisper-cli` (STT), `termux-tts-speak` (TTS), `porcupine` (wake word).
- `processVoiceQuery(transcript)`: Normalizes input, checks for slash commands, routes to context manager + model. Returns `{ok, type, response, shouldSpeak}`.
- `speak(text)`: Calls `termux-tts-speak` with 500-char limit. Returns `{spoken: true/false}`.
- Wake words: `['hey cipher', 'hey cyber', 'ok cipher']` — all lowercase, configurable.

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Voice command bypasses governance | Unauthorized action | Voice goes through same policy engine as text |
| Whisper model not installed | No STT available | Graceful fallback to text-only mode |
| Background noise triggers false wake | Unintended activation | Require explicit wake word; consider energy threshold |
| TTS reads secrets aloud | Credential exposure | Sanitize all output before TTS; never speak redacted tokens |

## Dependencies
- `whisper-cli` binary (optional — install via Termux `pkg install whisper.cpp`)
- `termux-api` package (optional — provides `termux-tts-speak`)
- `porcupine` binary (optional — wake word detection)
- Model provider (for processing voice queries)

## Pitfalls
- No audio hardware in dev environment — test capabilities detection only, not actual audio
- `termux-tts-speak` truncates at ~500 chars; longer responses need chunking
- Android kills background microphone access when screen locks; implement re-acquisition
- Whisper tiny model (~75MB) needs separate download; not bundled
- Voice queries produce evidence chain entries with `type: 'voice.query'` for audit trail
