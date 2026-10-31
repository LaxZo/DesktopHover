using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace DesktopHover
{
    internal sealed class AppSettings
    {
        public int MaxWidth=720;
        public int MaxHeight=540;
        public int DelayMs=450;
        public int VolumePercent=100;
        public int SlideIntervalSeconds=3;
        public bool Upscale=false;
        public bool AnimationsEnabled=true;
        public bool AutoPlayMedia=true;
        public bool LoopVideo=true;
        public bool PreviewImages=true, PreviewDocuments=true, PreviewText=true, PreviewAudio=true, PreviewVideo=true;

        internal bool Allows(MediaKind kind)
        {
            switch (kind)
            {
                case MediaKind.Image: return PreviewImages;
                case MediaKind.Document: return PreviewDocuments;
                case MediaKind.Text: return PreviewText;
                case MediaKind.Audio: return PreviewAudio;
                case MediaKind.Video: return PreviewVideo;
                default: return true;
            }
        }
        public bool StartWithWindows;
        public string Language="en";
        public string Theme="dark";

        internal static string DefaultPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DesktopHover","settings.ini"); }
        }
        public AppSettings Clone() { return (AppSettings)MemberwiseClone(); }
        internal void Validate()
        {
            MaxWidth=Math.Max(320,Math.Min(1600,MaxWidth));
            MaxHeight=Math.Max(240,Math.Min(1200,MaxHeight));
            DelayMs=Math.Max(0,Math.Min(2000,DelayMs));
            VolumePercent=Math.Max(0,Math.Min(100,VolumePercent));
            SlideIntervalSeconds=Math.Max(1,Math.Min(10,SlideIntervalSeconds));
            Language=L10n.Normalize(Language);
            Theme=String.Equals(Theme,"light",StringComparison.OrdinalIgnoreCase) ? "light" : "dark";
        }
        internal static AppSettings Load() { return Load(DefaultPath); }
        internal static AppSettings Load(string path)
        {
            var settings=new AppSettings();
            try
            {
                if(File.Exists(path))
                    foreach(string line in File.ReadAllLines(path))
                    {
                        int split=line.IndexOf('=');
                        if(split<1) continue;
                        string name=line.Substring(0,split).Trim();
                        string value=line.Substring(split+1).Trim();
                        int n; bool b;
                        if(name=="width" && int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out n)) settings.MaxWidth=n;
                        if(name=="height" && int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out n)) settings.MaxHeight=n;
                        if(name=="delay_ms" && int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out n)) settings.DelayMs=n;
                        if(name=="volume_percent" && int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out n)) settings.VolumePercent=n;
                        if(name=="slide_interval_seconds" && int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out n)) settings.SlideIntervalSeconds=n;
                        if(name=="upscale" && bool.TryParse(value,out b)) settings.Upscale=b;
                        if(name=="animations_enabled" && bool.TryParse(value,out b)) settings.AnimationsEnabled=b;
                        if(name=="autoplay_media" && bool.TryParse(value,out b)) settings.AutoPlayMedia=b;
                        if(name=="loop_video" && bool.TryParse(value,out b)) settings.LoopVideo=b;
                        if(name=="preview_images" && bool.TryParse(value,out b)) settings.PreviewImages=b;
                        if(name=="preview_documents" && bool.TryParse(value,out b)) settings.PreviewDocuments=b;
                        if(name=="preview_text" && bool.TryParse(value,out b)) settings.PreviewText=b;
                        if(name=="preview_audio" && bool.TryParse(value,out b)) settings.PreviewAudio=b;
                        if(name=="preview_video" && bool.TryParse(value,out b)) settings.PreviewVideo=b;
                        if(name=="language") settings.Language=value;
                        if(name=="theme") settings.Theme=value;
                    }
            }
            catch(IOException) { }
            catch(UnauthorizedAccessException) { }
            settings.Validate();
            return settings;
        }
        internal void Save() { Save(DefaultPath); }
        internal void Save(string path)
        {
            Validate();
            string directory=Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(directory);
            string temporary=Path.Combine(directory,"settings-"+Guid.NewGuid().ToString("N")+".tmp");
            try
            {
                string text=string.Format(CultureInfo.InvariantCulture,
                    "# Desktop Hover\r\nwidth={0}\r\nheight={1}\r\ndelay_ms={2}\r\nupscale={3}\r\nlanguage={4}\r\nvolume_percent={5}\r\ntheme={6}\r\nslide_interval_seconds={7}\r\nanimations_enabled={8}\r\n",
                    MaxWidth,MaxHeight,DelayMs,Upscale.ToString().ToLowerInvariant(),Language,VolumePercent,Theme,SlideIntervalSeconds,AnimationsEnabled.ToString().ToLowerInvariant());
                text += "autoplay_media=" + AutoPlayMedia.ToString().ToLowerInvariant() + "\r\n";
                text += "loop_video=" + LoopVideo.ToString().ToLowerInvariant() + "\r\n";
                text += "preview_images=" + PreviewImages.ToString().ToLowerInvariant() + "\r\n";
                text += "preview_documents=" + PreviewDocuments.ToString().ToLowerInvariant() + "\r\n";
                text += "preview_text=" + PreviewText.ToString().ToLowerInvariant() + "\r\n";
                text += "preview_audio=" + PreviewAudio.ToString().ToLowerInvariant() + "\r\n";
                text += "preview_video=" + PreviewVideo.ToString().ToLowerInvariant() + "\r\n";
                File.WriteAllText(temporary,text,new UTF8Encoding(false));
                if(File.Exists(path)) File.Replace(temporary,path,null);
                else File.Move(temporary,path);
            }
            finally { if(File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
