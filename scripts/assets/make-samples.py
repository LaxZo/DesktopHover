"""Create original Office fixtures for the README's real application previews."""
import sys
from pathlib import Path

from docx import Document
from docx.shared import RGBColor as WordColor
from openpyxl import Workbook
from openpyxl.styles import Font, PatternFill
from pptx import Presentation
from pptx.dml.color import RGBColor
from pptx.enum.shapes import MSO_SHAPE
from pptx.util import Inches, Pt

root = Path(sys.argv[1]).resolve()
fixtures = root / ".verification" / "readme-assets" / "fixtures"

document = Document()
document.core_properties.author = "Desktop Hover"
document.add_heading("Desktop Hover / Project brief", 0)
document.add_paragraph("A closer look. Just a hover away.")
document.add_heading("Overview", 1)
document.add_paragraph("Preview local images, documents, media and code without opening another application. A compact interface keeps the file in focus.")
document.add_heading("Visual direction", 1)
document.add_paragraph("Ink-black surfaces, burgundy details and rose accents. Clear typography, rounded corners and restrained motion.")
document.add_heading("Release checklist", 1)
table = document.add_table(rows=1, cols=3)
table.style = "Light Shading Accent 1"
for cell, text in zip(table.rows[0].cells, ["Area", "Deliverable", "Status"]):
    cell.text = text
for row in [("Identity", "Application icon", "Ready"), ("Interface", "Dark and light themes", "Ready"), ("Documentation", "Updated screenshots", "Ready")]:
    for cell, text in zip(table.add_row().cells, row):
        cell.text = text
for name in ("Title", "Heading 1", "Heading 2"):
    document.styles[name].font.color.rgb = WordColor.from_string("B6164B")
document.save(fixtures / "Project brief.docx")

workbook = Workbook()
sheet = workbook.active
sheet.title = "Launch budget"
rows = [
    ["Desktop Hover / Launch budget", "", "", ""],
    ["Workstream", "Hours", "Rate", "Budget"],
    ["Visual identity", 8, 40, 320],
    ["Interface design", 16, 40, 640],
    ["Preview playback", 12, 40, 480],
    ["Documentation", 6, 40, 240],
    ["Quality checks", 8, 40, 320],
    ["Total", 50, "", 2000],
    ["", "", "", ""],
    ["Demonstration values only", "", "", ""],
]
for row in rows:
    sheet.append(row)
for cell in sheet[2]:
    cell.fill = PatternFill("solid", fgColor="B6164B")
    cell.font = Font(color="FFFFFF", bold=True)
sheet.column_dimensions["A"].width = 34
for column in ("B", "C", "D"):
    sheet.column_dimensions[column].width = 14
workbook.properties.creator = "Desktop Hover"
workbook.save(fixtures / "Launch budget.xlsx")

presentation = Presentation()
presentation.slide_width, presentation.slide_height = Inches(13.333333), Inches(7.5)
presentation.core_properties.author = "Desktop Hover"
slide = presentation.slides.add_slide(presentation.slide_layouts[6])
slide.background.fill.solid()
slide.background.fill.fore_color.rgb = RGBColor.from_string("0C0A0C")


def text(value, x, y, width, height, size, color="E9E5E7", bold=False):
    frame = slide.shapes.add_textbox(Inches(x), Inches(y), Inches(width), Inches(height)).text_frame
    frame.margin_left = frame.margin_right = frame.margin_top = frame.margin_bottom = 0
    frame.word_wrap = True
    for i, line in enumerate(value.split("\n")):
        paragraph = frame.paragraphs[0] if i == 0 else frame.add_paragraph()
        paragraph.text = line
        paragraph.font.name = "Segoe UI"
        paragraph.font.size = Pt(size)
        paragraph.font.bold = bold
        paragraph.font.color.rgb = RGBColor.from_string(color)


def box(x, y, width, height, fill, outline=None):
    shape = slide.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE, Inches(x), Inches(y), Inches(width), Inches(height))
    shape.fill.solid()
    shape.fill.fore_color.rgb = RGBColor.from_string(fill)
    if outline:
        shape.line.color.rgb = RGBColor.from_string(outline)
    else:
        shape.line.fill.background()
    shape.adjustments[0] = 0.08


slide.shapes.add_picture(str(root / "assets" / "icon-preview.png"), Inches(.65), Inches(.55), Inches(.55), Inches(.55))
text("DESKTOP HOVER", 1.4, .68, 6, .4, 17, "A69BA1", True)
text("A closer look.", .68, 1.65, 8, 1.1, 50, bold=True)
text("Just a hover away.", .68, 2.65, 8.2, 1.1, 43, "E11F5E", True)
text("Preview local files without\nopening another application.", .72, 4.0, 7.1, 1.0, 22, "A69BA1")
for label, x, width in [("Images", .72, 1.45), ("Documents", 2.35, 2.0), ("Media & code", 4.55, 2.35)]:
    box(x, 5.55, width, .52, "31131F")
    text(label, x + .18, 5.65, width - .2, .28, 13, "E9E5E7")
box(8.8, 1.55, 3.82, 4.55, "150F13", "301D26")
slide.shapes.add_picture(str(fixtures / "Rose landscape.png"), Inches(8.97), Inches(1.73), width=Inches(3.48), height=Inches(2.175))
text("Rose landscape.png", 9.07, 4.15, 3.3, .48, 17, bold=True)
text("1280 × 800 px", 9.07, 4.72, 3.1, .4, 14, "A69BA1")
text("A PREVIEW, IN A MOMENT.", 9.07, 5.48, 3.1, .3, 11, "E11F5E", True)
text("WINDOWS  /  DARK & LIGHT  /  12 LANGUAGES", .72, 6.8, 12, .4, 12, "A69BA1")
presentation.save(fixtures / "Desktop Hover.pptx")
print("Created Word, Excel and PowerPoint demonstration files.")
