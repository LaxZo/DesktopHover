using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Xml;

namespace DesktopHover
{
    // Prepare only the disposable copy used for native export. OLE objects are never
    // activated: hidden data is omitted and visible objects keep their saved picture.
    internal static class PowerPointPreviewCopy
    {
        private const string Presentation = "http://schemas.openxmlformats.org/presentationml/2006/main";
        private const string Drawing = "http://schemas.openxmlformats.org/drawingml/2006/main";
        private const string Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private const string PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";
        private const string ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";
        private const long MaximumXmlBytes = 4L * 1024L * 1024L;
        private const long MaximumTotalXmlBytes = 32L * 1024L * 1024L;

        private sealed class Relation
        {
            internal string Id, Type, Target;
            internal XmlElement Element;
        }

        private sealed class RelationPart
        {
            internal string Name, Source;
            internal XmlDocument Document;
            internal readonly Dictionary<string, Relation> Items = new Dictionary<string, Relation>(StringComparer.Ordinal);
        }

        internal static bool Prepare(string temporaryCopy)
        {
            string rebuilt = temporaryCopy + ".sanitized";
            bool created = false;
            try
            {
                using (var input = new FileStream(temporaryCopy, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (!DocumentArchiveGuard.IsSafe(input, CancellationToken.None)) return false;
                    using (var archive = new ZipArchive(input, ZipArchiveMode.Read))
                    {
                        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
                        long expandedBytes = 0, xmlBytes = 0;
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            string name = entry.FullName;
                            if (!ValidName(name) || entries.ContainsKey(name)) return false;
                            string lower = name.ToLowerInvariant();
                            if (lower.Contains("vbaproject") || lower.StartsWith("ppt/activex/", StringComparison.Ordinal)) return false;
                            expandedBytes += entry.Length;
                            if (expandedBytes > DocumentPreviewContent.MaximumFileBytes) return false;
                            entries.Add(name, entry);
                        }
                        ZipArchiveEntry typesEntry;
                        if (!entries.TryGetValue("[Content_Types].xml", out typesEntry)) return false;
                        XmlDocument types = ReadXml(typesEntry, ref xmlBytes);
                        if (types.DocumentElement == null || types.DocumentElement.LocalName != "Types" ||
                            types.DocumentElement.NamespaceURI != ContentTypes) return false;
                        foreach (XmlNode node in types.DocumentElement.ChildNodes)
                        {
                            var element = node as XmlElement;
                            if (element == null) continue;
                            string type = element.GetAttribute("ContentType").ToLowerInvariant();
                            if (type.Contains("macroenabled") || type.Contains("vbaproject") || type.Contains("activex")) return false;
                        }

                        var relationParts = new List<RelationPart>();
                        var relationsBySource = new Dictionary<string, RelationPart>(StringComparer.OrdinalIgnoreCase);
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            if (!entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)) continue;
                            string source = RelationSource(entry.FullName);
                            if (source == null || (source.Length != 0 && !entries.ContainsKey(source)) || relationsBySource.ContainsKey(source)) return false;
                            var part = new RelationPart { Name = entry.FullName, Source = source, Document = ReadXml(entry, ref xmlBytes) };
                            XmlElement root = part.Document.DocumentElement;
                            if (root == null || root.LocalName != "Relationships" || root.NamespaceURI != PackageRelationships) return false;
                            foreach (XmlNode node in root.ChildNodes)
                            {
                                var element = node as XmlElement;
                                if (element == null) continue;
                                if (element.LocalName != "Relationship" || element.NamespaceURI != PackageRelationships) return false;
                                string id = element.GetAttribute("Id"), type = element.GetAttribute("Type"), target = element.GetAttribute("Target");
                                if (id.Length == 0 || type.Length == 0 || target.Length == 0 || part.Items.ContainsKey(id)) return false;
                                string mode = element.GetAttribute("TargetMode");
                                if (String.Equals(mode, "External", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (!type.EndsWith("/hyperlink", StringComparison.Ordinal)) return false;
                                    target = null;
                                }
                                else
                                {
                                    if (mode.Length != 0 && !String.Equals(mode, "Internal", StringComparison.OrdinalIgnoreCase)) return false;
                                    target = ResolveTarget(source, target);
                                    if (target == null || !entries.ContainsKey(target)) return false;
                                }
                                part.Items.Add(id, new Relation { Id = id, Type = type, Target = target, Element = element });
                            }
                            relationParts.Add(part);
                            relationsBySource.Add(source, part);
                        }

                        var changes = new Dictionary<string, XmlDocument>(StringComparer.OrdinalIgnoreCase);
                        var removedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            if (!entry.FullName.StartsWith("ppt/", StringComparison.OrdinalIgnoreCase) ||
                                !entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) continue;
                            XmlDocument document = ReadXml(entry, ref xmlBytes);
                            var ns = Namespaces(document);
                            RelationPart part;
                            relationsBySource.TryGetValue(entry.FullName, out part);
                            bool modified = false;
                            XmlNodeList frames = document.SelectNodes("//p:graphicFrame[.//p:oleObj]", ns);
                            foreach (XmlNode frame in frames)
                            {
                                if (part == null) return false;
                                var identity = frame.SelectSingleNode("p:nvGraphicFramePr/p:cNvPr", ns) as XmlElement;
                                if (identity == null) return false;
                                string objectId = null;
                                foreach (XmlElement ole in frame.SelectNodes(".//p:oleObj", ns))
                                {
                                    string id = ole.GetAttribute("id", Relationships);
                                    Relation relation;
                                    if (!part.Items.TryGetValue(id, out relation) ||
                                        !relation.Type.EndsWith("/oleObject", StringComparison.Ordinal) || relation.Target == null ||
                                        (objectId != null && objectId != id)) return false;
                                    objectId = id;
                                }
                                string hidden = identity.GetAttribute("hidden");
                                if (hidden == "1" || hidden == "true") frame.ParentNode.RemoveChild(frame);
                                else
                                {
                                    if (hidden.Length != 0 && hidden != "0" && hidden != "false") return false;
                                    XmlElement picture = SavedPicture(frame, part, identity, ns);
                                    if (picture == null) return false;
                                    frame.ParentNode.ReplaceChild(picture, frame);
                                }
                                modified = true;
                            }
                            // Unknown OLE placements cannot be safely flattened.
                            if (document.SelectSingleNode("//*[local-name()='oleObj']") != null) return false;
                            if (modified) changes.Add(entry.FullName, document);
                            if (part == null) continue;
                            foreach (Relation relation in part.Items.Values)
                            {
                                if (!relation.Type.EndsWith("/oleObject", StringComparison.Ordinal)) continue;
                                foreach (XmlElement element in document.SelectNodes("//*"))
                                    foreach (XmlAttribute attribute in element.Attributes)
                                        if (attribute.NamespaceURI == Relationships && attribute.Value == relation.Id) return false;
                                if (relation.Target != null) removedTargets.Add(relation.Target);
                                relation.Element.ParentNode.RemoveChild(relation.Element);
                                changes[part.Name] = part.Document;
                            }
                        }

                        var remainingTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (RelationPart part in relationParts)
                            foreach (Relation relation in part.Items.Values)
                                if (relation.Element.ParentNode != null)
                                {
                                    if (relation.Type.EndsWith("/oleObject", StringComparison.Ordinal)) return false;
                                    if (relation.Target != null) remainingTargets.Add(relation.Target);
                                }
                        var removed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (string target in removedTargets)
                            if (!remainingTargets.Contains(target) && target.StartsWith("ppt/embeddings/", StringComparison.OrdinalIgnoreCase)) removed.Add(target);
                        foreach (string name in entries.Keys)
                            if (name.StartsWith("ppt/embeddings/", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
                            {
                                if (remainingTargets.Contains(name)) return false;
                                removed.Add(name);
                            }
                        foreach (RelationPart part in relationParts)
                            if (removed.Contains(part.Source)) removed.Add(part.Name);
                        if (changes.Count == 0 && removed.Count == 0) return true;

                        var typeNodes = new List<XmlElement>();
                        foreach (XmlNode node in types.DocumentElement.ChildNodes)
                            if (node is XmlElement) typeNodes.Add((XmlElement)node);
                        foreach (XmlElement element in typeNodes)
                        {
                            if (element.LocalName == "Override")
                            {
                                string part = ResolveTarget("", element.GetAttribute("PartName"));
                                if (part == null) return false;
                                if (removed.Contains(part)) element.ParentNode.RemoveChild(element);
                            }
                            else if (element.LocalName == "Default")
                            {
                                string suffix = "." + element.GetAttribute("Extension");
                                bool used = false;
                                foreach (string name in entries.Keys)
                                    if (!removed.Contains(name) && name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) { used = true; break; }
                                if (!used) element.ParentNode.RemoveChild(element);
                            }
                            else return false;
                        }
                        changes[typesEntry.FullName] = types;
                        using (var output = new FileStream(rebuilt, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            created = true;
                            using (var result = new ZipArchive(output, ZipArchiveMode.Create, true))
                            {
                                byte[] buffer = new byte[65536];
                                foreach (ZipArchiveEntry entry in archive.Entries)
                                {
                                    if (removed.Contains(entry.FullName)) continue;
                                    XmlDocument changed;
                                    bool modified = changes.TryGetValue(entry.FullName, out changed);
                                    // Media is already compressed. This short-lived copy should
                                    // spend its time rendering slides, not recompressing images.
                                    using (Stream target = result.CreateEntry(entry.FullName,
                                        modified ? CompressionLevel.Optimal : CompressionLevel.NoCompression).Open())
                                    {
                                        if (modified)
                                        {
                                            using (XmlWriter writer = XmlWriter.Create(target, new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = false }))
                                                changed.Save(writer);
                                        }
                                        else using (Stream original = entry.Open())
                                        {
                                            long copied = 0;
                                            int count;
                                            while ((count = original.Read(buffer, 0, buffer.Length)) != 0)
                                            {
                                                copied += count;
                                                if (copied > entry.Length) return false;
                                                target.Write(buffer, 0, count);
                                            }
                                            if (copied != entry.Length) return false;
                                        }
                                    }
                                }
                            }
                            if (output.Length > DocumentPreviewContent.MaximumFileBytes) return false;
                        }
                    }
                }
                File.Replace(rebuilt, temporaryCopy, null);
                created = false;
                return true;
            }
            catch { return false; }
            finally { if (created) { try { File.Delete(rebuilt); } catch { } } }
        }

