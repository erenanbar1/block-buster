# Block Blast

**▶ Play it in your browser: https://erenanbar1.github.io/block-buster/**

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
| Combo (clearing moves, one dry move forgiven) | ×1 → ×5, one step per two combos |
| Perfect clear (board left empty) | +300 |
| Stage | multiplies everything above |

A row and a column clearing together share their intersection: it is removed and
scored once, not twice.

## Progression

A run is one continuous game divided into stages, defined in a single table in
`Core/DifficultyCurve.cs`. Stages advance on **lines cleared**, not score —
scoring accelerates as multipliers grow, so a score ladder would make late stages
arrive faster the better you play, which is backwards.

| Stage | Name | Lines | Pieces unlocked | Junk |
| --- | --- | --- | --- | --- |
| 1 | Warm Up | 4 | dots, short bars, 2×2, small corners | — |
| 2 | Rolling | 5 | + L/J/T/S/Z, 4-bars | — |
| 3 | Heating Up | 6 | + 3×2 blocks, 3×3 corners | 1 per 12 |
| 4 | Pressure | 7 | + 5-bars, 3×3 square | 1 per 8 |
| 5 | Squeeze | 8 | + plus, diagonals | 2 per 8 |
| 6 | Overload | 9 | everything | 2 per 6 |
| 7+ | Overload N | +2 each | everything | creeps to 3 per 5 |

Two things escalate together. The **piece pool** gets meaner: each stage unlocks
a tier and raises the odds of bulky shapes. The **board fills on its own**: from
stage 3, junk blocks are seeded periodically. Skill alone cannot hold the line
forever, so a run builds to a crescendo instead of wandering until an unlucky
trio ends it.

**Fairness rules**, all enforced in code rather than left to tuning:

- The dealer never hands out a trio in which nothing can be placed. If the random
  draw keeps missing, one slot is replaced with a shape known to fit.
- Junk hugs existing blocks and edges, never completes a line, and the entire
  drop is rolled back if it would leave the tray without a legal move. Junk
  raises pressure; it never lands the killing blow.
- Junk waits for a quiet beat rather than landing on top of a clear.
- Above ~75% board fill, bulky shapes are damped again regardless of stage.
  Being handed a 3×3 into a packed board is unfair, not hard.

A game over is therefore always a real dead end, never bad luck in the deal.

**Combos survive one non-clearing placement.** A tray holds three pieces and
rarely clears on more than one, so resetting the chain instantly pinned every
combo at ×1 and made the multiplier unreachable. With one move of grace, chains
of 3-5 are routine and ×5 becomes a genuine goal.

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

The EditMode suite includes a soak test that plays complete games with a greedy
bot, asserting after every one of tens of thousands of placements that occupancy
is consistent, that no completed line was ever left standing, that the stage never
goes backwards, and that no junk drop ever took away the tray's last legal move.

That bot is also the **tuning instrument for the difficulty curve**.
`RunsBuildToACrescendoAndEnd` plays 60 full games and logs the shape of a typical
run, so balance is measured rather than guessed:

```
[sim] median moves 89, score 3282, stage 6, best combo 5 |
      longest 308, top score 41939, deepest stage 14, junk dropped 1219
```

A median run is ~89 placements (5-10 minutes) reaching stage 6 with a peak combo
of 5, while an exceptional run reaches stage 14 and scores 10× the median. Before
the stage system the same bot ran a median of 149 moves — and up to 1000 — with
combos pinned at 1, because difficulty depended only on how badly you were
playing. Retune by editing the table in `Core/DifficultyCurve.cs` and re-reading
that line.

The PlayMode suite drives real `PointerEventData` through the uGUI drag handlers
rather than calling the controller directly, so the pointer-to-board maths and
the drag-layer reparenting are covered as well.

## Deployment

The playable build lives in `docs/` and GitHub Pages serves it directly
(Settings ▸ Pages ▸ Deploy from a branch ▸ `main` / `docs`). There is no CI
build step: building Unity on a runner would need a Unity licence in repository
secrets, whereas committing the built player needs no secrets at all.

Rebuild and redeploy after changing the project:

```
Unity.exe -quit -batchmode -nographics \
          -projectPath <project> \
          -buildTarget WebGL \
          -executeMethod BlockBlast.EditorTools.WebGLBuilder.Build \
          -logFile build.log
```

then commit `docs/` and push — Pages picks it up within a minute.

`WebGLBuilder` applies the two settings the deployment depends on rather than
trusting whatever is saved in ProjectSettings: **Gzip compression with the
decompression fallback enabled**. GitHub Pages serves static files without a
`Content-Encoding` header, so the player has to decompress the build itself;
without the fallback the page loads to a black screen. It also treats a build as
failed when the error count is non-zero or `index.html` is missing, because Unity
will otherwise report "Succeeded" for a build that wrote nothing at all.

To test the exact bytes Pages will serve, run any static server over `docs/`:

```
python -m http.server 8123 --directory docs
```
