-- Aux Head Unit: a car stereo for your own music.
-- Sends the car's state to the Aux Head Unit companion app over localhost UDP
-- and shows what it's playing in the Aux Head Unit UI app.

local M = {}

local logTag = 'btHeadUnit'
local COMPANION_PORT = 47811
local LISTEN_PORT = 47812
local SETTINGS_FILE = '/settings/btHeadUnit.json'
local SEND_INTERVAL = 1 / 20
local IGNITION_INTERVAL = 0.5
local UI_INTERVAL = 0.25
local OFFLINE_AFTER = 3

local udp
local settings = {power = true, vol = 0.8, src = 'pc', outside = 'realistic', carScreen = true}
local nowPlaying = {}
local art = {id = -1, data = ''}
local artIncoming = {id = -1, parts = {}, count = 0}
local ignition = true
local speakers         -- the car's speakers part (btHeadUnitSpeakers), nil for stock speakers
local carId            -- last real car the player drove (survives getting out and walking)
local sinceReceive = math.huge
local sendTimer, ignitionTimer, uiTimer = 0, 0, 0
local wasPlayingBeforePowerOff = false

local function loadSettings()
  local saved = jsonReadFile(SETTINGS_FILE)
  if type(saved) ~= 'table' then return end
  for k, v in pairs(saved) do
    if settings[k] ~= nil and type(v) == type(settings[k]) then settings[k] = v end
  end
end

local function saveSettings()
  jsonWriteFile(SETTINGS_FILE, settings, true)
end

local function openSocket()
  if udp then return true end
  udp = socket.udp()
  local ok, err = udp:setsockname('127.0.0.1', LISTEN_PORT)
  if not ok then
    log('E', logTag, 'Unable to listen on UDP port ' .. LISTEN_PORT .. ': ' .. tostring(err))
    udp:close()
    udp = nil
    return false
  end
  udp:settimeout(0)
  return true
end

local function send(msg)
  if udp then udp:sendto(jsonEncode(msg), '127.0.0.1', COMPANION_PORT) end
end

local function getCar()
  local veh = be:getPlayerVehicle(0)
  if veh and veh:getJBeamFilename() ~= 'unicycle' then carId = veh:getID() end
  local car = carId and be:getObjectByID(carId)
  if not car then carId = nil end
  return car, veh
end

local function sendState()
  local car, veh = getCar()
  local inside, dist = false, 0
  if car then
    -- Driving the car in any camera counts as sitting in it; only walking
    -- away or flying the free camera off makes it sound like it's outside.
    local cam = core_camera.getActiveCamName()
    dist = core_camera.getPosition():distance(vec3(car:getPosition()))
    inside = veh ~= nil and veh:getID() == car:getID() and cam ~= 'free' and cam ~= 'path'
  end
  send({
    t = 'state', power = settings.power, ign = ignition, hasCar = car ~= nil,
    inside = inside, dist = dist, vol = settings.vol, src = settings.src,
    outside = settings.outside, haveArt = art.id,
    spkMax = speakers and speakers.maxVolume or 1, spkBass = speakers and speakers.bassDb or 0,
    spkTreble = speakers and speakers.trebleDb or 0,
  })
end

local function pollIgnition()
  local car = carId and be:getObjectByID(carId)
  if not car then return end
  -- Radios work from accessory position (1) up; vehicles without an ignition count as on.
  -- The speakers part comes along, so swapping it in the parts menu takes effect right away.
  car:queueLuaCommand('local s = controller.getController("btHeadUnitSpeakers") '
    .. 'obj:queueGameEngineLua("if extensions.btHeadUnit then extensions.btHeadUnit.onIgnition(" .. tostring((electrics.values.ignitionLevel or 2) >= 1) .. ", " .. (s and s.luaSpec or "nil") .. ") end")')
end

