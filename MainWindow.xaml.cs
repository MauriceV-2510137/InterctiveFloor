using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Microsoft.Samples.Kinect.ControlsBasics
{
    /// <summary>Wires the pieces together: Kinect -> calibration -> game, plus keyboard handling.</summary>
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer timer = new DispatcherTimer { Interval = Settings.FrameInterval };
        private readonly GestureDetector gestures = new GestureDetector();

        private Hud hud;
        private Game game;
        private KinectService kinect;           // null => mouse simulation
        private Calibration calibration;
        private CalibrationWizard wizard;

        private bool Calibrating { get { return wizard != null && wizard.IsActive; } }

        public MainWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => Initialize();
        }

        // ---------------------------------------------------------------- setup

        private void Initialize()
        {
            Stage.Background = new SolidColorBrush(Color.FromRgb(16, 24, 32));

            hud = new Hud(Stage);
            game = new Game(Stage, new GameEffect[] { new ShockwaveEffect(), new MagnetEffect() });
            gestures.GestureDetected += OnGesture;

            ConnectKinect();
            if (kinect == null) StartMouseSimulation();
            else StartKinectCalibration();

            timer.Tick += OnTick;
            timer.Start();
        }

        private void ConnectKinect()
        {
            try { kinect = KinectService.Connect(Dispatcher); }
            catch (Exception ex) { MessageBox.Show("Could not start Kinect: " + ex.Message); }
        }

        private void StartKinectCalibration()
        {
            kinect.SkeletonTracked += OnSkeleton;

            calibration = new Calibration(kinect.Sensor.CoordinateMapper);
            wizard = new CalibrationWizard(Stage, calibration);
            wizard.Message += text => hud.Notify(text);
            wizard.Completed += error =>
            {
                EnterPlaying();
                hud.Notify(string.Format("Calibrated! Max error {0:F1} px (stage is {1}x{2})", error, Settings.Width, Settings.Height), 4);
            };

            EnterCalibration();
        }

        private void StartMouseSimulation()
        {
            Stage.MouseMove += (s, e) => game.MoveTo(e.GetPosition(Stage));
            Stage.MouseLeftButtonDown += (s, e) => OnGesture(GestureNames.HandsUp);
            Stage.MouseRightButtonDown += (s, e) => OnGesture(GestureNames.Swipe);

            EnterPlaying();
            hud.Notify("No Kinect found - using mouse simulation", 4);
        }

        // ---------------------------------------------------------------- modes

        private void EnterCalibration()
        {
            game.Stop();
            hud.ScoreVisible = false;
            hud.Hint = "";
            hud.Clear();
            wizard.Start();
        }

        private void EnterPlaying()
        {
            game.Start();
            hud.ScoreVisible = true;
            RefreshHint();
        }

        private void RefreshHint()
        {
            var parts = new List<string> { "Collect the orbs!" };
            parts.AddRange(game.Effects.Select(fx =>
                string.Format("[{0}] {1} {2} ({3})", fx.KeyLabel, fx.Name, fx.Enabled ? "ON" : "OFF", fx.Description)));
            parts.Add(wizard != null ? "R = recalibrate"
                                     : "Mouse: move = walk, left click = hands up, right click = swipe");
            parts.Add("F11 = fullscreen");
            hud.Hint = string.Join("  |  ", parts);
        }

        // ---------------------------------------------------------------- events

        private void OnTick(object sender, EventArgs e)
        {
            if (!Calibrating)
            {
                game.Tick();
                hud.Score = game.Score;
            }
            hud.Tick();
        }

        private void OnSkeleton(Skeleton skeleton)
        {
            if (Calibrating)
            {
                wizard.Feed(skeleton.Position);
                return;
            }
            game.MoveTo(calibration.ToStage(skeleton.Position));
            gestures.Update(skeleton);
        }

        private void OnGesture(string gesture)
        {
            hud.Notify("Gesture detected: " + gesture, 1.5);
            game.HandleGesture(gesture);
        }

        private void ToggleEffect(Key key)
        {
            GameEffect effect = game.Effects.FirstOrDefault(fx => fx.ToggleKey == key);
            if (effect == null) return;

            effect.Toggle();
            RefreshHint();
            hud.Notify(effect.Name + (effect.Enabled ? " ON" : " OFF"), 2);
        }

        // ---------------------------------------------------------------- window

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Space: if (wizard != null) wizard.Capture(); break;
                case Key.R: if (wizard != null) EnterCalibration(); break;
                case Key.Escape: Close(); break;
                case Key.F11: ToggleFullscreen(); break;
                case Key.Up: Tilt(5); break;
                case Key.Down: Tilt(-5); break;
                default: ToggleEffect(e.Key); break;
            }
        }

        private void ToggleFullscreen()
        {
            bool fullscreen = WindowStyle == WindowStyle.None;
            WindowStyle = fullscreen ? WindowStyle.SingleBorderWindow : WindowStyle.None;
            WindowState = fullscreen ? WindowState.Normal : WindowState.Maximized;
        }

        private void Tilt(int degrees)
        {
            if (kinect == null) return;
            try
            {
                kinect.Tilt(degrees);
                hud.Notify("Kinect tilt changed - press R to recalibrate", 4);
            }
            catch (Exception ex) { hud.Notify("Tilt failed: " + ex.Message); }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            timer.Stop();
            if (kinect != null) kinect.Dispose();
        }
    }
}
