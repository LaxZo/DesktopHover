using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Xml;

namespace DesktopHover
{
    // Static vector preview. Only fragment references in this document are resolved.
    internal sealed class SvgPreview
    {
        private const int MaxBytes = 2 * 1024 * 1024;
        private static readonly Regex number = new Regex(@"[-+]?(?:\d*\.?\d+)(?:[eE][-+]?\d+)?", RegexOptions.CultureInvariant);
        private static readonly Regex transform = new Regex(@"([a-zA-Z]+)\s*\(([^()]*)\)", RegexOptions.CultureInvariant);
        private readonly Dictionary<string, XmlElement> ids = new Dictionary<string, XmlElement>(StringComparer.Ordinal);
        private readonly Dictionary<XmlElement, Geometry> paths = new Dictionary<XmlElement, Geometry>();
        private readonly CancellationToken token;
        private int remaining = 12000;
        private double viewWidth, viewHeight;
        private SvgPreview(CancellationToken token) { this.token = token; }
        internal static bool Supports(string path) { return String.Equals(Path.GetExtension(path), ".svg", StringComparison.OrdinalIgnoreCase); }

        internal static System.Drawing.Bitmap Load(string path, System.Drawing.Size size, CancellationToken token, out int width, out int height)
        {
            var document = new XmlDocument { XmlResolver = null };
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, MaxCharactersInDocument = MaxBytes }))
            {
                if (stream.Length > MaxBytes) throw new InvalidDataException();
                document.Load(reader);
            }
            var root = document.DocumentElement;
            if (root == null || root.LocalName != "svg") throw new InvalidDataException();
            var svg = new SvgPreview(token);
            foreach (XmlElement element in document.GetElementsByTagName("*"))
            {
                token.ThrowIfCancellationRequested();
                if (element.HasAttribute("id")) svg.ids[element.GetAttribute("id")] = element;
            }
            double[] view = Values(root.GetAttribute("viewBox"), 4);
            bool box = view.Length == 4 && view[2] > 0 && view[3] > 0;
            double w = Length(root.GetAttribute("width"), box ? view[2] : 300);
            double h = Length(root.GetAttribute("height"), box ? view[3] : 150);
            if (w <= 0 || h <= 0) throw new InvalidDataException();
            width = Math.Max(1, (int)Math.Min(1000000, Math.Round(w)));
            height = Math.Max(1, (int)Math.Min(1000000, Math.Round(h)));
            if (!box) view = new[] { 0.0, 0.0, w, h };
            svg.viewWidth = view[2]; svg.viewHeight = view[3];
            double factor = Math.Min(Math.Min(4096, size.Width) / w, Math.Min(4096, size.Height) / h);
            int pw = Math.Max(1, (int)Math.Round(w * factor)), ph = Math.Max(1, (int)Math.Round(h * factor));
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                double sx = pw / view[2], sy = ph / view[3];
                string align = root.GetAttribute("preserveAspectRatio");
                if (align != "none") sx = sy = align.Contains("slice") ? Math.Max(sx, sy) : Math.Min(sx, sy);
                double x = (pw - view[2] * sx) * (align.Contains("xMin") ? 0 : align.Contains("xMax") ? 1 : .5);
                double y = (ph - view[3] * sy) * (align.Contains("YMin") ? 0 : align.Contains("YMax") ? 1 : .5);
                dc.PushTransform(new MatrixTransform(sx, 0, 0, sy, x - view[0] * sx, y - view[1] * sy));
                svg.Draw(dc, root, new Dictionary<string, string>(), 0);
                dc.Pop();
            }
            return VectorBitmap.Render(visual, pw, ph);
        }

        private static double Length(string text, double fallback)
        {
            double value;
            text = text.Trim();
            if (text.EndsWith("px", StringComparison.OrdinalIgnoreCase)) text = text.Substring(0, text.Length - 2);
            double multiplier = 1;
            if (text.EndsWith("%", StringComparison.Ordinal)) { multiplier = fallback / 100; text = text.Substring(0,text.Length-1); }
            return Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                !Double.IsNaN(value) && Math.Abs(value) <= 10000000 ? value * multiplier : fallback;
        }
        private static double[] Values(string text, int max)
        {
            var values = new List<double>();
            for (Match m = number.Match(text); m.Success && values.Count < max; m = m.NextMatch()) values.Add(Length(m.Value, 0));
            return values.ToArray();
        }
        private static Matrix GetTransform(string text)
        {
            var result = Matrix.Identity;
            foreach (Match m in transform.Matches(text))
            {
                double[] n = Values(m.Groups[2].Value, 6);
                if (n.Length == 0) continue;
                var t = Matrix.Identity;
                switch (m.Groups[1].Value)
                {
                    case "matrix": if (n.Length == 6) t = new Matrix(n[0],n[1],n[2],n[3],n[4],n[5]); break;
                    case "translate": t.Translate(n[0], n.Length > 1 ? n[1] : 0); break;
                    case "scale": t.Scale(n[0], n.Length > 1 ? n[1] : n[0]); break;
                    case "rotate": t.RotateAt(n[0], n.Length > 2 ? n[1] : 0, n.Length > 2 ? n[2] : 0); break;
                    case "skewX": t.Skew(n[0], 0); break;
                    case "skewY": t.Skew(0, n[0]); break;
                }
                result.Prepend(t);
            }
            return result;
        }
        private static string Get(Dictionary<string,string> style, string key, string fallback)
        { string value; return style.TryGetValue(key, out value) ? value : fallback; }
        private static Dictionary<string,string> Style(XmlElement e, Dictionary<string,string> parent)
        {
            var style = new Dictionary<string,string>(parent);
            style.Remove("opacity"); style.Remove("display");
            foreach (string key in properties)
                if (e.HasAttribute(key)) style[key] = e.GetAttribute(key);
            foreach (string rule in e.GetAttribute("style").Split(';'))
            {
                int colon = rule.IndexOf(':');
                if (colon > 0) style[rule.Substring(0,colon).Trim()] = rule.Substring(colon+1).Trim();
            }
            return style;
        }
        private static readonly string[] properties = { "fill","stroke","color","fill-rule","stroke-width","stroke-linecap",
            "stroke-linejoin","opacity","fill-opacity","stroke-opacity","display","visibility","stop-color","stop-opacity","font-family","font-size" };
        private static double Opacity(string text) { return Math.Max(0, Math.Min(1, Length(text, 1))); }
        private static Color ColorValue(string text)
        {
            text = text.Trim();
            try
            {
                if (text.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase))
                {
                    double[] v = Values(text, 3);
                    if (v.Length == 3) return Color.FromRgb((byte)Math.Max(0,Math.Min(255,v[0])), (byte)Math.Max(0,Math.Min(255,v[1])), (byte)Math.Max(0,Math.Min(255,v[2])));
                }
                // Limit the converter to named colors or hex. WPF also accepts color-profile URIs.
                if (Regex.IsMatch(text, "^#[0-9a-fA-F]{4}$"))
                    text = "#" + text[4] + text.Substring(1,3);
                else if (Regex.IsMatch(text, "^#[0-9a-fA-F]{8}$"))
                    text = "#" + text.Substring(7,2) + text.Substring(1,6);
                if (text.Length > 32 || !Regex.IsMatch(text, "^(?:[a-zA-Z]+|#[0-9a-fA-F]{3,8})$")) return Colors.Transparent;
                return (Color)ColorConverter.ConvertFromString(text);
            }
            catch (FormatException) { return Colors.Transparent; }
        }
        private Brush Paint(string value, Dictionary<string,string> style, string opacity)
        {
            if (value == "none" || value == "") return null;
            if (value == "currentColor") value = Get(style,"color","black");
            Brush brush;
            if (value.StartsWith("url(#", StringComparison.Ordinal) && value.EndsWith(")"))
            {
                XmlElement def;
                if (!ids.TryGetValue(value.Substring(5,value.Length-6), out def)) return null;
                GradientBrush g;
                if (def.LocalName == "linearGradient")
                    g = new LinearGradientBrush { StartPoint = new Point(Coord(def,"x1",0), Coord(def,"y1",0)), EndPoint = new Point(Coord(def,"x2",1),Coord(def,"y2",0)) };
                else if (def.LocalName == "radialGradient")
                {
                    var center = new Point(Coord(def,"cx",.5),Coord(def,"cy",.5)); double r = Coord(def,"r",.5);
                    g = new RadialGradientBrush { Center = center, GradientOrigin = center, RadiusX = Math.Max(0,r), RadiusY = Math.Max(0,r) };
                }
                else return null;
                if (def.GetAttribute("gradientUnits") == "userSpaceOnUse") g.MappingMode = BrushMappingMode.Absolute;
                foreach (XmlNode node in def.ChildNodes)
                {
                    var stop = node as XmlElement; if (stop == null || stop.LocalName != "stop") continue;
                    token.ThrowIfCancellationRequested();
                    if (--remaining < 0) throw new InvalidDataException("SVG complexity limit.");
                    var st = Style(stop, style); var c = ColorValue(Get(st,"stop-color","black"));
                    c.A = (byte)(c.A * Opacity(Get(st,"stop-opacity","1")));
                    g.GradientStops.Add(new GradientStop(c, Coord(stop,"offset",0)));
                }
                brush = g;
            }
            else brush = new SolidColorBrush(ColorValue(value));
            brush.Opacity = Opacity(Get(style,opacity,"1")); return brush;
        }
        private static double Coord(XmlElement e, string key, double fallback)
        {
            string v = e.GetAttribute(key);
            return v.EndsWith("%") ? Length(v, 1) : Length(v, fallback);
        }
        private double Position(XmlElement e, string key, double fallback)
        {
            string v = e.GetAttribute(key);
            if (!v.EndsWith("%", StringComparison.Ordinal)) return Length(v, fallback);
            double dimension = key == "r" ? Math.Sqrt((viewWidth*viewWidth + viewHeight*viewHeight)/2) :
                key.Contains("y") || key == "height" ? viewHeight : viewWidth;
            return Length(v, dimension);
        }
        private void Draw(DrawingContext dc, XmlElement e, Dictionary<string,string> parent, int depth)
        {
            token.ThrowIfCancellationRequested();
            if (depth > 64 || --remaining < 0) throw new InvalidDataException("SVG complexity limit.");
            var s = Style(e,parent);
            if (Get(s,"display","") == "none" || Get(s,"visibility","") == "hidden") return;
            dc.PushTransform(new MatrixTransform(GetTransform(e.GetAttribute("transform"))));
            dc.PushOpacity(Opacity(Get(s,"opacity","1")));
            try
            {
                switch (e.LocalName)
                {
                    case "svg": case "g": case "a":
                        foreach (XmlNode node in e.ChildNodes) if (node is XmlElement) Draw(dc,(XmlElement)node,s,depth+1);
                        return;
                    case "use":
                        string href = e.GetAttribute("href");
                        if (href.Length == 0) href = e.GetAttribute("href","http://www.w3.org/1999/xlink");
                        XmlElement linked;
                        if (href.StartsWith("#") && ids.TryGetValue(href.Substring(1),out linked))
                        {
                            dc.PushTransform(new TranslateTransform(Position(e,"x",0),Position(e,"y",0)));
                            try { Draw(dc,linked,s,depth+1); } finally { dc.Pop(); }
                        }
                        return;
                }
                Geometry geometry;
                switch (e.LocalName)
                {
                    case "path":
                        string d = e.GetAttribute("d"); if (d.Length == 0 || d.Length > 250000) return;
                        bool evenOdd = Get(s,"fill-rule","nonzero") == "evenodd";
                        if (evenOdd || !paths.TryGetValue(e, out geometry))
                        {
                            geometry = Geometry.Parse((evenOdd ? "F0 " : "F1 ") + d);
                            geometry.Freeze();
                            if (!evenOdd) paths[e] = geometry;
                        }
                        remaining -= d.Length / 128;
                        if (remaining < 0) throw new InvalidDataException("SVG complexity limit.");
                        break;
                    case "rect":
                        double rx = Position(e,"rx",Position(e,"ry",0));
                        geometry = new RectangleGeometry(new Rect(Position(e,"x",0),Position(e,"y",0),Math.Max(0,Position(e,"width",0)),Math.Max(0,Position(e,"height",0))),rx,Position(e,"ry",rx)); break;
                    case "ellipse": case "circle":
                        double r = Math.Max(0,Position(e,"r",0));
                        geometry = new EllipseGeometry(new Point(Position(e,"cx",0),Position(e,"cy",0)),Math.Max(0,Position(e,"rx",r)),Math.Max(0,Position(e,"ry",r))); break;
                    case "line":
                        geometry = new LineGeometry(new Point(Position(e,"x1",0),Position(e,"y1",0)),new Point(Position(e,"x2",0),Position(e,"y2",0))); break;
                    case "polygon": case "polyline":
                        string points = e.GetAttribute("points"); if (points.Length == 0 || points.Length > 250000) return;
                        geometry = Geometry.Parse("M" + points + (e.LocalName == "polygon" ? " Z" : "")); break;
                    case "text":
                        double fontSize = Math.Max(1,Position(e,"font-size",Length(Get(s,"font-size","16"),16)));
                        var text = new FormattedText(e.InnerText, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                            new Typeface("Segoe UI"), Math.Min(1000,fontSize), Brushes.Black, 1);
                        geometry = text.BuildGeometry(new Point(Position(e,"x",0),Position(e,"y",0) - text.Baseline)); break;
                    default: return;
                }
                Brush stroke = Paint(Get(s,"stroke","none"),s,"stroke-opacity");
                Pen pen = null;
                if (stroke != null)
                {
                    pen = new Pen(stroke,Math.Max(0,Length(Get(s,"stroke-width","1"),1)));
                    string cap = Get(s,"stroke-linecap","butt");
                    pen.StartLineCap = pen.EndLineCap = cap == "round" ? PenLineCap.Round : cap == "square" ? PenLineCap.Square : PenLineCap.Flat;
                    string join = Get(s,"stroke-linejoin","miter");
                    pen.LineJoin = join == "round" ? PenLineJoin.Round : join == "bevel" ? PenLineJoin.Bevel : PenLineJoin.Miter;
                }
                dc.DrawGeometry(Paint(Get(s,"fill","black"),s,"fill-opacity"),pen,geometry);
            }
            finally { dc.Pop(); dc.Pop(); }
        }
    }
}
