using System;
using System.IO;

namespace DesktopHover
{
    internal enum MediaKind { None, Image, Audio, Video, Text }

    internal static class MediaFiles
    {
        // Extensions identify candidates; Windows still needs a decoder for the file's codec.
        internal static MediaKind Kind(string path)
        {
            if(String.IsNullOrWhiteSpace(path) || path.IndexOfAny(Path.GetInvalidPathChars())>=0) return MediaKind.None;
            string extension;
            try { extension=Path.GetExtension(path).ToLowerInvariant(); }
            catch(ArgumentException) { return MediaKind.None; }
            switch(extension)
            {
                case ".jpg": case ".jpeg": case ".jfif": case ".png": case ".bmp":
                case ".gif": case ".tif": case ".tiff": case ".ico": case ".webp":
                case ".heic": case ".heif": case ".avif":
                    return MediaKind.Image;
                case ".mp3": case ".wav": case ".wma": case ".m4a": case ".aac":
                case ".flac": case ".aif": case ".aiff":
                    return MediaKind.Audio;
                case ".mp4": case ".m4v": case ".mov": case ".avi": case ".wmv":
                case ".mpg": case ".mpeg": case ".mkv": case ".webm":
                    return MediaKind.Video;
                default:
                    return TextPreviewContent.IsSupported(path) ? MediaKind.Text : MediaKind.None;
            }
        }
    }
}