local function pushUi()
  guihooks.trigger('BTHeadUnitUpdate', {
    settings = settings,
    np = nowPlaying,
    online = sinceReceive < OFFLINE_AFTER,
    ignition = ignition,
    hasCar = carId ~= nil,
    speakers = speakers and speakers.name or 'Stock',
  })
end

local function pushArt()
  guihooks.trigger('BTHeadUnitArt', {id = art.id, data = art.data})
end

-- Screens in the car: the dash-mounted unit (btHeadUnitScreen controller) and
-- the car's own navigation screen (btHeadUnitDash vehicle extension). Cars
-- with neither just ignore it. Cover art is resent now and then because a
-- screen's page may not have been loaded when it last went out.
local screenArtKey, screenArtTimer = nil, 0
local SCREEN_ART_INTERVAL = 5
local SCREEN_CMD = 'local j = %q local c = controller.getController("btHeadUnitScreen") if c then c.%s(j) end '
  .. 'local d = rawget(_G, "btHeadUnitDash") if d then d.%s(j, %s) end'

local function screenCommand(car, method, json, onCarScreen)
  car:queueLuaCommand(string.format(SCREEN_CMD, json, method, method, tostring(onCarScreen)))
end

local function pushScreen()
  local car = carId and be:getObjectByID(carId)
  if not car then return end
  local online = sinceReceive < OFFLINE_AFTER
  local bt = type(nowPlaying.bt) == 'table' and nowPlaying.bt or {}
  local status
  if not online then status = 'Start AuxHeadUnit.exe'
  elseif settings.src == 'bt' then status = bt.connected and tostring(bt.name) or 'No phone connected'
  else status = nowPlaying.app or '' end

  local data = {
    on = settings.power, src = settings.src, status = status, vol = settings.vol,
    title = online and nowPlaying.title or '', artist = online and nowPlaying.artist or '', album = online and nowPlaying.album or '',
    playing = nowPlaying.playing == true, pos = nowPlaying.pos or 0, dur = nowPlaying.dur or 0,
    pro = nowPlaying.tier ~= 'free',
  }
  -- The car's own screen goes back to its map when the head unit is off or the companion isn't running.
  local onCarScreen = settings.carScreen and settings.power and online
  screenCommand(car, 'update', jsonEncode(data), onCarScreen)

  screenArtTimer = screenArtTimer + UI_INTERVAL
  local key = carId .. ':' .. tostring(art.id) .. ':' .. tostring(onCarScreen)
  if key ~= screenArtKey or screenArtTimer >= SCREEN_ART_INTERVAL then
    screenArtKey, screenArtTimer = key, 0
    screenCommand(car, 'art', jsonEncode({id = art.id, data = art.data}), onCarScreen)
  end
end

-- Album art arrives in numbered chunks (this LuaSocket truncates datagrams at
-- 8192 bytes); the companion resends until our haveArt matches its art id.
local function receiveArtChunk(msg)
  local parts = tonumber(msg.parts) or 1
  local part = tonumber(msg.part) or 1
  if msg.id ~= artIncoming.id then artIncoming = {id = msg.id, parts = {}, count = 0} end
  if not artIncoming.parts[part] then
    artIncoming.parts[part] = msg.data or ''
    artIncoming.count = artIncoming.count + 1
  end
  if artIncoming.count >= parts then
    art.id, art.data = msg.id, table.concat(artIncoming.parts, '', 1, parts)
    pushArt()
  end
end

local function setSource(src)
  if settings.src == src then return end
  settings.src = src
  -- The phone takes over like a car stereo would: stop the PC player instead of just muting it.
  if src == 'bt' then send({t = 'cmd', c = 'pausePc'}) end
  saveSettings()
  sendState()
end

-- Switch to the phone when it connects and back to the PC when it drops.
local phoneConnected = nil
local function followPhone(connected)
  if phoneConnected ~= nil and connected ~= phoneConnected then
    if connected then setSource('bt')
    elseif settings.src == 'bt' then setSource('pc') end
  end
  phoneConnected = connected
