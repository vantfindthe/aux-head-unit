-- Aux Head Unit on the car's own screen. In cars with a built-in navigation
-- screen (anything using the beamNavigator controller, stock or modded) this
-- shows the head unit there instead of the map, and puts the map back when
-- the head unit is off. Fed by the btHeadUnit game extension.

local M = {}

local PAGE = "local://local/vehicles/common/btheadunit/screen.html"
local MIN_WIDTH = 512 -- the page is laid out for 512x256; small navigation textures are scaled up

local screens = {} -- {tag, html, width, height, fps, bootscreen} per navigation screen in this car
local showing = false

local function findScreens()
  screens = {}
  for _, c in pairs(v.data.controller or {}) do
    if c.fileName == "beamNavigator" then
      table.insert(screens, {
        tag = c.screenMaterialName or "@screen_gps",
        html = c.htmlFilePath or "local://local/vehicles/common/navi_screen.html",
        width = c.textureWidth or 256,
        height = c.textureHeight or 128,
        fps = c.textureFPS or 30,
        bootscreen = c.bootscreenImage,
      })
    end
  end
end

local function show(on)
  if on == showing then return end
  showing = on
  for _, s in ipairs(screens) do
    if on then
      local scale = math.max(1, MIN_WIDTH / s.width)
      obj:createWebView(s.tag, PAGE, math.floor(s.width * scale), math.floor(s.height * scale), 1, 20)
    else
      -- The same setup the beamNavigator controller does for its map.
      obj:createWebView(s.tag, s.html, s.width, s.height, 1, s.fps)
      obj:queueGameEngineLua(string.format("extensions.ui_uinavi.requestVehicleDashboardMap(%q, nil, %d)", s.tag, obj:getID()))
      if s.bootscreen then
        obj:queueWebViewJS(s.tag, string.format("map.setBootscreenImage(%s)", jsonEncode({url = s.bootscreen})))
      end
    end
  end
end

-- The game sometimes puts the map page back on its own. If the page that
-- gets the call isn't ours, it loads ours in place.
local function call(jsMethod, json)
  if not showing then return end
  local js = string.format("if (window.hu) %s(%s); else location.replace(%q)", jsMethod, json, PAGE)
  for _, s in ipairs(screens) do
    obj:queueWebViewJS(s.tag, js)
  end
end

-- json: a JSON string from the game extension; enabled: whether the head unit
-- should be on the car's screen right now (setting on, unit on, companion running).
local function update(json, enabled)
  show(enabled == true and #screens > 0)
  call("hu.update", json)
end

local function art(json)
  call("hu.art", json)
end

local function onExtensionLoaded()
  findScreens()
end

M.onExtensionLoaded = onExtensionLoaded
M.update = update
M.art = art

return M
