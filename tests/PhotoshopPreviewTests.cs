using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;

internal static class PhotoshopPreviewTests
{
    private static readonly Color CompositeColor = Color.FromArgb(24, 146, 216);
    private static readonly Color ThumbnailColor = Color.FromArgb(180, 50, 20);

    internal static void Run(Assembly app, string directory, Action<string, Action> test)
    {
        Directory.CreateDirectory(directory);
        string large = CreateFixture(directory);
        test("Photoshop uses 3000px composite at the default preview size", delegate
        {
            CheckPreview(app, large, new Size(720, 540), new Size(540, 540), "document_composite", CompositeColor);
        });
        test("Photoshop uses 3000px composite at the largest preview size", delegate
        {
            CheckPreview(app, large, new Size(1440, 1080), new Size(1080, 1080), "document_composite", CompositeColor);
        });
        test("Photoshop RLE PSD uses composite instead of saved thumbnail", delegate
        {
            string path = Path.Combine(directory, "photoshop-rle.psd");
            WriteFixture(path, 1, 600, 400, 8, 1, true, true, false);
            CheckPreview(app, path, new Size(720, 540), new Size(600, 400), "document_composite", CompositeColor);
        });
        test("Photoshop RLE PSB uses composite and preserves aspect ratio", delegate
        {
            string path = Path.Combine(directory, "photoshop-rle.psb");
            WriteFixture(path, 2, 1200, 800, 8, 1, true, true, false);
            CheckPreview(app, path, new Size(720, 540), new Size(720, 480), "document_composite", CompositeColor);
        });
        test("Photoshop composite without saved thumbnail remains supported", delegate
        {
            string path = Path.Combine(directory, "photoshop-no-thumbnail.psd");
            WriteFixture(path, 1, 300, 200, 8, 0, false, true, false);
            CheckPreview(app, path, new Size(720, 540), new Size(300, 200), "document_composite", CompositeColor);
        });
        test("Photoshop unsupported depth falls back to saved thumbnail", delegate
        {
            string path = Path.Combine(directory, "photoshop-depth16.psd");
            WriteFixture(path, 1, 3000, 3000, 16, 0, true, false, false);
            CheckThumbnail(app, path);
        });
        test("Photoshop unsupported compression falls back to saved thumbnail", delegate
        {
            string path = Path.Combine(directory, "photoshop-zip.psd");
            WriteFixture(path, 1, 3000, 3000, 8, 2, true, false, false);
            CheckThumbnail(app, path);
        });
        test("Photoshop composite pixel limit retains thumbnail fallback", delegate
        {
            string path = Path.Combine(directory, "photoshop-pixel-limit.psb");
            WriteFixture(path, 2, 8000, 8000, 8, 0, true, false, false);
            CheckThumbnail(app, path);
        });
        test("Photoshop truncated raw composite falls back to saved thumbnail", delegate
        {
            string path = Path.Combine(directory, "photoshop-truncated.psd");
            WriteFixture(path, 1, 3000, 3000, 8, 0, true, false, false);
            CheckThumbnail(app, path);
        });
        test("Photoshop corrupt RLE composite falls back after decoding error", delegate
        {
            string path = Path.Combine(directory, "photoshop-invalid-rle.psd");
            WriteFixture(path, 1, 600, 400, 8, 1, true, true, true);
            CheckThumbnail(app, path);
        });
        test("Photoshop unsupported composite without thumbnail returns no image", delegate
        {
            string path = Path.Combine(directory, "photoshop-unsupported-no-thumbnail.psd");
            WriteFixture(path, 1, 3000, 3000, 16, 0, false, false, false);
            object result = Load(app, path, new Size(720, 540), CancellationToken.None);
            if (result != null)
            {
                ((IDisposable)result).Dispose();
                throw new Exception("Unsupported data without a thumbnail must not produce a preview.");
            }
        });
        test("Photoshop cancellation is propagated instead of using fallback", delegate
        {
            using (var source = new CancellationTokenSource())
            {
                source.Cancel();
                try
                {
                    object result = Load(app, large, new Size(720, 540), source.Token);
                    if (result != null) ((IDisposable)result).Dispose();
                }
                catch (OperationCanceledException) { return; }
                throw new Exception("Expected cancellation to be propagated.");
            }
        });
    }

