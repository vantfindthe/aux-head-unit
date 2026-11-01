-- Aux Head Unit screen: shows what the head unit is playing on the screen of
-- the dash-mounted unit (the BeamNavigator mount). The btHeadUnit game
-- extension feeds it through update() and art().

local M = {}
M.type = "auxiliary"

local htmlTexture = require("htmlTexture")

local screenMaterialName

-- json: a JSON string from the game extension, handed straight to the page.
local function update(json)
  if screenMaterialName then obj:queueWebViewJS(screenMaterialName, "hu.update(" .. json .. ")") end
end

local function art(json)
  if screenMaterialName then obj:queueWebViewJS(screenMaterialName, "hu.art(" .. json .. ")") end
end

local function init(jbeamData)
  screenMaterialName = jbeamData.screenMaterialName or "@screen_gps"
  local htmlFilePath = jbeamData.htmlFilePath or "local://local/vehicles/common/btheadunit/screen.html"
  htmlTexture.create(screenMaterialName, htmlFilePath, jbeamData.textureWidth or 512, jbeamData.textureHeight or 256, jbeamData.textureFPS or 20, "automatic")
end

M.init = init
M.reset = nop -- keep the screen across vehicle resets instead of reloading the page
M.update = update
M.art = art

return M
