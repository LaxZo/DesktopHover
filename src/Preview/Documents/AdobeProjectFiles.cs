using System;
using System.IO;

namespace DesktopHover
{
    // Recognized projects can use their saved preview or an explicit .preview sidecar.
    // Recognition does not imply that a proprietary project can be fully rendered.
    internal static class AdobeProjectFiles
    {
        internal static bool IsSupported(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return false;
            string extension;
            try { extension = Path.GetExtension(path).ToLowerInvariant(); }
            catch (ArgumentException) { return false; }
            switch (extension)
            {
                case ".psd": case ".psb": case ".psdt":
                case ".ai": case ".ait": case ".eps": case ".epsf": case ".epsi":
                case ".indd": case ".indt": case ".indb": case ".idml": case ".inx":
                case ".icml": case ".icmt":
                case ".xd":
                case ".aep": case ".aepx": case ".aet": case ".aetx":
                case ".prproj": case ".prtl": case ".mogrt": case ".aegraphic":
                case ".fla": case ".xfl": case ".swf":
                case ".chproj": case ".puppet":
                case ".cpt": case ".cptx": case ".cptl": case ".cp":
                case ".dn": case ".sbs": case ".sbsar": case ".spp":
                case ".ssa": case ".alch": case ".ssg": case ".smod":
                case ".sesx":
                    return true;
                default:
                    return false;
            }
        }
    }
}
