# Continuous Integration in this project

A guide to what CI is, why it is worth the trouble, and exactly how Block Blast
uses it. The examples are all real failures from this project rather than
textbook ones.

---

## 1. What CI actually is

**Continuous Integration** is the practice of having a server rebuild and re-test
your project automatically, every time someone pushes code.

That is the whole idea. The interesting part is not the automation, it is the
word *continuously*. Without CI, "does the project still build and pass its
tests?" is a question somebody has to remember to ask, on one particular machine,
usually right before a release when it is most expensive to be wrong. With CI it
is answered within minutes of every change, on a clean machine, forever.

**Continuous Deployment (CD)** is the natural extension: if the tests pass, ship
it. No human copies files anywhere.

A CI system is really just three promises:

| Promise | What it prevents |
| --- | --- |
| Every change is built from scratch | "It works on my machine" |
| Every change runs the full test suite | Regressions nobody thought to re-check |
| Shipping is a script, not a ritual | Deploys that fail because a step was skipped |

---

## 2. Why bother — evidence from this project

The argument for CI is usually made in the abstract. It does not need to be here,
because this project produced textbook examples of each failure mode while it was
being built **by hand**.

### A build reported success and produced nothing

Unity was asked to build WebGL and returned `result=Succeeded`. It also returned
`errors=2` and wrote no files at all — the post-processor had been loaded before
the WebGL module was installed. A human reading "Succeeded" moves on. The fix now
lives in `Assets/Editor/WebGLBuilder.cs`:

```csharp
if (summary.result != BuildResult.Succeeded || summary.totalErrors > 0 || !loaderExists)
```

A build is only a success if the error count is zero **and** `index.html` actually
exists on disk. CI runs that same check on every push, so this specific lie can
never reach the live site again.

### Three shells waited on a build that had already finished

A folder was renamed from `WebGL/` to `docs/`. Unity names its output files after
the folder, so `WebGL.wasm.unityweb` silently became `docs.wasm.unityweb`, and
three separate polling loops sat waiting for a filename that would never exist —
for eight minutes after the build was done. Hand-rolled automation rots quietly.
A pipeline that declares its steps does not.

### The difficulty curve was measurably backwards

The game shipped with `SizeBias` handing out *easier* pieces as the board filled.
Playing well made the game easier, so runs never built to anything. This was not
found by reading the code — it was found by a test that plays 60 complete games
and prints the result:

```
[sim] median moves 89, score 3282, stage 6, best combo 5 |
      longest 308, top score 41939, deepest stage 14, junk dropped 1219
```

That line is a **balance regression detector**. If someone edits the difficulty
table and the median run collapses to 20 moves or balloons to 400, CI fails.
Game feel is not usually something you can put under test; here it partly is.

### A UI element rendered at negative width

The stage progress bar was computing its fill as though its anchors stretched,
when they did not — `sizeDelta.x` came out at `-120`, so the bar was invisible at
every fill level. **No test caught this, and CI would not have either.** It took
looking at a screenshot.

That is worth stating plainly: CI proves the things it was told to check. It is
not a substitute for looking at your game.

### Every deploy needed a human in the loop

Before this pipeline, shipping meant: build locally (~4 min), wait, commit 16MB,
push, wait for Pages. Every step a chance to forget one. CD reduces that to
`git push`.

---

## 3. How Block Blast uses CI

The pipeline lives in [`.github/workflows/ci.yml`](.github/workflows/ci.yml) and
runs on every push to `main`, on pull requests, and on demand.

### The pipeline

```
push to main
     │
     ├─► test    ─ 108 tests (87 EditMode + 21 PlayMode) in a clean Unity
     │            container. Publishes a pass/fail check on the commit.
     │
     ├─► build   ─ needs: test.  Builds the WebGL player, then verifies a
     │            player was really written.
     │
     └─► deploy  ─ needs: build.  Publishes to GitHub Pages.
```

Jobs are chained with `needs:`, so **a failing test cannot deploy**. That is the
single most valuable line in the file.

### What each job does, and why it is written that way

**`test`** runs `game-ci/unity-test-runner` in `unityci/editor:6000.3.12f1-webgl`
— the project's exact Unity version, so CI cannot pass on a version the team is
not using. It runs `testMode: all`, meaning both suites:

- **87 EditMode tests** — pure logic: board mechanics, scoring, the difficulty
  curve, junk fairness, and the headless game simulation above.
- **21 PlayMode tests** — the real game booted in a real scene, driving actual
  `PointerEventData` through the uGUI drag handlers.

**`build`** calls `BlockBlast.EditorTools.WebGLBuilder.Build` — *the same entry
point a developer uses locally*. This is deliberate. If CI had its own build
script, the two would drift and CI would stop predicting what you get on your
machine. The builder takes `-buildOutput` so CI can write elsewhere without
forking the logic.

