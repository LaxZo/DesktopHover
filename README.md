<p align="center">
  <img src="icon-preview.png" width="88" alt="Desktop Hover icon">
</p>

<h1 align="center">Desktop Hover</h1>

<p align="center"><strong>A closer look. Just a hover away.</strong></p>
<p align="center">Preview images, documents and design projects, play media, and read code without opening another app.</p>

<p align="center">
  <strong>Windows x64</strong> &nbsp;·&nbsp; Desktop &amp; File Explorer &nbsp;·&nbsp; Dark &amp; light themes &nbsp;·&nbsp; 12 languages
</p>

<p align="center">
  <a href="#get-started">Get started</a> &nbsp;·&nbsp;
  <a href="#screenshots">Screenshots</a> &nbsp;·&nbsp;
  <a href="#supported-files">Supported files</a> &nbsp;·&nbsp;
  <a href="README.lv.md">Latviski</a>
</p>

---

Desktop Hover is a Windows tray app that previews a local file when you rest the pointer over it on the desktop or in File Explorer. See an image, listen to an audio clip, watch a video, or inspect a source file. Move away and the preview closes; media playback stops with it.

<p align="center">
  <img src="docs/assets/image-preview.png" width="720" alt="Desktop Hover showing a sample landscape image with its filename, dimensions and file size">
</p>

## What you can do

| Feature | What it does |
| --- | --- |
| **See images at a glance** | A compact preview with the filename, dimensions and file size. Original proportions are preserved. |
| **Listen and watch** | Audio plays on hover. Video plays inside the preview with sound. One volume setting controls both. |
| **Read text and code** | Line numbers, a monospace font and syntax colors for common languages, scripts and configuration files. |
| **Look inside documents and projects** | Word excerpts, Excel worksheets, PowerPoint slides and Photoshop images. Other Adobe projects use saved previews or Windows thumbnails when available. |
| **Make it your own** | Dark or light appearance, 12 interface languages, adjustable preview size, hover delay and optional startup with Windows. |

## Screenshots

### Documents and design projects

These are real hover previews of demonstration files. Word, Excel and PowerPoint content is simplified when a usable saved preview is absent; the footer identifies the preview type.

<table>
  <tr><th>Photoshop</th><th>Word</th></tr>
  <tr>
    <td><img src="docs/assets/photoshop-preview.png" width="360" alt="PSD merged image preview"></td>
    <td><img src="docs/assets/word-preview.png" width="360" alt="DOCX excerpt with headings and a table"></td>
  </tr>
  <tr><th>Excel</th><th>PowerPoint</th></tr>
  <tr>
    <td><img src="docs/assets/excel-preview.png" width="360" alt="XLSX first worksheet with cached values"></td>
    <td><img src="docs/assets/powerpoint-preview.png" width="360" alt="PPTX first slide with text and an embedded image"></td>
  </tr>
</table>

### Code, without launching an editor

The preview highlights keywords, strings, comments, numbers and other tokens. Its colors follow your chosen theme.

![C++ code preview in the dark theme](docs/assets/code-dark.png)

<details>
<summary>View the same code in the light theme</summary>

![C++ code preview in the light theme](docs/assets/code-light.png)

</details>

### One settings window. Two themes.

Change the appearance with the sun/moon button beside the language selector. Settings save automatically and take effect immediately.

<table>
  <tr><th>Dark theme</th><th>Light theme</th></tr>
  <tr>
    <td><img src="docs/assets/settings-dark.png" width="360" alt="Dark settings window showing preview size, delay, volume and startup controls"></td>
    <td><img src="docs/assets/settings-light.png" width="360" alt="The same settings window in the light theme"></td>
  </tr>
</table>

<details>
<summary><strong>See the audio preview and tray menu</strong></summary>

| Audio preview | Quick settings |
| :---: | :---: |
| ![Audio preview with playback information](docs/assets/audio-preview.png) | ![Tray menu with preview, size, delay, volume and theme options](docs/assets/tray-menu.png) |

</details>

*Screenshots are rendered from the application with sample files. The landscape illustration is a documentation sample.*

## Get started

### Run it without installing

1. Open [DesktopHover.exe](DesktopHover.exe) in this repository and use **Download raw file**, or download and extract the repository ZIP.
2. Put `DesktopHover.exe` in a folder you want to keep and run it.
3. Find its icon in the Windows system tray. It may be inside the **Show hidden icons** menu.
4. Hover over a supported local file on the desktop or in File Explorer.

**Requirements:** 64-bit Windows, the desktop .NET Framework 4.x runtime with Windows Forms/WPF, and Windows codecs for the media you want to preview. The build script uses the system Framework64 installation. macOS, Linux and third-party file managers are not supported.

### Optional per-user installation

