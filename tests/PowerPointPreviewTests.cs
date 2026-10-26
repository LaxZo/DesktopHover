using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Threading;

internal static class PowerPointPreviewTests
{
    private const string PresentationNamespace = "http://schemas.openxmlformats.org/presentationml/2006/main";
    private const string DrawingNamespace = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private const string RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    internal static void Run(Assembly app, string directory, Action<string, Action> test)
    {
        Directory.CreateDirectory(directory);
        string small = CreateFixture(directory);
        test("PowerPoint preserves styled small thumbnails at the default preview size", delegate
        {
            AssertPreview(app, small, new Size(720, 540), 160, 90, "document_embedded");
        });
        test("PowerPoint does not replace saved styles when a larger preview is requested", delegate
        {
            AssertPreview(app, small, new Size(1440, 1080), 160, 90, "document_embedded");
        });
        test("PowerPoint keeps a saved preview that fills aspect-fitted bounds", delegate
        {
            string path = CreatePackage(directory, "adequate-thumbnail.pptx", new Size(1440, 810), "normal");
            AssertPreview(app, path, new Size(1440, 1080), 1440, 810, "document_embedded");
        });
        test("PowerPoint downsamples a sufficiently large saved preview", delegate
        {
            string path = CreatePackage(directory, "large-thumbnail.pptx", new Size(1600, 900), "normal");
            AssertPreview(app, path, new Size(720, 540), 720, 405, "document_embedded");
        });
        foreach (string scenario in new[] { "missing", "malformed", "malformed-presentation", "group", "chart", "mixed-styles", "missing-picture", "uniform" })
        {
            string mode = scenario;
            test("PowerPoint retains its saved preview for " + mode + " slide content", delegate
            {
                string path = CreatePackage(directory, mode + ".pptx", new Size(160, 90), mode);
                AssertPreview(app, path, new Size(1440, 1080), 160, 90, "document_embedded");
            });
        }
        test("PowerPoint chooses the largest available saved preview", delegate
        {
            string path = CreatePackage(directory, "multiple-thumbnails.pptx", new Size(160, 90), "multiple");
            AssertPreview(app, path, new Size(1440, 1080), 1280, 720, "document_embedded");
        });
        foreach (string scenario in new[] { "normal", "blank", "mixed-styles" })
        {
            string mode = scenario;
            test("PowerPoint does not invent a simplified " + mode + " slide without a saved preview", delegate
            {
                string path = CreatePackage(directory, "no-thumbnail-" + mode + ".pptx", Size.Empty, mode);
                object result = Invoke(app, path, new Size(720, 540), CancellationToken.None);
                if (result != null)
                {
                    ((IDisposable)result).Dispose();
                    throw new Exception("Slides without a saved preview must be left to a native renderer.");
                }
            });
        }
        test("PowerPoint honors cancellation before opening a package", delegate
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                try
                {
                    object result = Invoke(app, small, new Size(720, 540), cancellation.Token);
                    if (result != null) ((IDisposable)result).Dispose();
                }
                catch (TargetInvocationException exception)
                {
                    if (exception.InnerException is OperationCanceledException) return;
                    throw;
                }
            }
            throw new Exception("Canceled PowerPoint loading did not throw OperationCanceledException.");
        });
        foreach (string suffix in new[] { ".docx", ".xlsx" })
        {
            string extension = suffix;
            test("Office preserves thumbnail-first behavior for " + extension, delegate
            {
                string path = CreatePackage(directory, "thumbnail-first" + extension, new Size(160, 90), "missing");
                AssertPreview(app, path, new Size(1440, 1080), 160, 90, "document_embedded");
            });
        }
    }

    internal static string CreateFixture(string directory)
    {
        Directory.CreateDirectory(directory);
        return CreatePackage(directory, "powerpoint-small-thumbnail.pptx", new Size(160, 90), "normal");
    }

    internal static string CreateWindowFixture(string directory)
    {
        return CreatePackage(directory, "powerpoint-styled-window.pptx", new Size(720, 405), "mixed-styles");
    }

    private static object Invoke(Assembly app, string path, Size maximum, CancellationToken cancellation)
    {
        Type reader = app.GetType("DesktopHover.OfficePreviewReader", true);
        MethodInfo load = reader.GetMethod("TryLoad", BindingFlags.Static | BindingFlags.NonPublic);
        return load.Invoke(null, new object[] { path, maximum, cancellation });
    }

    private static void AssertPreview(Assembly app, string path, Size maximum, int width, int height, string note)
    {
        object result = Invoke(app, path, maximum, CancellationToken.None);
        if (result == null) throw new Exception("No preview returned for " + Path.GetFileName(path));
        using ((IDisposable)result)
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            Type type = result.GetType();
            var image = (Bitmap)type.GetField("Image", fields).GetValue(result);
            string actualNote = (string)type.GetField("NoteKey", fields).GetValue(result);
            if (image == null || image.Width != width || image.Height != height || actualNote != note)
                throw new Exception(String.Format("Expected {0} {1}x{2}; got {3} {4}.", note, width, height,
                    actualNote, image == null ? "no image" : image.Width + "x" + image.Height));
            // The simplified slide content is navy; its saved design is magenta/green.
            // Its original pixels must survive even when more space is requested.
            Color pixel = image.GetPixel(Math.Min(10, image.Width - 1), Math.Min(10, image.Height - 1));
            Color expected = Color.FromArgb(180, 40, 130);
            if (Math.Abs(pixel.R - expected.R) + Math.Abs(pixel.G - expected.G) + Math.Abs(pixel.B - expected.B) > 10)
                throw new Exception("Unexpected preview pixels: " + pixel + ", expected " + expected);
            if (Path.GetFileNameWithoutExtension(path) != "uniform")
            {
                pixel = image.GetPixel(image.Width * 3 / 4, image.Height / 2);
                expected = Color.FromArgb(30, 190, 130);
                if (Math.Abs(pixel.R - expected.R) + Math.Abs(pixel.G - expected.G) + Math.Abs(pixel.B - expected.B) > 10)
                    throw new Exception("Saved design elements were changed or omitted.");
            }
        }
    }

    private static string CreatePackage(string directory, string name, Size thumbnailSize, string mode)
    {
        string path = Path.Combine(directory, name);
        using (var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
        {
            string extension = Path.GetExtension(name);
            string main = extension == ".docx" ? "word/document.xml" : extension == ".xlsx" ? "xl/workbook.xml" : "ppt/presentation.xml";
            Entry(zip, "[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/><Default Extension=\"png\" ContentType=\"image/png\"/>" +
                "<Override PartName=\"/ppt/presentation.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml\"/>" +
                "<Override PartName=\"/ppt/slides/slide1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slide+xml\"/></Types>");
            Entry(zip, "_rels/.rels", Relationships(
                "<Relationship Id=\"rId1\" Type=\"" + RelationshipNamespace + "/officeDocument\" Target=\"" + main + "\"/>" +
                (thumbnailSize.IsEmpty ? "" : "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/thumbnail\" Target=\"docProps/" +
                    (mode == "multiple" ? "custom-thumbnail.png" : "thumbnail.png") + "\"/>")));
            if (!thumbnailSize.IsEmpty)
            {
                using (var thumbnail = new Bitmap(thumbnailSize.Width, thumbnailSize.Height))
                using (Graphics graphics = Graphics.FromImage(thumbnail))
                {
                    graphics.Clear(Color.FromArgb(180, 40, 130));
                    if (mode != "uniform")
                        using (var brush = new SolidBrush(Color.FromArgb(30, 190, 130)))
                            graphics.FillRectangle(brush, thumbnailSize.Width / 2, 0, thumbnailSize.Width / 2, thumbnailSize.Height);
                    using (Stream output = zip.CreateEntry(mode == "multiple" ? "docProps/custom-thumbnail.png" : "docProps/thumbnail.png").Open())
                        thumbnail.Save(output, ImageFormat.Png);
                }
            }
            if (mode == "multiple")
            {
                using (var thumbnail = new Bitmap(1280, 720))
                using (Graphics graphics = Graphics.FromImage(thumbnail))
                {
                    graphics.Clear(Color.FromArgb(180, 40, 130));
                    using (var brush = new SolidBrush(Color.FromArgb(30, 190, 130))) graphics.FillRectangle(brush, 640, 0, 640, 720);
                    using (Stream output = zip.CreateEntry("docProps/thumbnail.png").Open()) thumbnail.Save(output, ImageFormat.Png);
                }
            }
            if (extension != ".pptx") return path;
            Entry(zip, "ppt/presentation.xml", mode == "malformed-presentation" ? "<p:presentation" :
                "<p:presentation xmlns:p=\"" + PresentationNamespace + "\" xmlns:r=\"" + RelationshipNamespace + "\">" +
                "<p:sldIdLst><p:sldId id=\"256\" r:id=\"rId1\"/></p:sldIdLst><p:sldSz cx=\"12192000\" cy=\"6858000\"/></p:presentation>");
            Entry(zip, "ppt/_rels/presentation.xml.rels", Relationships(
                "<Relationship Id=\"rId1\" Type=\"" + RelationshipNamespace + "/slide\" Target=\"slides/slide1.xml\"/>"));
            if (mode != "missing")
                Entry(zip, "ppt/slides/slide1.xml", mode == "malformed" ? "<p:sld" : Slide(mode));
        }
        return path;
    }

    private static string Slide(string mode)
    {
        string content;
        if (mode == "group") content = "<p:grpSp><p:nvGrpSpPr/><p:grpSpPr/>" + TextShape() + "</p:grpSp>";
        else if (mode == "chart") content = "<p:graphicFrame><a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/chart\">" +
            "<c:chart xmlns:c=\"http://schemas.openxmlformats.org/drawingml/2006/chart\" r:id=\"rIdChart\"/></a:graphicData></a:graphic></p:graphicFrame>";
        else if (mode == "mixed-styles") content = TextShape() + "<p:grpSp><p:nvGrpSpPr/><p:grpSpPr/>" +
            "<p:graphicFrame><a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/chart\">" +
            "<c:chart xmlns:c=\"http://schemas.openxmlformats.org/drawingml/2006/chart\" r:id=\"rIdChart\"/></a:graphicData></a:graphic></p:graphicFrame></p:grpSp>";
        else if (mode == "missing-picture") content = "<p:pic><p:blipFill><a:blip r:embed=\"missingImage\"/></p:blipFill></p:pic>";
        else if (mode == "blank") content = "";
        else content = TextShape();
        return "<p:sld xmlns:p=\"" + PresentationNamespace + "\" xmlns:a=\"" + DrawingNamespace + "\" xmlns:r=\"" + RelationshipNamespace + "\">" +
            "<p:cSld><p:bg><p:bgPr><a:solidFill><a:srgbClr val=\"13253D\"/></a:solidFill></p:bgPr></p:bg>" +
            "<p:spTree><p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/>" +
            content + "</p:spTree></p:cSld></p:sld>";
    }

    private static string TextShape()
    {
        return "<p:sp><p:nvSpPr><p:cNvPr id=\"2\" name=\"Preview title\"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>" +
            "<p:spPr><a:xfrm><a:off x=\"914400\" y=\"1828800\"/><a:ext cx=\"10363200\" cy=\"2743200\"/></a:xfrm></p:spPr>" +
            "<p:txBody><a:bodyPr/><a:lstStyle/><a:p><a:r><a:rPr lang=\"en-US\" sz=\"3600\" b=\"1\">" +
            "<a:solidFill><a:srgbClr val=\"FFFFFF\"/></a:solidFill></a:rPr><a:t>Large slide preview</a:t></a:r></a:p>" +
            "<a:p><a:r><a:rPr lang=\"en-US\" sz=\"2000\"><a:solidFill><a:srgbClr val=\"69DBE8\"/></a:solidFill></a:rPr>" +
            "<a:t>Rendered from slide content at the selected size.</a:t></a:r></a:p></p:txBody></p:sp>";
    }

    private static string Relationships(string body)
    {
        return "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" + body + "</Relationships>";
    }

    private static void Entry(ZipArchive zip, string name, string contents)
    {
        using (Stream stream = zip.CreateEntry(name).Open())
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(contents);
    }
}
