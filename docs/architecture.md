# Rémi 2: native rebuild and validation

## Scope and runtime

The default application is C#/.NET 10 WPF, targeting Windows 10/11. It replaces the Electron main/preload/renderer architecture and continuous Three.js scene with one native process and retained vector drawing. WPF was chosen for native transparent windows, Windows speech, DPI-aware vector rendering, and access to the notification area and global shortcuts without a browser engine. macOS and Linux builds are not provided by this implementation.

`native/Remi.Core` handles settings, explicit Electron settings imports, theme palette generation, shared conversation context, and the Ollama/OpenAI HTTP protocols. It has no UI dependencies. `native/Remi.Desktop` owns windows, the controller, vector artwork, tray, native speech, microphone recording, and the optional Whisper subprocess. The original Electron source is retained under `legacy/electron`; pre-existing root `dist` artifacts are untouched.

## Work and resource lifetime

- Startup constructs only the pet window. Chat and settings are created on first use. Native speech initializes only when needed. No microphone or inference request is started automatically.
- The character uses frozen brushes and cached geometry. There is no render event or application frame loop. A sparse timer closes and reopens the eyes approximately every 6.4 seconds. State animations use the native animation system at 20 fps, and stop while idle or hidden. Reduced motion also disables blinking.
- Separate pet and chat windows keep the desktop overlay small. The pet uses transparent pixels for click-through; no cursor or position polling is needed. Dragging uses the native window move loop.
- One reused HTTP client serves both providers. Authorization is scoped to individual OpenAI requests and is never attached to Ollama requests. Model enumeration happens on first chat, refresh, or after an error. Chat consumes NDJSON incrementally with cancellation and a three-minute upper limit. OpenAI consumes Responses API SSE incrementally, rejects failed/incomplete streams, uses `store: false`, and limits output to 2048 tokens. It does not query Ollama when selected. Display updates are coalesced to 50 ms.
- Context is bounded to 24 recent messages and begins at a user turn. UI retention is bounded to 100 entries. The most recent user prompt is sent once. Failed and cancelled exchanges do not enter the next model context; their text is restored to the composer for retry.
- Ollama receives a 4096-token context setting, a 1024-token output limit, and an immediate model-release request by default. Keeping the model warm is an explicit setting. These options do not make the model itself smaller; inference costs remain separate from the desktop UI.
- Microphone capture writes mono 16-bit, 16 kHz PCM to a temporary WAV with a 45-second limit. Whisper reads the PCM array directly, avoiding ffmpeg and base64 copies. It runs only during transcription, on CPU with at most four threads. Cancelling terminates the worker process tree. Recording and transcription files are removed on all handled exit paths.
- Closing chat, hiding the companion, clearing chat, and quitting stop active recording or generation. Preferences use a temporary file and atomic replacement. Damaged preferences are preserved until an explicit save.

## Visual design

The character is authored in `Character/KoalaVisual.cs` as three retained vector designs. Professional is the default: a longer silhouette, restrained facial features, a tailored navy jacket, and brass pin. Cute preserves the original scalloped ears, eucalyptus scarf, warm muzzle, and botanical sprig. Robot combines a pearl helmet, cyan visor eyes, articulated red-and-gold armor, and a chest core. Koalas offer three coat palettes, including a warm golden amber, and four outfits; robot artwork uses its own signature palette. Professional and Robot use subtler motion than Cute. Expression, blinking, mouth articulation, and state movement are independent of appearance settings. The desktop companion has no caption below it.

The chat uses warm paper, eucalyptus green, generous spacing, readable message bubbles, selectable text, a clear stop control, and an explicit voice-recording state. Settings use equal-width tabs, a companion dropdown with live preview, independent Ollama/OpenAI model choices, masked API-key entry, audio choices, and motion preferences. Eucalyptus, Slate, and Midnight palettes use dynamic resources across windows and controls. Custom accent/background colors are validated as six-digit hex and foreground colors adapt for contrast. Theme preview is reversible on Cancel; Save persists it. OpenAI keys use Windows CurrentUser DPAPI in a separate atomic file; the key field never repopulates with the saved secret. Changing providers clears memory-only chat. Right-clicking the companion exposes chat, settings, hide, and Close Rémi; the close handler calls application shutdown and cleans up the tray and active work. `preview.png` is rendered from the actual native windows during the smoke test.

