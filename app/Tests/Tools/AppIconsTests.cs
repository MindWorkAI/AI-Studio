using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

using AIStudio.Tools;

using MudBlazor;

using Point = (double X, double Y);

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks that the icons we draw ourselves lie on the grid of Material's icons, so that they look as
/// large as the Material icons next to them, see documentation/Icons.md.
/// </summary>
/// <remarks>
/// The bounds come from the path data itself. Curves and arcs are followed in small steps, which finds
/// their outermost points to a few thousandths of a unit, far below the tolerance.
/// </remarks>
public sealed class AppIconsTests
{
    private const double TOLERANCE = 0.05;
    private const double CANVAS_CENTER = 12;
    private const int STEPS_PER_CURVE = 64;

    /// <summary>
    /// The keyline shapes of Material's grid, as width and height on the canvas of 24 by 24 units: the
    /// square, the circle, the upright rectangle, and the wide rectangle.
    /// </summary>
    private static readonly (double Width, double Height)[] KEYLINE_SHAPES = [(18, 18), (20, 20), (16, 20), (20, 16)];

    private static readonly Regex ELEMENTS = new(@"<(\w+)\b[^>]*>", RegexOptions.Compiled);
    private static readonly Regex PATH_DATA = new(@"\sd=""([^""]*)""", RegexOptions.Compiled);
    private static readonly Regex INVISIBLE = new(@"\sfill=""none""", RegexOptions.Compiled);

