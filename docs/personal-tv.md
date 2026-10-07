# Personal Wall Screens 0.1.2

Buy a **Wall Screen** for $450 at Herbert's Bleuball's Boutique. Equip it and place it on a wall using the game's normal furniture placement. Interact with it to choose a source, view the larger preview, or use playback controls. Escape returns to the game; the screen keeps playing and its audio comes from the furniture.

The centered **E · Use Wall Screen** hint fades away after one second. Moving while looking at the same screen does not restart it; E remains available while the hint is hidden. Looking away and back shows it briefly again. Other furniture prompts keep their normal behavior.

## Sources and controls

- **Images:** put PNG/JPEG files directly in `Schedule I/UserData/PersonalTV/Images`, then select Rescan folder. No file picker or arbitrary computer paths are available. Source files may be up to 8 MiB and 16 megapixels. Imported images are converted to PNG, scaled to at most 2048 pixels per side, and reduced until they fit the 4 MiB transfer limit. Original files stay unchanged.
- **Video URL:** paste a direct HTTP(S) media URL. MP4 containing H.264 video and AAC audio is the recommended profile. A website/player page is not a direct media file. Each viewer downloads the video independently from that URL; the host does not upload video bytes through the game connection.
- **Relay:** the host configures the API origin and a scoped game token. Search a title, choose a season, episode, source and version. Preparation runs in the background. The client polls until Relay supplies a playable MP4 and refreshes expiring grants. This uses the API implemented in merged [Relay PR #9](https://github.com/Axl47/Relay/pull/9), fulfilling [issue #7](https://github.com/Axl47/Relay/issues/7). The mod never sends the account token to peers. Search accepts up to 200 characters; preparation allows Relay's 30-minute conversion window plus a minute for polling.
- **Favorites:** save the current image, URL or stable Relay episode selection for later use. Favorite numbers start at 1 and can be removed from the picker.
- **Local MP4:** put regular MP4 files directly in `Schedule I/UserData/PersonalTV/Videos`. These are available only in single-player and stop when another player joins. They are not uploaded. The local test installation includes a copy of `Moon Landing.mp4`; the package does not include that video.

The panel has play/pause, a seeking timeline, ±10-second controls, volume/mute, stop, retry and a larger viewing mode. Seeking requires a seekable source; remote video servers should support HTTP byte ranges. Video textures preserve source resolution up to 4096 pixels per side, while the preview and wall display preserve aspect ratio.

The **Remember** option saves a video selection with that piece of furniture. Saved videos start paused at the beginning when a save is loaded. Relay saves episode/source/version identities, then resolves a fresh playback session on Play. Images are remembered automatically. Without Remember, video selections clear on loading the save; favorites remain available.

## Multiplayer and saves

Every player needs this same mod version and compatible S1API/MelonLoader. The host owns screen state, validates controls against the requesting player's distance and an owned property, and broadcasts the media selection and playback timeline. Viewers estimate the host clock with ping samples and correct decoder drift periodically. This aims for nearby playback within roughly a second; it is not frame-accurate synchronization. Pausing, seeking, volume and source changes are shared.

Selected images use a SHA-256 cache, bounded chunk transfers and checksum verification. A late joiner requests missing image content from the host. Image files and selected state are stored with the host's normal game save under `Modded/PersonalTV`; favorites are stored there too. Playback grants, Relay tokens and active session IDs are excluded from save data. A damaged TV state file is left intact and disables TV loading/saving for that session instead of silently replacing it.

Beyond the configured playback distance, or when the game culls the furniture, the local decoder and textures are released. Shared timing continues. Returning to the screen prepares the source again and seeks to the current shared position. A paused screen returns to its paused position. Different sources and connection speeds may affect preparation and catch-up time.

Host settings live in the `[PersonalTV]` category of `UserData/MelonPreferences.cfg`: `RelayApiOrigin`, `RelayGameToken`, `PlaybackDistance` (default 35 metres), and `PersonalVolume` (local listening gain). Relay supports HTTPS, plus loopback HTTP for development. Its token is stored in this local preferences file. The host's Configure Relay panel can update it.

On a deployed Relay instance, create a scoped token under **Settings → Game Integration**, then enter its API origin and token in the screen's **Relay → Configure Relay → Save & test** panel. The merged PR still requires deployment of its database migration, media signing key, public HTTPS API URL and FFmpeg worker with subtitle support/shared media storage. A merged PR alone does not establish that the hosted service is ready. Relay grants expire and are renewed during playback; saved favorites keep episode/source/version IDs, not expiring links. The host's token has its own expiry and may need replacing.

There are limits of 64 screens and 32 favorites per save. Furniture retains the game's normal pickup/destruction behavior. Images belong to a placed furniture GUID: moving it by picking it up and placing it again creates a new screen and clears that screen's selection. Keep useful selections in Favorites. Remove custom screens from saves before uninstalling the mod, because the native furniture definition comes from this DLL.

## Build and install

Run `scripts/Build-PersonalTV.ps1` to run the standalone contract/state tests, compile the IL2CPP mod with warnings treated as errors, and create `artifacts/PersonalTV-0.1.2.zip`. Add `-Install` while the game is closed to install `Mods/Personal.TV.dll`, back up an existing DLL, and disable the old `Personal.TVProof.dll`. The script verifies the installed file hash.

Dependencies are the existing IL2CPP MelonLoader installation and S1API 3.2.1-beta.7 or a compatible version exposing its furniture builder and lifecycle APIs. S1API and MelonLoader are not bundled. The implementation does not embed a browser, scrape provider websites, or support generic HLS/DASH/DRM streams; Relay supplies the compatible file.

## Validation

Standalone tests cover the host timeline, paused/save state, exclusion of grants, image persistence, transfer ordering/duplicates/corruption/limits, path restrictions, clock sampling, and the scoped Relay JSON contract, including the merged PR's published fixtures pinned to commit `d5089752`. The opt-in native diagnostic uses an isolated copy of a save inside this workspace's `artifacts` directory. It exercises boutique-only registration, the native placement ghost's wall orientation, interaction and horizontal prompt centering, image/video display, playback controls, distance suspension, and saving. A separate reload run verifies native furniture and selected-image persistence. The loopback fixture serves the approved local test video with GET/HEAD/Range and a fake Relay game API; it does not contact the real Relay service.

Two-player synchronization and live Relay remain separate playtests. Automated decoder/rendering checks do not establish perceived audio quality or every placement angle.

Example developer-only invocation, with the game closed:

```text
Schedule I.exe --tv-system-check=C:\...\artifacts\tv-check --tv-check-save=C:\...\artifacts\tv-check\SaveGame_5 "--tv-check-video=Moon Landing.mp4"
```

Add `--tv-check-reload` to verify the previously saved screen. For HTTP/Relay fixture testing, run `tests/PersonalTV/serve-fixture.py --video "C:\Users\ortaa\Videos\Moon Landing.mp4"`, then add `--tv-check-origin=http://127.0.0.1:49183`. The diagnostic refuses normal save paths. The fixture listens only on loopback and uses a disposable test token.