It then does something a stock pipeline would not:

```yaml
- name: Check the player was actually written
  run: |
    test -f build/WebGL/index.html || { echo "::error::no index.html"; exit 1; }
```

That step exists **because of the silent-success failure in section 2**. Every
odd-looking line in a good pipeline is usually a scar.

**`deploy`** publishes to Pages via `actions/deploy-pages`, and only on pushes to
`main` — never from a pull request.

### Decisions worth understanding

**The build is not committed.** Committing a 16MB player on every rebuild grows
the repository without bound; git keeps every version forever. CI builds it fresh
and hands it to Pages as an artifact.

The tradeoff, stated honestly: you can no longer inspect the exact deployed bytes
in the repo, and a broken pipeline means no deploy at all. That is accepted here
because the build is fully reproducible from source.

**Forked pull requests skip the Unity jobs.**

```yaml
if: github.event_name != 'pull_request' || github.event.pull_request.head.repo.full_name == github.repository
```

Forks cannot read repository secrets, so the Unity licence would be missing and
every fork PR would fail at activation — red for a reason that has nothing to do
with the change under review. A CI system that cries wolf gets ignored, and an
ignored CI system is worse than none.

**`Library/` is cached.** A cold Unity build is 15-25 minutes, most of it
re-importing assets. The cache cuts that substantially. Actions minutes are free
on public repositories, so this buys feedback speed rather than money.

**Concurrency is limited.** `cancel-in-progress: true` means pushing twice
quickly cancels the stale run instead of queueing a build of code nobody cares
about any more.

---

## 4. Setup

CI needs three things that cannot live in the repository.

**1. A push token with `workflow` scope.** GitHub rejects any push that adds or
edits `.github/workflows/` without it — this project hit that exact wall:

```
! [remote rejected] main -> main (refusing to allow a Personal Access Token
  to create or update workflow .github/workflows/ci.yml without workflow scope)
```

```
gh auth refresh -s workflow
gh auth setup-git
```

**2. A `UNITY_LICENSE` secret.** Unity refuses to run without activation, so
GameCI fails immediately without it. Getting one is a manual round trip:

```
Unity.exe -batchmode -nographics -quit -createManualActivationFile
```

Upload the resulting `.alf` at <https://license.unity3d.com/manual>, download the
`.ulf` it returns, then:

```
gh secret set UNITY_LICENSE < Unity_v6000.3.12f1.ulf
```

Piping from the file keeps the licence out of your shell history.

> Unity 6 activates locally through the newer licensing client
> (`UnityEntitlementLicense.xml`), which GameCI does **not** accept. The manual
> `.alf` → `.ulf` round trip above is still required even on a machine where
> Unity already runs fine.

**3. Pages set to the "GitHub Actions" source**, rather than deploying from a
branch.

### Order matters

Switching Pages to Actions before a CI build has ever succeeded takes the live
game offline, because the old branch deploy stops and nothing has replaced it.
The safe sequence:

1. Push the workflow — site still served from the committed `docs/`
2. Add the `UNITY_LICENSE` secret
3. Switch Pages to Actions and confirm a run goes green
4. **Then** remove `docs/` from git

---

## 5. Reading a failure

```
gh run list --limit 5          # recent runs
gh run view <id> --log-failed  # just the failing step
gh run watch <id>              # follow a run live
```

Failures generally sort into three kinds, and telling them apart quickly is most
of the skill:

| Symptom | Usually means |
| --- | --- |
| Fails in seconds, mentions licence or activation | Setup, not code. Check the secret. |
| A named test fails | A real regression, or a test encoding behaviour you deliberately changed. |
| Build fails after the tests pass | Platform-specific: a stripping, shader or module problem that the editor hides. |

That middle row matters. When the combo rules changed, two existing tests failed
because they asserted the *old* behaviour — `ComboIsExtendedByClearsAndBrokenBy
ADryPlacement` and `CrowdedBoardsFavourSmallerPieces`. Both were correct failures
of obsolete tests, and both were rewritten rather than deleted or forced green. A
red CI is information, not an obstacle.

---

## 6. Current status

| Piece | State |
| --- | --- |
| Workflow committed and pushed | ✅ |
| Token has `workflow` scope | ✅ |
| CI reaches the Unity step | ✅ (13s — YAML and job graph verified good) |
| `UNITY_LICENSE` secret | ⏳ pending manual activation |
| Pages switched to Actions | ⏳ after CI is green |
| `docs/` removed from git | ⏳ last, so the site never goes dark |

The pipeline has **not yet completed a green run** — it currently stops at licence
activation, which is a setup step rather than a defect. Until then the game is
still deployed the previous way, from the committed `docs/` folder, and remains
live throughout.
