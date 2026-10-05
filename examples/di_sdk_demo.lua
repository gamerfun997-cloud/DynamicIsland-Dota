local APP = "SDK Demo"
local APP_B = "SDK Demo Runes"
local APP_C = "SDK Demo Courier"

DynamicIslandQueue = DynamicIslandQueue or {}
table.insert(DynamicIslandQueue, { app = APP, title = "SDK demo is loaded", icon = "check", level = "passive" })

local function Island()
    local di = DynamicIsland
    if type(di) == "table" and di.api then return di end
    return nil
end

local function Say(msg)
    Log.Write("[SDK Demo] " .. msg)
end

local function Notify(opts, app)
    local di = Island()
    if not di then
        Say("Dynamic Island is not loaded")
        return nil
    end
    opts.app = app or APP
    local id, err = di.Notify(opts)
    if not id then Say("notify from " .. opts.app .. " failed: " .. tostring(err)) end
    return id
end

local function Start(opts)
    local di = Island()
    if not di then
        Say("Dynamic Island is not loaded")
        return nil
    end
    local act, err = di.Activity.Start(opts)
    if not act then Say("activity from " .. tostring(opts.app) .. " failed: " .. tostring(err)) end
    return act
end

local Acts = {}
local Timer = { ends = 0, len = 30 }
local Delivery = { start = 0, len = 8 }
local Rune = { start = 0, len = 20 }
local Gallery = { tint = 0, glyph = 0, sound = 0, toggle = false }
local Check = { restoreAt = nil }
local Widgets = { clock = nil, counter = nil, count = 0, at = 0 }

local Tints = { "red", "orange", "yellow", "green", "mint", "teal", "cyan", "blue", "indigo", "purple", "pink", "brown", "gray", "#FF2D55", "5AC8FA" }
local Glyphs = { "bell", "bolt", "check", "close", "clock", "stack", "swords", "moon", "music", "search", "home", "plus", "flame", "gold", "courier", "heart_fill", "volume", "headphones", "display" }
local Sounds = { "default", "chime", "success", "failure" }

local function EndOne(key)
    if Acts[key] then Acts[key]:End() end
    Acts[key] = nil
end

local function StartTimer()
    EndOne("timer")
    Timer.ends = os.clock() + Timer.len
    Acts.timer = Start({
        app = APP,
        icon = "clock",
        tint = "orange",
        title = "Ancients stack",
        subtitle = "Pull the camp when the timer hits zero",
        timer = Timer.len,
        progress = 0,
        staleAfter = 120,
        onTap = function() Say("stack timer tapped") end,
        onEnd = function(reason) Say("stack timer ended by the island: " .. reason) end
    })
end

local function StartRune()
    EndOne("rune")
    Rune.start = os.clock()
    Acts.rune = Start({
        app = APP_B,
        icon = "bolt",
        tint = "purple",
        title = "Power rune",
        subtitle = "Spawns on both sides of the river",
        trailing = "0:20",
        progress = 0,
        onTap = function() Say("rune activity tapped") end,
        onEnd = function(reason) Say("rune activity ended by the island: " .. reason) end
    })
end

local function StartDelivery()
    EndOne("delivery")
    Delivery.start = os.clock()
    Acts.delivery = Start({
        app = APP_C,
        icon = "courier",
        tint = "blue",
        title = "Item delivery",
        subtitle = "Courier is on the way",
        trailing = "0%",
        progress = 0,
        onEnd = function(reason) Say("delivery ended by the island: " .. reason) end
    })
end

local function EndAll()
    for key in pairs(Acts) do EndOne(key) end
end

local tab = Menu.Create("General", "Dynamic Island", "SDK Demo")
local page = tab:Create("Demo")
local gN = page:Create("Notifications", Enum.GroupSide.Left)
local gG = page:Create("Gallery", Enum.GroupSide.Left)
local gA = page:Create("Live Activities", Enum.GroupSide.Right)
local gX = page:Create("Chaos", Enum.GroupSide.Right)
local gW = page:Create("Widgets", Enum.GroupSide.Left)

local function RegisterWidgets()
    local di = Island()
    if not di or not di.Widget then
        Say("this island has no widgets")
        return
    end
    Widgets.clock = di.Widget.Register({ app = APP, id = "uptime", title = "Demo Uptime", icon = "clock", tint = "teal", text = "0:00" })
    Widgets.counter = di.Widget.Register({ app = APP_B, id = "counter", title = "Demo Counter", icon = "plus", tint = "pink", text = "0" })
    Say("widgets registered, add them in the widget editor")
end

gW:Button("Register widgets", RegisterWidgets)
gW:Button("Count +1", function()
    if not Widgets.counter then return end
    Widgets.count = Widgets.count + 1
    Widgets.counter:Set(tostring(Widgets.count))
end)
gW:Button("Change counter look", function()
    if not Widgets.counter then return end
    Gallery.tint = Gallery.tint % #Tints + 1
    Gallery.glyph = Gallery.glyph % #Glyphs + 1
    Widgets.counter:Update({ tint = Tints[Gallery.tint], icon = Glyphs[Gallery.glyph] })
end)
gW:Button("Remove widgets", function()
    if Widgets.clock then Widgets.clock:Remove() end
    if Widgets.counter then Widgets.counter:Remove() end
    Widgets.clock, Widgets.counter = nil, nil
end)

