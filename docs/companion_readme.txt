AUX HEAD UNIT - COMPANION APP
=============================

The Aux Head Unit mod for BeamNG.drive plays your own music through an in-game
car stereo. This small Windows app is the part that talks to your music player
and phone. The game mod can't do that by itself.


INSTALL
-------
1. Install the Aux Head Unit mod in BeamNG.drive (from the repository, or put
   aux_head_unit.zip in your BeamNG mods folder).
2. Unzip this folder somewhere permanent, for example Documents\AuxHeadUnit.
3. Run AuxHeadUnit.exe and leave its window open while you play.
   Windows may say "Windows protected your PC" because the app isn't signed.
   Click "More info" and then "Run anyway". The full source code is in the
   "source" folder, and build.bat builds the exe yourself if you prefer.
4. In the game, open the UI apps menu and add "Aux Head Unit" (Dashboard).


PC MUSIC (Spotify, browsers, other players)
-------------------------------------------
Play music in any player that shows up in the Windows media controls (the
volume pop-up). Its sound follows the car. Aux Head Unit Pro also shows the
song and album art on the head unit, with play/pause, next and previous.


PHONE OVER BLUETOOTH
--------------------
Needs Windows 10 version 2004 or newer and a Bluetooth adapter.
1. Pair your phone with the PC once in Windows Settings > Bluetooth & devices.
2. In the game, open the head unit menu (the three-line button) and press
   Connect next to your phone. Keep the phone unlocked and nearby.
3. The source switches to Phone automatically. Play music on the phone.
   In the Windows volume mixer the phone shows up as "Microphone".
The app reconnects to the same phone automatically next time.
Windows doesn't pass on the phone's song titles, and play/pause and skip
for the phone are done on the phone itself.


HOW THE SOUND WORKS
-------------------
The music isn't played by the game. The app sets your music player's (or
phone's) volume in the Windows mixer to match the head unit: the head unit's
volume is the mixer level, it goes silent with the ignition or head unit
off, and it fades with distance when you walk away from the car (choose
"Always full" in the menu to turn that off). When the game or this app
closes, the player's original mixer volume is put back.

The app only talks to the game on your own PC (UDP ports 47811 and 47812 on
127.0.0.1). It doesn't use the internet.


TROUBLESHOOTING
---------------
- "Companion not running" in the game: start AuxHeadUnit.exe.
- "Port 47811 is already in use": the app is already running; close the
  other copy.
- Phone won't connect: turn its Bluetooth on, unlock it, disconnect it from
  earbuds or a car, and try again. If it still fails, remove the phone in
  Windows Bluetooth settings and pair it again.
- Phone connected but silent: on the phone, open Bluetooth, tap the gear next
  to the PC and turn on Media audio; make sure the music app is playing to the
  PC (not earbuds or the phone's speaker) and the phone's media volume is up.
  Still nothing: press Disconnect, then Connect, and press play again.
- From a command prompt in this folder:
    AuxHeadUnit.exe --list     shows every app's audio and mixer volume
    AuxHeadUnit.exe --bttest   tries connecting to your phone and prints any error
