using Microsoft.Kinect;
using System;
using System.Collections.Generic;

namespace Microsoft.Samples.Kinect.ControlsBasics
{
    /// <summary>
    /// Two custom skeleton gestures:
    ///  "HandsUp" - both hands above the head for 0.25 s
    ///  "Swipe"   - right hand moves sideways > 0.45 m within 0.5 s (hand above hip)
    /// </summary>
    public class GestureDetector
    {
        public event Action<string> GestureDetected;

        private readonly List<KeyValuePair<DateTime, SkeletonPoint>> m_rightHand = new List<KeyValuePair<DateTime, SkeletonPoint>>();
        private DateTime? m_handsUpSince = null;
        private DateTime m_lastHandsUp = DateTime.MinValue;
        private DateTime m_lastSwipe = DateTime.MinValue;

        private static readonly TimeSpan HandsUpHold = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan SwipeWindow = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan Cooldown = TimeSpan.FromMilliseconds(1500);
        private const double SwipeDistance = 0.45; //meters
        private const double SwipeMaxVertical = 0.25;

        private static bool Ok(Joint j) { return j.TrackingState == JointTrackingState.Tracked; }

        public void Update(Skeleton sk)
        {
            DateTime now = DateTime.UtcNow;
            Joint head = sk.Joints[JointType.Head];
            Joint lh = sk.Joints[JointType.HandLeft];
            Joint rh = sk.Joints[JointType.HandRight];
            Joint hip = sk.Joints[JointType.HipCenter];

            // --- gesture 1: both hands above head ---
            bool up = Ok(head) && Ok(lh) && Ok(rh)
                      && lh.Position.Y > head.Position.Y && rh.Position.Y > head.Position.Y;
            if (up)
            {
                if (m_handsUpSince == null) m_handsUpSince = now;
                else if (now - m_handsUpSince.Value > HandsUpHold && now - m_lastHandsUp > Cooldown)
                {
                    m_lastHandsUp = now;
                    Raise("HandsUp");
                }
            }
            else m_handsUpSince = null;

            // --- gesture 2: right hand horizontal swipe ---
            if (Ok(rh) && Ok(hip) && rh.Position.Y > hip.Position.Y)
            {
                m_rightHand.Add(new KeyValuePair<DateTime, SkeletonPoint>(now, rh.Position));
                while (m_rightHand.Count > 0 && now - m_rightHand[0].Key > SwipeWindow)
                    m_rightHand.RemoveAt(0);

                if (m_rightHand.Count > 1 && now - m_lastSwipe > Cooldown)
                {
                    SkeletonPoint first = m_rightHand[0].Value;
                    double dx = rh.Position.X - first.X;
                    double dy = rh.Position.Y - first.Y;
                    if (Math.Abs(dx) > SwipeDistance && Math.Abs(dy) < SwipeMaxVertical)
                    {
                        m_lastSwipe = now;
                        m_rightHand.Clear();
                        Raise("Swipe");
                    }
                }
            }
            else m_rightHand.Clear();
        }

        private void Raise(string name)
        {
            Action<string> h = GestureDetected;
            if (h != null) h(name);
        }
    }
}
