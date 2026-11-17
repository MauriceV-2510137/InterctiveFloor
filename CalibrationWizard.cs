using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Microsoft.Samples.Kinect.ControlsBasics
{
    /// <summary>
    /// Walks the user through standing on each target and feeds the samples into a <see cref="Calibration"/>.
    /// Owns all calibration UI (target dot, instructions, button).
    /// </summary>
    public class CalibrationWizard
    {
        private const int MaxSamples = 15;
        private const int MinSamples = 5;
        private const double MaxStaleSeconds = 0.5;

        // order matches the slides: top-left, top-right, bottom-left, bottom-right
        private static readonly Point[] Targets =
        {
            new Point(Settings.Margin, Settings.Margin),
            new Point(Settings.Width - Settings.Margin, Settings.Margin),
            new Point(Settings.Margin, Settings.Height - Settings.Margin),
            new Point(Settings.Width - Settings.Margin, Settings.Height - Settings.Margin)
        };

        private readonly Calibration calibration;
        private readonly Ellipse targetDot;
        private readonly TextBlock instructions;
        private readonly Button button;
        private readonly Queue<SkeletonPoint> samples = new Queue<SkeletonPoint>();
        private DateTime lastSample = DateTime.MinValue;
        private int step;

        public bool IsActive { get; private set; }

        /// <summary>A user-facing message (errors, hints).</summary>
        public event Action<string> Message;

        /// <summary>Raised when calibration succeeded; the argument is the max error in stage units.</summary>
        public event Action<double> Completed;

        public CalibrationWizard(Canvas stage, Calibration calibration)
        {
            this.calibration = calibration;

            targetDot = new Ellipse { Width = 90, Height = 90, Fill = Brushes.Yellow, Stroke = Brushes.White, StrokeThickness = 4 };
            stage.Children.Add(targetDot);

            instructions = Ui.Text(stage, top: Settings.Height / 2 - 120, size: 28, width: 800);

            button = new Button { Content = "Calibrate", Width = 160, Height = 44, FontSize = 20, Focusable = false };
            Canvas.SetLeft(button, Settings.Width / 2 - button.Width / 2);
            Canvas.SetTop(button, Settings.Height / 2);
            button.Click += (s, e) => Capture();
            stage.Children.Add(button);

            SetVisible(false);
        }

        public void Start()
        {
            step = 0;
            samples.Clear();
            calibration.Reset();
            IsActive = true;
            SetVisible(true);
            ShowStep();
        }

        public void Stop()
        {
            IsActive = false;
            SetVisible(false);
        }

        /// <summary>Feed the tracked user's position every frame while the wizard is active.</summary>
        public void Feed(SkeletonPoint position)
        {
            lastSample = DateTime.UtcNow;
            samples.Enqueue(position);
            while (samples.Count > MaxSamples) samples.Dequeue();
        }

        /// <summary>Records the current position for the current target.</summary>
        public void Capture()
        {
            if (!IsActive) return;

            if (samples.Count < MinSamples || (DateTime.UtcNow - lastSample).TotalSeconds > MaxStaleSeconds)
            {
                Message?.Invoke("No user tracked - stand fully in view of the Kinect and try again");
                return;
            }

            // average the last frames for a stable position
            var average = new SkeletonPoint
            {
                X = samples.Average(p => p.X),
                Y = samples.Average(p => p.Y),
                Z = samples.Average(p => p.Z)
            };
            if (!calibration.AddPoint(Targets[step], average))
            {
                Message?.Invoke("Invalid depth at this position - try again");
                return;
            }

            samples.Clear();
            if (++step < Targets.Length) ShowStep();
            else Finish();
        }

        private void Finish()
        {
            double error;
            try { error = calibration.Calibrate(); }
            catch (Exception ex)
            {
                Start();
                Message?.Invoke("Calibration failed: " + ex.Message);
                return;
            }
            Stop();
            Completed?.Invoke(error);
        }

        private void ShowStep()
        {
            Ui.SetCenter(targetDot, Targets[step]);
            instructions.Text = string.Format(
                "Calibration {0}/{1}\nStand on the yellow circle, then press the Calibrate button (or Space)",
                step + 1, Targets.Length);
        }

        private void SetVisible(bool visible)
        {
            Ui.SetVisible(targetDot, visible);
            Ui.SetVisible(instructions, visible);
            Ui.SetVisible(button, visible);
        }
    }
}
