# Screenshot assets

These PNGs were rendered from Desktop Hover's real Windows Forms controls on 2026-10-23. They use the English interface, a 720 × 540 preview limit, a 0.45-second delay and a 65% playback-volume setting.

| File | Content |
| --- | --- |
| `settings-dark.png`, `settings-light.png` | The same settings in both themes. |
| `code-dark.png`, `code-light.png` | An original `hello.cpp` sample in the code preview. |
| `image-preview.png` | An original, programmatically drawn landscape opened by the image preview. |
| `audio-preview.png` | A silent WAV sample opened by the real audio player. |
| `tray-menu.png` | The application's quick settings menu. |

They contain demonstration data, not personal files or desktop captures. The application UI was not redesigned for these images. No video screenshot is included; the documentation describes video support separately.

On 2026-10-24, four document previews were rendered from the updated application with the same 720 × 540 limit:

| File | Content |
| --- | --- |
| `photoshop-preview.png` | An original design board saved as a valid RGB PSD with a raw merged image. |
| `word-preview.png` | A generated DOCX project brief, headings and table, rendered as a simplified excerpt. |
| `excel-preview.png` | A generated XLSX project budget using demonstration values. |
| `powerpoint-preview.png` | The first slide of a generated PPTX with text and an embedded design-board image. |

The Office samples were generated with python-docx, openpyxl and python-pptx; they are test fixtures, not user documents. The PSD sample was written according to its file structure. Previews were captured with the real `PreviewWindow.DrawToBitmap` after asynchronous loading completed. Fixture generators and test programs remain outside the repository.

On 2026-10-26, `powerpoint-preview.png` was replaced with a native PowerPoint preview of the original styled fixture in `tests/fixtures`. It shows Georgia italic text, a rotated rounded shape and custom colors. The screenshot uses the real preview window at the same 720 × 540 limit; the native-render regression tests now live in `tests`.

The application icon and README logo live in the separate [assets folder](../../assets). This folder contains documentation screenshots only.