## Validation on 2026-10-11

Verified on Windows build 26200, x64, .NET 10.0.12:

- Release build completed with zero warnings and errors; warnings are treated as errors.
- Eighteen deterministic checks passed: host validation, settings bounds and corruption recovery, atomic save, Electron preference migration, model enumeration, streaming order and Unicode, bounded context, keep-alive choices, interrupted streams, server errors, request/stream cancellation, backwards-compatible provider/character/theme defaults, theme-color validation and contrast, OpenAI SSE Unicode and multiline data, credential isolation, missing-key handling, redacted API errors, failed/incomplete streams, and stream cancellation.
- Native smoke tests passed for the actual WPF windows, appearance changes, settings, all moods, tray creation, global shortcut registration, shortcut message dispatch, visibility, clear chat, send-button-to-HTTP streaming, cancellation, server failure feedback for both providers, all three character styles, settings save/reopen, equal tab dimensions, theme preview/cancel, encrypted key roundtrip/removal, and the right-click menu. The smoke test exits through the same Close Rémi menu handler, so a broken quit action times out validation.
- The published single-file executable passed the same native smoke test.
- Framework-dependent and standalone publishing use separate intermediate directories; each package is smoke-tested before archiving on a matching host architecture. This avoids cross-contaminating dependency and bundling caches when switching package types.
- A real local `llama3.1:8b` model returned a streamed reply. The request included immediate model release.
- Native speech generated a 16 kHz PCM WAV. The real cached Whisper `tiny` model transcribed it as “Hello Remi. It is a lovely day.” The test did not capture a live microphone.

The repeatable idle benchmark warms the app for 2.5 seconds, then measures 12 seconds with the pet visible, tray and shortcut registered, blinking enabled, and chat closed. It reports working set, private memory, and CPU as a percentage of one logical core. Snapshot/smoke-test memory is higher because those tests construct every window, initialize speech enumeration, and render several bitmaps; it is not an idle measurement.

Before the additional companion styles and provider/theme settings were added, the published framework-dependent native executable and archived Electron 28.3.3 renderer were measured sequentially on the same machine:

| Visible idle pet | Electron baseline | Native Rémi |
| --- | ---: | ---: |
| Application processes | 6 | 1 |
| Working set | 399.8 MiB | 127.9 MiB |
| Private memory | 283.1 MiB | 76.4 MiB |
| CPU, percentage of one core | 17.164% | 1.692% |

This sample shows approximately 68% less summed working set, 73% less private memory, and 90% less CPU time. These are observed application-level differences, not a claim about every Electron or WPF application. The native default pet is 280×322 DIP; the original is 400×500 DIP. The Electron baseline loads the original tracked Three.js bundle in an isolated profile through a minimal main-process harness, with microphone permission denied, no inference, and no model polling. It verifies that the original canvas mounted before measuring. The native run includes tray and shortcut registration. Both have chat closed. Summing process working sets can count shared pages more than once, so private-memory totals are also reported. The sample is short and machine-specific.

Raw measurements are preserved in [benchmarks.json](benchmarks.json). To reproduce the optional historical baseline while its original dependency installation and root `dist` output are available, run `powershell -NoProfile -File scripts/benchmark-electron.ps1`. It does not install Electron or touch the user's Electron profile. Run `scripts/benchmark.ps1 -Executable artifacts/Remi-win-x64-desktop-runtime/Remi.exe` for the corresponding native package.

Ollama and transient Whisper resource use are excluded from both measurements. The implementation removes the bundled browser/JavaScript/3D runtime, continuous application rendering, startup microphone access, polling, and persistent speech workers.

## Remaining platform checks

Live OpenAI API-account access has not been exercised; the OpenAI path is validated with deterministic HTTP/SSE fixtures and synthetic credentials. Live microphone hardware capture, physically dragging across monitors with different DPI settings, ARM64 execution, and macOS/Linux behavior have not been validated. The speech test validates native synthesis and real local transcription using a WAV fixture. A process crash or forced system shutdown can leave a temporary recording directory; normal completion, cancellation, and handled failures remove it. The app uses Windows-installed voices, which may differ from the old browser voice list.
