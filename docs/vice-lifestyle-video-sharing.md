# Wall Screen playback and video sharing

Buy the **$450 Wall Screen** at Herbert's Bleuball's Boutique and place it on a wall. Interact to choose an image, direct HTTP(S) video URL, Relay episode, or local MP4. Controls include pause, seeking, volume, favorites, Remember, and a larger viewing mode.

Put images in `UserData/VICE/Lifestyle/Television/Images` and MP4s in `UserData/VICE/Lifestyle/Television/Videos`, relative to the game folder. TV preferences use `PersonalTV` in `UserData/MelonPreferences.cfg`; the name is retained for compatibility.

## Local MP4 playback

Choose **Local MP4**, select a file, then press **Play**. Selection leaves the screen paused.

Single-player uses the original file without a sharing size cap or cache copy. In multiplayer, a host or guest can supply a file up to **512 MiB**. Use MP4 with H.264 video and AAC audio. All participants need matching Lifestyle/Core binaries and compatible S1API/MelonLoader.

Selecting a shared file sends its name, size, and checksum; it does not upload the video or synchronize the folder. On Play, a guest source uploads to the host if needed, then the host distributes it to peers without a copy. Playback waits until connected peers have the file. A late joiner downloads the playing file and catches up; paused selections do not download automatically.

Pause cancels unfinished transfers. Stop or a source change cancels transfers no other screen needs. Press Play to retry. Synchronization follows the host timeline with periodic correction; it is not frame accurate. Local single-player playback stops when another player joins; reselect a shareable file to play together.

## Storage and limits

- Received files use `UserData/VICE/Lifestyle/Television/Cache/Videos`, with a **1 GiB** limit including incoming reservations.
- Unselected cached videos can be evicted. Selected videos are protected; stop another screen if they fill the cache.
- Downloads and partial files are deleted on session exit and initialization. Originals are retained. Video bytes are not stored in saves.
- Each sender has a **1 MiB/s** total payload budget shared across destinations. Large files or several peers can take minutes to transfer.
- Transfers fail after 60 seconds without progress. Initial sharing allows at least 30 minutes, extended for file size and peer count. Failures leave playback paused and remove partial files.

Saved selections load paused. If a host's file changed, select it again. After reloading a guest-supplied selection, that guest should reselect the original so the host can find its supplier. Remember and Favorites store metadata.

## Troubleshooting

A genuine two-computer test remains required for throughput, audio, timing correction, and late joins. Check file size/format, cache capacity, matching dependencies, and `MelonLoader/Latest.log`.

See the [user-data guide](vice-user-data.md) and [optional YouTube guide](vice-lifestyle-youtube.md). Remove screens from saves before uninstalling Lifestyle.