    private static IEnumerable<TestCaseData> AllAppIcons() => typeof(AppIcons)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string))
        .Select(field => new TestCaseData((string)field.GetRawConstantValue()!).SetArgDisplayNames(field.Name));

    [TestCaseSource(nameof(AllAppIcons))]
    public void EveryIconFillsAKeylineShape(string icon)
    {
        var bounds = Bounds.Of(icon);
        var isKeylineShape = KEYLINE_SHAPES.Any(shape => Math.Abs(bounds.Width - shape.Width) <= TOLERANCE && Math.Abs(bounds.Height - shape.Height) <= TOLERANCE);

        Assert.That(isKeylineShape, Is.True, FormattableString.Invariant($"The icon is {bounds.Width:0.##} units wide and {bounds.Height:0.##} tall. Its outer bounds have to be one of the keyline shapes of Material's grid: a square of 18 by 18 units, a circle 20 across, an upright rectangle 16 wide and 20 tall, or a wide rectangle 20 wide and 16 tall."));
    }

    [TestCaseSource(nameof(AllAppIcons))]
    public void EveryIconSitsInTheCenter(string icon)
    {
        var bounds = Bounds.Of(icon);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(bounds.CenterX, Is.EqualTo(CANVAS_CENTER).Within(TOLERANCE), "The icon has to sit in the middle of its canvas, from left to right.");
            Assert.That(bounds.CenterY, Is.EqualTo(CANVAS_CENTER).Within(TOLERANCE), "The icon has to sit in the middle of its canvas, from top to bottom.");
        }
    }

    [TestCase(Icons.Material.Outlined.Folder, 2, 4, 22, 20, TestName = "Material's folder, with lines and curves")]
    [TestCase(Icons.Material.Outlined.Save, 3, 3, 21, 21, TestName = "Material's floppy disk, with shorthand curves")]
    [TestCase("""<path d="M2 12a10 10 0 1 0 20 0a10 10 0 1 0-20 0"/>""", 2, 2, 22, 22, TestName = "A circle drawn with two arcs")]
    [TestCase("""<path d="M2 12a10 10 0 1020 0a10 10 0 10-20 0"/>""", 2, 2, 22, 22, TestName = "A circle whose arc flags run into the numbers after them")]
    public void TheBoundsAreMeasuredRight(string icon, double minX, double minY, double maxX, double maxY)
    {
        var bounds = Bounds.Of(icon);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(bounds.MinX, Is.EqualTo(minX).Within(TOLERANCE));
            Assert.That(bounds.MinY, Is.EqualTo(minY).Within(TOLERANCE));
            Assert.That(bounds.MaxX, Is.EqualTo(maxX).Within(TOLERANCE));
            Assert.That(bounds.MaxY, Is.EqualTo(maxY).Within(TOLERANCE));
        }
    }

    /// <summary>
    /// The outer bounds of what an icon draws. Elements without a fill, such as the empty square in
    /// front of many Material icons, draw nothing and do not count.
    /// </summary>
    private readonly record struct Bounds(double MinX, double MinY, double MaxX, double MaxY)
    {
        public double Width => this.MaxX - this.MinX;

        public double Height => this.MaxY - this.MinY;

        public double CenterX => (this.MinX + this.MaxX) / 2;

        public double CenterY => (this.MinY + this.MaxY) / 2;

        public static Bounds Of(string icon)
        {
            var points = new List<Point>();
            foreach (Match element in ELEMENTS.Matches(icon))
            {
                if (INVISIBLE.IsMatch(element.Value))
                    continue;

                var name = element.Groups[1].Value;
                if (name is "g")
                    continue;

                Assert.That(name, Is.EqualTo("path"), "The test measures path elements only, so an icon draws with paths.");
                var data = PATH_DATA.Match(element.Value);
                Assert.That(data.Success, Is.True, "A path element needs its path data.");
                new PathWalker(data.Groups[1].Value, points).Walk();
            }

            Assert.That(points, Is.Not.Empty, "The icon draws nothing.");
            return new(points.Min(point => point.X), points.Min(point => point.Y), points.Max(point => point.X), points.Max(point => point.Y));
        }
    }

    /// <summary>
    /// Walks the data of one SVG path and collects the points it passes, see https://www.w3.org/TR/SVG2/paths.html.
    /// Curves and arcs add points along their way, not only their ends.
    /// </summary>
    private sealed class PathWalker(string data, List<Point> points)
    {
        private int position;
        private Point current;
        private Point subpathStart;
        private Point lastControl;
        private char lastCommand;

        public void Walk()
        {
            var command = '\0';
            while (true)
            {
                this.SkipSeparators();
                if (this.position >= data.Length)
                    return;

                //
                // Numbers without a command of their own repeat the last one. After a move, they draw lines:
                //
                if (char.IsLetter(data[this.position]))
                    command = data[this.position++];
                else if (command is '\0' or 'Z' or 'z')
                    throw new FormatException($"The path data holds a number without a command, at position {this.position}.");
                else if (command is 'M')
                    command = 'L';
                else if (command is 'm')
                    command = 'l';

                this.Step(command);
            }
        }

        private void Step(char command)
        {
            var origin = char.IsLower(command) ? this.current : (0, 0);
            switch (char.ToUpperInvariant(command))
            {
                case 'M':
                    this.current = this.ReadPoint(origin);
                    this.subpathStart = this.current;
                    points.Add(this.current);
                    break;

                case 'L':
                    this.LineTo(this.ReadPoint(origin));
                    break;

                case 'H':
                    this.LineTo((origin.X + this.ReadNumber(), this.current.Y));
                    break;

                case 'V':
                    this.LineTo((this.current.X, origin.Y + this.ReadNumber()));
                    break;

                case 'C':
                    this.CubicTo(this.ReadPoint(origin), this.ReadPoint(origin), this.ReadPoint(origin));
                    break;

                case 'S':
                    this.CubicTo(this.ReflectedControl('C', 'S'), this.ReadPoint(origin), this.ReadPoint(origin));
                    break;

                case 'Q':
                    this.QuadraticTo(this.ReadPoint(origin), this.ReadPoint(origin));
                    break;

                case 'T':
                    this.QuadraticTo(this.ReflectedControl('Q', 'T'), this.ReadPoint(origin));
                    break;

                case 'A':
                    this.ArcTo(this.ReadNumber(), this.ReadNumber(), this.ReadNumber(), this.ReadFlag(), this.ReadFlag(), this.ReadPoint(origin));
                    break;

                case 'Z':
                    this.LineTo(this.subpathStart);
                    break;

                default:
                    throw new FormatException($"The path data holds the unknown command '{command}'.");
            }

            this.lastCommand = char.ToUpperInvariant(command);
        }

        /// <summary>
        /// The first control point of a shorthand curve: the last control point mirrored at the current
        /// point, when the command before drew a curve of the same kind, and the current point otherwise.
        /// </summary>
        private Point ReflectedControl(char curve, char shorthand) => this.lastCommand == curve || this.lastCommand == shorthand
            ? (2 * this.current.X - this.lastControl.X, 2 * this.current.Y - this.lastControl.Y)
            : this.current;

        private void LineTo(Point end)
        {
            this.current = end;
            points.Add(end);
        }

        private void CubicTo(Point control1, Point control2, Point end)
        {
            var start = this.current;
            for (var step = 1; step <= STEPS_PER_CURVE; step++)
            {
                var t = (double)step / STEPS_PER_CURVE;
                var u = 1 - t;
                points.Add((
                    u * u * u * start.X + 3 * u * u * t * control1.X + 3 * u * t * t * control2.X + t * t * t * end.X,
                    u * u * u * start.Y + 3 * u * u * t * control1.Y + 3 * u * t * t * control2.Y + t * t * t * end.Y));
            }

            this.lastControl = control2;
            this.current = end;
        }

        private void QuadraticTo(Point control, Point end)
        {
            var start = this.current;
            for (var step = 1; step <= STEPS_PER_CURVE; step++)
            {
                var t = (double)step / STEPS_PER_CURVE;
                var u = 1 - t;
                points.Add((
                    u * u * start.X + 2 * u * t * control.X + t * t * end.X,
                    u * u * start.Y + 2 * u * t * control.Y + t * t * end.Y));
            }

            this.lastControl = control;
            this.current = end;
        }

        /// <summary>
        /// Follows an elliptical arc, after its conversion from end points to a center in
        /// https://www.w3.org/TR/SVG2/implnote.html#ArcConversionEndpointToCenter.
        /// </summary>
        private void ArcTo(double radiusX, double radiusY, double rotationDegrees, bool isLargeArc, bool isSweep, Point end)
        {
            var start = this.current;
            this.current = end;

            radiusX = Math.Abs(radiusX);
            radiusY = Math.Abs(radiusY);
            if (radiusX == 0 || radiusY == 0 || start == end)
            {
                points.Add(end);
                return;
            }

            var (sin, cos) = Math.SinCos(rotationDegrees * Math.PI / 180);
            var halfDeltaX = (start.X - end.X) / 2;
            var halfDeltaY = (start.Y - end.Y) / 2;
            var x1 = cos * halfDeltaX + sin * halfDeltaY;
            var y1 = -sin * halfDeltaX + cos * halfDeltaY;

            //
            // Radii too small to reach the end point grow until they just do:
            //
            var lambda = x1 * x1 / (radiusX * radiusX) + y1 * y1 / (radiusY * radiusY);
            if (lambda > 1)
            {
                radiusX *= Math.Sqrt(lambda);
                radiusY *= Math.Sqrt(lambda);
            }

            var rx2 = radiusX * radiusX;
            var ry2 = radiusY * radiusY;
            var factor = Math.Sqrt(Math.Max(0, (rx2 * ry2 - rx2 * y1 * y1 - ry2 * x1 * x1) / (rx2 * y1 * y1 + ry2 * x1 * x1)));
            if (isLargeArc == isSweep)
                factor = -factor;

            var centerX1 = factor * radiusX * y1 / radiusY;
            var centerY1 = -factor * radiusY * x1 / radiusX;
            var centerX = cos * centerX1 - sin * centerY1 + (start.X + end.X) / 2;
            var centerY = sin * centerX1 + cos * centerY1 + (start.Y + end.Y) / 2;

            var startAngle = Angle(1, 0, (x1 - centerX1) / radiusX, (y1 - centerY1) / radiusY);
            var sweepAngle = Angle((x1 - centerX1) / radiusX, (y1 - centerY1) / radiusY, (-x1 - centerX1) / radiusX, (-y1 - centerY1) / radiusY);
            if (!isSweep && sweepAngle > 0)
                sweepAngle -= 2 * Math.PI;
            else if (isSweep && sweepAngle < 0)
                sweepAngle += 2 * Math.PI;

            for (var step = 1; step <= STEPS_PER_CURVE; step++)
            {
                var (angleSin, angleCos) = Math.SinCos(startAngle + sweepAngle * step / STEPS_PER_CURVE);
                points.Add((
                    centerX + radiusX * angleCos * cos - radiusY * angleSin * sin,
                    centerY + radiusX * angleCos * sin + radiusY * angleSin * cos));
            }
        }

        private static double Angle(double fromX, double fromY, double toX, double toY) => Math.Atan2(fromX * toY - fromY * toX, fromX * toX + fromY * toY);

        private Point ReadPoint(Point origin) => (origin.X + this.ReadNumber(), origin.Y + this.ReadNumber());

        private double ReadNumber()
        {
            this.SkipSeparators();
            var start = this.position;
            if (this.position < data.Length && data[this.position] is '+' or '-')
                this.position++;

            var hasPoint = false;
            while (this.position < data.Length && (char.IsDigit(data[this.position]) || data[this.position] == '.' && !hasPoint))
            {
                if (data[this.position] == '.')
                    hasPoint = true;

                this.position++;
            }

            if (this.position < data.Length && data[this.position] is 'e' or 'E')
            {
                this.position++;
                if (this.position < data.Length && data[this.position] is '+' or '-')
                    this.position++;

                while (this.position < data.Length && char.IsDigit(data[this.position]))
                    this.position++;
            }

            return double.Parse(data.AsSpan(start, this.position - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Reads an arc flag, which is a single digit and may run into the number after it.
        /// </summary>
        private bool ReadFlag()
        {
            this.SkipSeparators();
            return data[this.position++] switch
            {
                '0' => false,
                '1' => true,
                var flag => throw new FormatException($"An arc flag is 0 or 1, not '{flag}'."),
            };
        }

        private void SkipSeparators()
        {
            while (this.position < data.Length && (char.IsWhiteSpace(data[this.position]) || data[this.position] == ','))
                this.position++;
        }
    }
}