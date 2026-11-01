"https://www.youtube.com/embed/3QaeNEeESDY" 

# Aux Head Unit for BeamNG.drive

You can pair your phone to your cars Bluetooth so why not your virtual car? Play Spotify (or any app that shows up in Windows' media controls) or stream from your phone over Bluetooth.  You can control the local spotify or other media exe but the Bluetooth stream is just audio.  Below theres a link to the paid feature that enables control of the bt device. The music follows the car: it's muted with the ignition or head unit off and, by default, quieter outside the car, fading
with distance.

The audio never actually goes through the game. The free companion (`AuxHeadUnit.exe`, source in `companion/`, built with `companion/build.bat` using the C# compiler that comes with Windows) does the sound: the PC player or phone audio is tied to the car. The companion app sets the playing app's volume in the Windows mixer based on what the mod tells it: the head unit's volume is the mixer level (head unit 60 = 60 in the Windows mixer), scaled by the car's speakers part (Speakers slot under the dash-mount head unit: 939 Guys top out at 65, CaneTurf Mail Order at 90, DIY 4kW Install at 100; their bass and treble numbers aren't used yet). Driving in any camera counts as being in the car; walking away or using the free camera makes it quieter with distance (unless set to "Always full").

The app's original mixer level comes back when the game or companion closes.



## install

**Download from [Releases](https://github.com/vantfindthe/aux-head-unit/releases/latest), not the green "Code > Download ZIP" button.** That button gives you the source code, which isn't a working mod (BeamNG can't find the UI app inside it) and has no `AuxHeadUnit.exe`.

From the latest release, grab both files:

- **`aux_head_unit.zip`**: the mod. Put it in your BeamNG mods folder as-is, don't unzip it (`Documents\BeamNG.drive\current\mods`, or open the game's Mod Manager and click "Open Mods Folder").
- **`AuxHeadUnit_companion.zip`**: the companion app. Unzip it somewhere permanent, like `Documents\AuxHeadUnit`. It has `AuxHeadUnit.exe` ready to run, no building needed. Windows may say "Windows protected your PC" because the app isn't signed: click "More info", then "Run anyway".

If you'd rather build the exe yourself from the source here, double-click `companion/build.bat`. It uses the C# compiler that comes with Windows (no Visual Studio needed) and puts `AuxHeadUnit.exe` in the `companion` folder.

## how to operate

1. start `AuxHeadUnit.exe` and leave its window open.
2. in beamng, open the UI apps menu and add the **Aux Head Unit** app from the **Dashboard** category.
3. play music on your PC (Spotify, a browser, anything in the Windows volume pop-up). that's it for PC music.
4. phone: pair it with the pc in windows bluetooth settings once. then in the head unit menu (the ≡ button), press **connect** next to the phone, switch the source to **bluetooth**, and play music on the phone. the companion reconnects to the last phone automatically next time.

**App not in the list?** The mod zip is the wrong one or got unzipped: the mods folder should have `aux_head_unit.zip` directly in it, and inside that zip `lua`, `scripts`, `ui` and `vehicles` should be the top-level folders. Restart the game after fixing it.

If the companion is killed mid-game, the next start puts back any app volumes it had turned down (saved in `companion/state.json`).

## free and pro

I tried to make the best parts free, and the least important functions the donation part. So if you want to be able to see the album cover and control playback, then you can go to the link below. The amount is up to you, but I seriously have nothing to my name but a computer, a studio apartment, and a WiFi connection. If you like this kinda stuff and wanna help out a fellow gamer, I wouldn’t be opposed.


Head Unit Pro, a paid upgrade (https://ko-fi.com/imhousing), adds song info, album art and play/pause/skip on the head unit, VLC, and the Head Unit Mod Remote Android app for the phone's song, cover and controls.

```

 To try the companion without the game: `py -3 companion/tools/fake_game.py 6 0.5`.

.__        .__                        .__                
|__| _____ |  |__   ____  __ __  _____|__| ____    ____  
|  |/     \|  |  \ /  _ \|  |  \/  ___/  |/    \  / ___\ 
|  |  Y Y  \   Y  (  <_> )  |  /\___ \|  |   |  \/ /_/  >
|__|__|_|  /___|  /\____/|____//____  >__|___|  /\___  / 
         \/     \/                  \/        \//_____/  
```

buy me a ramen
cashapp:
$markkaspertl


