# PROG2200 Assignment 2 - ASCII Lily Garden Screen Saver

A Windows Forms screen saver with four kinds of lily (spider, calla, tiger and lily of the valley) drawn entirely in ASCII,
all growing out of one vase. Every stem starts at the same point, the vase mouth, and branches out to its flower head.
The heads drift upwards, bounce off the walls and each other, and stay within reach of their stems, which grow long
enough to reach the top of the window (the reach is worked out from the window's size every time it is resized).
When two lilies are about to touch they bounce apart and wilt: the head crumbles and falls to the bottom of the window,
its stem shrinks back into the vase, and it resprouts from there. Growth is slow, like a time-lapse, and each head's
picture is redrawn in stop-motion steps while it keeps moving smoothly. Clicking puts a fully grown lily into the vase.

The ASCII is generated the way TouchDesigner's ASCII effect works: each shape is a brightness field that is sampled on a
grid of character cells, every cell gets the character from a brightness ramp (`" .:-=+*#%@"`) that matches it, and the
characters are looked up in a pre-drawn glyph atlas. Grain, dark specks and a few stray flickering "data" characters give
it the look of a TouchDesigner render.

All lilies inherit from one abstract class and are processed polymorphically from an `ArrayList` on a timer.
The shapes and collision detection are in a class library.

## Quick Start

1. **Clone and build**:
   ```powershell
   git clone https://github.com/bakuncwa/prog2200_assign2.git
   cd prog2200_assign2
   start prog2200_assign2.sln      # Ctrl+Shift+B to build
   ```

2. **Run**: Press **F5** in Visual Studio. One seed of each kind is planted in the vase and they branch out from it.

3. **Grow the garden**: see [Events That Plant New Lilies](#events-that-plant-new-lilies).

## Architecture

| Component | Purpose |
|-----------|---------|
| **Flower.cs** | Abstract base: position, velocity, growth stages, wall bouncing, stem tether (reach grows and shrinks with the life cycle), falling while wilting, resprouting, starting fully grown (`GrowFully`), abstract `Draw` and `Size`, virtual `Move` and `StemEnd` |
| **AsciiLily.cs** | Abstract ASCII lily head: samples the head on a character grid every 4 ticks (stop-motion), maps brightness to the ramp, adds grain / specks / data characters, and copies glyphs from the atlas into one bitmap per lily. Provides the shared seed, sprout and bud; subclasses supply the flower (`SampleBloom`) |
| **AsciiCanvas.cs** | Shared ASCII helpers: brightness ramp, shading, noise, rendering a fixed picture to a bitmap |
| **AsciiSample.cs** | Brightness and colour of one point of a shape |
| **GlyphAtlas.cs** | Each character drawn once into a 5 × 8 pixel cell and kept as a coverage mask and as small coloured bitmaps |
| **GrowthStage.cs** | Life cycle enum: Seed → Sprout → Bud → Bloom → Wilt (then resprouts) |
| **SpiderLily.cs** | Six thin curling petals and pollen-tipped stamens, coloured by a gradient that spins around the flower |
| **CallaLily.cs** | Irregular 13-point trumpet polygon (`GraphicsPath.AddPolygon` / `IsVisible`) filled with ASCII, bright edges, veins, faint `DrawPolygon` trace, yellow spadix; changes colour on every wall bounce |
| **TigerLily.cs** | Six wide spotted petals with curled-back tips; switches character set at every growth stage |
| **LilyOfTheValley.cs** | A Windows Forms `Label` control (transparent background) whose text is the ASCII grid: an arched stem with hanging bells. Text flashes gold on collision |
| **BufferedLabel.cs** | Double-buffered `Label` used by the lily of the valley, so changing its text every tick does not flicker |
| **Stems.cs** | Draws each stem from the vase mouth to its head as a curve of line characters (`\| / \ -`) with two shaded leaves |
| **Vase.cs** | The one vase: a glazed ASCII pot, lit from the upper left, with gold bands; fixed at the bottom middle |
| **CollisionDetector.cs** | Circle-based collision detection (reacting just before contact) and elastic bounce between every pair of heads; reports where two blooming lilies met |
| **GardenPanel.cs** | Double-buffered `Panel` everything is drawn on (no flicker) |
| **MainForm.cs** | Timer, `ArrayList` of lilies, vase, menus, mouse planting, pollination and resize planting, full screen |

## How It Works

- **Polymorphic Processing**: Each `Timer` tick loops over the `ArrayList`, explicitly casts each item `(Flower)item` and calls `Move`. `Paint` does the same with `Draw`. Which override runs is decided by each lily's real type at run time (late binding), and inside `AsciiLily` the abstract `SampleBloom` is late-bound again for every cell
- **Explicit Casting**: New lilies are explicitly upcast `(Flower)new CallaLily(...)` and `(object)flower` when added to the list, and explicitly cast back to `Flower` whenever they are read. Pollination points come back in an `ArrayList` and are cast with `(PointF)item`
- **One Origin**: Every lily is planted at the vase mouth and its stem is tied there (`TieTo`). How far the stem can reach grows from nothing (seed) to its full length (by the time it blooms), stays at full length while the wilting head falls, and then shrinks back to nothing, so lilies grow out of the vase and are drawn back into it
- **Reaching the Edges**: A stem's full length is the distance from the vase mouth to a top corner of the window, worked out again on every resize, and every growing head is nudged upwards, so the lilies grow up to the top of the window; wilting heads fall under gravity to the bottom
- **Life Cycle**: Seed → Sprout (a shoot with two small leaves) → Bud (swells) → Bloom (opens) → Wilt (droops, browns, crumbles, falls to the bottom, pulled back into the vase) → resprouts and heads off upwards again. A whole cycle takes about 50 seconds
- **Stop-Motion Growth**: Each head's picture is redrawn every 4 ticks (and straight away when it changes stage), so it grows in visible steps while its position and stem keep moving smoothly every tick
- **Wall Bouncing**: After moving, a head past an edge is put back on that edge and its velocity is pointed away from that wall
- **Stem Reach**: A head that gets further from the vase than its stem can reach is pulled back and bounces off that limit
- **Responds to Resizing**: The panel's current `ClientRectangle` is passed to `Move` every tick. `Resize` moves the vase to the new bottom middle, reties every stem with a reach that fits the new size, pulls any head that is now outside back in, and plants new lilies if the garden got bigger
- **Collision Detection**: Each head is a circle around its centre. When two circles come within 6 pixels of each other they are pushed apart, and if they are moving towards each other the velocity parts along the line between their centres are swapped (elastic, equal mass). A bud or bloom that is bumped starts to wilt, so lilies wilt and fall away just before they touch. Seeds and sprouts are skipped because they all start together at the vase mouth
- **Library Control**: `LilyOfTheValley` adds a real `Label` to the panel and moves it by setting `Location`; its `Draw` override does nothing because the control paints itself. Its background is transparent, so it shows as characters only
- **Double Buffering**: The garden panel paints into an off-screen buffer (`DoubleBuffered`, `OptimizedDoubleBuffer`, `AllPaintingInWmPaint`), the label lily uses the same through `BufferedLabel`, and each lily head and the vase are composed into their own bitmaps first and copied to the panel in one `DrawImage` call each
- **No Rectangles**: Nothing is drawn as a box. Data characters only appear in a round area around each head, and the label's background is transparent

## Events That Plant New Lilies

Every new lily's stem is in the vase mouth. Clicks put in a fully grown lily, which carries on from full bloom to wilting
and then resprouts from a seed; every other event plants a seed whose stem heads the way the event decides.

| Event | What is planted | Rubric |
|-------|-----------------|--------|
| Start-up | One seed of each kind | - |
| Left-click | One fully grown random lily, with its head at the click | Adding shapes |
| Right-click | One fully grown lily of each kind, spread around the click | Adding shapes |
| Garden menu / shortcuts | A seed of the chosen kind, or one of each | Adding shapes |
| Two blooming lilies meet | They pollinate each other and a new lily grows towards where they met | Collision detection |
| Window made bigger | One new lily for every 150,000 new square pixels | Shapes respond to frame size |

Lilies that sprout by themselves (pollination and a bigger window) stop once the garden holds `MaxFlowers` lilies.

## Visual State Changes

| # | Lily | Change | Trigger |
|---|------|--------|---------|
| 1 | **TigerLily** | Character set changes: `" .o"` → `" .:;\|"` → `" .-~=oO"` → full ramp `" .:-=+*#%@"` → `" .,;:~"` | Growth stage |
| 2 | **SpiderLily** | Gradient angle turns 3° per tick (shown in stop-motion steps), so the colour spins around the petals and stamens | Every tick |
| 3 | **CallaLily** | Next colour in a 6-colour palette | Wall bounce |
| 4 | **LilyOfTheValley** | Text flashes gold for 12 ticks | Collision with another lily |

## Controls

| Action | Menu | Shortcut |
|--------|------|----------|
| Add a fully grown random lily at the mouse | - | Left-click garden |
| Add a fully grown lily of each kind around the mouse | - | Right-click garden |
| Add Spider / Calla / Tiger Lily / Lily of the Valley | Garden → Add ... | Ctrl+1 / 2 / 3 / 4 |
| Add random lily | Garden → Add Random Lily | Ctrl+R |
| Grow one of each kind | Garden → Grow One of Each | Ctrl+S |
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

Run the program and watch the seeds sprout out of the vase and grow up to the top of the window (a full life cycle is about
50 seconds). Let two blooming lilies drift towards each other: they bounce apart just before touching, wilt, fall to the
bottom and are pulled back into the vase, a new lily grows towards where they met, and both resprout. Click around the
garden to put fully grown lilies in, and drag the window narrower and wider (the vase stays at the bottom middle, the stems'
reach changes with the window, and bigger windows grow new lilies). Calla lilies change colour at every wall and lilies of the valley flash gold
when they hit another lily.
