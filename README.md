# PROG2200 Assignment 2 - ASCII Lily Garden Screen Saver

A Windows Forms screen saver with four kinds of lily (spider, calla, tiger and lily of the valley) drawn entirely in ASCII.
Each one grows as a whole plant (seed in the soil, sprout, leaves, bud, bloom, wilt) while it bounces around the window.
The ASCII is generated the way TouchDesigner's ASCII effect works: each plant is a brightness field that is sampled on a grid
of character cells, and every cell shows the character from a brightness ramp (`" .:-=+*#%@"`) that matches it.
All lilies inherit from one abstract class and are processed polymorphically from an `ArrayList` on a timer.
The shapes and collision detection are in a class library.

## Quick Start

1. **Clone and build**:
   ```powershell
   git clone https://github.com/bakuncwa/prog2200_assign2.git
   cd prog2200_assign2
   start prog2200_assign2.sln      # Ctrl+Shift+B to build
   ```

2. **Run**: Press **F5** in Visual Studio. Two of each kind of lily are planted at start-up.

3. **Grow the garden**:
   - **Click** anywhere in the garden to plant a random lily at the mouse position
   - Or use **Garden → Add ...** (Ctrl+1 to Ctrl+4, Ctrl+R for random)
   - Resize the window or press **F11** for full screen (Esc to leave). The flowers stay inside the new size

## Architecture

| Component | Purpose |
|-----------|---------|
| **Flower.cs** | Abstract base: position, velocity, growth stages, wall bouncing, abstract `Draw` and `Size`, virtual `Move` |
| **AsciiLily.cs** | Abstract ASCII renderer: samples the plant on a character grid, maps brightness to the ramp, draws runs of same-coloured characters. Provides the shared soil, seed, swaying stem, leaves and bud; subclasses supply the flower (`SampleBloom`) |
| **AsciiSample.cs** | Brightness and colour of one point of a plant |
| **GrowthStage.cs** | Life cycle enum: Seed → Sprout → Bud → Bloom → Wilt (then reseeds) |
| **SpiderLily.cs** | Six thin curling petals and pollen-tipped stamens, coloured by a gradient that spins around the flower |
| **CallaLily.cs** | Irregular 13-point trumpet polygon (`GraphicsPath.AddPolygon` / `IsVisible`) filled with ASCII, bright edges, faint `DrawPolygon` trace, yellow spadix; changes colour on every wall bounce |
| **TigerLily.cs** | Six wide spotted petals with curled-back tips; switches character set at every growth stage |
| **LilyOfTheValley.cs** | A Windows Forms `Label` control whose text is the ASCII grid: an arched stem with hanging bells. Swaps colours on collision |
| **CollisionDetector.cs** | Circle-based collision detection and elastic bounce between every pair of flowers |
| **GardenPanel.cs** | Double-buffered `Panel` the flowers are drawn on (no flicker) |
| **MainForm.cs** | Timer, `ArrayList` of flowers, menus, mouse planting, resize handling, full screen |

## How It Works

- **Polymorphic Processing**: Each `Timer` tick loops over the `ArrayList`, explicitly casts each item `(Flower)item` and calls `Move`. `Paint` does the same with `Draw`. Which override runs is decided by each flower's real type at run time (late binding)
- **Explicit Casting**: New flowers are explicitly upcast `(Flower)new CallaLily(...)` and `(object)flower` when added to the list, and explicitly cast back to `Flower` whenever they are read
- **Growth**: Every lily goes through Seed → Sprout → Bud → Bloom → Wilt and starts again. The seed swells in the soil, the stem rises and leaves unfold, a bud swells and opens into the flower, then it droops and browns. The character grid gets more columns and rows as the plant grows, and the plant is shifted so its soil stays put and it grows upwards
- **ASCII rendering**: Each cell is sampled at 2 × 3 points and the average is mixed with the brightest point, so thin stems still show while petals are shaded smoothly
- **Wall Bouncing**: After moving, a flower past an edge is put back on that edge and its velocity is pointed away from the wall
- **Responds to Resizing**: The panel's current `ClientRectangle` is passed to `Move` every tick, and `Resize` pulls any flower that is now outside back in
- **Collision Detection**: Each flower is a circle around its centre. Overlapping pairs are pushed apart, and if they are moving towards each other the velocity parts along the line between their centres are swapped (elastic, equal mass)
- **Library Control**: `LilyOfTheValley` adds a real `Label` to the panel and moves it by setting `Location`; its `Draw` override does nothing because the control paints itself

## Visual State Changes

| # | Flower | Change | Trigger |
|---|--------|--------|---------|
| 1 | **TigerLily** | Character set changes: `" .o"` → `" .:;\|"` → `" .-~=oO"` → full ramp `" .:-=+*#%@"` → `" .,;:~"` | Growth stage |
| 2 | **SpiderLily** | Gradient angle turns 6° per tick, so the colour spins around the petals and stamens | Every tick |
| 3 | **CallaLily** | Next colour in a 6-colour palette | Wall bounce |
| 4 | **LilyOfTheValley** | Text and background colours swap for 12 ticks | Collision with another flower |

## Controls

| Action | Menu | Shortcut |
|--------|------|----------|
| Plant random lily at mouse | - | Left-click garden |
| Add Spider / Calla / Tiger Lily / Lily of the Valley | Garden → Add ... | Ctrl+1 / 2 / 3 / 4 |
| Add random lily | Garden → Add Random Lily | Ctrl+R |
| Pause / resume | Garden → Pause | Ctrl+P |
| Remove all lilies | Garden → Clear Garden | Ctrl+Delete |
| Full screen | View → Full Screen | F11 (Esc to leave) |

## Config

Animation settings live in `prog2200_assign2/App.config`. After building they are copied to
`bin\Debug\prog2200_assign2.exe.config`, so they can be changed there without recompiling:
```xml
<appSettings>
    <add key="TickIntervalMs" value="30" />
    <add key="StartingFlowersPerKind" value="2" />
</appSettings>
```
A missing or invalid value falls back to the default shown above.

## Testing

Run the program and watch a few life cycles (about 15 seconds each): every lily starts as a seed in the soil. Plant lilies near the edges and corners, drag the window
smaller and larger, and plant many lilies in a small window to see collisions. Calla lilies change colour at every wall and
lilies of the valley flash when they hit another flower.
