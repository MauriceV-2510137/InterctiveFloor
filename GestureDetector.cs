using Microsoft.Kinect;
using System;
using System.Collections.Generic;

namespace Microsoft.Samples.Kinect.ControlsBasics
{
    public static class GestureNames
    {
        public const string HandsUp = "HandsUp";
        public const string Swipe = "Swipe";
    }

    /// <summary>Runs all gestures against each skeleton frame and raises an event when one fires.</summary>
    public class GestureDetector
    {
        private readonly Gesture[] gestures = { new HandsUpGesture(), new SwipeGesture() };

        public event Action<string> GestureDetected;

        public void Update(Skeleton skeleton)
        {
            DateTime now = DateTime.UtcNow;
            foreach (Gesture gesture in gestures)
                if (gesture.Update(skeleton, now))
                    GestureDetected?.Invoke(gesture.Name);
        }
    }

    /// <summary>Base class: subclasses only say "is the gesture happening right now"; cooldown is handled here.</summary>
    public abstract class Gesture
    {
        private DateTime lastFired = DateTime.MinValue;

        public abstract string Name { get; }

        protected virtual TimeSpan Cooldown { get { return TimeSpan.FromMilliseconds(1500); } }

        public bool Update(Skeleton skeleton, DateTime now)
        {
            bool detected = Detect(skeleton, now);
            if (!detected || now - lastFired < Cooldown) return false;

            lastFired = now;
            OnFired();
            return true;
        }

        protected abstract bool Detect(Skeleton skeleton, DateTime now);

        protected virtual void OnFired() { }

        protected static bool IsTracked(Joint joint)
        {
            return joint.TrackingState == JointTrackingState.Tracked;
        }
    }

    /// <summary>Both hands above the head for 0.25 s.</summary>
    public class HandsUpGesture : Gesture
    {
        private static readonly TimeSpan HoldTime = TimeSpan.FromMilliseconds(250);
        private DateTime? handsUpSince;

        public override string Name { get { return GestureNames.HandsUp; } }

        protected override bool Detect(Skeleton skeleton, DateTime now)
        {
            Joint head = skeleton.Joints[JointType.Head];
            Joint left = skeleton.Joints[JointType.HandLeft];
            Joint right = skeleton.Joints[JointType.HandRight];

            bool handsUp = IsTracked(head) && IsTracked(left) && IsTracked(right)
                           && left.Position.Y > head.Position.Y
                           && right.Position.Y > head.Position.Y;
            if (!handsUp)
            {
                handsUpSince = null;
                return false;
            }

            if (handsUpSince == null) handsUpSince = now;
            return now - handsUpSince.Value > HoldTime;
        }
    }

    /// <summary>Right hand (above the hip) moves sideways more than 0.45 m within 0.5 s.</summary>
    public class SwipeGesture : Gesture
    {
        private const double MinDistance = 0.45;  // meters, horizontal
        private const double MaxDrift = 0.25;     // meters, vertical
        private static readonly TimeSpan Window = TimeSpan.FromMilliseconds(500);

        private readonly List<KeyValuePair<DateTime, SkeletonPoint>> history = new List<KeyValuePair<DateTime, SkeletonPoint>>();

        public override string Name { get { return GestureNames.Swipe; } }

        protected override bool Detect(Skeleton skeleton, DateTime now)
        {
            Joint hand = skeleton.Joints[JointType.HandRight];
            Joint hip = skeleton.Joints[JointType.HipCenter];

            if (!IsTracked(hand) || !IsTracked(hip) || hand.Position.Y <= hip.Position.Y)
            {
                history.Clear();
                return false;
            }

            history.Add(new KeyValuePair<DateTime, SkeletonPoint>(now, hand.Position));
            history.RemoveAll(h => now - h.Key > Window);
            if (history.Count < 2) return false;

            SkeletonPoint start = history[0].Value;
            double dx = hand.Position.X - start.X;
            double dy = hand.Position.Y - start.Y;
            return Math.Abs(dx) > MinDistance && Math.Abs(dy) < MaxDrift;
        }

        protected override void OnFired() { history.Clear(); }
    }
}
