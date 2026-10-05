# Dynamic Island for script authors

Any Umbrella script can send notifications and live activities to the island. You don't draw anything yourself: you pass text, an icon and a color, the island does the rest and keeps the look consistent.

A full working example: [examples/di_sdk_demo.lua](../examples/di_sdk_demo.lua). Drop it next to `dynamic_island.lua` and open General > Dynamic Island > SDK Demo.

## Getting the island

All Umbrella scripts share one set of globals, so the island is just a global called `DynamicIsland`. It only exists when the user runs the island script, so always check:

```lua
local function Island()
    local di = DynamicIsland
    if type(di) == "table" and di.api then return di end
    return nil
end
```

Scripts load in alphabetical order, so yours may load before the island. Two ways around that:

- call the island from `OnScriptsLoaded` or later, by then every script is loaded
- or, if you want to say something right at load time, put it in the queue. The island picks it up as soon as it starts. Queued notifications follow the same limits as `Notify`:

```lua
DynamicIslandQueue = DynamicIslandQueue or {}
table.insert(DynamicIslandQueue, { app = "My Script", title = "Loaded", level = "passive" })
```

`DynamicIsland` is read only. `DynamicIsland.api` is the API version, currently `2`. `DynamicIsland.Has("actions")` tells you if a feature is there.

## Permission

The first time your script sends something, the island asks the user if it's allowed, the same way an iPhone asks for an app. Until they answer, up to 3 notifications wait. If they say no, your calls return `nil, "not allowed"`. The user can change it later in Alerts > Scripts. The gear next to each script also has Allow in Focus and Sound, so the user can let your script through Focus or keep its banners but mute its sounds.

`DynamicIsland.IsAllowed("My Script")` returns `true` once the user has allowed it.

## Notifications

```lua
local id, err = DynamicIsland.Notify({
    app = "Auto Stacker",
    title = "Stack the ancients in 0:10",
    body = "Pull at 0:53 from the left side",
    icon = "stack",
    tint = "green",
    level = "active",
    sound = "default",
    trailing = "On",
    duration = 4,
    onTap = function() end,
    actions = {
        { title = "Remind me", fn = function() end },
        { title = "Skip", destructive = true, fn = function() end }
    }
})
```

Only `app` and `title` are required.

| Field | What it does |
| --- | --- |
| `app` | Your script's name, up to 24 characters. Shown above the title, groups your alerts in the notification center, and it's what the user allows or denies. Keep it the same everywhere. |
| `title` | Main line, up to 80 characters. |
| `body` | Longer text, up to 160 characters. Shown when the user hovers the notification and it expands. |
| `icon` | One of the island's glyphs (see below) or a path to an image, for example `panorama/images/items/blink_png.vtex_c`. |
| `tint` | `"red"`, `"orange"`, `"yellow"`, `"green"`, `"mint"`, `"teal"`, `"cyan"`, `"blue"`, `"indigo"`, `"purple"`, `"pink"`, `"brown"`, `"gray"`, a hex string like `"FF9F0A"`, or a `Color`. Without it your app gets its own color. |
| `level` | `"passive"` goes straight to the notification center without a banner or sound. `"active"` is the normal banner. `"time-sensitive"` jumps the queue and gets through Focus when the user allows urgent alerts. |
| `sound` | `"default"`, `"chime"`, `"success"`, `"failure"`, or `false` for silence. |
| `trailing` | A short pill on the right, up to 12 characters, like `"On"` or `"Off"`. |
| `duration` | Seconds on screen, 1.5 to 8. Without it the user's own setting is used. |
| `onTap` | Called when the user clicks the notification, in the island or in the notification center. |
| `actions` | Up to 2 buttons in the expanded notification. `fn` is called on click, then the notification closes. |

A new notification from the same app replaces the one on screen instead of waiting in line, so toggles like On / Off feel instant.

## Live activities

For things that last: timers, progress, anything the user wants to keep an eye on.

```lua
local act = DynamicIsland.Activity.Start({
    app = "Auto Stacker",
    icon = "clock",
    tint = "orange",
    title = "Ancients stack",
    subtitle = "Pull when the timer hits zero",
    timer = 30,
    progress = 0,
    onTap = function() end,
    onEnd = function(reason) end
})

act:Update({ progress = 0.5 })
act:End({ title = "Stacked", trailing = "Done", after = 3 })
```

Compact, the island shows the icon, a progress bar if you set `progress`, and the timer or `trailing` text. With neither, it shows the title. Hovered, it expands with the title, subtitle and bar.

| Field | What it does |
| --- | --- |
| `title`, `subtitle` | Text in the expanded view. |
| `trailing` | Short text on the right, up to 12 characters. Setting it in `Update` or `End` stops the timer. |
| `timer` | Seconds to count down. The island counts by itself, you don't need to update it. |
| `progress` | 0 to 1. `false` hides the bar. |
| `icon`, `tint` | Same as notifications. |
| `staleAfter` | Seconds without an `Update` after which the island ends the activity. Handy if your script can lose track of it. |
| `onTap` | Called when the user clicks the expanded activity or its side bubble. |
| `onEnd` | Called when the island ends the activity, not when you do. The reason is `"dismissed"` (the user swiped it away), `"denied"`, `"muted"`, `"stale"` or `"expired"` (after 4 hours). |

