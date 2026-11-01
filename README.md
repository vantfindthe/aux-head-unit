# Aux Head Unit for BeamNG.drive

You can pair your phone to your cars Bluetooth so why not your virtual car? Play Spotify (or any app that shows up in Windows' media controls) or stream from your phone over Bluetooth.  Sadly you can only control the local spotify or other media exe, the Bluetooth stream is just audio. The music follows the car: it's muted with the ignition or head unit off and, by default, quieter outside the car, fading
with distance.

The audio never actually goes through the game. The companion app sets the playing app's volume in the Windows mixer based on what the mod tells it: the head unit's volume is the mixer level (head unit 60 = 60 in the Windows mixer), scaled by the car's speakers part (Speakers slot under the dash-mount head unit: 939 Guys top out at 65, CaneTurf Mail Order at 90, DIY 4kW Install at 100; their bass and treble numbers aren't used yet). Driving in any camera counts as being in the car; walking away or using the free camera makes it quieter with distance (unless set to "Always full").

The app's original mixer level comes back when the game or companion closes.



## how to operate

1. start `companion/AuxHeadUnit.exe` and leave its window open.
2. in beamng, add the **Aux Head Unit** ui app.
3. phone: pair it with the pc in windows bluetooth settings
4. head unit menu (the ≡ button), press **connect** next to the phone, switch the source to **bluetooth**, and play music on the phone. the companion reconnects to the last phone automatically next time.


Phone: pair it with the PC in Windows Bluetooth settings once, then open the head unit menu (the ≡ button), press **Connect** next to the phone, switch the source to **BLUETOOTH**, and play music on the phone. The companion reconnects to the last phone automatically next time.

If the companion is killed mid-game, the next start puts back any app volumes it had turned down (saved in `companion/state.json`).

## free and pro

The free companion (`AuxHeadUnit.exe`, source in `companion/`, built with `companion/build.bat` using the C# compiler that comes with Windows) does the sound: the PC player or phone follows the car. Aux Head Unit Pro, a paid upgrade, adds song info, album art and play/pause/skip on the head unit, VLC, and the Head Unit Mod Remote Android app for the phone's song, cover and controls. The mod is the same for both; with the free companion it hides the song display and player buttons. To try the companion without the game: `py -3 companion/tools/fake_game.py 6 0.5`.

```
.__        .__                        .__                
|__| _____ |  |__   ____  __ __  _____|__| ____    ____  
|  |/     \|  |  \ /  _ \|  |  \/  ___/  |/    \  / ___\ 
|  |  Y Y  \   Y  (  <_> )  |  /\___ \|  |   |  \/ /_/  >
|__|__|_|  /___|  /\____/|____//____  >__|___|  /\___  / 
         \/     \/                  \/        \//_____/  
```

buy me a beer
cashapp:
$markkaspertl


