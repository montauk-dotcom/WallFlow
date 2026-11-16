import os, sys
from PIL import Image
from reportlab.lib.pagesizes import A4
from reportlab.lib.units import mm
from reportlab.lib import colors
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.enums import TA_LEFT, TA_CENTER
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (BaseDocTemplate, PageTemplate, Frame, Paragraph, Spacer, Image as RLImage,
                                KeepTogether, PageBreak, Table, TableStyle, NextPageTemplate, ListFlowable, ListItem)

sys.path.insert(0, os.path.dirname(__file__))
from content import RU, EN

FONT_DIR = "/usr/share/fonts/truetype/dejavu"
pdfmetrics.registerFont(TTFont("Body", f"{FONT_DIR}/DejaVuSans.ttf"))
pdfmetrics.registerFont(TTFont("Body-Bold", f"{FONT_DIR}/DejaVuSans-Bold.ttf"))
pdfmetrics.registerFontFamily("Body", normal="Body", bold="Body-Bold", italic="Body", boldItalic="Body-Bold")

ACCENT = colors.HexColor("#7C5CFF")
INK = colors.HexColor("#1B1D2A")
MUTED = colors.HexColor("#5D6278")
TIPBG = colors.HexColor("#F1EDFF")
DARK = colors.HexColor("#0D0F17")

# which part of each 1920x1080 screenshot to show
CROPS = {
    "03-folder": (0, 280, 900, 500),
    "04-todo-menu": (930, 520, 1240, 680),
    "05-tray": (1530, 720, 1920, 1080),
    "06-settings-widgets": (465, 132, 1455, 890),
    "07-settings-colors": (465, 132, 1455, 890),
    "08-settings-glass": (465, 132, 1455, 890),
    "09-color-picker": (784, 336, 1137, 692),
    "10-settings-desktop": (465, 132, 1455, 890),
    "11-settings-wallpapers": (465, 132, 1455, 890),
    "12-settings-general": (465, 132, 1455, 890),
}

# max picture height per screenshot (small menus don't need half a page)
MAX_H = {"04-todo-menu": 48, "05-tray": 72, "09-color-picker": 72, "03-folder": 70}
SETTINGS_H = 86

PAGE_W, PAGE_H = A4
MARGIN = 18 * mm
CONTENT_W = PAGE_W - 2 * MARGIN


def styles():
    return {
        "h1": ParagraphStyle("h1", fontName="Body-Bold", fontSize=17, leading=22, textColor=INK, spaceBefore=4, spaceAfter=8),
        "p": ParagraphStyle("p", fontName="Body", fontSize=10.3, leading=15, textColor=INK, spaceAfter=6),
        "li": ParagraphStyle("li", fontName="Body", fontSize=10.3, leading=14.5, textColor=INK),
        "tip": ParagraphStyle("tip", fontName="Body", fontSize=10, leading=14.5, textColor=INK),
        "cap": ParagraphStyle("cap", fontName="Body", fontSize=8.5, leading=11, textColor=MUTED, alignment=TA_CENTER, spaceBefore=3, spaceAfter=10),
        "toc": ParagraphStyle("toc", fontName="Body", fontSize=11, leading=19, textColor=INK),
        "toch": ParagraphStyle("toch", fontName="Body-Bold", fontSize=20, leading=26, textColor=INK, spaceAfter=12),
    }


def prepared_image(shots, name, tmp):
    src = os.path.join(shots, name + ".png")
    if not os.path.exists(src):
        return None
    im = Image.open(src).convert("RGB")
    if name in CROPS:
        im = im.crop(CROPS[name])
    out = os.path.join(tmp, f"{os.path.basename(shots)}-{name}.jpg")
    im.save(out, quality=88)
    return out, im.size


def figure(path_size, caption, st, max_h=98 * mm):
    path, (w, h) = path_size
    width = CONTENT_W
    height = width * h / w
    if height > max_h:
        height = max_h
        width = height * w / h
    img = RLImage(path, width=width, height=height)
    frame = Table([[img]], colWidths=[width + 2])
    frame.setStyle(TableStyle([
        ("BOX", (0, 0), (-1, -1), 0.6, colors.HexColor("#C9CBDA")),
        ("LEFTPADDING", (0, 0), (-1, -1), 1), ("RIGHTPADDING", (0, 0), (-1, -1), 1),
        ("TOPPADDING", (0, 0), (-1, -1), 1), ("BOTTOMPADDING", (0, 0), (-1, -1), 1),
    ]))
    return [frame, Paragraph(caption, st["cap"])]


def tip_box(text, st):
    t = Table([["", Paragraph(text, st["tip"])]], colWidths=[3, CONTENT_W - 3])
    t.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (0, 0), ACCENT),
        ("BACKGROUND", (1, 0), (1, 0), TIPBG),
        ("LEFTPADDING", (1, 0), (1, 0), 10), ("RIGHTPADDING", (1, 0), (1, 0), 10),
        ("TOPPADDING", (1, 0), (1, 0), 7), ("BOTTOMPADDING", (1, 0), (1, 0), 8),
        ("LEFTPADDING", (0, 0), (0, 0), 0), ("RIGHTPADDING", (0, 0), (0, 0), 0),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
    ]))
    return [Spacer(1, 2), t, Spacer(1, 8)]


def bullets(items, st):
    return [ListFlowable([ListItem(Paragraph(x, st["li"]), leftIndent=14, value="•") for x in items],
                         bulletType="bullet", start="•", bulletFontName="Body-Bold", bulletColor=ACCENT,
                         leftIndent=14, bulletFontSize=10, spaceAfter=6), Spacer(1, 4)]