        private static XmlElement SavedPicture(XmlNode frame, RelationPart part, XmlElement identity, XmlNamespaceManager ns)
        {
            var source = frame.SelectSingleNode(".//p:oleObj/p:pic", ns) as XmlElement;
            var transform = frame.SelectSingleNode("p:xfrm", ns) as XmlElement;
            if (source == null || transform == null) return null;
            var blip = source.SelectSingleNode("p:blipFill/a:blip", ns) as XmlElement;
            Relation image;
            if (blip == null || blip.HasAttribute("link", Relationships) ||
                !part.Items.TryGetValue(blip.GetAttribute("embed", Relationships), out image) || image.Target == null ||
                !image.Type.EndsWith("/image", StringComparison.Ordinal)) return null;
            var picture = (XmlElement)source.CloneNode(true);
            XmlNode previousIdentity = picture.SelectSingleNode("p:nvPicPr/p:cNvPr", ns);
            var properties = picture.SelectSingleNode("p:spPr", ns) as XmlElement;
            if (previousIdentity == null || properties == null) return null;
            previousIdentity.ParentNode.ReplaceChild(identity.CloneNode(true), previousIdentity);
            XmlElement placement = picture.OwnerDocument.CreateElement("a", "xfrm", Drawing);
            foreach (XmlAttribute attribute in transform.Attributes)
                if (attribute.Prefix != "xmlns" && attribute.Name != "xmlns") placement.Attributes.Append((XmlAttribute)attribute.CloneNode(true));
            foreach (XmlNode child in transform.ChildNodes) placement.AppendChild(child.CloneNode(true));
            XmlNode previousPlacement = properties.SelectSingleNode("a:xfrm", ns);
            if (previousPlacement != null) properties.ReplaceChild(placement, previousPlacement);
            else properties.PrependChild(placement);
            return picture;
        }

