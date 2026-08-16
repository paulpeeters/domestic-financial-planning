from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
WEBROOT = ROOT / "FinancialPlanningApp.Web" / "wwwroot"
IMG_DIR = WEBROOT / "img"


def rounded_rectangle(draw, xy, radius, fill, outline=None, width=1):
    draw.rounded_rectangle(xy, radius=radius, fill=fill, outline=outline, width=width)


def make_icon(size: int) -> Image.Image:
    scale = size / 512
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    def p(value: float) -> int:
        return round(value * scale)

    # Main tile.
    rounded_rectangle(
        draw,
        (p(40), p(40), p(472), p(472)),
        p(104),
        fill=(23, 96, 118, 255),
    )

    # Soft lower accent.
    draw.pieslice((p(168), p(238), p(584), p(654)), 180, 270, fill=(37, 154, 127, 255))

    # Calendar sheet.
    rounded_rectangle(
        draw,
        (p(112), p(120), p(400), p(386)),
        p(34),
        fill=(246, 252, 252, 255),
    )
    draw.rectangle((p(112), p(120), p(400), p(188)), fill=(216, 239, 235, 255))
    rounded_rectangle(
        draw,
        (p(112), p(120), p(400), p(386)),
        p(34),
        fill=None,
        outline=(13, 72, 92, 120),
        width=max(1, p(4)),
    )

    # Binding rings.
    for x in (178, 334):
        draw.line((p(x), p(94), p(x), p(150)), fill=(255, 255, 255, 255), width=max(1, p(18)))
        draw.line((p(x), p(108), p(x), p(150)), fill=(23, 96, 118, 255), width=max(1, p(6)))

    # Calendar grid.
    grid_color = (80, 125, 137, 150)
    for x in (186, 256, 326):
        draw.line((p(x), p(214), p(x), p(340)), fill=grid_color, width=max(1, p(6)))
    for y in (254, 300):
        draw.line((p(150), p(y), p(362), p(y)), fill=grid_color, width=max(1, p(6)))

    # Planned check mark.
    draw.line((p(164), p(332), p(218), p(278), p(286), p(330), p(352), p(244)), fill=(255, 255, 255, 255), width=max(1, p(18)), joint="curve")
    draw.line((p(164), p(332), p(218), p(278), p(286), p(330), p(352), p(244)), fill=(23, 96, 118, 255), width=max(1, p(9)), joint="curve")

    # Provision coin.
    draw.ellipse((p(314), p(310), p(438), p(434)), fill=(236, 184, 74, 255), outline=(153, 111, 36, 130), width=max(1, p(5)))
    draw.ellipse((p(342), p(338), p(410), p(406)), outline=(255, 244, 210, 230), width=max(1, p(8)))
    draw.line((p(376), p(334), p(376), p(410)), fill=(255, 244, 210, 230), width=max(1, p(7)))

    return img


def write_svg(path: Path) -> None:
    path.write_text(
        """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" role="img" aria-label="Domestic Financial Planning">
  <rect x="40" y="40" width="432" height="432" rx="104" fill="#176076"/>
  <path d="M168 446a208 208 0 0 0 208-208h208v416H168Z" fill="#259a7f"/>
  <rect x="112" y="120" width="288" height="266" rx="34" fill="#f6fcfc"/>
  <path d="M146 120h220a34 34 0 0 1 34 34v34H112v-34a34 34 0 0 1 34-34Z" fill="#d8efeb"/>
  <rect x="112" y="120" width="288" height="266" rx="34" fill="none" stroke="#0d485c" stroke-opacity=".47" stroke-width="4"/>
  <path d="M178 94v56M334 94v56" fill="none" stroke="#fff" stroke-width="18" stroke-linecap="round"/>
  <path d="M178 108v42M334 108v42" fill="none" stroke="#176076" stroke-width="6" stroke-linecap="round"/>
  <path d="M186 214v126M256 214v126M326 214v126M150 254h212M150 300h212" fill="none" stroke="#507d89" stroke-opacity=".59" stroke-width="6" stroke-linecap="round"/>
  <path d="m164 332 54-54 68 52 66-86" fill="none" stroke="#fff" stroke-width="18" stroke-linecap="round" stroke-linejoin="round"/>
  <path d="m164 332 54-54 68 52 66-86" fill="none" stroke="#176076" stroke-width="9" stroke-linecap="round" stroke-linejoin="round"/>
  <circle cx="376" cy="372" r="62" fill="#ecb84a" stroke="#996f24" stroke-opacity=".51" stroke-width="5"/>
  <circle cx="376" cy="372" r="34" fill="none" stroke="#fff4d2" stroke-opacity=".9" stroke-width="8"/>
  <path d="M376 334v76" fill="none" stroke="#fff4d2" stroke-opacity=".9" stroke-width="7" stroke-linecap="round"/>
</svg>
""",
        encoding="utf-8",
    )


def main() -> None:
    IMG_DIR.mkdir(parents=True, exist_ok=True)

    write_svg(IMG_DIR / "app-icon.svg")

    for size in (16, 32, 48, 64, 128, 180, 192, 256, 512):
        make_icon(size).save(IMG_DIR / f"app-icon-{size}.png")

    make_icon(256).save(IMG_DIR / "app-icon.png")

    ico_sizes = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
    make_icon(256).save(WEBROOT / "favicon.ico", sizes=ico_sizes)
    make_icon(256).save(WEBROOT / "app-icon.ico", sizes=ico_sizes)


if __name__ == "__main__":
    main()