The handle has `Update(fields)`, `End(fields)` and `IsActive()`. `IsActive()` turns false as soon as you call `End` or the island ends it. Both `act:Update{}` and `act.Update{}` work. `End` with `after` keeps your final content on screen for up to 10 seconds. `false` or `""` clears a text field.

When music is playing, the activity takes the island and the music moves to a side bubble. With two activities from different scripts, the newest one is in the island and the other one sits in the side bubble. In a fight the fight wins and your activity goes to the bubble.

## Widgets

Small values that sit in the island next to the clock, like WidgetKit on the iPhone. Your script registers a widget, the user decides whether to put it on the island in the widget editor, the same way as the built-in ones.

```lua
local w = DynamicIsland.Widget.Register({
    app = "Rune Timer",
    id = "next_rune",
    title = "Next Rune",
    icon = "clock",
    tint = "orange",
    text = "1:42"
})

w:Set("1:41")
w:Update({ tint = "red", icon = "bolt" })
w:Remove()
```

| Field | What it does |
| --- | --- |
| `app` | Your app name, the same one you use for notifications. Required. |
| `id` | Stays the same between reloads. The island remembers where the user put your widget by it. Required, letters and digits count. |
| `title` | Name in the widget editor, up to 24 characters. |
| `text` | What the island shows, up to 16 characters. Set it again with `Set` whenever it changes. |
| `icon`, `tint` | Glyph and color of the widget in the editor. The user can recolor the text on the island. |

The handle has `Set(text)`, `Update(fields)`, `Remove()` and `IsOnIsland()`. Both `w:Set("x")` and `w.Set("x")` work. Registering the same `app` and `id` again replaces the old widget and keeps its place on the island.

The island only draws what you `Set`, it never calls your code while drawing, so a slow script can't slow the island down. Update the text from your own `OnUpdate` or `OnFrame` when the value changes.

Widgets don't ask for permission: nothing shows up until the user adds your widget. If the user turns your script off in Alerts > Scripts, its widgets disappear and come back on their old place when it's turned on again. If your script isn't loaded, its widget waits for it and the rest of the island stays as it was.

## Sounds

```lua
DynamicIsland.PlaySound("wheel_notch", 0.8)
```

Plays one of the island's own sounds through the user's sound settings. Allowed names: `notification_toast`, `timer_chime`, `courier_delivered`, `courier_death_or_fail`, `button_press`, `button_dismiss`, `wheel_notch`, `wheel_boundary_bump`, `island_expand`, `island_collapse`, `island_hover`, `toast_dismiss`, plus the aliases `default`, `chime`, `success`, `failure`. Returns `false` for anything else.

## Face ID

```lua
DynamicIsland.FaceID({ result = "ok" })
DynamicIsland.FaceID({ result = "fail", scan = 1.5 })
```

The island turns into a rounded square, draws a face and scans it, like Face ID on iPhone. `result` is `"ok"` (the face turns green) or `"fail"` (it turns red and shakes), `scan` is how long it looks, from 0.4 to 3 seconds. The whole thing takes about 2.5 seconds. Returns `true` when it started, `false` if another one is still playing or the island is busy (at most one every 3 seconds). Check `DynamicIsland.Has("faceId")` first if your script also runs on older versions.

## Icons

`DynamicIsland.Glyphs()` returns every glyph name. The ones that work best as monochrome icons: `bell`, `bolt`, `check`, `close`, `clock`, `stack`, `swords`, `moon`, `music`, `search`, `home`, `plus`, `flame`, `gold`, `courier`, `heart_fill`, `volume`, `headphones`, `display`.

## Limits

- 3 notifications in a row per app, then one every 2 seconds. A script that keeps hammering gets muted until the next reload, and it's written to the console.
- One live activity per app. Starting a new one ends your previous one. 3 activities at most across all scripts.
- 4 widgets per app, 12 across all scripts.
- Your callbacks run in `pcall`. After 3 errors the island stops calling that script's callbacks and writes why to the console.
- The island copies what it needs from your tables and trims long text, so changing a table after the call does nothing.

- Up to 24 different app names per session and 6 permission prompts waiting at once.
- At most 6 script notifications wait in the island's queue at a time, across all scripts.
- A callback that runs for too long is stopped, when the Umbrella build has debug hooks.
- 25 sounds per second across all scripts.

Every call returns `nil, reason` when it doesn't go through, so you can log it.

## What the island protects itself from

All Umbrella scripts share one Lua state, so the island assumes another script can be broken or hostile:

- bad arguments are rejected with `nil, "bad arguments"`, including tables with trapped metamethods, NaN and infinite numbers, and huge strings
- errors inside your callbacks never reach the island, and error objects that can't even be printed are handled too
- if `DynamicIsland` or `DynamicIslandQueue` get overwritten, the island puts them back and writes it to the console
- the island keeps its own copies of `string`, `table`, `math`, `os` and `io`, so a script that replaces `math.floor` or `table.insert` doesn't take it down
- image paths must be relative game paths, `..` and absolute paths are ignored
