using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace DesktopHover
{
    internal sealed class FolderNameIndex
    {
        private readonly Dictionary<string, string> names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        internal void Add(string name, string path, bool supported)
        { AddName(names, name, path, supported); }

        internal void AddItem(string display, string path, bool supported)
        {
            Add(display, path, supported);
            AddName(aliases, Path.GetFileName(path), path, supported);
        }

        private static void AddName(Dictionary<string, string> map, string name, string path, bool supported)
        {
            if (String.IsNullOrEmpty(name)) return;
            string old;
            string value = supported ? path : null;
            if (map.TryGetValue(name, out old) && !String.Equals(old, value, StringComparison.OrdinalIgnoreCase)) value = null;
            map[name] = value;
        }
        internal string Find(string name)
        {
            string path;
            if (names.TryGetValue(name, out path)) return path;
            return aliases.TryGetValue(name, out path) ? path : null;
        }
        internal bool Contains(string name) { return names.ContainsKey(name) || aliases.ContainsKey(name); }
    }

    // Accessed by the shell worker only. Watcher callbacks update just an atomic revision.
    internal sealed class FolderLookupCache : IDisposable
    {
        private readonly LinkedList<Entry> entries = new LinkedList<Entry>();
        private sealed class Entry : IDisposable
        {
            internal string Path;
            internal FolderNameIndex Index;
            internal FileSystemWatcher Watcher;
            internal int Revision, BuiltRevision;
            internal long Written;
            internal DateTime Built;
            internal DateTime LastMissRebuild;
            internal bool ShowExtensions;
            internal volatile bool Reliable;
            internal void Changed(object sender, FileSystemEventArgs e) { Interlocked.Increment(ref Revision); }
            internal void Failed(object sender, ErrorEventArgs e) { Reliable = false; Interlocked.Increment(ref Revision); }
            public void Dispose() { if (Watcher != null) Watcher.Dispose(); }
        }
        internal string Resolve(string directory, string name, Func<FolderNameIndex> build, bool showExtensions = true)
        {
            // Virtual/search folders may merge multiple locations; never reuse their name map.
            if (!String.IsNullOrEmpty(directory) && directory.StartsWith(@"\\", StringComparison.Ordinal)) return null;
            if (String.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return build().Find(name);
            if (
                new DriveInfo(System.IO.Path.GetPathRoot(directory)).DriveType == DriveType.Network) return null;
            Entry entry = null;
            for (var node = entries.First; node != null; node = node.Next)
                if (String.Equals(node.Value.Path, directory, StringComparison.OrdinalIgnoreCase))
                { entry = node.Value; entries.Remove(node); entries.AddFirst(node); break; }
            if (entry == null)
            {
                entry = new Entry { Path = directory };
                try
                {
                    entry.Watcher = new FileSystemWatcher(directory) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName };
                    entry.Watcher.Created += entry.Changed; entry.Watcher.Deleted += entry.Changed;
                    entry.Watcher.Renamed += entry.Changed; entry.Watcher.Error += entry.Failed;
                    entry.Watcher.EnableRaisingEvents = true; entry.Reliable = true;
                }
                catch { entry.Reliable = false; }
                while (entries.Count >= 4) { entries.Last.Value.Dispose(); entries.RemoveLast(); }
                entries.AddFirst(entry);
            }
            int revision = Interlocked.CompareExchange(ref entry.Revision, 0, 0);
            long written = Directory.GetLastWriteTimeUtc(directory).Ticks;
            bool rebuilt = false;
            if (entry.Index == null || entry.ShowExtensions != showExtensions || entry.BuiltRevision != revision || entry.Written != written ||
                DateTime.UtcNow - entry.Built > TimeSpan.FromSeconds(entry.Reliable ? 60 : 1))
            {
                entry.Index = build(); entry.BuiltRevision = revision; entry.Written = written; entry.Built = DateTime.UtcNow;
                entry.ShowExtensions = showExtensions;
                rebuilt = true;
            }
            // Explorer can change display names (e.g. hidden extensions) without a
            // filesystem event. Retry unknown names, but not known unsupported or
            // ambiguous items, and bound rescans of large folders on repeated misses.
            if (!rebuilt && !entry.Index.Contains(name) &&
                DateTime.UtcNow - entry.LastMissRebuild >= TimeSpan.FromSeconds(2))
            {
                entry.LastMissRebuild = DateTime.UtcNow;
                entry.Index = build(); entry.BuiltRevision = revision; entry.Written = written; entry.Built = DateTime.UtcNow;
            }
            return entry.Index.Find(name);
        }
        public void Dispose() { foreach (Entry entry in entries) entry.Dispose(); entries.Clear(); }
    }
}
