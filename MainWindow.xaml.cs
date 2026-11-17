using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Microsoft.Samples.Kinect.ControlsBasics
{
    public partial class MainWindow : Window
    {
        const double W = 1280, H = 720, Edge = 120, PlayerR = 28, OrbR = 18;
        const string Hint = "Collect the orbs!  Both hands above head = shockwave  |  Swipe right hand sideways = magnet  |  R = recalibrate  |  F11 = fullscreen";

        enum Mode { Calibrating, Playing }
        Mode mode = Mode.Calibrating;

        KinectSensor sensor;
        CalibrationClass calib;
        readonly GestureDetector gestures = new GestureDetector();
        bool simulation;

        // calibration order matches the slides: top-left, top-right, bottom-left, bottom-right
        readonly Point[] targets = {
            new Point(Edge, Edge), new Point(W - Edge, Edge),
            new Point(Edge, H - Edge), new Point(W - Edge, H - Edge) };
        int calibStep;
        readonly Queue<SkeletonPoint> recent = new Queue<SkeletonPoint>();
        DateTime lastSeen = DateTime.MinValue;

        // game state
        readonly Random rng = new Random();
        readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        Point playerPos = new Point(W / 2, H / 2), playerTarget = new Point(W / 2, H / 2);
        readonly List<Ellipse> orbs = new List<Ellipse>();
        int score;
        DateTime magnetUntil = DateTime.MinValue, lastSpawn = DateTime.UtcNow, statusUntil = DateTime.MinValue;

        Ellipse player, targetDot, ring;
        TextBlock title, status, scoreText;
        Button calibButton;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                BuildUi();
                gestures.GestureDetected += OnGesture;
                Stage.MouseMove += (a, b) => { if (simulation) playerTarget = b.GetPosition(Stage); };
                Stage.MouseLeftButtonDown += (a, b) => { if (simulation) Shockwave(); };
                Stage.MouseRightButtonDown += (a, b) => { if (simulation) ActivateMagnet(); };
                InitKinect();
                timer.Tick += Tick;
                timer.Start();
            };
        }

        // ---------------------------------------------------------------- UI
        TextBlock MakeText(double top, double size, double width)
        {
            var t = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = size,
                Width = width,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            Canvas.SetLeft(t, (W - width) / 2);
            Canvas.SetTop(t, top);
            Stage.Children.Add(t);
            return t;
        }

        void BuildUi()
        {
            Stage.Background = new SolidColorBrush(Color.FromRgb(16, 24, 32));

            targetDot = new Ellipse { Width = 90, Height = 90, Fill = Brushes.Yellow, Stroke = Brushes.White, StrokeThickness = 4 };
            player = new Ellipse
            {
                Width = PlayerR * 2,
                Height = PlayerR * 2,
                Fill = Brushes.LimeGreen,
                Stroke = Brushes.White,
                StrokeThickness = 3,
                Visibility = Visibility.Hidden
            };
            ring = new Ellipse { Stroke = Brushes.Cyan, StrokeThickness = 6, Opacity = 0, IsHitTestVisible = false };
            Stage.Children.Add(targetDot);
            Stage.Children.Add(player);
            Stage.Children.Add(ring);

            title = MakeText(H / 2 - 120, 28, 800);
            status = MakeText(H - 50, 18, W - 40);
            scoreText = MakeText(20, 32, 400);
            Canvas.SetLeft(scoreText, 20);
            scoreText.TextAlignment = TextAlignment.Left;
            scoreText.Visibility = Visibility.Hidden;

            calibButton = new Button { Content = "Calibrate", Width = 160, Height = 44, FontSize = 20, Focusable = false };
            Canvas.SetLeft(calibButton, W / 2 - 80);
            Canvas.SetTop(calibButton, H / 2);
            calibButton.Click += (s, e) => CaptureCalibrationPoint();
            Stage.Children.Add(calibButton);
        }

        void SetStatus(string text, double seconds = 2)
        {
            status.Text = text;
            statusUntil = DateTime.UtcNow.AddSeconds(seconds);
        }

        // ---------------------------------------------------------------- Kinect
        void InitKinect()
        {
            sensor = KinectSensor.KinectSensors.FirstOrDefault(k => k.Status == KinectStatus.Connected);
            if (sensor != null)
            {
                try
                {
                    sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                    sensor.SkeletonStream.Enable(new TransformSmoothParameters
                    {
                        Smoothing = 0.5f,
                        Correction = 0.5f,
                        Prediction = 0.5f,
                        JitterRadius = 0.05f,
                        MaxDeviationRadius = 0.04f
                    });
                    sensor.SkeletonFrameReady += OnSkeletonFrame;
                    sensor.Start();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not start Kinect: " + ex.Message);
                    sensor = null;
                }
            }

            if (sensor == null)
            {
                simulation = true;
                StartPlaying();
                SetStatus("No Kinect found - MOUSE SIMULATION: move = walk, left click = shockwave, right click = magnet", 9999);
                return;
            }
            calib = new CalibrationClass(sensor);
            StartCalibration();
        }

        void OnSkeletonFrame(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons;
            using (SkeletonFrame f = e.OpenSkeletonFrame())
            {
                if (f == null) return;
                skeletons = new Skeleton[f.SkeletonArrayLength];
                f.CopySkeletonDataTo(skeletons);
            }
            Dispatcher.BeginInvoke(new Action(() => ProcessSkeletons(skeletons)));
        }

        void ProcessSkeletons(Skeleton[] skeletons)
        {
            Skeleton sk = skeletons.Where(s => s.TrackingState == SkeletonTrackingState.Tracked)
                                   .OrderBy(s => s.Position.Z).FirstOrDefault();
            if (sk == null) return;

            lastSeen = DateTime.UtcNow;
            recent.Enqueue(sk.Position);
            while (recent.Count > 15) recent.Dequeue();

            if (mode == Mode.Playing && calib != null && calib.IsCalibrated)
            {
                Point p = calib.KinectToProjectionPoint(sk.Position);
                playerTarget = new Point(Math.Max(0, Math.Min(W, p.X)), Math.Max(0, Math.Min(H, p.Y)));
                gestures.Update(sk);
            }
        }

        // ---------------------------------------------------------------- calibration
        void StartCalibration()
        {
            mode = Mode.Calibrating;
            calibStep = 0;
            calib.Reset();
            recent.Clear();
            ClearOrbs();
            player.Visibility = Visibility.Hidden;
            scoreText.Visibility = Visibility.Hidden;
            title.Visibility = Visibility.Visible;
            calibButton.Visibility = Visibility.Visible;
            status.Text = "";
            ShowCalibrationStep();
        }

        void ShowCalibrationStep()
        {
            Point t = targets[calibStep];
            Canvas.SetLeft(targetDot, t.X - 45);
            Canvas.SetTop(targetDot, t.Y - 45);
            targetDot.Visibility = Visibility.Visible;
            title.Text = string.Format("Calibration {0}/4\nStand on the yellow circle, then press the Calibrate button (or Space)", calibStep + 1);
        }

        void CaptureCalibrationPoint()
        {
            if (calib == null || mode != Mode.Calibrating) return;
            if (recent.Count < 5 || (DateTime.UtcNow - lastSeen).TotalSeconds > 0.5)
            {
                SetStatus("No user tracked - stand fully in view of the Kinect and try again", 3);
                return;
            }
            // average the last frames for a stable position
            SkeletonPoint avg = new SkeletonPoint
            {
                X = recent.Average(p => p.X),
                Y = recent.Average(p => p.Y),
                Z = recent.Average(p => p.Z)
            };
            if (!calib.AddPoint(targets[calibStep], avg))
            {
                SetStatus("Invalid depth at this position - try again", 3);
                return;
            }
            recent.Clear();
            calibStep++;
            if (calibStep < 4) { ShowCalibrationStep(); return; }

            try
            {
                double err = calib.Calibrate();
                StartPlaying();
                SetStatus(string.Format("Calibrated! Max error {0:F1} px (stage is 1280x720)", err), 4);
            }
            catch (Exception ex)
            {
                StartCalibration();
                SetStatus("Calibration failed: " + ex.Message, 5);
            }
        }

        // ---------------------------------------------------------------- game
        void StartPlaying()
        {
            mode = Mode.Playing;
            targetDot.Visibility = Visibility.Hidden;
            title.Visibility = Visibility.Hidden;
            calibButton.Visibility = Visibility.Hidden;
            player.Visibility = Visibility.Visible;
            scoreText.Visibility = Visibility.Visible;
            ClearOrbs();
            score = 0;
        }

        void ClearOrbs()
        {
            foreach (var o in orbs) Stage.Children.Remove(o);
            orbs.Clear();
        }

        void Tick(object sender, EventArgs e)
        {
            if (mode != Mode.Playing) return;
            DateTime now = DateTime.UtcNow;

            playerPos = new Point(playerPos.X + (playerTarget.X - playerPos.X) * 0.35,
                                  playerPos.Y + (playerTarget.Y - playerPos.Y) * 0.35);
            Canvas.SetLeft(player, playerPos.X - PlayerR);
            Canvas.SetTop(player, playerPos.Y - PlayerR);

            bool magnet = now < magnetUntil;
            player.Stroke = magnet ? Brushes.Magenta : Brushes.White;
            double pickup = PlayerR + OrbR + (magnet ? 90 : 0);

            if ((now - lastSpawn).TotalSeconds > 1 && orbs.Count < 8)
            {
                lastSpawn = now;
                var o = new Ellipse { Width = OrbR * 2, Height = OrbR * 2, Fill = Brushes.Gold, Stroke = Brushes.White, StrokeThickness = 2 };
                Canvas.SetLeft(o, Edge + rng.NextDouble() * (W - 2 * Edge) - OrbR);
                Canvas.SetTop(o, Edge + rng.NextDouble() * (H - 2 * Edge) - OrbR);
                Stage.Children.Add(o);
                orbs.Add(o);
            }

            for (int i = orbs.Count - 1; i >= 0; i--)
            {
                Ellipse o = orbs[i];
                Point c = new Point(Canvas.GetLeft(o) + OrbR, Canvas.GetTop(o) + OrbR);
                Vector d = playerPos - c;
                if (magnet && d.Length > 1)
                {
                    d.Normalize();
                    c = new Point(c.X + d.X * 6, c.Y + d.Y * 6);
                    Canvas.SetLeft(o, c.X - OrbR);
                    Canvas.SetTop(o, c.Y - OrbR);
                }
                if ((playerPos - c).Length < pickup) CollectOrb(i);
            }

            scoreText.Text = "Score: " + score;
            if (now > statusUntil) status.Text = Hint;
        }

        void CollectOrb(int i)
        {
            Stage.Children.Remove(orbs[i]);
            orbs.RemoveAt(i);
            score++;
        }

        // ---------------------------------------------------------------- gesture actions
        void OnGesture(string name)
        {
            SetStatus("Gesture detected: " + name, 1.5);
            if (name == "HandsUp") Shockwave();
            else if (name == "Swipe") ActivateMagnet();
        }

        void Shockwave()
        {
            Point c = playerPos;
            for (int i = orbs.Count - 1; i >= 0; i--)
            {
                Point oc = new Point(Canvas.GetLeft(orbs[i]) + OrbR, Canvas.GetTop(orbs[i]) + OrbR);
                if ((oc - c).Length < 300) CollectOrb(i);
            }
            var d = TimeSpan.FromMilliseconds(500);
            ring.BeginAnimation(WidthProperty, new DoubleAnimation(40, 600, d));
            ring.BeginAnimation(HeightProperty, new DoubleAnimation(40, 600, d));
            ring.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(c.X - 20, c.X - 300, d));
            ring.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(c.Y - 20, c.Y - 300, d));
            ring.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, d));
        }

        void ActivateMagnet()
        {
            magnetUntil = DateTime.UtcNow.AddSeconds(5);
        }

        // ---------------------------------------------------------------- window
        void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Space: CaptureCalibrationPoint(); break;
                case Key.R: if (calib != null) StartCalibration(); break;
                case Key.Escape: Close(); break;
                case Key.F11:
                    if (WindowStyle == WindowStyle.None) { WindowStyle = WindowStyle.SingleBorderWindow; WindowState = WindowState.Normal; }
                    else { WindowStyle = WindowStyle.None; WindowState = WindowState.Maximized; }
                    break;
                case Key.Up: Tilt(5); break;
                case Key.Down: Tilt(-5); break;
            }
        }

        void Tilt(int delta)
        {
            if (sensor == null) return;
            try
            {
                sensor.ElevationAngle = Math.Max(sensor.MinElevationAngle, Math.Min(sensor.MaxElevationAngle, sensor.ElevationAngle + delta));
                SetStatus("Kinect tilt changed - press R to recalibrate", 4);
            }
            catch (Exception ex) { SetStatus("Tilt failed: " + ex.Message, 3); }
        }

        void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            timer.Stop();
            if (sensor != null) { try { sensor.Stop(); } catch { } }
        }
    }
}