# Optional YouTube playback

The **VICE YouTube** development add-on enables single-player YouTube on Lifestyle's **$450 Wall Screen**. Install `Mods/VICE.YouTube.dll`, compatible Lifestyle/Core binaries, and the TV browser companion. Controls appear only when the adapter and companion are available.

Images, MP4s, direct URLs, and Relay work independently. Removing the add-on hides YouTube controls while preserving saved selections and favorites.

## Installation

Close the game. Install the suite or add-on package, then extract the optional `VICE-YouTube-Companion-<tag>.zip` into the Schedule I folder. The companion uses roughly **489 MiB** under `UserData/BaseBuilder/TvBrowser`. The Build-A-Base construction gameplay DLL is not required.

For a source export and an authorized original archive, run:

```powershell
./scripts/Install-TVCompanion.ps1 -Archive 'C:\path\to\Build-A-Base-0.4.26.zip'
```

Use `./Install-TVCompanion.ps1` from an extracted add-on export. The installer accepts the pinned **Build-A-Base 0.4.26 IL2CPP** archive and verifies files/licenses. Incomplete or different existing files produce an error; preserve or move the directory before retrying into an empty target. This pinned dependency version is required.

Companion redistribution permission has not been established. Preserve upstream licenses and use it within the permissions recorded in [provenance](provenance.md).

## Controls

Interact, choose **YouTube**, paste a watch/share/short/live link, and select **Play YouTube**. Existing play/pause, seeking, volume/mute, favorites, Remember, Stop, and Retry controls apply.

**CC · Toggle** toggles captions through YouTube. Availability/language depend on video/player settings; the button does not report current caption state.

Chapter marks appear on the playback bar with the current title below. Open **Chapters** to jump. Creator chapters take priority; missing metadata does not prevent playback or seeking. **Retry** reloads video and chapters.

Only one YouTube screen can be selected at a time. Selecting another clears the first. Remembered videos load paused at the beginning. Distance suspension resumes from the last browser position. Live streams and restricted videos may offer fewer controls.

## Limitations and troubleshooting

Playback stops when another player joins; multiplayer synchronization is unavailable. Browser audio follows screen/personal volume, game FX volume, and distance, fading from full at two metres to silent at 22 metres. It is not directional Unity audio.

Stop, source changes, distance suspension, and session exit release the helper. Retry recreates it after failure. Playback and chapters depend on YouTube and companion behavior, which can change independently of VICE.

Check complete companion installation and matching dependencies, then include relevant `MelonLoader/Latest.log` lines when reporting problems. See the [build guide](vice-build.md) and [TV guide](vice-lifestyle-video-sharing.md).
