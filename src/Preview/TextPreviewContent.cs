using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DesktopHover
{
    internal enum TextTokenKind { Plain, Keyword, String, Comment, Number, Type, Function, Operator, Preprocessor, Property }

    internal sealed class TextSpan
    {
        internal int Start, Length;
        internal TextTokenKind Kind;
    }

    internal sealed class TextPreviewLine
    {
        internal string Text;
        internal TextSpan[] Spans;
    }

    internal sealed class TextPreviewContent
    {
        private const int MaximumBytes = 256 * 1024;
        private const int MaximumLines = 300;
        private const int MaximumColumns = 500;
        internal TextPreviewLine[] Lines;
        internal string LanguageName, EncodingName;
        internal bool IsCode, Truncated;
        internal long FileBytes;

        internal static bool IsSupported(string path)
        {
            if (String.IsNullOrWhiteSpace(path) || path.IndexOfAny(Path.GetInvalidPathChars()) >= 0) return false;
            try { return LanguageFor(path) != null; }
            catch (ArgumentException) { return false; }
        }

        internal static TextPreviewContent Load(string path)
        {
            if (!IsSupported(path)) throw new InvalidDataException("Unsupported text file.");
            path = Path.GetFullPath(path);
            // Hovering must never fetch network content or hydrate an online-only cloud file.
            if (path.StartsWith(@"\\", StringComparison.Ordinal) ||
                new DriveInfo(Path.GetPathRoot(path)).DriveType == DriveType.Network)
                throw new IOException("Only local files can be previewed.");
            var file = new FileInfo(path);
            if (!file.Exists || ((int)file.Attributes & (0x10 | 0x1000 | 0x40000 | 0x400000)) != 0)
                throw new IOException("The file is not available locally.");

            byte[] bytes = new byte[MaximumBytes];
            int count = 0;
            long fileBytes;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan))
            {
                fileBytes = stream.Length;
                while (count < bytes.Length)
                {
                    int read = stream.Read(bytes, count, bytes.Length - count);
                    if (read == 0) break;
                    count += read;
                }
            }
            bool truncated = fileBytes > count;
            string encodingName;
            string text = Decode(bytes, count, truncated, out encodingName);
            RejectBinary(text);
            string language = LanguageFor(path);
            var lexer = new Lexer(language);
            var lines = new List<TextPreviewLine>();
            int start = 0;
            while (start <= text.Length && lines.Count < MaximumLines)
            {
                int end = start;
                while (end < text.Length && text[end] != '\r' && text[end] != '\n') end++;
                string line = NormalizeLine(text, start, end);
                if (line.Length > MaximumColumns) truncated = true;
                // Scan the complete (bounded) line so a comment after column 500 still
                // affects the following line, even though only its first 500 columns show.
                TextSpan[] spans = lexer.Scan(line);
                lines.Add(new TextPreviewLine {
                    Text = line.Length > MaximumColumns ? line.Substring(0, MaximumColumns) : line,
                    Spans = spans
                });
                if (end == text.Length) { start = text.Length + 1; break; }
                start = end + 1;
                if (text[end] == '\r' && start < text.Length && text[start] == '\n') start++;
            }
            if (start <= text.Length) truncated = true;
            return new TextPreviewContent {
                Lines = lines.ToArray(), LanguageName = language, EncodingName = encodingName,
                IsCode = language != "Plain text", Truncated = truncated, FileBytes = fileBytes
            };
        }

        private static string Decode(byte[] bytes, int count, bool truncated, out string name)
        {
            Encoding encoding;
            int offset = 0;
            bool explicitEncoding = true;
            if (count >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0 && bytes[3] == 0)
            { encoding = new UTF32Encoding(false, true, true); name = "UTF-32 LE"; offset = 4; }
            else if (count >= 4 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0xFE && bytes[3] == 0xFF)
            { encoding = new UTF32Encoding(true, true, true); name = "UTF-32 BE"; offset = 4; }
            else if (count >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            { encoding = new UTF8Encoding(false, true); name = "UTF-8 BOM"; offset = 3; }
            else if (count >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            { encoding = new UnicodeEncoding(false, true, true); name = "UTF-16 LE"; offset = 2; }
            else if (count >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            { encoding = new UnicodeEncoding(true, true, true); name = "UTF-16 BE"; offset = 2; }
            else
            { encoding = new UTF8Encoding(false, true); name = "UTF-8"; explicitEncoding = false; }
            try { return DecodePrefix(encoding, bytes, offset, count - offset, truncated); }
            catch (DecoderFallbackException)
            {
                if (explicitEncoding) throw new InvalidDataException("The text encoding is invalid.");
                encoding = Encoding.Default;
                // A UTF-8 system locale still needs a deterministic legacy fallback.
                if (encoding.CodePage == 65001) encoding = Encoding.GetEncoding(1252);
                name = "Windows-" + encoding.CodePage;
                return encoding.GetString(bytes, 0, count);
            }
        }

        private static string DecodePrefix(Encoding encoding, byte[] bytes, int offset, int count, bool truncated)
        {
            char[] chars = new char[encoding.GetMaxCharCount(count)];
            int usedBytes, usedChars;
            bool complete;
            // Do not mistake an incomplete final multibyte character at our byte cap for ANSI.
            encoding.GetDecoder().Convert(bytes, offset, count, chars, 0, chars.Length,
                !truncated, out usedBytes, out usedChars, out complete);
            return new string(chars, 0, usedChars);
        }

        private static void RejectBinary(string text)
        {
            int controls = 0;
            foreach (char c in text)
            {
                if (c == '\0') throw new InvalidDataException("Binary files cannot be previewed as text.");
                if (Char.IsControl(c) && c != '\r' && c != '\n' && c != '\t' && c != '\f') controls++;
            }
            if (controls > 16 || (controls > 0 && controls * 100 > text.Length))
                throw new InvalidDataException("Binary files cannot be previewed as text.");
        }

        private static string NormalizeLine(string text, int start, int end)
        {
            var line = new StringBuilder(end - start);
            for (int i = start; i < end; i++)
            {
                char c = text[i];
                if (c == '\t') line.Append(' ', 4 - line.Length % 4);
                else if (Char.IsControl(c) || (c >= '\u202A' && c <= '\u202E') ||
                    (c >= '\u2066' && c <= '\u2069') || c == '\u2028' || c == '\u2029') line.Append(' ');
                else line.Append(c);
            }
            return line.ToString();
        }

        private static string LanguageFor(string path)
        {
            string name = Path.GetFileName(path).ToLowerInvariant();
            if (name == "dockerfile" || name.StartsWith("dockerfile.", StringComparison.Ordinal)) return "Dockerfile";
            if (name == "makefile" || name == "gnumakefile" || name == "cmakelists.txt") return name == "cmakelists.txt" ? "CMake" : "Makefile";
            if (name == ".gitignore" || name == ".gitattributes" || name == ".editorconfig" || name == ".npmrc" || name == ".env" || name.StartsWith(".env.", StringComparison.Ordinal)) return "INI";
            switch (Path.GetExtension(name))
            {
                case ".txt": case ".text": case ".log": case ".csv": case ".tsv": return "Plain text";
                case ".md": case ".markdown": case ".mdown": return "Markdown";
                case ".c": case ".h": return "C";
                case ".cpp": case ".cc": case ".cxx": case ".hpp": case ".hh": case ".hxx": case ".ino": return "C++";
                case ".cs": case ".csx": return "C#";
                case ".java": return "Java";
                case ".js": case ".jsx": case ".mjs": case ".cjs": return "JavaScript";
                case ".ts": case ".tsx": case ".mts": case ".cts": return "TypeScript";
                case ".py": case ".pyw": case ".pyi": return "Python";
                case ".ps1": case ".psm1": case ".psd1": return "PowerShell";
                case ".sh": case ".bash": case ".zsh": case ".fish": return "Shell";
                case ".bat": case ".cmd": return "Batch";
                case ".json": case ".jsonc": case ".jsonl": case ".ipynb": return "JSON";
                case ".yaml": case ".yml": return "YAML";
                case ".ini": case ".cfg": case ".conf": case ".toml": case ".properties": return "INI";
                case ".html": case ".htm": case ".xhtml": case ".vue": case ".svelte": return "HTML";
                case ".xml": case ".xaml": case ".svg": case ".csproj": case ".vbproj": case ".props": case ".targets": case ".resx": case ".config": return "XML";
                case ".css": case ".scss": case ".sass": case ".less": return "CSS";
                case ".sql": return "SQL";
                case ".go": return "Go";
                case ".rs": return "Rust";
                case ".kt": case ".kts": return "Kotlin";
                case ".swift": return "Swift";
                case ".php": case ".phtml": return "PHP";
                case ".rb": return "Ruby";
                case ".cmake": return "CMake";
            }
            if (name == "readme" || name == "license" || name == "licence" || name == "copying" || name == "notice" || name == "changelog" || name == "authors") return "Plain text";
            return null;
        }

        // A bounded, deterministic lexer: source is never evaluated and no regex backtracking occurs.
        private sealed class Lexer
        {
            private readonly string language;
            private readonly HashSet<string> keywords;
            private readonly HashSet<string> types;
            private readonly bool hashComments, slashComments, blockComments, markup;
            private string blockEnd, stringEnd;
            private bool doubledQuote, backtickEscape, insideTag, tagName;
            private char markdownFence;

            internal Lexer(string language)
            {
                this.language = language;
                markup = language == "HTML" || language == "XML";
                hashComments = language == "Python" || language == "PowerShell" || language == "Shell" || language == "Ruby" ||
                    language == "YAML" || language == "INI" || language == "Dockerfile" || language == "Makefile" || language == "CMake" || language == "PHP";
                slashComments = !markup && !hashComments && language != "Plain text" && language != "Markdown" && language != "SQL" && language != "Batch";
                if (language == "PHP") slashComments = true;
                blockComments = slashComments || language == "SQL";
                var comparer = language == "SQL" || language == "PowerShell" || language == "Batch" || language == "Dockerfile" || language == "CMake" ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
                keywords = new HashSet<string>(KeywordList(language).Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), comparer);
                types = new HashSet<string>(("bool boolean byte char decimal double dynamic float int integer long object sbyte short signed size_t string String uint ulong unsigned ushort var void wchar_t auto number symbol bigint never unknown any list dict set tuple bytes str Array Object Date Promise Task Exception IEnumerable List Dictionary Console std").Split(' '), StringComparer.Ordinal);
            }

            internal TextSpan[] Scan(string line)
            {
                var spans = new List<TextSpan>();
                if (language == "Plain text") return spans.ToArray();
                if (language == "Markdown") { ScanMarkdown(line, spans); return spans.ToArray(); }
                int i = 0;
                bool firstToken = true;
                while (i < line.Length)
                {
                    int start = i;
                    if (blockEnd != null)
                    {
                        int close = line.IndexOf(blockEnd, i, StringComparison.Ordinal);
                        i = close < 0 ? line.Length : close + blockEnd.Length;
                        if (close >= 0) blockEnd = null;
                        Add(spans, start, i, TextTokenKind.Comment);
                        continue;
                    }
                    if (stringEnd != null)
                    {
                        ConsumeString(line, ref i);
                        Add(spans, start, i, TextTokenKind.String);
                        continue;
                    }
                    char c = line[i];
                    if (Char.IsWhiteSpace(c)) { i++; continue; }
                    if (markup)
                    {
                        if (At(line, i, "<!--")) { blockEnd = "-->"; continue; }
                        if (At(line, i, "<![CDATA["))
                        {
                            stringEnd = "]]>"; doubledQuote = false; backtickEscape = false;
                            i += 9; ConsumeString(line, ref i); Add(spans, start, i, TextTokenKind.String); continue;
                        }
                        if (c == '<') { insideTag = true; tagName = true; i++; Add(spans, start, i, TextTokenKind.Operator); continue; }
                        if (c == '>') { insideTag = false; i++; Add(spans, start, i, TextTokenKind.Operator); continue; }
                        if (!insideTag) { i++; continue; }
                    }
                    if ((hashComments && c == '#') || (slashComments && At(line, i, "//")) ||
                        (language == "SQL" && At(line, i, "--")) || (language == "INI" && c == ';') ||
                        (language == "Batch" && firstToken && (At(line, i, "::") || WordAt(line, i, "rem"))))
                    { Add(spans, start, line.Length, TextTokenKind.Comment); break; }
                    if (blockComments && At(line, i, "/*")) { blockEnd = "*/"; continue; }
                    if (language == "PowerShell" && At(line, i, "<#")) { blockEnd = "#>"; continue; }
                    if (firstToken && c == '#' && (language == "C" || language == "C++" || language == "C#"))
                    { Add(spans, start, line.Length, TextTokenKind.Preprocessor); break; }
                    firstToken = false;
                    bool verbatim = language == "C#" && (At(line, i, "@\"") || At(line, i, "$@\"") || At(line, i, "@$\""));
                    if (verbatim) i += line[i] == '@' && At(line, i, "@\"") ? 1 : 2;
                    if (verbatim || c == '\'' || c == '"' || ((language == "JavaScript" || language == "TypeScript" || language == "Go") && c == '`'))
                    {
                        char quote = line[i];
                        bool triple = language == "Python" && At(line, i, new string(quote, 3));
                        stringEnd = new string(quote, triple ? 3 : 1);
                        doubledQuote = verbatim || language == "SQL" || language == "PowerShell";
                        backtickEscape = language == "PowerShell";
                        i += stringEnd.Length;
                        ConsumeString(line, ref i);
                        TextTokenKind kind = language == "JSON" && NextNonSpace(line, i) == ':' ? TextTokenKind.Property : TextTokenKind.String;
                        Add(spans, start, i, kind);
                        bool multiline = triple || verbatim || quote == '`' || language == "SQL" || language == "Shell" || language == "PowerShell" || markup;
                        if (!multiline) stringEnd = null;
                        continue;
                    }
                    if (Char.IsDigit(c) || (c == '.' && i + 1 < line.Length && Char.IsDigit(line[i + 1])) ||
                        (language == "CSS" && c == '#' && i + 1 < line.Length && IsHex(line[i + 1])))
                    {
                        i++;
                        while (i < line.Length && (Char.IsLetterOrDigit(line[i]) || line[i] == '.' || line[i] == '_' ||
                            ((line[i] == '+' || line[i] == '-') && (line[i - 1] == 'e' || line[i - 1] == 'E')))) i++;
                        Add(spans, start, i, TextTokenKind.Number); continue;
                    }
                    if (IsIdentifierStart(c) || (markup && (c == ':' || c == '-')))
                    {
                        i++;
                        while (i < line.Length && (IsIdentifierPart(line[i]) ||
                            ((markup || language == "CSS" || language == "PowerShell" || language == "YAML" || language == "INI") && line[i] == '-') ||
                            ((markup || language == "PowerShell") && line[i] == ':'))) i++;
                        string word = line.Substring(start, i - start);
                        char next = NextNonSpace(line, i);
                        TextTokenKind kind = TextTokenKind.Plain;
                        if (markup) { kind = tagName ? TextTokenKind.Type : TextTokenKind.Property; tagName = false; }
                        else if (keywords.Contains(word)) kind = TextTokenKind.Keyword;
                        else if (types.Contains(word)) kind = TextTokenKind.Type;
                        else if ((language == "CSS" || language == "YAML" || language == "INI") && (next == ':' || next == '=' || word.EndsWith(":", StringComparison.Ordinal))) kind = TextTokenKind.Property;
                        else if (next == '(') kind = TextTokenKind.Function;
                        else if (c == '$') kind = TextTokenKind.Property;
                        else if (Char.IsUpper(c) && language != "JSON" && language != "YAML" && language != "INI") kind = TextTokenKind.Type;
                        Add(spans, start, i, kind); continue;
                    }
                    i++;
                    if ("+-*/%=!<>?:&|^~.,;(){}[]@\\".IndexOf(c) >= 0) Add(spans, start, i, TextTokenKind.Operator);
                }
                return spans.ToArray();
            }

            private void ConsumeString(string line, ref int i)
            {
                while (i < line.Length)
                {
                    if (At(line, i, stringEnd))
                    {
                        i += stringEnd.Length;
                        if (doubledQuote && At(line, i, stringEnd)) { i += stringEnd.Length; continue; }
                        stringEnd = null; return;
                    }
                    if ((line[i] == '\\' && !doubledQuote && !backtickEscape) || (line[i] == '`' && backtickEscape))
                        i = Math.Min(line.Length, i + 2);
                    else i++;
                }
            }

            private void ScanMarkdown(string line, List<TextSpan> spans)
            {
                int first = 0;
                while (first < line.Length && Char.IsWhiteSpace(line[first])) first++;
                if (first + 2 < line.Length && ((line[first] == '`' && At(line, first, "```")) || (line[first] == '~' && At(line, first, "~~~"))))
                {
                    if (markdownFence == '\0') markdownFence = line[first];
                    else if (markdownFence == line[first]) markdownFence = '\0';
                    Add(spans, first, line.Length, TextTokenKind.Preprocessor); return;
                }
                if (markdownFence != '\0') { Add(spans, 0, line.Length, TextTokenKind.String); return; }
                if (first < line.Length && line[first] == '#') { Add(spans, first, line.Length, TextTokenKind.Keyword); return; }
                if (first < line.Length && line[first] == '>') { Add(spans, first, line.Length, TextTokenKind.Comment); return; }
                int i = 0;
                while (i < line.Length)
                {
                    char c = line[i];
                    if (c == '`' || c == '*' || c == '_')
                    {
                        int end = line.IndexOf(c, i + 1);
                        if (end >= 0) { Add(spans, i, end + 1, c == '`' ? TextTokenKind.String : TextTokenKind.Keyword); i = end + 1; continue; }
                    }
                    if (c == '[')
                    {
                        int end = line.IndexOf(']', i + 1);
                        if (end >= 0) { Add(spans, i, end + 1, TextTokenKind.Property); i = end + 1; continue; }
                    }
                    i++;
                }
            }

            private static void Add(List<TextSpan> spans, int start, int end, TextTokenKind kind)
            {
                if (kind == TextTokenKind.Plain || start >= MaximumColumns) return;
                end = Math.Min(end, MaximumColumns);
                if (end > start) spans.Add(new TextSpan { Start = start, Length = end - start, Kind = kind });
            }

            private static bool At(string text, int index, string value)
            { return index + value.Length <= text.Length && String.CompareOrdinal(text, index, value, 0, value.Length) == 0; }
            private static bool WordAt(string text, int index, string word)
            { return index + word.Length <= text.Length && String.Compare(text, index, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) == 0 && (index + word.Length == text.Length || Char.IsWhiteSpace(text[index + word.Length])); }
            private static char NextNonSpace(string text, int index)
            { while (index < text.Length && Char.IsWhiteSpace(text[index])) index++; return index < text.Length ? text[index] : '\0'; }
            private static bool IsIdentifierStart(char c) { return Char.IsLetter(c) || c == '_' || c == '$'; }
            private static bool IsIdentifierPart(char c) { return Char.IsLetterOrDigit(c) || c == '_' || c == '$'; }
            private static bool IsHex(char c) { return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'); }

            private static string KeywordList(string language)
            {
                switch (language)
                {
                    case "Python": return "False None True and as assert async await break class continue def del elif else except finally for from global if import in is lambda nonlocal not or pass raise return try while with yield match case self";
                    case "PowerShell": return "begin break catch class continue data define do dynamicparam else elseif end enum exit filter finally for foreach from function if in param process return switch throw trap try until using var while workflow parallel sequence $true $false $null";
                    case "Shell": return "if then else elif fi for while until in do done case esac function select time export local readonly return break continue source alias unset true false";
                    case "SQL": return "select from where join inner outer left right full on as union all distinct group by order having limit offset asc desc insert into values update set delete create alter drop table database index view primary foreign key references null not and or in is like between exists case when then else end begin commit rollback with over partition true false";
                    case "JSON": return "true false null";
                    case "YAML": case "INI": return "true false null yes no on off";
                    case "Dockerfile": return "FROM RUN CMD LABEL EXPOSE ENV ADD COPY ENTRYPOINT VOLUME USER WORKDIR ARG ONBUILD STOPSIGNAL HEALTHCHECK SHELL AS";
                    case "Makefile": return "include define endef ifdef ifndef ifeq ifneq else endif export unexport override private vpath";
                    case "CMake": return "cmake_minimum_required project set unset option add_executable add_library target_link_libraries target_include_directories find_package include if elseif else endif foreach endforeach function endfunction macro endmacro return message install";
                    case "Batch": return "echo off on if else for in do set call goto exit exist not defined errorlevel shift cd pushd popd copy move del mkdir rmdir start pause";
                    case "Ruby": return "alias and begin break case class def defined do else elsif end ensure false for if in module next nil not or redo rescue retry return self super then true undef unless until when while yield require include";
                    case "CSS": return "important inherit initial unset revert none auto block flex grid inline absolute relative fixed sticky solid transparent";
                    default: return "abstract as async await base break case catch checked class const constructor continue debugger default delegate delete do dynamic else enum event explicit export extends extern false final finally fixed fn for foreach friend from function get goto if impl implements implicit import in inline instanceof interface internal is let lock match module namespace native new noexcept null nullptr operator out override package params partial private protected public readonly record ref register require return sealed set sizeof stackalloc static struct super switch synchronized template this throw throws trait true try type typedef typeof unchecked union unsafe use using virtual volatile when where while with yield";
                }
            }
        }
    }
}
