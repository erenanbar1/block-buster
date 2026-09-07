# Block Blast

A portrait-orientation mobile block-puzzle game built in Unity 6.3 (URP 2D).

Drag one of three offered pieces onto an 8×8 grid. Fill a row or a column and it
pops. Pieces never rotate, and they keep coming until nothing in the tray fits.

## Running it

Open `Assets/Scenes/Game.unity` and press Play. The scene contains three objects —
a camera, an EventSystem, and a `GameBootstrap` — everything else (canvas, board,
tray, HUD, effects, audio) is built at runtime from code. There are **no imported
art or audio assets**: every sprite is generated procedurally in `SpriteFactory`
and every sound is synthesised in `SfxPlayer`.

## Layout of the code

```
Assets/Scripts/
  Core/           pure rules, no MonoBehaviours - unit testable in isolation
    PieceShape      one piece definition, normalised to its bounding box
    PieceLibrary    ~40 shapes (every rotation is its own entry)
    BoardModel      8x8 grid: placement, line clears, dead-board detection
    ScoreRules      placement / line / combo / perfect-clear scoring
    PieceGenerator  weighted dealing with the anti-frustration guarantee
  Presentation/   views and juice
    Theme           every colour, size and timing in one file
    SpriteFactory   procedural rounded rects, bevelled blocks, gradients
    Ui, Tween       terse uGUI builders and coroutine tweens
    BoardView       grid rendering plus all board-space maths
    PieceView       drag handling, snapping, return-to-tray
    TrayView        the three slots
    HudView         score, best, combo badge, game-over card, buttons
    EffectsLayer    pooled confetti, floating text, screen shake
    SfxPlayer       procedurally synthesised sound effects
  Game/
    GameBootstrap   builds the whole scene at runtime
    GameController  the game loop; owns the model, drives the views
    SaveSystem      best score plus a full resume snapshot (PlayerPrefs)
    SafeAreaFitter  keeps the UI clear of notches and gesture bars
    DesignFitter    scales the fixed 1080x1920 design to any aspect ratio
```

The model is always updated up front and the animation follows, so what the rules
see can never drift from what the player sees.

## Rules

| Event | Points |
| --- | --- |
| Placing a piece | 1 per block |
| 1 / 2 / 3 / 4 lines at once | 10 / 30 / 60 / 100 |
| Combo (consecutive clearing moves) | ×1 → ×5, one step per two combos |
| Perfect clear (board left empty) | +300 |

A row and a column clearing together share their intersection: it is removed and
scored once, not twice.

**Fairness.** The dealer damps large pieces as the board fills, and will not hand
out a trio in which nothing at all can be placed — if the random draw keeps
missing, one slot is replaced with a shape that is known to fit. A game over is
therefore always a real dead end, never bad luck in the deal.

## Mobile specifics

- Portrait only, 1080×1920 design frame, uniformly scaled to fit any aspect.
- Safe-area aware, with the reported area clamped to the screen (some devices,
  and the editor after a resolution change, report one that spills outside it).
- The dragged piece floats ~210px above the fingertip so it is never hidden by
  the hand; a ghost and a gold wash show where it lands and what will pop.
- Progress is written to `PlayerPrefs` after every move and on pause/quit, so a
  run survives the app being swiped away.
- 60 fps target, screen sleep disabled, SFX toggle persisted.

## Tests

```
Assets/Tests/EditMode/   42 tests - board, pieces, scoring, headless game sims
Assets/Tests/PlayMode/   14 tests - bootstrap, placement, drag input, save/resume
```

Run them from **Window ▸ General ▸ Test Runner**.

The EditMode suite includes a soak test that plays 100 complete games with a
greedy bot, asserting after every one of tens of thousands of placements that
occupancy is consistent and that no completed line was ever left standing.

The PlayMode suite drives real `PointerEventData` through the uGUI drag handlers
rather than calling the controller directly, so the pointer-to-board maths and
the drag-layer reparenting are covered as well.