end

local function receive()
  -- Bounded so a flood can't stall a frame; errors other than "timeout" are
  -- usually a stale "port unreachable" from Windows, so keep reading past them.
  for _ = 1, 50 do
    local data, err = udp:receivefrom(8192)
    if not data then
      if err == 'timeout' then break end
    else
      local ok, msg = pcall(jsonDecode, data)
      if ok and type(msg) == 'table' then
        sinceReceive = 0
        if msg.t == 'np' then
          followPhone(type(msg.bt) == 'table' and msg.bt.connected == true)
          nowPlaying = msg
          -- The free companion sends no song info or covers, so drop one left by a Pro companion.
          if msg.tier == 'free' and art.id ~= -1 then
            art = {id = -1, data = ''}
            pushArt()
          end
        elseif msg.t == 'art' then
          receiveArtChunk(msg)
        end
      end
    end
  end
end

local function setVolume(v)
  settings.vol = math.floor(clamp(v, 0, 1) * 100 + 0.5) / 100
end

function M.uiCmd(cmd, arg)
  local changed = true
  if cmd == 'toggle' or cmd == 'next' or cmd == 'prev' then
    send({t = 'cmd', c = cmd})
    changed = false
  elseif cmd == 'power' then
    settings.power = not settings.power
    if settings.power then
      if wasPlayingBeforePowerOff then send({t = 'cmd', c = 'play'}) end
    else
      wasPlayingBeforePowerOff = nowPlaying.playing == true
      send({t = 'cmd', c = 'pause'})
    end
  elseif cmd == 'volUp' then
    setVolume(settings.vol + 0.05)
  elseif cmd == 'volDown' then
    setVolume(settings.vol - 0.05)
  elseif cmd == 'setVol' then
    setVolume(tonumber(arg) or settings.vol)
  elseif cmd == 'source' then
    -- arg 'pc' or 'bt' picks one; no arg toggles (key binding, badge click).
    setSource((arg == 'pc' or arg == 'bt') and arg or (settings.src == 'pc' and 'bt' or 'pc'))
    changed = false
  elseif cmd == 'outside' then
    settings.outside = settings.outside == 'realistic' and 'full' or 'realistic'
  elseif cmd == 'carScreen' then
    settings.carScreen = not settings.carScreen
  elseif cmd == 'btConnect' then
    local devices = nowPlaying.bt and nowPlaying.bt.devices
    local device = devices and devices[tonumber(arg)]
    if device then send({t = 'bt', c = 'connect', id = device.id}) end
    changed = false
  elseif cmd == 'btDisconnect' then
    send({t = 'bt', c = 'disconnect'})
    changed = false
  else
    return
  end
  if changed then
    saveSettings()
    sendState()
  end
  pushUi()
end

function M.onIgnition(on, spk)
  speakers = type(spk) == 'table' and spk or nil
  if ignition ~= on then
    ignition = on
    pushUi()
  end
end

function M.requestState()
  pushUi()
  pushArt()
end

function M.onUpdate(dtReal)
  if not udp then return end
  sinceReceive = sinceReceive + dtReal
  receive()

  sendTimer = sendTimer + dtReal
  if sendTimer >= SEND_INTERVAL then
    sendTimer = 0
    sendState()
  end

  ignitionTimer = ignitionTimer + dtReal
  if ignitionTimer >= IGNITION_INTERVAL then
    ignitionTimer = 0
    pollIgnition()
  end

  uiTimer = uiTimer + dtReal
  if uiTimer >= UI_INTERVAL then
    uiTimer = 0
    pushUi()
    pushScreen()
  end
end

function M.onExtensionLoaded()
  loadSettings()
  if openSocket() then
    log('I', logTag, 'Head unit ready. Start AuxHeadUnit.exe to play your music.')
  end
end

function M.onExtensionUnloaded()
  if udp then
    send({t = 'bye'})
    udp:close()
    udp = nil
  end
end

return M