Download or clone the whole repository, open PowerShell in its folder, then run:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install.ps1 -OpenSettings
```

The script copies the app and documentation to `%LOCALAPPDATA%\DesktopHover` and opens Settings. **A first installation enables startup with Windows.** You can turn it off in Settings. Running the script again updates the installed copy while keeping your preferences.

## Everyday controls

| Action | Result |
| --- | --- |
| Hover over a supported file | Show its preview after the selected delay. |
| Move off the file | Close the preview and stop playback. |
| Left-click the tray icon | Open Settings in the center of the pointer's monitor. |
| Right-click the tray icon | Open quick settings for previews, size, delay, volume, theme and startup. |
| Click **−** in Settings | Minimize Settings to the Windows taskbar. Click its taskbar button to restore it. |
| Click **×** in Settings | Close the settings window and keep Desktop Hover running in the system tray. |
| Choose **Quit app** in the tray menu | Quit Desktop Hover completely. |
| Switch off **Previews enabled** | Pause previews without closing the app. |

### Available settings

| Setting | Options |
| --- | --- |
| Preview size | 480 × 360, 720 × 540, 1080 × 810 or 1440 × 1080; a slider adjusts the maximum size. Content keeps its aspect ratio. |
| Hover delay | 0, 0.25, 0.45, 0.75 or 1.00 seconds. Zero removes the added wait; file detection and loading still take time. |
| Playback volume | 0–100% for audio and video. Set 0% to mute. |
| Appearance | Dark or light, including menus and code previews. |
| Enlarge small images | Allow an image smaller than the preview limit to be enlarged. |
| Start with Windows | Run automatically after you sign in. |

**Interface languages:** English · Latviešu · Русский · 简体中文 · Deutsch · Español · Français · Português (Brasil) · Italiano · 日本語 · 한국어 · Українська.

Preferences are stored in `%LOCALAPPDATA%\DesktopHover\settings.ini`. Startup is stored separately in the current user's Windows Run registry key.

## Supported files

| Kind | Recognized formats and examples |
| --- | --- |
| **Images** | JPG/JPEG/JFIF, PNG, BMP, GIF, TIFF, ICO, WebP, HEIC/HEIF, AVIF. |
| **Audio** | MP3, WAV, WMA, M4A, AAC, FLAC, AIF/AIFF. |
| **Video** | MP4/M4V, MOV, AVI, WMV, MPG/MPEG, MKV, WebM. |
| **Text & data** | TXT, LOG, CSV/TSV, Markdown, JSON, YAML, INI, TOML and other configuration files. |
| **Source code** | C/C++, C#, JavaScript/TypeScript, Python, Java, Go, Rust, Kotlin, Swift, PHP, Ruby, SQL, HTML/XML/CSS and related formats. |
| **Scripts & project files** | PowerShell, shell and batch scripts; Dockerfile, Makefile, CMakeLists.txt, README, LICENSE and common dotfiles. |
| **Photoshop** | PSD/PSB saved thumbnails, or merged 8-bit RGB/grayscale images with raw or RLE compression. |
| **Word, Excel & PowerPoint** | DOCX, XLSX, PPTX and their macro-enabled/template variants; PPSX/PPSM presentations. Saved preview first, then a simplified excerpt, first visible worksheet or first slide. |
| **Other Adobe projects** | AI, EPS, INDD/INDT, IDML, XD, AEP/AEPX, PRPROJ and FLA **when a supported embedded preview or Windows thumbnail is available**. |
| **Other documents** | Legacy DOC/XLS/PPT and templates, XLSB and PDF depend on a Windows thumbnail provider. ODT/ODS/ODP can also use their saved PNG thumbnail. |

File extensions identify preview candidates; **actual image decoding and media playback depend on the Windows codecs installed on your machine**. See [MediaFiles.cs](MediaFiles.cs), [TextPreviewContent.cs](TextPreviewContent.cs) and [DocumentPreviewContent.cs](DocumentPreviewContent.cs) for the full extension lists.

Document previews load in the background and are canceled when you move away. The built-in readers work without Office or Adobe applications. Windows thumbnail support depends on the handlers installed on your computer; if no usable preview exists, the app shows an explanation.

### Current limits

- Previews work with **locally available files**. Network shares, mapped network drives and online-only cloud files are skipped.
- Text previews read up to **256 KiB**, **300 lines** and **500 characters per line**. Only the beginning that fits in the preview is visible; shortened previews are marked in the footer.
- Syntax coloring is a basic lexer, not a full language service. Code is displayed without being executed or edited.
- Markdown, HTML and SVG are shown as source text, rather than rendered documents.
- Document/project previews are limited to **512 MiB** per file. Office ZIP packages also have limits on entries, XML and embedded images; encrypted, malformed or oversized content may be unavailable.
- Office content previews are excerpts, not exact print layouts. Complex formatting, charts, slide masters, animations and embedded objects are not fully rendered. Excel shows cached values or formula text; formulas and macros are never run, and external links are not loaded. Only the first visible worksheet's beginning (up to 8 columns/30 rows) and the first slide are considered.
- Photoshop merged-image decoding is limited to **40 million pixels**, 8-bit RGB/grayscale and raw/RLE compression. Other color modes, depths and ZIP compression require a saved or Windows thumbnail. Layers are not reconstructed and color profiles are not applied. Other Adobe files use supported XMP/ZIP previews or Windows thumbnails; full project rendering is not provided.
- Saved previews and Windows thumbnails can reflect an earlier saved state. Their resolution depends on the source; saving the project with its preview enabled can help.
- Animated or multi-frame images show a still frame. Images over **100 MiB** or **100 million pixels** are skipped.
- Previews are passive: they do not take keyboard focus or provide an editor or interactive playback controls.

## Build from source

The app uses C#, Windows Forms for its interface, WPF for media playback and image decoding, and Windows Shell/UI Automation to identify hovered files. It has no NuGet package restore step or `.sln`/`.csproj` build entry point.

```powershell
git clone https://github.com/LaxZo/DesktopHover.git
cd DesktopHover
powershell -ExecutionPolicy Bypass -File .\build.ps1
.\DesktopHover.exe --settings
```

`build.ps1` invokes the 64-bit .NET Framework C# compiler:

```text
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
```

Stop a running copy before replacing its executable, or build to a separate location with `-OutputPath`.

<details>
<summary><strong>Source map and useful launch options</strong></summary>

| File | Responsibility |
| --- | --- |
| [Program.cs](Program.cs) | App lifecycle, tray icon, hover timing and startup registration. |
| [ShellResolver.cs](ShellResolver.cs) | Resolve the file beneath the pointer in Explorer or on the desktop. |
| [PreviewWindow.cs](PreviewWindow.cs) | Preview layout, rendering and content switching. |
| [MediaPlayback.cs](MediaPlayback.cs) | Audio/video playback using Windows media support. |
| [TextPreviewContent.cs](TextPreviewContent.cs) | Bounded text loading, encoding detection and syntax tokens. |
| [DocumentPreviewContent.cs](DocumentPreviewContent.cs), [DocumentArchiveGuard.cs](DocumentArchiveGuard.cs) | Document routing, limits and bounded ZIP validation. |
| [OfficePreviewReader.cs](OfficePreviewReader.cs), [AdobePreviewReader.cs](AdobePreviewReader.cs) | Local document content and embedded project-image readers. |
| [ShellThumbnailProvider.cs](ShellThumbnailProvider.cs) | Windows thumbnail fallback in a separate helper with a timeout. |
| [SettingsWindow.cs](SettingsWindow.cs), [TrayMenu.cs](TrayMenu.cs) | Settings window and quick menu. |
| [Brand.cs](Brand.cs), [Localization.cs](Localization.cs) | Theme palettes, icons and translations. |
| [AppSettings.cs](AppSettings.cs) | Preference validation and persistence. |

```powershell
.\DesktopHover.exe --settings
.\DesktopHover.exe --enable-startup
.\DesktopHover.exe --disable-startup
```

The startup options register or remove the path of the executable you run. Use the installed copy if that is the one you normally use. Starting a second copy opens the existing app's Settings window.

</details>

## Troubleshooting

| Problem | What to check |
| --- | --- |
| Nothing happens when hovering | Confirm the app is running, previews are enabled, and the file is local and supported. Use the Windows desktop or File Explorer. |
| A cloud file does not preview | Make it available offline first. Hovering does not download online-only files. |
| Audio or video will not play | Check the volume and the file's codec support in Windows. A recognized extension alone does not guarantee decoding. |
| Text is cut off | Large files and long lines are intentionally shortened. Open the file in an editor to read the rest. |
| An Adobe or older Office file has no preview | Save it with an embedded preview, or use a compatible Windows thumbnail handler. Recognizing the extension does not guarantee content can be decoded. |
| A document looks different from the editor | The footer marks simplified content. Open the original application for complete formatting, all pages, worksheets and slides. |
| Settings is minimized | Restore it from the Windows taskbar or left-click the tray icon. |
| The app still runs after clicking **×** | This closes Settings and keeps previews running. Right-click the tray icon and choose **Quit app** to quit completely. |

To remove an installed copy, switch off **Start with Windows**, quit the app, and delete `%LOCALAPPDATA%\DesktopHover`. Deleting that folder also removes its saved preferences.

## Feedback and contributions

[Report a bug or suggest a feature](https://github.com/LaxZo/DesktopHover/issues). For a preview issue, include your Windows version, file type, selected settings and steps to reproduce it. A small non-sensitive sample file helps.

Contributions are welcome. Describe the behavior you changed, build with `build.ps1`, and include screenshots for interface changes.

**License:** a license has not yet been specified in this repository.
