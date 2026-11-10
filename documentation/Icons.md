# Icons

AI Studio draws its icons with the Material icons MudBlazor ships, `Icons.Material.*`, and with a few icons of its own in `AppIcons`. This document explains which icon to take, how large it is drawn, and how to draw one of your own. The short version: take Material's icons as they are, draw your own on Material's grid, size every icon by the role it plays, and never adjust a single icon by eye.

## Why Icons Look Alike In Size

Material draws every icon on a canvas of 24 by 24 units, with 2 units of padding on each side. The main shape of an icon fills one of four keyline shapes:

| Keyline shape | Width × height | Material icons which fill it |
|---|---|---|
| Square | 18 × 18 | `Outlined.Save`, `Outlined.Edit`, `Outlined.OpenInNew` |
| Circle | 20 across | `Outlined.Info` |
| Upright rectangle | 16 × 20 | `Outlined.Description` |
| Wide rectangle | 20 × 16 | `Outlined.Folder`, `Outlined.Mail` |

A square is drawn smaller than a circle on purpose: a square fills more of its box, and at the same width it would look larger. The keylines are what make a folder, a floppy disk, and a pencil look equally large next to each other. Some icons follow their own drawing within the padding, such as the bin of `Outlined.Delete`. That is Material's design, too.

So an icon which looks a little off next to its neighbors is no reason to scale it where it is used. Shrinking it there thins its lines, breaks the balance Material built, and the next icon added beside it needs its own correction. If an icon really does not fit, take another one, or redraw it once on the grid. Some of Material's classic icons miss their own keyline: the chat bubble is a square with a tail, yet fills 20 × 20 units, the size of the circle, and so looks larger than a folder next to it. `AppIcons.CHAT_BUBBLE_OUTLINED` draws it on the square, with lines still 2 units wide, and every place uses it like any other icon.

## Which Icon

- **Material's icons, unchanged.** Look for the symbol in `Icons.Material` first.
- **Your own icon, on purpose.** When Material has no symbol for what you need, draws it in a way which does not suit, as with copying, where we show two squares instead of Material's rectangles, or draws it off its own grid, as with the chat bubble, draw one of your own, see "Drawing An Icon". A test keeps it on the grid, so its size is never an accident.
- **No icon carries a size of its own.** Material's icons and ours are drawn on the same grid, so every one of them takes the size of the place it is put in, see "How Large". Reusing an icon or a component in another place needs no resizing: the new place sizes it.
- **One meaning, one symbol.** The same action shows the same symbol everywhere in the app:

  | Symbol | Meaning |
  |---|---|
  | `Save` (floppy disk) | Store something inside AI Studio, such as a chat |
  | `FileDownload` (arrow onto a bar) | Write a file to a place the user chooses |
  | `IosShare` (box with an arrow) | Hand something to the share sheet of the operating system |

- **One style per area.** `Filled` is the default of the app. A part of the app may use `Outlined` throughout instead, as the workspace sidebar and its menus do. Within one row, toolbar, or menu, never mix the two.

## How Large

An icon takes its size from the role it plays, never from how it looks:

| Role | Size | Where it is set |
|---|---|---|
| Icon buttons, list items, and most other places | 24 px | MudBlazor's default, `Size.Medium` |
| Actions on an item of a compact list: the buttons of a row in the workspace sidebar and the entries of its menu | 20 px | `--icon-size-compact` in `wwwroot/app.css`; elsewhere MudBlazor's `Size.Small` |
| An icon button next to text | 16 px | the class `icon-button-compact` in `wwwroot/app.css` |
| An icon inside an avatar or a disc | 75 % of it | as MudBlazor sizes the fallback icon of an avatar |

In a compact list, the icon which names an item, such as a folder or a chat bubble, keeps 24 px, and the actions on it take 20 px. The same action thus shows the same icon at the same size in the row and in its menu. A list or menu which follows these rules sizes every icon placed in it by itself, so adding a button needs no size of its own.

An icon in a color which hardly shows on its background gets a darker shade of that color, not a highlight behind it: a filled shape behind an icon makes it look larger than its neighbors, whatever size the icon itself has. The magnifier of the search thus takes a darker yellow than the one behind found words, `--chat-search-sign` in `wwwroot/app.css`, and keeps the size of its place, as any icon there does.

A new role gets a rule of its own in one place, named after the role, so that every icon in it gets the same size.

## Drawing An Icon

Draw your own icon in `app/MindWork AI Studio/Tools/AppIcons.cs`:

1. **Start from Material's geometry.** Build the icon from parts of Material icons where you can, so that its corners, lines, and gaps look like theirs.
2. **Use the grid.** The outer bounds of the icon are exactly one keyline shape from the table above, centered on the canvas. Lines and borders are 2 units wide, and outer corners of outlined shapes have a radius of 2 units, as in Material's outlined icons.
3. **Draw with paths only.** The string holds `<path>` elements, without an `<svg>` root element, see the remarks of `AppIcons`.
4. **Say where it comes from.** The comment of the icon names the Material icons it is built from and the keyline shape it fills.

`AppIconsTests` measures the bounds of every icon in `AppIcons` and fails when they are not a keyline shape or not centered. Its message says how large the icon is, so you know which way to go.