def build(lang, data, shots, out_pdf, tmp):
    st = styles()

    def cover(c, doc):
        c.saveState()
        c.setFillColor(DARK)
        c.rect(0, 0, PAGE_W, PAGE_H, stroke=0, fill=1)
        # hero screenshot
        hero = prepared_image(shots, "01-desktop", tmp)
        if hero:
            w = PAGE_W - 2 * MARGIN
            h = w * hero[1][1] / hero[1][0]
            c.drawImage(hero[0], MARGIN, PAGE_H * 0.36 - h / 2 + 10 * mm, width=w, height=h)
        c.setFillColor(colors.white)
        c.setFont("Body-Bold", 44)
        c.drawString(MARGIN, PAGE_H - 52 * mm, data["title"])
        c.setFillColor(ACCENT)
        c.rect(MARGIN, PAGE_H - 59 * mm, 38 * mm, 2.2 * mm, stroke=0, fill=1)
        c.setFillColor(colors.HexColor("#E7E9F3"))
        c.setFont("Body", 20)
        c.drawString(MARGIN, PAGE_H - 72 * mm, data["subtitle"])
        p = Paragraph(data["lead"], ParagraphStyle("lead", fontName="Body", fontSize=12, leading=18, textColor=colors.HexColor("#B9BED3")))
        pw, ph = p.wrap(PAGE_W - 2 * MARGIN, 60 * mm)
        p.drawOn(c, MARGIN, PAGE_H - 82 * mm - ph)
        c.setFont("Body", 9)
        c.setFillColor(colors.HexColor("#8A90A8"))
        c.drawString(MARGIN, 14 * mm, "github.com/montauk-dotcom/WallFlow · Windows 10 / 11")
        c.restoreState()

    def page(c, doc):
        c.saveState()
        c.setStrokeColor(colors.HexColor("#E2E3EC"))
        c.setLineWidth(0.6)
        c.line(MARGIN, 13 * mm, PAGE_W - MARGIN, 13 * mm)
        c.setFont("Body", 8)
        c.setFillColor(MUTED)
        c.drawString(MARGIN, 8.5 * mm, data["footer"])
        c.drawRightString(PAGE_W - MARGIN, 8.5 * mm, str(doc.page))
        c.setFillColor(ACCENT)
        c.rect(MARGIN, PAGE_H - 11 * mm, 14 * mm, 1.2 * mm, stroke=0, fill=1)
        c.restoreState()

    doc = BaseDocTemplate(out_pdf, pagesize=A4, leftMargin=MARGIN, rightMargin=MARGIN, topMargin=17 * mm, bottomMargin=19 * mm,
                          title=f"Wallflow — {data['subtitle']}", author="Wallflow", lang=lang)
    frame = Frame(MARGIN, 19 * mm, CONTENT_W, PAGE_H - 36 * mm, id="f", leftPadding=0, rightPadding=0, topPadding=0, bottomPadding=0)
    doc.addPageTemplates([PageTemplate(id="cover", frames=[frame], onPage=cover),
                          PageTemplate(id="page", frames=[frame], onPage=page)])

    story = [NextPageTemplate("page"), PageBreak()]

    # contents
    story.append(Paragraph(data["toc"], st["toch"]))
    rows = []
    for i, (title, _, _) in enumerate(data["sections"], 1):
        rows.append([Paragraph(f'<font color="#7C5CFF"><b>{i:02d}</b></font>', st["toc"]), Paragraph(title, st["toc"])])
    toc = Table(rows, colWidths=[12 * mm, CONTENT_W - 12 * mm])
    toc.setStyle(TableStyle([("LINEBELOW", (0, 0), (-1, -1), 0.4, colors.HexColor("#E2E3EC")),
                             ("VALIGN", (0, 0), (-1, -1), "MIDDLE"), ("LEFTPADDING", (0, 0), (-1, -1), 0)]))
    story += [toc, PageBreak()]

    fig_no = 0
    for i, (title, blocks, shot) in enumerate(data["sections"], 1):
        head = Paragraph(f'<font color="#7C5CFF">{i:02d}</font>&nbsp;&nbsp;{title}', st["h1"])
        body = []
        for kind, val in blocks:
            if kind == "p":
                body.append(Paragraph(val, st["p"]))
            elif kind == "ul":
                body += bullets(val, st)
            elif kind == "tip":
                body += tip_box(val, st)
        fig = []
        if shot:
            ps = prepared_image(shots, shot, tmp)
            if ps:
                fig_no += 1
                mh = MAX_H.get(shot, SETTINGS_H if shot in CROPS else 98)
                fig = figure(ps, f'{data["fig"]} {fig_no}. {title}', st, max_h=mh * mm)
        # a section and its picture always stay on one page
        story.append(KeepTogether([head] + body + fig))
        story.append(Spacer(1, 8))

    doc.build(story)


if __name__ == "__main__":
    shots_root, out_dir = sys.argv[1], sys.argv[2]
    tmp = "/tmp/guide_tmp"
    os.makedirs(tmp, exist_ok=True)
    os.makedirs(out_dir, exist_ok=True)
    build("ru", RU, os.path.join(shots_root, "guide-ru"), os.path.join(out_dir, "Wallflow — инструкция (RU).pdf"), tmp)
    build("en", EN, os.path.join(shots_root, "guide-en"), os.path.join(out_dir, "Wallflow — user guide (EN).pdf"), tmp)
    print("ok")
