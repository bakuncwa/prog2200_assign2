# PROG2200 Assignment 2 - ASCII Garden Screen Saver

A Windows Forms screen saver with ASCII flowers that grow from seed to bloom while they bounce around the window.
All flowers inherit from one abstract class and are processed polymorphically from an `ArrayList` on a timer.
The shapes and collision detection are in a class library.

## Quick Start

1. **Clone and build**:
   ```powershell
   git clone https://github.com/bakuncwa/prog2200_assign2.git
   cd prog2200_assign2
   start prog2200_assign2.sln      # Ctrl+Shift+B to build
   ```

2. **Run**: Press **F5** in Visual Studio. Two of each kind of flower are planted at start-up.

3. **Grow the garden**:
   - **Click** anywhere in the garden to plant a random flower at the mouse position
   - Or use **Garden → Add ...** (Ctrl+1 to Ctrl+4, Ctrl+R for random)
   - Resize the window or press **F11** for full screen (Esc to leave). The flowers stay inside the new size

## Architecture

| Component | Purpose |
|-----------|---------|
| **Flower.cs** | Abstract base: position, velocity, growth stages, wall bouncing, abstract `Draw` and `Size`, virtual `Move` |
| **GrowthStage.cs** | Life cycle enum: Seed → Sprout → Bud → Bloom → Wilt (then reseeds) |
| **DaisyFlower.cs** | Ellipse head filled with a rotating `LinearGradientBrush`, ASCII glyph in the middle |
| **TulipFlower.cs** | Irregular 10-point polygon (`FillPolygon`) that changes colour on every wall bounce |
| **AsciiSprout.cs** | Multi-line ASCII art drawn with `TextRenderer`, with new art for each growth stage |
| **LabelFlower.cs** | A Windows Forms `Label` control showing an ASCII bouquet, which swaps colours on collision |
| **CollisionDetector.cs** | Circle-based collision detection and elastic bounce between every pair of flowers |
| **GardenPanel.cs** | Double-buffered `Panel` the flowers are drawn on (no flicker) |
| **MainForm.cs** | Timer, `ArrayList` of flowers, menus, mouse planting, resize handling, full screen |

## How It Works

- **Polymorphic Processing**: Each `Timer` tick loops over the `ArrayList`, explicitly casts each item `(Flower)item` and calls `Move`. `Paint` does the same with `Draw`. Which override runs is decided by each flower's real type at run time (late binding)
- **Explicit Casting**: New flowers are explicitly upcast `(Flower)new TulipFlower(...)` and `(object)flower` when added to the list, and explicitly cast back to `Flower` whenever they are read
- **Growth**: Every flower goes through Seed → Sprout → Bud → Bloom → Wilt and starts again. Drawn flowers scale up smoothly; ASCII flowers switch to bigger art
- **Wall Bouncing**: After moving, a flower past an edge is put back on that edge and its velocity is pointed away from the wall
- **Responds to Resizing**: The panel's current `ClientRectangle` is passed to `Move` every tick, and `Resize` pulls any flower that is now outside back in
- **Collision Detection**: Each flower is a circle around its centre. Overlapping pairs are pushed apart, and if they are moving towards each other the velocity parts along the line between their centres are swapped (elastic, equal mass)
- **Library Control**: `LabelFlower` adds a real `Label` to the panel and moves it by setting `Location`; its `Draw` override does nothing because the control paints itself

## Visual State Changes

| # | Flower | Change | Trigger |
|---|--------|--------|---------|
| 1 | **AsciiSprout** | ASCII art redrawn for each stage (`.` → `,\|` → bud → bloom → drooping brown) | Growth stage |
| 2 | **DaisyFlower** | Gradient angle turns 6° per tick, so the colour spins around the head | Every tick |
| 3 | **TulipFlower** | Next colour in a 6-colour palette | Wall bounce |
| 4 | **LabelFlower** | Text and background colours swap for 12 ticks | Collision with another flower |

## Controls

| Action | Menu | Shortcut |
|--------|------|----------|
| Plant random flower at mouse | - | Left-click garden |
| Add Daisy / Tulip / ASCII Sprout / Label Bouquet | Garden → Add ... | Ctrl+1 / 2 / 3 / 4 |
| Add random flower | Garden → Add Random Flower | Ctrl+R |
| Pause / resume | Garden → Pause | Ctrl+P |
| Remove all flowers | Garden → Clear Garden | Ctrl+Delete |
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

Run the program and watch a few life cycles (about 15 seconds each). Plant flowers near the edges and corners, drag the window
smaller and larger, and plant many flowers in a small window to see collisions. Tulips change colour at every wall and label
bouquets flash when they hit another flower.