gN:Button("Send notification", function()
    Notify({ title = "Stack the ancients in 0:10", icon = "stack", tint = "green", onTap = function() Say("notification tapped") end })
end)

gN:Button("Send with actions", function()
    Notify({
        title = "Ancients are ready to stack",
        body = "Pull at 0:53 from the left side so the creeps leave the camp before the minute mark.",
        icon = "stack",
        tint = "green",
        onTap = function() Say("expanded notification tapped") end,
        actions = {
            { title = "Remind at 0:50", fn = function() Say("remind pressed") end },
            { title = "Skip", destructive = true, fn = function() Say("skip pressed") end }
        }
    })
end)

gN:Button("Send toggle", function()
    Gallery.toggle = not Gallery.toggle
    local on = Gallery.toggle
    Notify({ title = "Blink Dagger", icon = on and "check" or "close", tint = on and "green" or "red", trailing = on and "On" or "Off", duration = 2.5 })
end)

gN:Button("Send time-sensitive", function()
    Notify({ title = "Roshan respawns in 0:30", icon = "swords", tint = "red", level = "time-sensitive", sound = "chime" })
end)

gN:Button("Send passive", function()
    Notify({ title = "Saved quietly to Notification Center", icon = "moon", tint = "indigo", level = "passive", onTap = function() Say("passive notification tapped in the center") end })
end)

gN:Button("Send with item picture", function()
    Notify({ title = "Blink Dagger is ready", icon = "panorama/images/items/blink_png.vtex_c", tint = "orange" })
end)

gN:Button("Send very long text", function()
    Notify({
        title = "This title is way too long for the island and has to be cut somewhere reasonable",
        body = string.rep("A long body that keeps going and going to see how three lines get trimmed. ", 4),
        icon = "display",
        actions = { { title = "A button with a very long title" } }
    })
end)

gN:Button("Three apps at once", function()
    Notify({ title = "First app", icon = "stack", tint = "green" })
    Notify({ title = "Second app", icon = "bolt", tint = "purple" }, APP_B)
    Notify({ title = "Third app", icon = "courier", tint = "blue" }, APP_C)
end)

