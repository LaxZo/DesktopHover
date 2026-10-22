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
        public bool Upscale=false;
        public bool StartWithWindows;
        public string Language="en";

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
            Language=L10n.Normalize(Language);
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
                        if(name=="upscale" && bool.TryParse(value,out b)) settings.Upscale=b;
                        if(name=="language") settings.Language=value;
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
                    "# Desktop Hover\r\nwidth={0}\r\nheight={1}\r\ndelay_ms={2}\r\nupscale={3}\r\nlanguage={4}\r\nvolume_percent={5}\r\n",
                    MaxWidth,MaxHeight,DelayMs,Upscale.ToString().ToLowerInvariant(),Language,VolumePercent);
                File.WriteAllText(temporary,text,new UTF8Encoding(false));
                if(File.Exists(path)) File.Replace(temporary,path,null);
                else File.Move(temporary,path);
            }
            finally { if(File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
