# Living-room Kinect (Assignment 3)

A floor-projected game: the Kinect tracks you, calibration maps your position on the floor to the projected image, and you walk around collecting orbs. Gestures trigger power-ups (effects) that can be switched on and off with the keyboard.

## Setup (once)
1. Install the **Kinect for Windows SDK 1.8** (+ Developer Toolkit). The installer sets the `KINECTSDK10_DIR` variable. Reboot afterwards.
2. Install **Visual Studio 2019/2022** with ".NET desktop development" and the .NET Framework 4.8 targeting pack.
3. Open `LivingRoomKinect.slnx` (or the `.csproj`). NuGet restores `Emgu.CV` and `Emgu.CV.runtime.windows` automatically.
   - If `Microsoft.Kinect` shows a yellow warning, set the HintPath in the `.csproj` to
     `C:\Program Files\Microsoft SDKs\Kinect\v1.8\Assemblies\Microsoft.Kinect.dll`.

## Running
Plug in the Kinect (its own USB 3 port and power supply) and press F5. With no Kinect connected the game starts in **mouse simulation mode**.

### Calibration
1. Mark four corners on the floor, or let the projector show the yellow circles.
2. Stand on each circle in this order: top-left, top-right, bottom-left, bottom-right of the projected image.
3. At each one, press the **Calibrate** button (or Space).
4. After the fourth point the game starts, and the status line shows the calibration error in pixels (stage is 1280x720).

For the projector, drag the window to the projector display and press F11. Recalibrate (R) whenever the Kinect or projector moves.

### Controls
| Key | Action |
|---|---|
| Space | Capture calibration point |
| R | Recalibrate |
| 1 | Toggle Shockwave |
| 2 | Toggle Magnet |
| F11 | Fullscreen |
| Up / Down | Tilt Kinect (recalibrate afterwards) |
| Esc | Quit |

Mouse simulation: move = walk, left click = hands-up gesture, right click = swipe gesture.

### Effects
| Effect | Gesture | What it does |
|---|---|---|
| Shockwave | Both hands above head | Instantly collects all orbs within a radius |
| Magnet | Right-hand swipe sideways | For 5 s, pulls orbs in and enlarges the pickup radius |

Effects are on by default. A disabled effect ignores its gesture, and disabling one cancels it if it is running. The status line lists each effect with its key and ON/OFF state.

## Project structure
| File | Responsibility |
|---|---|
| `MainWindow.xaml(.cs)` | Wiring only: Kinect, calibration, game and keyboard handling |
| `Game.cs` | Game model: player, orbs, score, effect dispatch. No Kinect dependency. |
| `Effects.cs` | `GameEffect` base class, `ShockwaveEffect`, `MagnetEffect` |
| `GestureDetector.cs` | `Gesture` base class (handles cooldown), `HandsUpGesture`, `SwipeGesture` |
| `Calibration.cs` | Skeleton position to stage position: depth space, floor-plane flattening, perspective transform |
| `CalibrationWizard.cs` | Calibration UI and sample collection |
| `KinectService.cs` | Sensor setup, closest-skeleton selection, tilt, UI-thread marshalling |
| `Hud.cs` | Score, status line, canvas helpers |
| `Settings.cs` | Stage size and gameplay constants |

## Extending
**New effect:** derive from `GameEffect` in `Effects.cs` (name, description, toggle key, trigger gesture, and whichever of `Trigger`, `Tick`, `PickupBonus`, `PlayerStroke`, `Reset` you need), then add an instance to the list in `MainWindow.Initialize()`. The hint line, key toggle and gesture binding pick it up automatically.

**New gesture:** derive from `Gesture` in `GestureDetector.cs`, implement `Detect`, add it to the array in `GestureDetector`, and add its name to `GestureNames`.

## Tuning
- Gameplay (orb count, spawn rate, player smoothing, sizes): `Settings.cs`
- Gesture thresholds (hold time, swipe distance and window): constants at the top of each gesture class in `GestureDetector.cs`
- Effect parameters (radius, duration, pull strength): constants at the top of each effect class in `Effects.cs`
