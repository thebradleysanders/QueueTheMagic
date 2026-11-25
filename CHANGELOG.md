# Changelog

Every release is published on the [Releases page](../../releases) with the notes below.
Upgrading: `docker compose pull && docker compose up -d` (database changes are applied automatically).

## [1.0.17]

### Added
- **Lead-in and lead-out for moving heads.** Requested songs now run the lead-in and lead-out of the song's FPP playlist once per show instead of never: the lead-in plays before the first requested song (or the first song after switching playlists), and the lead-out plays about 60 seconds after the queue empties or when the show closes. Set them on each playlist in FPP; nothing to configure here.
- **Show open and close sequences** (Admin, Config, FPP): pick a sequence that plays once when the show opens (season start, show hours begin, or the override is turned on) and another once when it closes. The open sequence plays before any playlist lead-in, and the close sequence plays after the lead-out. They never replay if the app restarts mid-show.

### Changed
- **Reorganised admin pages.** Config is split into five groups with a side menu (Show & schedule, Falcon Player, Requests & money, Audience, Site & integrations). Reports are split into Money, Audience and Songs. A status bar at the top of every admin page shows whether the show is live and holds the Automatic / Force on / Force off switch. Saving now uses an "unsaved changes" bar (with Discard) instead of a floating button, and the Config tab shows a dot while changes are unsaved. The Queue tab opens first and shows the live queue above the player diagnostics. "Clear for new season" moved from Reports to Config, Show & schedule. The weekly schedule fits on phones.

### Fixed
- **The default playlist now picks up where it left off** after a requested song, instead of starting over at the first song. If the interrupted song was less than half played it restarts from its beginning, otherwise the playlist continues with the next song. A new show (after the season or hours close) still starts from the top.
- The queue state showed "playing default playlist" while a requested song was playing.

## [1.0.16]

### Added
- **Clear for new season** (Admin, Reports): the old "Reset stats" button now also clears all donations and the supporters list (so the donation goal restarts at $0), song ratings, the played-song history and the live activity feed. Songs, playlists, settings, friends & family passes and anything still in the queue are kept.

### Fixed
- **Listen live on iPhones:** audio only played through headphones while the ringer switch was on silent. It now plays through the phone speaker.

## [1.0.15]

### Changed
- README rewritten, with fresh screenshots of the whole app.
- Docs corrections for MQTT.

## [1.0.14]

### Added
- **Reorder playlists and songs** in Admin with arrows or drag and drop. New items from FPP imports go to the end.

## [1.0.13]

### Added
- **Friends & family passes:** give someone a code or a link to sign in, listen, and pick songs for free, even when the show is closed. Passes can be revoked at any time.

### Fixed
- The song list no longer shows FPP's temporary playlist or empty playlists, and says "1 song" correctly.
- The Info page shows the real release version.

## [1.0.12]

### Added
- **Listen live statistics** (taps, listeners, time listened) and a **Reset stats** button.
- **Sun-based schedules:** show hours can use Dawn, Sunrise, Sunset and Dusk with offsets. FPP's latitude and longitude are imported.

### Fixed
- The default playlist no longer starts outside the season or the scheduled hours. Saving settings can no longer flip the manual override by accident, and a banner shows when the override is forcing the show on or off.
- The Listen card sometimes did not appear on first load.
- Pinch zoom is disabled on phones.

## [1.0.11]

### Added
- **Season start and end dates** and a manual **on / off / auto override**, also controllable over MQTT (`<prefix>/control/show`).
- **Audience features:** a Cheer button, a donation goal, live activity pop-ups and a festive light string.
- **Supporters list** with optional names (anonymous by default), switchable in config.
- **Listen live:** the show's audio on visitors' phones with very low delay. FPP sends Opus RTP to the server, which relays it over a WebSocket. No port forwarding. See [docs/LISTEN.md](docs/LISTEN.md).
- A redesigned, calmer interface.

### Changed
- Em dashes removed from all text.
- Removed the "neighbors tuned in" line from the home page.

## [1.0.10]

### Added
- Star favicon and a matching seasonal header logo.
