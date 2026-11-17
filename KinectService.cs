using Microsoft.Kinect;
using System;
using System.Linq;
using System.Windows.Threading;

namespace Microsoft.Samples.Kinect.ControlsBasics
{
    /// <summary>
    /// Owns the sensor. Publishes the closest tracked skeleton on the UI thread,
    /// so the rest of the app never touches Kinect threading or frame handling.
    /// </summary>
    public sealed class KinectService : IDisposable
    {
        public const DepthImageFormat DepthFormat = DepthImageFormat.Resolution640x480Fps30;

        private readonly Dispatcher dispatcher;

        public KinectSensor Sensor { get; }

        /// <summary>Raised on the UI thread, once per frame that contains a tracked user.</summary>
        public event Action<Skeleton> SkeletonTracked;

        private KinectService(KinectSensor sensor, Dispatcher dispatcher)
        {
            Sensor = sensor;
            this.dispatcher = dispatcher;
        }

        /// <summary>Returns a started service, or null when no sensor is connected. Throws if the sensor fails to start.</summary>
        public static KinectService Connect(Dispatcher dispatcher)
        {
            KinectSensor sensor = KinectSensor.KinectSensors.FirstOrDefault(k => k.Status == KinectStatus.Connected);
            if (sensor == null) return null;

            var service = new KinectService(sensor, dispatcher);
            service.Start();
            return service;
        }

        private void Start()
        {
            Sensor.DepthStream.Enable(DepthFormat);
            Sensor.SkeletonStream.Enable(new TransformSmoothParameters
            {
                Smoothing = 0.5f,
                Correction = 0.5f,
                Prediction = 0.5f,
                JitterRadius = 0.05f,
                MaxDeviationRadius = 0.04f
            });
            Sensor.SkeletonFrameReady += OnSkeletonFrame;
            Sensor.Start();
        }

        private void OnSkeletonFrame(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons;
            using (SkeletonFrame frame = e.OpenSkeletonFrame())
            {
                if (frame == null) return;
                skeletons = new Skeleton[frame.SkeletonArrayLength];
                frame.CopySkeletonDataTo(skeletons);
            }

            Skeleton closest = skeletons
                .Where(s => s.TrackingState == SkeletonTrackingState.Tracked)
                .OrderBy(s => s.Position.Z)
                .FirstOrDefault();
            if (closest == null) return;

            dispatcher.BeginInvoke(new Action(() => SkeletonTracked?.Invoke(closest)));
        }

        /// <summary>Tilts the sensor by <paramref name="degrees"/>, clamped to its limits.</summary>
        public void Tilt(int degrees)
        {
            int angle = Sensor.ElevationAngle + degrees;
            Sensor.ElevationAngle = Math.Max(Sensor.MinElevationAngle, Math.Min(Sensor.MaxElevationAngle, angle));
        }

        public void Dispose()
        {
            try { Sensor.Stop(); } catch { }
        }
    }
}
