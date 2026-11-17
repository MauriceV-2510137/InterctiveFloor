using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Microsoft.Samples.Kinect.ControlsBasics
{
    /// <summary>
    /// A power-up the player can trigger with a gesture and switch on/off with a key.
    /// To add a new one: derive from this class and add it to the list in MainWindow.
    /// Disabled effects are never triggered, ticked or asked for bonuses by <see cref="Game"/>.
    /// </summary>
    public abstract class GameEffect
    {
        public abstract string Name { get; }
        public abstract string Description { get; }     // how to trigger it, shown in the hint line
        public abstract Key ToggleKey { get; }          // use a digit key (D1, D2, ...)
        public abstract string TriggerGesture { get; }  // one of GestureNames

        public bool Enabled { get; private set; } = true;

        public string KeyLabel { get { return ToggleKey.ToString().TrimStart('D'); } }

        /// <summary>Extra pickup radius while the effect is active.</summary>
        public virtual double PickupBonus { get { return 0; } }

        /// <summary>Overrides the player's outline colour while active; null = no opinion.</summary>
        public virtual Brush PlayerStroke { get { return null; } }

        public void Toggle()
        {
            Enabled = !Enabled;
            if (!Enabled) Reset();
        }

        /// <summary>The trigger gesture was performed.</summary>
        public virtual void Trigger(Game game) { }

        /// <summary>Called every frame.</summary>
        public virtual void Tick(Game game) { }

        /// <summary>Cancels any running state (on disable, restart and recalibration).</summary>
        public virtual void Reset() { }
    }

    /// <summary>Both hands up: instantly collects every orb nearby.</summary>
    public class ShockwaveEffect : GameEffect
    {
        private const double Radius = 300;
        private static readonly Duration AnimationTime = new Duration(TimeSpan.FromMilliseconds(500));

        public override string Name { get { return "Shockwave"; } }
        public override string Description { get { return "both hands above head"; } }
        public override Key ToggleKey { get { return Key.D1; } }
        public override string TriggerGesture { get { return GestureNames.HandsUp; } }

        public override void Trigger(Game game)
        {
            Point center = game.PlayerPosition;
            foreach (Orb orb in game.Orbs.Where(o => (o.Center - center).Length < Radius).ToList())
                game.Collect(orb);

            PlayRing(game.Canvas, center);
        }

        private static void PlayRing(Canvas canvas, Point c)
        {
            var ring = new Ellipse { Stroke = Brushes.Cyan, StrokeThickness = 6, IsHitTestVisible = false };
            canvas.Children.Add(ring);

            ring.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(40, Radius * 2, AnimationTime));
            ring.BeginAnimation(FrameworkElement.HeightProperty, new DoubleAnimation(40, Radius * 2, AnimationTime));
            ring.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(c.X - 20, c.X - Radius, AnimationTime));
            ring.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(c.Y - 20, c.Y - Radius, AnimationTime));

            var fade = new DoubleAnimation(1, 0, AnimationTime);
            fade.Completed += (s, e) => canvas.Children.Remove(ring);
            ring.BeginAnimation(UIElement.OpacityProperty, fade);
        }
    }

    /// <summary>Right-hand swipe: for 5 seconds orbs are pulled towards the player and are easier to grab.</summary>
    public class MagnetEffect : GameEffect
    {
        private const double PullPerFrame = 6;
        private const double ExtraRadius = 90;
        private static readonly TimeSpan ActiveFor = TimeSpan.FromSeconds(5);

        private DateTime activeUntil = DateTime.MinValue;

        private bool IsActive { get { return DateTime.UtcNow < activeUntil; } }

        public override string Name { get { return "Magnet"; } }
        public override string Description { get { return "swipe right hand sideways"; } }
        public override Key ToggleKey { get { return Key.D2; } }
        public override string TriggerGesture { get { return GestureNames.Swipe; } }

        public override double PickupBonus { get { return IsActive ? ExtraRadius : 0; } }
        public override Brush PlayerStroke { get { return IsActive ? Brushes.Magenta : null; } }

        public override void Trigger(Game game)
        {
            activeUntil = DateTime.UtcNow + ActiveFor;
        }

        public override void Tick(Game game)
        {
            if (!IsActive) return;

            foreach (Orb orb in game.Orbs)
            {
                Vector toPlayer = game.PlayerPosition - orb.Center;
                if (toPlayer.Length <= 1) continue;
                toPlayer.Normalize();
                orb.Center += toPlayer * PullPerFrame;
            }
        }

        public override void Reset()
        {
            activeUntil = DateTime.MinValue;
        }
    }
}
