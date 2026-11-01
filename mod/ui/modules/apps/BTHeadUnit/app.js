angular.module('beamng.apps')
.directive('btHeadUnit', [function () {
  var icons = {
    power: 'M11 2h2v10h-2zM6.3 5.3l1.4 1.4A7 7 0 1 0 16.3 6.7l1.4-1.4A9 9 0 1 1 6.3 5.3z',
    prev: 'M6 5h2v14H6zM20 5v14L9 12z',
    next: 'M16 5h2v14h-2zM4 5l11 7-11 7z',
    play: 'M7 4l13 8-13 8z',
    pause: 'M6 4h4v16H6zM14 4h4v16h-4z',
    menu: 'M3 6h18v2H3zM3 11h18v2H3zM3 16h18v2H3z'
  }
  function icon(name) {
    return '<svg viewBox="0 0 24 24"><path d="' + icons[name] + '"/></svg>'
  }

  return {
    template: `
<div class="bthu" ng-class="{'bthu-off': !isOn()}">
  <style>
    .bthu { position: relative; width: 100%; height: 100%; box-sizing: border-box; padding: 8px 10px; display: flex; flex-direction: column;
      background: linear-gradient(#1b1d22, #0d0e11); border: 1px solid #2c2f36; border-radius: 6px;
      color: #e8eaed; font-family: Roboto, sans-serif; font-size: 13px; user-select: none; overflow: hidden; }
    .bthu-top { display: flex; align-items: center; gap: 8px; font-size: 11px; letter-spacing: .06em; color: #9aa0a6; }
    .bthu-src { padding: 2px 7px; border-radius: 3px; background: #26303d; color: #7fb6ff; cursor: pointer; font-weight: 500; }
    .bthu-src.bt { background: #1d3550; color: #8fd0ff; }
    .bthu-status { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .bthu-status.warn { color: #f2b45c; }
    .bthu-main { flex: 1; display: flex; gap: 10px; min-height: 0; margin: 6px 0; }
    .bthu-art { flex: none; height: 100%; aspect-ratio: 1; min-width: 80px; border-radius: 4px; background: #24272d center/cover no-repeat;
      display: flex; align-items: center; justify-content: center; color: #4a4f57; font-size: 30px; }
    .bthu-info { flex: 1; min-width: 0; display: flex; flex-direction: column; justify-content: center; }
    .bthu-title { font-size: 16px; font-weight: 500; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .bthu-artist, .bthu-album { color: #9aa0a6; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .bthu-album { font-size: 11px; }
    .bthu-progress { height: 3px; background: #2c2f36; border-radius: 2px; margin-top: 8px; }
    .bthu-progress div { height: 100%; background: #7fb6ff; border-radius: 2px; }
    .bthu-times { display: flex; justify-content: space-between; font-size: 10px; color: #6f757d; margin-top: 2px; }
    .bthu-controls { display: flex; align-items: center; gap: 4px; }
    .bthu button { background: #24272d; border: 1px solid #33373f; color: #e8eaed; border-radius: 4px; height: 28px; min-width: 32px;
      padding: 0 6px; display: inline-flex; align-items: center; justify-content: center; cursor: pointer; font-size: 13px; }
    .bthu button:hover { background: #2e3239; }
    .bthu button svg { width: 15px; height: 15px; fill: currentColor; }
    .bthu button.bthu-play { min-width: 40px; background: #2a3a52; border-color: #3b5272; }
    .bthu button.bthu-power.on { color: #7fb6ff; }
    .bthu-vol { flex: 1; height: 8px; background: #2c2f36; border-radius: 4px; cursor: pointer; margin: 0 4px; }
    .bthu-vol div { height: 100%; background: #9aa0a6; border-radius: 4px; }
    .bthu-volnum { width: 26px; text-align: right; font-size: 11px; color: #9aa0a6; }
    .bthu-menu { position: absolute; left: 6px; right: 6px; top: 6px; bottom: 44px; background: #15171bf2; border: 1px solid #33373f;
      border-radius: 5px; padding: 8px 10px; overflow-y: auto; }
    .bthu-menu h4 { margin: 0 0 6px; font-size: 11px; font-weight: 500; letter-spacing: .06em; color: #9aa0a6; }
    .bthu-row { display: flex; align-items: center; justify-content: space-between; gap: 8px; margin-bottom: 6px; }
    .bthu-row span { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .bthu-hint { color: #6f757d; font-size: 11px; }
    .bthu-seg { justify-content: flex-start; gap: 0; }
    .bthu-seg button { flex: 1; border-radius: 0; }
    .bthu-seg button:first-child { border-radius: 4px 0 0 4px; }
    .bthu-seg button:last-child { border-radius: 0 4px 4px 0; border-left: none; }
    .bthu .bthu-seg button.sel { background: #2a3a52; border-color: #3b5272; color: #8fd0ff; }
    .bthu-off .bthu-main, .bthu-off .bthu-top { opacity: .35; }
  </style>
  <div class="bthu-top">
    <span class="bthu-src" ng-class="{bt: s.settings.src == 'bt'}" ng-click="cmd('source')" title="Switch source">
      {{s.settings.src == 'bt' ? 'BLUETOOTH' : 'PC MEDIA'}} &#8644;
    </span>
    <span class="bthu-status" ng-class="{warn: statusWarn()}">{{status()}}</span>
  </div>
  <div class="bthu-main">
    <div class="bthu-art" ng-style="art && pro() ? {'background-image': 'url(' + art + ')'} : {}"><span ng-if="!art || !pro()">&#9835;</span></div>
    <div class="bthu-info">
      <div class="bthu-title">{{s.np.title || (pro() ? 'Nothing playing' : (s.settings.src == 'bt' ? 'Bluetooth audio' : 'PC audio'))}}</div>
      <div class="bthu-artist">{{s.np.artist}}</div>
      <div class="bthu-album">{{s.np.album}}</div>
      <div class="bthu-hint" ng-if="!pro()">Song info, album art and play/pause/skip: Aux Head Unit Pro</div>
      <div class="bthu-progress" ng-if="pro()"><div ng-style="{width: progress() + '%'}"></div></div>
      <div class="bthu-times" ng-if="pro()"><span>{{time(s.np.pos)}}</span><span>{{time(s.np.dur)}}</span></div>
    </div>
  </div>
  <div class="bthu-controls">
    <button class="bthu-power" ng-class="{on: s.settings.power}" ng-click="cmd('power')" title="Power">${icon('power')}</button>
    <button ng-if="pro()" ng-click="cmd('prev')" title="Previous">${icon('prev')}</button>
    <button ng-if="pro()" class="bthu-play" ng-click="cmd('toggle')" title="Play / pause">
      <span ng-if="s.np.playing">${icon('pause')}</span><span ng-if="!s.np.playing">${icon('play')}</span>
    </button>
    <button ng-if="pro()" ng-click="cmd('next')" title="Next">${icon('next')}</button>
    <button ng-click="cmd('volDown')" title="Volume down">&minus;</button>
    <div class="bthu-vol" ng-click="setVolume($event)" title="Volume"><div ng-style="{width: volume() + '%'}"></div></div>
    <button ng-click="cmd('volUp')" title="Volume up">+</button>
    <span class="bthu-volnum">{{volume()}}</span>
    <button ng-click="menu = !menu" title="Settings">${icon('menu')}</button>
  </div>
  <div class="bthu-menu" ng-if="menu">
    <h4>SOURCE</h4>
    <div class="bthu-row bthu-seg">
      <button ng-class="{sel: s.settings.src == 'pc'}" ng-click="cmd('source', 'pc')">PC media (Spotify)</button>
      <button ng-class="{sel: s.settings.src == 'bt'}" ng-click="cmd('source', 'bt')">Phone (Bluetooth)</button>
    </div>
    <h4>SOUND OUTSIDE THE CAR</h4>
    <div class="bthu-row">
      <span>{{s.settings.outside == 'full' ? 'Always full volume' : 'Quieter outside, fades with distance'}}</span>
      <button ng-click="cmd('outside')">{{s.settings.outside == 'full' ? 'Make realistic' : 'Always full'}}</button>
    </div>
    <h4>SPEAKERS</h4>
    <div class="bthu-row">
      <span>{{s.speakers}} (change them in the car's parts, under the head unit)</span>
    </div>
    <h4>CAR'S OWN SCREEN</h4>
    <div class="bthu-row">
      <span>{{s.settings.carScreen ? 'Shows the head unit instead of the map' : 'Shows the navigation map'}}</span>
      <button ng-click="cmd('carScreen')">{{s.settings.carScreen ? 'Show map' : 'Show head unit'}}</button>
    </div>
    <h4>PHONE (BLUETOOTH)</h4>
    <div class="bthu-row" ng-if="s.np.bt">
      <span>{{s.np.bt.status}}</span>
      <button ng-if="s.np.bt.connected" ng-click="cmd('btDisconnect')">Disconnect</button>
    </div>
    <div class="bthu-row" ng-repeat="d in s.np.bt.devices">
      <span>{{d.name}}</span>
      <button ng-if="!(s.np.bt.connected && s.np.bt.name == d.name)" ng-click="cmd('btConnect', $index + 1)">Connect</button>
      <span class="bthu-hint" ng-if="s.np.bt.connected && s.np.bt.name == d.name">connected</span>
    </div>
    <div class="bthu-hint" ng-if="!hasDevices()">
      No phones found. Pair your phone with this PC in Windows Bluetooth settings first.
    </div>
    <div class="bthu-hint" ng-if="!s.online">The companion app isn't running. Start AuxHeadUnit.exe.</div>
  </div>
</div>`,
    replace: true,
    restrict: 'EA',
    scope: true,
    link: function (scope, element) {
      scope.s = { settings: { power: true, vol: 0.8, src: 'pc', outside: 'realistic', carScreen: true }, np: {}, online: false, ignition: true, hasCar: false }
      scope.art = ''
      scope.menu = false

      scope.cmd = function (cmd, arg) {
        var args = JSON.stringify(cmd) + (arg === undefined ? '' : ', ' + JSON.stringify(arg))
        bngApi.engineLua('if extensions.btHeadUnit then extensions.btHeadUnit.uiCmd(' + args + ') end')
      }

      scope.setVolume = function (event) {
        var rect = event.currentTarget.getBoundingClientRect()
        scope.cmd('setVol', Math.max(0, Math.min(1, (event.clientX - rect.left) / rect.width)))
      }

      // The free companion says tier 'free'; anything else (Pro, or none yet) shows the full unit.
      scope.pro = function () {
        return !(scope.s.np && scope.s.np.tier == 'free')
      }

      scope.isOn = function () {
        return scope.s.online && scope.s.settings.power && scope.s.ignition && scope.s.hasCar
      }

      scope.status = function () {
        var s = scope.s, bt = (s.np && s.np.bt) || {}
        if (!s.online) return 'Companion not running: start AuxHeadUnit.exe'
        if (!s.hasCar) return 'No car'
        if (!s.ignition) return 'Ignition off'
        if (!s.settings.power) return 'Off'
        if (s.settings.src == 'bt') return bt.connected ? bt.name : 'No phone connected: open the menu to connect'
        return s.np.app || 'Play something in Spotify'
      }

      scope.statusWarn = function () {
        var s = scope.s, bt = (s.np && s.np.bt) || {}
        return !s.online || (s.settings.src == 'bt' && !bt.connected)
      }

      scope.hasDevices = function () {
        var d = scope.s.np && scope.s.np.bt && scope.s.np.bt.devices
        return d && Object.keys(d).length > 0
      }

      scope.volume = function () { return Math.round((scope.s.settings.vol || 0) * 100) }

      scope.progress = function () {
        var np = scope.s.np || {}
        return np.dur > 0 ? Math.min(100, 100 * (np.pos || 0) / np.dur) : 0
      }

      scope.time = function (sec) {
        if (!(sec > 0)) return '0:00'
        sec = Math.floor(sec)
        var m = Math.floor(sec / 60), r = sec % 60
        return m + ':' + (r < 10 ? '0' : '') + r
      }

      scope.$on('BTHeadUnitUpdate', function (event, data) {
        scope.$evalAsync(function () {
          if (!data) return
          if (!data.np || Array.isArray(data.np)) data.np = {}   // Lua sends an empty table as []
          scope.s = data
        })
      })

      scope.$on('BTHeadUnitArt', function (event, data) {
        scope.$evalAsync(function () {
          scope.art = data && data.data ? 'data:image/jpeg;base64,' + data.data : ''
        })
      })

      bngApi.engineLua('if extensions.btHeadUnit then extensions.btHeadUnit.requestState() end')
    }
  }
}])
