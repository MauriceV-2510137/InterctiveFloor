using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Microsoft.Samples.Kinect.ControlsBasics
{
    /// <summary>Small helpers for placing WPF elements on the stage canvas.</summary>
    public static class Ui
    {
        public static TextBlock Text(Canvas parent, double top, double size, double width, double? left = null)
        {
            var text = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = size,
                Width = width,
                TextAlignment = left.HasValue ? TextAlignment.Left : TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            Canvas.SetLeft(text, left ?? (Settings.Width - width) / 2);
            Canvas.SetTop(text, top);
            parent.Children.Add(text);
            return text;
        }

        public static void SetCenter(FrameworkElement element, Point center)
        {
            Canvas.SetLeft(element, center.X - element.Width / 2);
            Canvas.SetTop(element, center.Y - element.Height / 2);
        }

        public static Point GetCenter(FrameworkElement element)
        {
            return new Point(Canvas.GetLeft(element) + element.Width / 2,
                             Canvas.GetTop(element) + element.Height / 2);
        }

        public static void SetVisible(UIElement element, bool visible)
        {
            element.Visibility = visible ? Visibility.Visible : Visibility.Hidden;
        }
    }

    /// <summary>Score and status line. Temporary messages override the permanent <see cref="Hint"/>.</summary>
    public class Hud
    {
        private readonly TextBlock scoreText;
        private readonly TextBlock statusText;
        private DateTime messageUntil = DateTime.MinValue;

        /// <summary>Shown whenever no temporary message is active.</summary>
        public string Hint { get; set; } = "";

        public Hud(Canvas stage)
        {
            scoreText = Ui.Text(stage, top: 20, size: 32, width: 400, left: 20);
            statusText = Ui.Text(stage, top: Settings.Height - 50, size: 18, width: Settings.Width - 40);
            ScoreVisible = false;
        }

        public int Score { set { scoreText.Text = "Score: " + value; } }

        public bool ScoreVisible { set { Ui.SetVisible(scoreText, value); } }

        public void Notify(string text, double seconds = 3)
        {
            statusText.Text = text;
            messageUntil = DateTime.UtcNow.AddSeconds(seconds);
        }

        public void Clear() { Notify("", 0); }

        /// <summary>Call once per frame.</summary>
        public void Tick()
        {
            if (DateTime.UtcNow > messageUntil) statusText.Text = Hint;
        }
    }
}
