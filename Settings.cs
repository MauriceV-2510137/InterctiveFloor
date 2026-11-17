using System;

namespace Microsoft.Samples.Kinect.ControlsBasics
{
    /// <summary>Every tunable number in one place. Width/Height must match the Canvas in MainWindow.xaml.</summary>
    public static class Settings
    {
        // stage
        public const double Width = 1280, Height = 720;
        public const double Margin = 120;               // calibration targets and orb spawns stay this far from the edge

        // gameplay
        public const double PlayerRadius = 28, OrbRadius = 18;
        public const double PlayerSmoothing = 0.35;     // 0..1, higher = snappier
        public const int MaxOrbs = 8;
        public static readonly TimeSpan SpawnInterval = TimeSpan.FromSeconds(1);
        public static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(33);
    }
}
