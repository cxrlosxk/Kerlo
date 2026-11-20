using System.Windows;
using System.Windows.Media;

namespace Kerlo.Controls;

/// <summary>Lightweight on-render sparkline. No retained visual tree per sample.</summary>
public sealed class TrendGraph : FrameworkElement
{
    private readonly List<double> _points = [];
    private Brush _stroke = Brushes.LightGreen;
    public double Maximum { get; set; } = 100;

    public void Update(IEnumerable<double> points, Color color, double maximum = 100)
    {
        _points.Clear();
        _points.AddRange(points.Select(p => double.IsFinite(p) ? Math.Max(0, p) : 0));
        _stroke = new SolidColorBrush(color);
        Maximum = Math.Max(1, maximum);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double width = ActualWidth, height = ActualHeight;
        if (width <= 2 || height <= 2) return;
        var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(46, 64, 76)), 1);
        for (var i = 1; i < 4; i++)
        {
            var y = height * i / 4;
            dc.DrawLine(gridPen, new Point(0, y), new Point(width, y));
        }
        if (_points.Count < 2) return;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var step = width / (_points.Count - 1);
            for (var i = 0; i < _points.Count; i++)
            {
                var point = new Point(i * step, height - Math.Clamp(_points[i] / Maximum, 0, 1) * (height - 9) - 5);
                if (i == 0) ctx.BeginFigure(point, false, false);
                else ctx.LineTo(point, true, false);
            }
        }
        geometry.Freeze();
        dc.DrawGeometry(null, new Pen(_stroke, 2), geometry);
    }
}
