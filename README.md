# PROG2200 Assignment 2 - ASCII Lily Garden Screen Saver

A Windows Forms screen saver with four kinds of lily (spider, calla, tiger and lily of the valley) drawn entirely in ASCII.
Every lily starts as a single seed, sprouts and grows into a whole plant while it drifts around the window. When two lilies
are about to touch they bounce apart and wilt: the plant crumbles away as it falls, bounces off the floor, and resprouts
from where it landed.

The ASCII is generated the way TouchDesigner's ASCII effect works: each plant is a brightness field that is sampled on a grid
of character cells, every cell gets the character from a brightness ramp (`" .:-=+*#%@"`) that matches it, and the characters
are looked up in a pre-drawn glyph atlas. Grain, dark specks and stray flickering "data" characters give it the look of a
TouchDesigner render.

All lilies inherit from one abstract class and are processed polymorphically from an `ArrayList` on a timer.
The shapes and collision detection are in a class library.

## Quick Start

1. **Clone and build**:
   ```powershell
   git clone https://github.com/bakuncwa/prog2200_assign2.git
   cd prog2200_assign2
   start prog2200_assign2.sln      # Ctrl+Shift+B to build
   ```

2. **Run**: Press **F5** in Visual Studio. One seed of each kind is planted in the middle of the garden and they scatter outwards.

