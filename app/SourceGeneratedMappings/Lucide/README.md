# Lucide

AI Studio draws its icons with [Lucide](https://lucide.dev), taken from the npm package [`lucide-static`](https://www.npmjs.com/package/lucide-static), version **1.71.0**. The app uses the icon font of that package, not single SVG files: one font holds every icon, and MudBlazor shows a font glyph wherever it accepts an icon.

## Files

| File | Taken from the package | Purpose |
|---|---|---|
| `app/SourceGeneratedMappings/Lucide/codepoints.json` | `font/codepoints.json` | Maps the name of every icon, aliases included, to its code point in the font. |
| `app/MindWork AI Studio/wwwroot/fonts/lucide.woff2` | `font/lucide.woff2` | The font the app ships, declared in `wwwroot/app.css`. |
| `app/MindWork AI Studio/wwwroot/fonts/LICENSE-lucide.txt` | `LICENSE` | The license, which has to travel with every copy of the font. |

All three files are copies without any change. The code points belong to one release of the font, so always replace the three files together.

## Using the icons

While the app is built, `LucideIconGenerator` turns `codepoints.json` into two classes in the namespace `AIStudio.Tools`:

- `LucideIcons` holds a constant for every name, such as `LucideIcons.HOUSE`. Its value is `lucide/` followed by the glyph of the icon: MudBlazor renders such a string as a span with the class `lucide` and the glyph as its text, and `wwwroot/app.css` applies the font to that class.
- `LucideIconRegistry` finds an icon by the name lucide.dev shows for it, such as `house`. Assistant plugins name their icons this way.

Besides the current names, `codepoints.json` holds aliases and names Lucide has deprecated, such as `home` for `house` or `trash-2` for `trash`, so that a plugin written with an older name keeps working. In the app itself, always pick the current name as lucide.dev shows it.

The app takes its icons only from `AppIcons` in `app/MindWork AI Studio/Tools/AppIcons.cs`. It names every icon after its meaning in AI Studio, such as `AppIcons.DELETE`, and decides there which Lucide icon stands for it. To use an icon, take the constant of its meaning from `AppIcons`; when there is none yet, add one there. Never use `LucideIcons` or the icons of MudBlazor anywhere else.

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
5. A few places show Lucide icons as images, where no web font works, and hold copies of their SVG files from `package/icons/`. Check whether these icons changed in the new version, and take them anew if so:
   - `cpu-ai` and `server` in `wwwroot/images/provider-icons/`, see the README there,
   - `blocks` as the default icon of plugins, `DEFAULT_ICON_SVG` in `Tools/PluginSystem/PluginBase.Icon.cs`,
   - `settings` in the `icon.lua` of `Plugins/assistants/` and `Plugins/configuration/`.
6. Generate the banner of the start page anew, since its icons come from the font, see `media/Banner/README.md`.