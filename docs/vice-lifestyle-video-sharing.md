# VICE Lifestyle 0.1.2: on-demand local video sharing

Put an MP4 directly in `Schedule I/UserData/VICE/Lifestyle/Television/Videos`. Open a Wall Screen, choose **Local MP4**, select the file, then press **Play**. Both hosts and guests can supply videos. Everyone in the session needs VICE Lifestyle 0.1.2, VICE Core 0.1.1 and compatible S1API/MelonLoader. Existing `UserData/PersonalTV` files move automatically on initialization; see the [user-data migration guide](vice-user-data.md).

Selecting a file hashes that file in the background and sends its name, size and SHA-256 identity. It does not upload the video, scan the contents of other videos, or synchronize the folder. The wall screen stays off until Play. Direct Video URL and Relay playback retain their existing behavior.

On Play, a guest's selected file uploads to the host if needed. The host distributes that one file to connected peers that do not already have it. Initial playback waits for the host and all connected peers to have the file, then starts the shared timeline. A late joiner downloads the currently playing file and catches up to the host's position. A late joiner does not download a paused selection. Decoder preparation and periodic timing correction still apply; this is not frame-accurate synchronization.

Pause while sharing cancels unfinished transfers. Stop or switching sources also cancels transfers for a video that no other screen needs. Press Play to restart an interrupted transfer. A successfully received copy can be reused during the same session. Seeking and shared controls continue to use the existing host-authoritative timeline and proximity checks.

## Storage and transfer limits

- Each file can be up to **512 MiB**. Use MP4 containing H.264 video and AAC audio for the existing game decoder.
- Received files go into `UserData/VICE/Lifestyle/Television/Cache/Videos`, with a **1 GiB total limit**, including reserved space for incoming transfers. Original files in `Videos` are never copied into this cache or deleted.
- Unselected cached videos are evicted when more space is needed. Selected videos are protected. If those selections fill the cache, stop another screen or choose a different source before retrying.
- Downloaded videos and partial files are deleted when leaving the session and during initialization. Video bytes are never copied into the game save. Remember and Favorites store selection metadata only.
- The host and each uploading guest send at most **1 MiB of video payload per second in total**, split across destinations. Data is written to disk in 16 KiB chunks and verified incrementally. The network queue never holds an entire video. Large files or several peers can take several minutes to share.
- Transfers fail after 60 seconds without incoming data; initial sharing has a 30-minute overall timeout. Failed transfers leave playback paused and remove partial files.

Saved video selections start paused without downloading. A host's original can be found again by filename and verified when Play is pressed. If a saved file has changed, select it again. For a guest's video after reloading the session, that guest should reselect the original from Local MP4 so the host knows who can supply it. Existing selections saved as the older single-player local-video type must likewise be reselected to use sharing.

## Build and validation

Run `scripts/Build-VICE.ps1 -Modules Lifestyle` to compile VICE Lifestyle and the current Core, execute the migration, state, transfer and Relay checks, check native Harmony patch targets, and export both DLLs in one update ZIP. The command prints the fresh export directory and ZIP path. Install with `scripts/Install-VICE.ps1 -PackageDir <export-directory>` while the game is closed. To update purchases in the same export, select `-Modules Commerce,Lifestyle`. The package does not include video files.

The tests execute the production video cache and transfer coordinator with transport doubles. They cover no traffic before Play, host and guest sources, checksum failures, sequential chunks, cancellation, the readiness barrier, disconnected peers, storage reservations, eviction and session cleanup. Compilation uses the installed IL2CPP assemblies with warnings treated as errors. A live two-player playtest is still required to verify FishNet transport throughput, decoder/audio behavior and catch-up under real network conditions.