gG:Button("Next tint", function()
    Gallery.tint = Gallery.tint % #Tints + 1
    local t = Tints[Gallery.tint]
    Notify({ title = "Tint: " .. t, icon = "heart_fill", tint = t, trailing = tostring(Gallery.tint) .. "/" .. #Tints, duration = 2 })
end)

gG:Button("Next glyph", function()
    Gallery.glyph = Gallery.glyph % #Glyphs + 1
    local g = Glyphs[Gallery.glyph]
    Notify({ title = "Glyph: " .. g, icon = g, tint = "blue", trailing = tostring(Gallery.glyph) .. "/" .. #Glyphs, duration = 2 })
end)

gG:Button("Next sound", function()
    Gallery.sound = Gallery.sound % #Sounds + 1
    local snd = Sounds[Gallery.sound]
    Notify({ title = "Sound: " .. snd, icon = "volume", tint = "pink", sound = snd, duration = 2 })
end)

gG:Button("Play wheel sound", function()
    local di = Island()
    if di then Say("PlaySound: " .. tostring(di.PlaySound("wheel_notch", 0.8))) end
end)

gG:Button("Print API info", function()
    local di = Island()
    if not di then
        Say("Dynamic Island is not loaded")
        return
    end
    local feats = {}
    for _, f in ipairs({ "notify", "activity", "queue", "levels", "sounds", "body", "actions", "trailing", "onEnd", "staleAfter", "endAfter", "playSound", "focus" }) do
        feats[#feats + 1] = f .. "=" .. tostring(di.Has(f))
    end
    Say("api " .. tostring(di.api) .. ", version " .. tostring(di.version))
    Say(table.concat(feats, " "))
    Say("allowed: " .. APP .. "=" .. tostring(di.IsAllowed(APP)) .. ", " .. APP_B .. "=" .. tostring(di.IsAllowed(APP_B)) .. ", " .. APP_C .. "=" .. tostring(di.IsAllowed(APP_C)))
    Say(#di.Glyphs() .. " glyphs: " .. table.concat(di.Glyphs(), ", "))
end)

gA:Button("Stack timer", StartTimer)
gA:Button("Rune progress (2nd app)", StartRune)
gA:Button("Delivery (3rd app)", StartDelivery)
gA:Button("Two at once", function()
    StartTimer()
    StartRune()
end)
gA:Button("Three at once", function()
    StartTimer()
    StartRune()
    StartDelivery()
end)
gA:Button("Finish with final content", function()
    if Acts.timer then
        Acts.timer:End({ title = "Stacked", trailing = "Done", progress = 1, after = 3 })
        Acts.timer = nil
    else
        Say("start the stack timer first")
    end
end)
gA:Button("Stale test (5 s)", function()
    Start({
        app = APP,
        icon = "moon",
        tint = "gray",
        title = "Nobody updates me",
        trailing = "zzz",
        staleAfter = 5,
        onEnd = function(reason) Say("stale test ended: " .. reason) end
    })
end)
gA:Button("End activities", EndAll)

gX:Button("Garbage calls", function()
    local di = Island()
    if not di then return end
    local results = {}
    local function r(tag, ...)
        results[#results + 1] = tag .. "=" .. tostring((select(2, ...)))
    end
    r("nil", di.Notify())
    r("number", di.Notify(42))
    r("no app", di.Notify({ title = "x" }))
    r("no title", di.Notify({ app = APP }))
    r("nan", di.Notify({ app = APP, title = "NaN everywhere", duration = 0 / 0, tint = { r = 0 / 0, g = math.huge, b = -1 } }))
    r("trap", di.Notify(setmetatable({}, { __index = function() error("trap") end })))
    r("tostring", di.Notify({ app = APP, title = setmetatable({}, { __tostring = function() error("boom") end }) }))
    r("path", di.Notify({ app = APP, title = "Bad path", icon = "C:/Windows/../../x.png" }))
    r("activity", di.Activity.Start({ app = APP, timer = math.huge, progress = 0 / 0 }))
    Say("garbage results: " .. table.concat(results, " "))
end)

gX:Button("Broken callbacks", function()
    for i = 1, 3 do
        Notify({ title = "Tap me, I crash (" .. i .. "/3)", icon = "close", tint = "red", onTap = function() error("crash number " .. i) end })
    end
    Say("tap the three red notifications, after the third the island stops calling this script's callbacks")
end)

gX:Button("Callback infinite loop", function()
    if not (debug and debug.sethook and debug.gethook) then
        Say("no debug hooks in this Umbrella build, the island can't cut a frozen callback, skipping")
        return
    end
    Notify({ title = "Tap me, I never return", icon = "flame", tint = "red", onTap = function() while true do end end })
end)

gX:Button("Break the queue", function()
    DynamicIslandQueue = 42
    Say("queue set to 42, the island should reset it and say so in the console")
end)

gX:Button("Replace DynamicIsland", function()
    DynamicIsland = { api = 1, Notify = function() return "fake" end }
    Check.restoreAt = os.clock() + 0.5
    Say("DynamicIsland replaced with a fake")
end)

gX:Button("Spam 20 in a row", function()
    local di = Island()
    if not di then return end
    local sent, blocked = 0, 0
    for i = 1, 20 do
        local id = di.Notify({ app = APP, title = "Spam " .. i })
        if id then sent = sent + 1 else blocked = blocked + 1 end
    end
    Say(string.format("spam test: %d sent, %d blocked", sent, blocked))
end)

local function Tick()
    local now = os.clock()
    if Widgets.clock and now - Widgets.at >= 1 then
        Widgets.at = now
        local s = math.floor(now)
        Widgets.clock:Set(string.format("%d:%02d", math.floor(s / 60), s % 60))
    end
    if Check.restoreAt and now >= Check.restoreAt then
        Check.restoreAt = nil
        local di = DynamicIsland
        Say("after replacing: island restored = " .. tostring(type(di) == "table" and di.Has ~= nil and di.Has("notify") == true))
    end
    if Acts.timer and Acts.timer:IsActive() then
        local left = Timer.ends - now
        Acts.timer:Update({ progress = 1 - math.max(0, left) / Timer.len })
        if left <= 0 then
            Acts.timer:End({ title = "Stacked", trailing = "Done", progress = 1, after = 3 })
            Acts.timer = nil
            Notify({ title = "Pull the camp now", icon = "stack", tint = "orange", level = "time-sensitive", sound = "success" })
        end
    end
    if Acts.rune and Acts.rune:IsActive() then
        local left = math.max(0, Rune.len - (now - Rune.start))
        Acts.rune:Update({ progress = 1 - left / Rune.len, trailing = string.format("%d:%02d", math.floor(left / 60), math.floor(left % 60)) })
        if left <= 0 then
            Acts.rune:End({ subtitle = "Spawned", trailing = "Now", after = 2 })
            Acts.rune = nil
            Notify({ title = "Power rune is up", icon = "bolt", tint = "purple" }, APP_B)
        end
    end
    if Acts.delivery and Acts.delivery:IsActive() then
        local p = math.min(1, (now - Delivery.start) / Delivery.len)
        Acts.delivery:Update({ progress = p, trailing = string.format("%d%%", math.floor(p * 100)) })
        if p >= 1 then
            Acts.delivery:End({ subtitle = "Delivered", after = 2 })
            Acts.delivery = nil
            Notify({ title = "Items delivered", icon = "courier", tint = "blue", sound = "success" }, APP_C)
        end
    end
end

return {
    OnFrame = Tick
}