    // Also used by the preview-window integration test.
    internal static string CreateFixture(string directory)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "photoshop-full-resolution.psd");
        WriteFixture(path, 1, 3000, 3000, 8, 0, true, true, false);
        return path;
    }

    private static void CheckThumbnail(Assembly app, string path)
    {
        CheckPreview(app, path, new Size(720, 540), new Size(160, 160), "document_embedded", ThumbnailColor);
    }

    private static void CheckPreview(Assembly app, string path, Size maximum, Size expected, string note, Color color)
    {
        object result = Load(app, path, maximum, CancellationToken.None);
        if (result == null) throw new Exception("Expected a preview for " + Path.GetFileName(path));
        using ((IDisposable)result)
        {
            Type type = result.GetType();
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            Bitmap image = (Bitmap)type.GetField("Image", fields).GetValue(result);
            string actualNote = (string)type.GetField("NoteKey", fields).GetValue(result);
            if (image == null || image.Size != expected)
                throw new Exception("Expected image " + expected + "; got " + (image == null ? "null" : image.Size.ToString()));
            if (actualNote != note) throw new Exception("Expected " + note + "; got " + actualNote);
            if (image.GetPixel(image.Width / 2, image.Height / 2).ToArgb() != color.ToArgb())
                throw new Exception("Preview pixels came from the wrong image source.");
        }
    }

    private static object Load(Assembly app, string path, Size maximum, CancellationToken cancellation)
    {
        MethodInfo method = app.GetType("DesktopHover.AdobePreviewReader", true).GetMethod("TryLoad", BindingFlags.Static | BindingFlags.NonPublic);
        try { return method.Invoke(null, new object[] { path, maximum, cancellation }); }
        catch (TargetInvocationException error)
        {
            if (error.InnerException != null) throw error.InnerException;
            throw;
        }
    }

    private static void WriteFixture(string path, int version, int width, int height, int depth, int compression,
        bool thumbnail, bool composite, bool corruptRle)
    {
        using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            WriteU32(output, 0x38425053); // 8BPS
            WriteU16(output, version);
            output.Write(new byte[6], 0, 6);
            WriteU16(output, 3);
            WriteU32(output, (uint)height);
            WriteU32(output, (uint)width);
            WriteU16(output, depth);
            WriteU16(output, 3); // RGB
            WriteU32(output, 0); // Color-mode data.
            byte[] resources = thumbnail ? ThumbnailResource() : new byte[0];
            WriteU32(output, (uint)resources.Length);
            output.Write(resources, 0, resources.Length);
            if (version == 2) WriteU32(output, 0);
            WriteU32(output, 0); // Layer/mask section: 32 bits in PSD, 64 in PSB.
            WriteU16(output, compression);
            if (!composite) return;
            byte[] colors = { CompositeColor.R, CompositeColor.G, CompositeColor.B };
            if (compression == 1)
            {
                int rowLength = corruptRle ? 2 : SolidRleRow(width, 0).Length;
                for (int row = 0; row < height * 3; row++)
                {
                    if (version == 1) WriteU16(output, rowLength);
                    else WriteU32(output, (uint)rowLength);
                }
                if (corruptRle)
                {
                    // Literal run announces two bytes but provides only one.
                    output.WriteByte(1);
                    output.WriteByte(24);
                    return;
                }
            }
            foreach (byte value in colors)
            {
                byte[] row = compression == 1 ? SolidRleRow(width, value) : new byte[width];
                if (compression == 0) for (int x = 0; x < row.Length; x++) row[x] = value;
                for (int y = 0; y < height; y++) output.Write(row, 0, row.Length);
            }
        }
    }

    private static byte[] ThumbnailResource()
    {
        const int width = 160, height = 160, rowBytes = width * 3;
        using (var output = new MemoryStream())
        {
            WriteU32(output, 0x3842494d); // 8BIM
            WriteU16(output, 1036); // RGB thumbnail.
            WriteU16(output, 0); // Empty padded Pascal name.
            WriteU32(output, 28 + rowBytes * height);
            WriteU32(output, 0); // Uncompressed RGB.
            WriteU32(output, width);
            WriteU32(output, height);
            WriteU32(output, rowBytes);
            WriteU32(output, rowBytes * height);
            WriteU32(output, rowBytes * height);
            WriteU16(output, 24);
            WriteU16(output, 1);
            byte[] row = new byte[rowBytes];
            for (int x = 0; x < row.Length; x += 3)
            {
                row[x] = ThumbnailColor.R;
                row[x + 1] = ThumbnailColor.G;
                row[x + 2] = ThumbnailColor.B;
            }
            for (int y = 0; y < height; y++) output.Write(row, 0, row.Length);
            return output.ToArray();
        }
    }

    private static byte[] SolidRleRow(int width, byte value)
    {
        using (var output = new MemoryStream())
        {
            while (width > 0)
            {
                int run = Math.Min(128, width);
                output.WriteByte(unchecked((byte)(1 - run)));
                output.WriteByte(value);
                width -= run;
            }
            return output.ToArray();
        }
    }

    private static void WriteU16(Stream output, int value)
    {
        output.WriteByte((byte)(value >> 8));
        output.WriteByte((byte)value);
    }

    private static void WriteU32(Stream output, uint value)
    {
        WriteU16(output, (int)(value >> 16));
        WriteU16(output, (int)(value & 0xffff));
    }
}
