using System;
using System.IO;
using System.Text;

namespace DesktopHover
{
    internal static class PreviewFileStamp
    {
        internal static string Read(string path)
        {
            if (String.IsNullOrEmpty(path)) return null;
            var stamp = new StringBuilder();
            Append(stamp, path);
            if (AdobeProjectFiles.IsSupported(path))
            {
                string directory = Path.GetDirectoryName(path);
                foreach (string stem in new[] { path, Path.Combine(directory, Path.GetFileNameWithoutExtension(path)) })
                    foreach (string extension in new[] { ".mp4", ".mov", ".webm", ".m4v", ".avi", ".wmv", ".png", ".jpg", ".jpeg", ".webp", ".bmp" })
                        Append(stamp, stem + ".preview" + extension);
            }
            return stamp.ToString();
        }
        private static void Append(StringBuilder stamp, string path)
        {
            try
            {
                var file = new FileInfo(path);
                stamp.Append(file.Exists ? file.Length + ":" + file.LastWriteTimeUtc.Ticks : "missing");
            }
            catch { stamp.Append("unavailable"); }
            stamp.Append('|');
        }
    }
}
