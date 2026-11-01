# Screenshot assets

All 11 screenshots were regenerated from Desktop Hover's actual Windows Forms controls on **2026-11-01**, using the black, burgundy and rose palette. They use English, a 720 × 540 preview limit, a 0.45-second delay and 65% playback volume. Interface animations are disabled for stable captures. The settings window has a 1000 × 780 content area at 100% scaling.

| File | Content |
| --- | --- |
| `settings-dark.png`, `settings-light.png` | Current settings in both themes, including the compact header, video looping, slide timing and file-type switches. |
| `code-dark.png`, `code-light.png` | An original `hello.cpp` sample in the real code preview. |
| `image-preview.png` | An original landscape illustration drawn in the new palette and opened by the image preview. |
| `photoshop-preview.png` | An original typography and palette board saved as a valid RGB PSD with a raw merged image. |
| `word-preview.png` | A generated DOCX project brief with headings and a table, shown as a simplified excerpt. |
| `excel-preview.png` | A generated XLSX launch budget with demonstration values. |
| `powerpoint-preview.png` | A generated PPTX with native text, shapes, the new logo and landscape sample. |
| `audio-preview.png` | A silent 30-second WAV sample opened by the real audio player. |
| `tray-menu.png` | The compact quick-settings menu, including keyboard shortcuts. |

These images contain demonstration data, not personal files or desktop captures. The UI is rendered with `DrawToBitmap` after real file loading completes; it is not a mockup. Document pages and syntax colors retain their normal application rendering. The logo and artwork are drawn from code, and the Office fixtures are generated with python-docx, openpyxl and python-pptx. No video screenshot is included.

## Regenerate

On Windows, with .NET Framework 4.8, Microsoft PowerPoint for native slide rendering, and Python containing `python-docx`, `openpyxl` and `python-pptx`:

```powershell
.\scripts\refresh-assets.ps1 -PythonPath 'C:\path\to\python.exe'
.\build.ps1
```

The generator lives in [scripts/assets](../../scripts/assets), and temporary sample files and its executable go into the ignored `.verification/readme-assets` folder. It does not save or change the user's application preferences.

To refresh only the tray-menu screenshot, run `.\scripts\refresh-assets.ps1 -TrayOnly`; this does not require Python or Office.

The application icon and README logo live in [assets](../../assets). Both are generated from `Brand.DrawLogo`, matching the in-app logo. `DesktopHover.ico` contains 16, 20, 24, 32, 48, 64, 128 and 256-pixel PNG frames; `icon-preview.png` is a transparent 512-pixel export. Rebuild the executable after regeneration to embed the updated icon.
