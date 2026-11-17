using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Microsoft.Samples.Kinect.ControlsBasics
{
    public class Orb
    {
        public Ellipse Shape { get; }

        public Orb(Point center)
        {
            Shape = new Ellipse
            {
                Width = Settings.OrbRadius * 2,
                Height = Settings.OrbRadius * 2,
                Fill = Brushes.Gold,
                Stroke = Brushes.White,
                StrokeThickness = 2
            };
            Center = center;
        }

        public Point Center
        {
            get { return Ui.GetCenter(Shape); }
            set { Ui.SetCenter(Shape, value); }
        }
    }

    /// <summary>
    /// The collect-the-orbs game. Knows nothing about Kinect or calibration:
    /// feed it a target position, a gesture name and a tick, and it does the rest.
    /// </summary>
    public class Game
    {
        private readonly Random rng = new Random();
        private readonly List<Orb> orbs = new List<Orb>();
        private readonly Ellipse player;
        private DateTime lastSpawn = DateTime.UtcNow;
        private Point target = new Point(Settings.Width / 2, Settings.Height / 2);

        public Canvas Canvas { get; }
        public IReadOnlyList<GameEffect> Effects { get; }
        public IReadOnlyList<Orb> Orbs { get { return orbs; } }
        public Point PlayerPosition { get; private set; }
        public int Score { get; private set; }

        public Game(Canvas canvas, IEnumerable<GameEffect> effects)
        {
            Canvas = canvas;
            Effects = effects.ToList();

            player = new Ellipse
            {
                Width = Settings.PlayerRadius * 2,
                Height = Settings.PlayerRadius * 2,
                Fill = Brushes.LimeGreen,
                Stroke = Brushes.White,
                StrokeThickness = 3
            };
            canvas.Children.Add(player);

            PlayerPosition = target;
            Stop();
        }

        // ------------------------------------------------------------ lifecycle

        public void Start()
        {
            Clear();
            Ui.SetVisible(player, true);
        }

        public void Stop()
        {
            Clear();
            Ui.SetVisible(player, false);
        }

        private void Clear()
        {
            foreach (Orb orb in orbs) Canvas.Children.Remove(orb.Shape);
            orbs.Clear();
            Score = 0;
            foreach (GameEffect effect in Effects) effect.Reset();
        }

        // ------------------------------------------------------------ input

        /// <summary>Where the player should walk to (stage coordinates, clamped to the stage).</summary>
        public void MoveTo(Point position)
        {
            target = new Point(Math.Max(0, Math.Min(Settings.Width, position.X)),
                               Math.Max(0, Math.Min(Settings.Height, position.Y)));
        }

        /// <summary>Triggers every enabled effect bound to this gesture.</summary>
        public void HandleGesture(string gesture)
        {
            foreach (GameEffect effect in Effects.Where(e => e.Enabled && e.TriggerGesture == gesture))
                effect.Trigger(this);
        }

        // ------------------------------------------------------------ frame update

        public void Tick()
        {
            List<GameEffect> active = Effects.Where(e => e.Enabled).ToList();

            MovePlayer();
            foreach (GameEffect effect in active) effect.Tick(this);
            SpawnOrb();
            CollectNearbyOrbs(Settings.PlayerRadius + Settings.OrbRadius + active.Sum(e => e.PickupBonus));

            player.Stroke = active.Select(e => e.PlayerStroke).FirstOrDefault(b => b != null) ?? Brushes.White;
        }

        private void MovePlayer()
        {
            PlayerPosition = new Point(
                PlayerPosition.X + (target.X - PlayerPosition.X) * Settings.PlayerSmoothing,
                PlayerPosition.Y + (target.Y - PlayerPosition.Y) * Settings.PlayerSmoothing);
            Ui.SetCenter(player, PlayerPosition);
        }

        private void SpawnOrb()
        {
            DateTime now = DateTime.UtcNow;
            if (now - lastSpawn < Settings.SpawnInterval || orbs.Count >= Settings.MaxOrbs) return;

            lastSpawn = now;
            var orb = new Orb(new Point(
                Settings.Margin + rng.NextDouble() * (Settings.Width - 2 * Settings.Margin),
                Settings.Margin + rng.NextDouble() * (Settings.Height - 2 * Settings.Margin)));
            Canvas.Children.Add(orb.Shape);
            orbs.Add(orb);
        }

        private void CollectNearbyOrbs(double pickupRadius)
        {
            foreach (Orb orb in orbs.Where(o => (PlayerPosition - o.Center).Length < pickupRadius).ToList())
                Collect(orb);
        }

        /// <summary>Removes the orb and scores a point. Effects call this too.</summary>
        public void Collect(Orb orb)
        {
            Canvas.Children.Remove(orb.Shape);
            orbs.Remove(orb);
            Score++;
        }
    }
}
