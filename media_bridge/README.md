# media bridge

Small background app for the island. Reads what's playing on windows, controls playback and volume, plays the ui sounds, watches the sound output and battery, checks for updates and installs them. No window, around 50 mb of ram, listens on `127.0.0.1:45455`.

Works with anything that shows up in the windows media overlay: spotify, yandex music, browsers, dotify and so on. How good seek and the progress bar are depends on the player, some of them barely report their position.

## Run

Just start `media_bridge.exe`. Only one copy runs at a time, starting it again replaces the old one.

- `media_bridge.exe %command%` in steam launch options starts dota through it
- it quits when dota closes, `--stay` turns that off
- sounds are built in, a `sounds` folder next to the exe overrides them by file name

## Endpoints

| path | what |
| --- | --- |
| `/media` | current track, cover, position, volume, waveform |
| `/media/playpause`, `/next`, `/prev` | playback |
| `/media/shuffle`, `/repeat` | toggles |
| `/media/seek?pos=<seconds>` | seek |
| `/media/volup`, `/voldown` | volume +-5% |
| `/media/like` | spotify like (spicetify + debug port) |
| `/playlist/open` | open the current Yandex Music track's playlist picker and return playlist names |
| `/playlist/add?index=<zero-based index>` | add the current track to the selected playlist |
| `/playlist/remove?index=<zero-based index>` | remove the current track from the selected playlist |
| `/sound?name=&vol=&duck=` | play a ui sound |
| `/focus` | is dota in front |
| `/status` | version, latest release, sound and spotify state |
| `/system` | default sound output, mute, battery |
| `/update/start?dir=<scripts folder>` | download the latest release and install the script |
| `/update/status` | download progress and state |
| `/update/restart` | swap in the new exe and restart |
| `/open?url=` | open the latest release page, or a page of this repo |
| `/level` | live audio level of the playing app, 5 bars |
| `/key?vk=<code>` | press a key in the dota window (a short list of safe keys) |
| `/reveal` | show `dynamic_island_debug.log` in explorer |
| `/fonts`, `/fonts/install` | check and install the sf pro fonts and the sf symbols icon font for the current user, the icon font is also copied to the cheat `fonts` folder |
| `/lyrics?artist=&title=&album=&dur=` | synced lyrics from lrclib as text (`ok`, then `ms<TAB>line` rows), or `none`, `instrumental`, `pending` while it is still downloading |
| `/diag/audio` | audio sessions the bridge sees and the app it targets, for bug reports |

## Build

```powershell
./csharp/build.ps1
```

Keep `BuiltInComInteropSupport` on, trimming turns com off and then sounds, volume and ducking silently stop working.

## Release

Bump `BridgeVersion` in `UpdateChecker.cs` and `SCRIPT_VERSION` in the script, tag the release like `v2.0.1`.
