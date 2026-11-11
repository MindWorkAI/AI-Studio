# Banner

`generate_banner.py` creates `app/MindWork AI Studio/wwwroot/svg/banner.svg`, the banner on the start page of the app and at the top of the repository's README.

The app shows the banner through an `<img>` element, where neither the app's CSS nor its web fonts apply. Therefore, the script converts every text into outlines, so the banner looks the same on Windows, macOS, Linux, and GitHub. The text uses [Inter](https://github.com/rsms/inter) (SIL Open Font License 1.1). The icons are the [Lucide](https://lucide.dev) icons the app itself uses (ISC License, with parts of Feather under the MIT License), read from the Lucide font which the app ships, `app/MindWork AI Studio/wwwroot/fonts/lucide.woff2`, with the code points in `app/SourceGeneratedMappings/Lucide/codepoints.json`. The banner therefore always shows the icons in the version of the app.

## Setup

You need Python 3. The script needs no build of the app: it reads the Lucide font and its code points straight from the repository.

In this folder, create a virtual environment and install the packages:

```bash
python3 -m venv .venv
.venv/bin/pip install -r requirements.txt
```

Then download the font. The script expects this exact version of Inter and warns about any other one, because a different version changes the outlines:

```bash
curl -L -o Inter.ttf "https://raw.githubusercontent.com/google/fonts/e1d6480102fed30739fead0faee463101f892c8f/ofl/inter/Inter%5Bopsz,wght%5D.ttf"
```

Neither the font nor the virtual environment belongs in the repository; `.gitignore` keeps both out.

On Windows, use `.venv\Scripts\pip` and `.venv\Scripts\python` instead.

## Usage

```bash
.venv/bin/python generate_banner.py
```

The script overwrites the banner in the app. To write it somewhere else, for example to compare two versions, pass `--output <file>`. Without any change to the script, the output is identical to the committed banner.

On macOS, `qlmanage -t -s 1920 -o <folder> <file>` renders a preview with WebKit, the engine of the app on macOS. Any browser shows the file as well.

## Changing the banner

The content sits at the top of the script:

- `EYEBROW`, `NAME`, and `TAGLINE` form the wordmark on the left.
- `CHIPS` lists the eight capabilities on the right: two rows of four, where the chips of one column share their width. Pair labels of similar length in a column. The icon is the name of a Lucide icon as [lucide.dev](https://lucide.dev/icons/) shows it, such as `message-square-text`, ideally the one the app uses for that feature in `app/MindWork AI Studio/Tools/AppIcons.cs`. When a column would show the same icon twice, pick another one for one of them.
- `LAYOUT` holds positions and sizes, `CREAM` the color of text and icons.

Keep the labels in US English and short. Check the result in a narrow window as well: at 700 pixels, the labels are only about 10 pixels high. On wide windows, the start page crops up to 32 units at the top and the bottom, so text and chips must stay between y = 48 and y = 272.