3. **Grow the garden**: see [Events That Plant New Lilies](#events-that-plant-new-lilies).

## Architecture

| Component | Purpose |
|-----------|---------|
| **Flower.cs** | Abstract base: position, velocity, growth stages, wall bouncing, falling while wilting, resprouting, abstract `Draw` and `Size`, virtual `Move` |
| **AsciiLily.cs** | Abstract ASCII renderer: samples the plant on a character grid, maps brightness to the ramp, adds grain / specks / data characters, and copies glyphs from the atlas into one bitmap per lily. Provides the shared soil, seed, swaying stem with leaves and a twig, and bud; subclasses supply the flower (`SampleBloom`) |
| **AsciiSample.cs** | Brightness and colour of one point of a plant |
| **GlyphAtlas.cs** | Each character drawn once into a 5 × 8 pixel cell and kept as a coverage mask |
| **GrowthStage.cs** | Life cycle enum: Seed → Sprout → Bud → Bloom → Wilt (then resprouts) |
| **SpiderLily.cs** | Six thin curling petals and pollen-tipped stamens, coloured by a gradient that spins around the flower |
| **CallaLily.cs** | Irregular 13-point trumpet polygon (`GraphicsPath.AddPolygon` / `IsVisible`) filled with ASCII, bright edges, veins, faint `DrawPolygon` trace, yellow spadix; changes colour on every wall bounce |
| **TigerLily.cs** | Six wide spotted petals with curled-back tips; switches character set at every growth stage |
| **LilyOfTheValley.cs** | A Windows Forms `Label` control whose text is the ASCII grid: an arched stem with hanging bells between two broad leaves. Swaps colours on collision |
| **BufferedLabel.cs** | Double-buffered `Label` used by the lily of the valley, so changing its text every tick does not flicker |
| **CollisionDetector.cs** | Circle-based collision detection (reacting just before contact) and elastic bounce between every pair; reports where two blooming lilies met |
| **GardenPanel.cs** | Double-buffered `Panel` the lilies are drawn on (no flicker) |
| **MainForm.cs** | Timer, `ArrayList` of lilies, menus, mouse planting, pollination and resize planting, full screen |

## How It Works

- **Polymorphic Processing**: Each `Timer` tick loops over the `ArrayList`, explicitly casts each item `(Flower)item` and calls `Move`. `Paint` does the same with `Draw`. Which override runs is decided by each lily's real type at run time (late binding), and inside `AsciiLily` the abstract `SampleBloom` is late-bound again for every cell
- **Explicit Casting**: New lilies are explicitly upcast `(Flower)new CallaLily(...)` and `(object)flower` when added to the list, and explicitly cast back to `Flower` whenever they are read. Pollination points come back in an `ArrayList` and are cast with `(PointF)item`
- **Life Cycle**: Seed (swells in the soil) → Sprout (stem rises, leaves unfold) → Bud (swells on top of the stem) → Bloom (opens) → Wilt (droops, browns, crumbles, falls) → resprouts as a seed and drifts off in a new direction. The character grid gets more columns and rows as the plant grows, and the plant is shifted so its soil stays put and it grows upwards
- **Wall Bouncing**: After moving, a lily past an edge is put back on that edge and its velocity is pointed away from that wall. A wilting lily falls under gravity, loses speed each time it hits the floor and comes to rest there
- **Responds to Resizing**: The panel's current `ClientRectangle` is passed to `Move` every tick, and `Resize` pulls any lily that is now outside back in (and plants new seedlings if the garden got bigger)
- **Collision Detection**: Each lily is a circle around its centre. When two circles come within 6 pixels of each other they are pushed apart, and if they are moving towards each other the velocity parts along the line between their centres are swapped (elastic, equal mass). A bud or bloom that is bumped starts to wilt, so lilies wilt and fall away just before they touch
- **Library Control**: `LilyOfTheValley` adds a real `Label` to the panel and moves it by setting `Location`; its `Draw` override does nothing because the control paints itself
- **Double Buffering**: The garden panel paints into an off-screen buffer (`DoubleBuffered`, `OptimizedDoubleBuffer`, `AllPaintingInWmPaint`), the label lily uses the same through `BufferedLabel`, and each drawn lily is first composed into its own bitmap and copied to the panel in one `DrawImage` call
- **ASCII Rendering**: Each cell is sampled at 2 × 2 points and the average is mixed with the brightest point, so thin stems still show while petals and leaves are shaded smoothly

## Events That Plant New Lilies

| Event | What is planted | Rubric |
|-------|-----------------|--------|
| Start-up | One seed of each kind at the centre of the garden, scattering outwards | - |
| Left-click | One random seed at the mouse | Adding shapes |
| Right-click | One seed of each kind, scattering from the mouse | Adding shapes |
| Garden menu / shortcuts | Chosen kind at a random spot, or a scatter from the centre | Adding shapes |
| Two blooming lilies meet | They pollinate each other and a new seedling sprouts between them | Collision detection |
| Window made bigger | One seedling for every 150,000 new square pixels | Shapes respond to frame size |

Lilies that sprout by themselves (pollination and a bigger window) stop once the garden holds `MaxFlowers` lilies.

## Visual State Changes

| # | Lily | Change | Trigger |
|---|------|--------|---------|
| 1 | **TigerLily** | Character set changes: `" .o"` → `" .:;\|"` → `" .-~=oO"` → full ramp `" .:-=+*#%@"` → `" .,;:~"` | Growth stage |
| 2 | **SpiderLily** | Gradient angle turns 6° per tick, so the colour spins around the petals and stamens | Every tick |
| 3 | **CallaLily** | Next colour in a 6-colour palette | Wall bounce |
| 4 | **LilyOfTheValley** | Text and background colours swap for 12 ticks | Collision with another lily |

## Controls

| Action | Menu | Shortcut |
|--------|------|----------|
| Plant random seed at mouse | - | Left-click garden |
| Scatter one seed of each kind from mouse | - | Right-click garden |
| Add Spider / Calla / Tiger Lily / Lily of the Valley | Garden → Add ... | Ctrl+1 / 2 / 3 / 4 |
| Add random lily | Garden → Add Random Lily | Ctrl+R |
| Scatter seeds from the centre | Garden → Scatter Seeds From Centre | Ctrl+S |
| Pause / resume | Garden → Pause | Ctrl+P |
| Remove all lilies | Garden → Clear Garden | Ctrl+Delete |
| Full screen | View → Full Screen | F11 (Esc to leave) |

## Config

Animation settings live in `prog2200_assign2/App.config`. After building they are copied to
`bin\Debug\prog2200_assign2.exe.config`, so they can be changed there without recompiling:
```xml
<appSettings>
    <add key="TickIntervalMs" value="30" />
    <add key="StartingFlowersPerKind" value="1" />
    <add key="MaxFlowers" value="16" />
</appSettings>
```
A missing or invalid value falls back to the default shown above.

## Testing

Run the program and watch the seeds scatter from the middle and grow (a full life cycle is about 20 seconds). Let two blooming
lilies drift towards each other: they bounce apart just before touching, wilt and fall to the floor, a new seedling sprouts
between them, and both resprout where they land. Plant lilies near the edges and corners, drag the window smaller and larger
(bigger plants new seedlings), and right-click to scatter seeds. Calla lilies change colour at every wall and lilies of the
valley flash when they hit another lily.