        private static XmlNamespaceManager Namespaces(XmlDocument document)
        {
            var ns = new XmlNamespaceManager(document.NameTable);
            ns.AddNamespace("p", Presentation); ns.AddNamespace("a", Drawing);
            return ns;
        }

        private static XmlDocument ReadXml(ZipArchiveEntry entry, ref long totalBytes)
        {
            totalBytes += entry.Length;
            if (entry.Length > MaximumXmlBytes || totalBytes > MaximumTotalXmlBytes) throw new InvalidDataException();
            var document = new XmlDocument { XmlResolver = null, PreserveWhitespace = true };
            using (Stream input = entry.Open())
            using (XmlReader reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null, MaxCharactersInDocument = MaximumXmlBytes })) document.Load(reader);
            return document;
        }

        private static bool ValidName(string name)
        {
            if (String.IsNullOrEmpty(name) || name[0] == '/' || name.IndexOfAny(new[] { '\\', ':', '?', '#', '%' }) >= 0) return false;
            string[] parts = name.Split('/');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i] == "." || parts[i] == ".." || (parts[i].Length == 0 && i != parts.Length - 1)) return false;
            return true;
        }

        private static string RelationSource(string name)
        {
            if (String.Equals(name, "_rels/.rels", StringComparison.OrdinalIgnoreCase)) return "";
            int marker = name.LastIndexOf("/_rels/", StringComparison.OrdinalIgnoreCase);
            if (marker < 0) return null;
            return name.Substring(0, marker + 1) + name.Substring(marker + 7, name.Length - marker - 12);
        }

        private static string ResolveTarget(string source, string target)
        {
            if (String.IsNullOrEmpty(target) || target.IndexOfAny(new[] { '\\', ':', '?', '#' }) >= 0) return null;
            target = Uri.UnescapeDataString(target);
            if (target.IndexOfAny(new[] { '\\', ':', '?', '#', '%' }) >= 0) return null;
            int slash = source.LastIndexOf('/');
            string combined = target.StartsWith("/", StringComparison.Ordinal) ? target.Substring(1)
                : (slash < 0 ? "" : source.Substring(0, slash + 1)) + target;
            var parts = new List<string>();
            foreach (string part in combined.Split('/'))
            {
                if (part == ".") continue;
                if (part == "..") { if (parts.Count == 0) return null; parts.RemoveAt(parts.Count - 1); }
                else { if (part.Length == 0) return null; parts.Add(part); }
            }
            string result = String.Join("/", parts.ToArray());
            return ValidName(result) ? result : null;
        }
    }
}
