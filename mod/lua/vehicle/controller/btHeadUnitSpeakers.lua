-- Aux Head Unit speakers: the part in the head unit's Speakers slot. Holds how
-- loud the speakers go and their bass and treble; the btHeadUnit game
-- extension reads luaSpec when it checks the ignition.

local M = {}
M.type = "auxiliary"

M.luaSpec = "nil"

local function init(jbeamData)
  M.luaSpec = string.format("{name = %q, maxVolume = %s, bassDb = %s, trebleDb = %s}",
    tostring(jbeamData.speakerName or "Speakers"), tonumber(jbeamData.maxVolume) or 1,
    tonumber(jbeamData.bassDb) or 0, tonumber(jbeamData.trebleDb) or 0)
end

M.init = init

return M
