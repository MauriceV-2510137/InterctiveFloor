# Living-room Kinect project (Assignment 3)

## Install (once)
1. Kinect for Windows SDK 1.8 (+ Developer Toolkit) - installer sets the KINECTSDK10_DIR variable. Reboot after install.
2. Visual Studio 2019/2022 with ".NET desktop development" and the .NET Framework 4.8 targeting pack.
3. Open LivingRoomKinect.csproj. NuGet restores Emgu.CV + Emgu.CV.runtime.windows automatically.
   (If Microsoft.Kinect shows a yellow warning: edit the HintPath in the .csproj to
   C:\Program Files\Microsoft SDKs\Kinect\v1.8\Assemblies\Microsoft.Kinect.dll)

## Run / test
- Plug in the Kinect (own USB 3 port + power supply), run (F5). No Kinect connected = mouse simulation mode.
- Mark 4 corners on the floor (or let the projector show the yellow circles), stand on each circle in order:
  top-left, top-right, bottom-left, bottom-right of the projected image. Press the Calibrate button (or Space) at each.
- After the 4th point the game starts; the status bar shows the calibration error in pixels.
- Projector: drag the window to the projector display and press F11. Re-calibrate (R) whenever Kinect/projector moves.
- Keys: Space = capture point, R = recalibrate, F11 = fullscreen, Up/Down = tilt Kinect, Esc = quit.

## Gestures
- Both hands above head -> shockwave (collects orbs within a radius)
- Right hand swipe sideways -> 5 s magnet mode (pulls in orbs, larger pickup radius)
Thresholds are constants at the top of GestureDetector.cs.

## Files
CalibrationClass.cs = completed PartialCalibrationClass | Util.cs = unchanged | GestureDetector.cs | MainWindow.xaml(.cs) = calibration UI + game
