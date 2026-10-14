using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ZAchievements.Views
{
    /// <summary>A stroked arc from twelve o'clock, clockwise, for completion readouts.</summary>
    internal sealed class Ring : Shape
    {
        public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
            nameof(Progress),
            typeof(double),
            typeof(Ring),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>Zero to one.</summary>
        public double Progress
        {
            get => (double)this.GetValue(ProgressProperty);
            set => this.SetValue(ProgressProperty, value);
        }

        protected override Geometry DefiningGeometry
        {
            get
            {
                var inset = this.StrokeThickness / 2;
                var radius = Math.Min(this.ActualWidth, this.ActualHeight) / 2 - inset;
                if (radius <= 0)
                {
                    return Geometry.Empty;
                }

                var sweep = Math.Clamp(this.Progress, 0, 1);
                if (sweep <= 0)
                {
                    return Geometry.Empty;
                }

                var centre = new Point(this.ActualWidth / 2, this.ActualHeight / 2);
                var top = new Point(centre.X, centre.Y - radius);

                if (sweep >= 1)
                {
                    return new EllipseGeometry(centre, radius, radius);
                }

                var angle = sweep * 2 * Math.PI;
                var end = new Point(
                    centre.X + radius * Math.Sin(angle),
                    centre.Y - radius * Math.Cos(angle));

                PathFigure figure = new() { StartPoint = top, IsClosed = false };
                figure.Segments.Add(new ArcSegment(
                    end,
                    new Size(radius, radius),
                    0,
                    sweep > 0.5,
                    SweepDirection.Clockwise,
                    true));

                PathGeometry geometry = new();
                geometry.Figures.Add(figure);
                return geometry;
            }
        }

        protected override Size MeasureOverride(Size constraint)
        {
            var side = double.IsInfinity(constraint.Width) == true ? 0 : constraint.Width;
            return new Size(side, double.IsInfinity(constraint.Height) == true ? 0 : constraint.Height);
        }
    }
}
