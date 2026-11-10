# Lucide

AI Studio draws its icons with [Lucide](https://lucide.dev), taken from the npm package [`lucide-static`](https://www.npmjs.com/package/lucide-static), version **1.71.0**. The app uses the icon font of that package, not single SVG files: one font holds every icon, and MudBlazor shows a font glyph wherever it accepts an icon.

## Files

| File | Taken from the package | Purpose |
|---|---|---|
| `app/SourceGeneratedMappings/Lucide/codepoints.json` | `font/codepoints.json` | Maps the name of every icon, aliases included, to its code point in the font. |
| `app/MindWork AI Studio/wwwroot/fonts/lucide.woff2` | `font/lucide.woff2` | The font the app ships, declared in `wwwroot/app.css`. |
| `app/MindWork AI Studio/wwwroot/fonts/LICENSE-lucide.txt` | `LICENSE` | The license, which has to travel with every copy of the font. |

All three files are copies without any change. The code points belong to one release of the font, so always replace the three files together.

## License

Lucide is licensed under the [ISC License](https://github.com/lucide-icons/lucide/blob/main/LICENSE). The icons Lucide derived from [Feather](https://github.com/feathericons/feather) are licensed under the MIT License; `LICENSE-lucide.txt` lists them.

## Updating

1. Download and unpack the package of the new version:

   ```bash
   curl -L -o lucide-static.tgz "https://registry.npmjs.org/lucide-static/-/lucide-static-<version>.tgz"
   tar -xzf lucide-static.tgz
   ```

2. Copy the three files from `package/` to the places in the table above.
3. Change the version at the top of this file.
4. Build the app and run the tests.