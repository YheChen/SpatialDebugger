# SpatialDebugger — overnight progress

Written 2026-09-19, overnight. Everything below is either **verified by an
actual run** or explicitly marked as not verified. Nothing is claimed to work on
a headset, because no headset was attached.

---

## TL;DR for the morning

The full `target → reasoning → spatial annotation` pipeline is built, compiles
clean, passes **102 tests**, and **builds a working Quest 3 APK**. There is an
APK ready to sideload at `Unity/Build/Android/SpatialDebugger.apk`.

Three blockers were found and fixed that would each have killed the demo. All
three were found by actually running the build, not by reading code:

1. **No XR plug-in was installed at all.** Meta XR SDK v205 ships no XR loader
   of its own — it integrates with Quest as an *OpenXR feature* gated on
   `USING_XR_SDK_OPENXR`. An APK would have built, installed and launched as a
   **flat 2D Android app** with no head tracking and no stereo.
   Fixed: `com.unity.xr.openxr` 1.18.0 added, loader wired, Meta XR feature set
   and Oculus Touch profile enabled.

2. **The Android build failed on duplicate `libopenxr_loader.so`.** Unity's
   OpenXR plugin and Meta's OVRPlugin both ship one. Meta has a build hook that
   disables Unity's copy — but it resolves `MetaXRFeature` through a specific
   lookup, OpenXR settings had ended up with *two* instances of that feature,
   and the one the lookup found was disabled, so the hook never ran.
   Fixed: the setup tool now enables every instance and verifies through Meta's
   own lookup.

3. **The Android manifest had no hand-tracking, scene or anchor permissions.**
   `OVRManifestPreprocessor.GenerateManifestForSubmission()` puts up a
   confirmation dialog, and in batch mode `EditorUtility.DisplayDialog` returns
   false — so it silently did nothing while reporting success. The APK built
   fine and hand tracking would simply not have worked on device.
   Fixed: uses `GenerateOrUpdateAndroidManifest(silentMode: true)` and reads the
   manifest back to confirm the entries landed.

Start with the **Morning checklist** at the bottom.

## WORKING — verified by running it

### Backend — 54/54 tests pass

```bash
cd backend && .venv/bin/python -m pytest
# 54 passed in 0.17s
```

- `GET /health`, `POST /ask`, `POST /analyze`, `POST /clear`, `GET /scenarios`
- 8 deterministic scenarios; same input always gives the same answer
- Runs with **no API keys**
- Keyword routing ("my LED won't turn on" → `led_not_working`)
- Malformed input → 422 with a typed error envelope
- Provider failure → degrades to the offline scenario, still HTTP 200
- Optional OpenAI (reasoning) and Huawei OMNI (perception) adapters, both
  lazily imported so the service starts and tests pass with neither installed

### Unity — 48/48 EditMode tests pass

```
unity tests: total=48 passed=48 failed=0 result=Passed
```

Covering the `SpatialAction` protocol, the JSON parser against real backend
payloads and every malformed shape (HTML error pages, bare arrays, NaN
coordinates, short vectors, unknown types, the backend's error envelope), and
the on-device demo scenarios. `DemoScenarioTests` also pins the scenario id
list against the backend's, so the two hand-maintained copies cannot drift
silently.

Run them with:

```bash
/Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -runTests -projectPath Unity \
  -testPlatform EditMode -testResults results.xml -logFile tests.log
```

### Android — a real APK, verified

```
[SpatialDebugger] BUILD Succeeded | errors=0 ... SpatialDebugger.apk
```

62 MB, at `Unity/Build/Android/SpatialDebugger.apk` (gitignored). Contents
checked with `aapt2`:

- package `com.htn2026.spatialdebugger`
- exactly **one** `libopenxr_loader.so` (the duplicate-loader bug is gone)
- `libOVRPlugin.so`, `libUnityOpenXR.so`, `libUnityOpenXRHands.so`,
  `libmrutilitykitshared.so`, `libInteractionSdk.so`, ARM64 only
- `com.oculus.permission.HAND_TRACKING`, `com.oculus.permission.USE_SCENE`,
  `com.oculus.permission.USE_ANCHOR_API`
- `oculus.software.handtracking`, `com.oculus.feature.PASSTHROUGH`,
  `android.hardware.vr.headtracking`
- `com.oculus.intent.category.VR` launch category

**This APK has never been run on a headset.** It is a verified *build*, not a
verified *experience*.

### Unity — compiles clean in batch mode

Verified repeatedly through a headless compile loop against a clone of the
project (the Editor was open on the real one all night, and two Unity
processes cannot share a project path).

```
UNITY_EXIT=0
=== compile errors ===
=== end ===
```

- 390 assemblies build, zero errors, with `com.unity.xr.openxr` 1.18.0 added

### Editor tooling — runs headlessly, end to end

`Tools > SpatialDebugger > 1. Configure Project` — actual output from a
headless run against an already-configured project (note every line reports
"already", i.e. it is genuinely idempotent):

```
config asset: already present
TMP resources: not imported (text falls back to legacy TextMesh and still renders)
shaders: already pinned
player settings: already correct
XR loader: OpenXR already active for Android
OpenXR features: Meta XR feature set already enabled; MetaXRFeature: already enabled;
                 OculusTouchControllerProfile: already enabled;
                 verified: Meta's own lookup resolves an enabled MetaXRFeature
Meta project config: already correct
AndroidManifest: updated; verified: hand tracking, scene, anchor and VR entries all present
```

Both the OpenXR-feature step and the manifest step now **read their work back
and report what they actually see**, rather than trusting that the API did what
it said. Both of those verifications exist because the un-verified versions
reported success while silently doing nothing.

`Tools > SpatialDebugger > 2. Build MR Scene` — actual output, zero warnings:

```
light: directional, no shadows
camera rig: instantiated Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab
OVRManager: passthrough on, floor-level tracking origin
passthrough layer: added to the rig root (underlay)
centre-eye camera: clears to transparent, tagged MainCamera
hands: OVRHandLeft attached to TrackingSpace/LeftHandAnchor
hands: OVRHandRight attached to TrackingSpace/RightHandAnchor
systems: AIClient, dispatcher, target controller, UI driver, demo analysis
pointer sources: hand, controller, editor mouse, head gaze
panel: world-space control panel, follows the viewer
editor test surface: stand-in breadboard at (0.00, 0.90, 0.90) (Editor only)
saved Assets/Scenes/SpatialDebugger.unity and set it as build scene 0
```

`Tools > SpatialDebugger > 3. Build Android APK` — builds the verified APK
described above.

### Code that exists and compiles

| Area | Files |
|------|-------|
| Protocol | `Core/SpatialAction.cs`, `Core/SpatialTarget.cs`, `Core/SpatialDebuggerConfig.cs` |
| Backend client | `AI/AIClient.cs`, `AI/AIRequest.cs`, `AI/AIResponse.cs` |
| Annotations | `Annotations/SpatialActionDispatcher.cs` + Label/Warning/Marker/Arrow/Highlight renderers, `AnnotationVisuals.cs`, `SpatialText.cs` |
| Interaction | `IPointerSource`, `SpatialRaycaster`, `MrukSurfaceProbe`, `SpatialTargetController`, 4 pointer sources |
| UI | `SpatialButton`, `SpatialUIDriver`, `SpatialDebuggerPanel` |
| Demo | `DemoScenarios`, `DemoAnalysis`, `EditorOnlyProp` |
| Editor | `SpatialDebuggerProjectSetup`, `SpatialDebuggerSceneSetup`, `SpatialDebuggerBuild` |

---

## NEEDS DEVICE TEST

**Nothing below has been run on a Quest.** The code compiles and the scene is
correctly wired according to the installed SDK source, but none of it has met
real hardware.

### Critical — check these first

1. **Does the app enter XR at all?** This is the big one. If it launches flat
   and 2D, XR Management did not start. Check `Project Settings > XR Plug-in
   Management > Android` shows **OpenXR** ticked, and that under **OpenXR** the
   **Meta XR** feature group and **Oculus Touch Controller Profile** are ticked.
2. **Passthrough.** Should appear automatically. If you get a black void
   instead of your room, the centre-eye camera's background alpha is not 0 — the
   setup tool sets it, but a prefab override could undo it.
3. **Hand tracking and pinch.** `OVRHand.PointerPose` + index pinch drives
   targeting. Hand tracking must also be enabled in the *headset's* system
   settings, not just in the project.
4. **Is the reticle where you're actually pointing?** Hand ray origin/direction
   conventions are the kind of thing that only shows up on device.

### Also unverified

- Whether `OVRHandPrefab` renders hands correctly with `XRHandLeft`/`XRHandRight`
  skeleton and mesh types (this project's `OVRRuntimeSettings` selects the
  OpenXR hand skeleton, `handSkeletonVersion: 1`). If hands are invisible or
  mangled, try `HandLeft`/`HandRight` instead — pointing and pinching will still
  work either way, since `OVRHand` polls `OVRPlugin` directly.
- Whether the world-space panel sits at a comfortable distance and its buttons
  are hittable with a pinch. Tune `followDistance` / `followDrop` on
  `SpatialDebuggerPanel`.
- Whether annotation text is legible at breadboard scale. `annotationScale` on
  the config asset scales everything at once. Note text currently renders
  through the legacy `TextMesh` fallback unless you import TMP Essentials.
- Whether the Quest can reach the Mac's uvicorn over the venue Wi-Fi.
- `EnvironmentRaycastManager` (depth-based surface hits) — device-only, and not
  in the scene by default.
- MRUK scene surfaces — not in the scene by default; see Known issues.

---

## NOT IMPLEMENTED

- **Computer vision / passthrough camera capture.** Deliberately out of scope
  for the night, per the brief. `ARCHITECTURE.md` documents the intended
  pipeline and which installed Meta XR APIs would support each step.
- **Live sponsor APIs.** OMNI and OpenAI are adapters behind interfaces with
  mock implementations. Neither has been called for real.
- **Speech in and out.** `AIResponse.speech` is displayed as text on the panel,
  not spoken. The Voice SDK is installed but unused.
- **Persisting annotations across sessions** (spatial anchors).
- **Free-text question entry in-headset.** "Ask AI" sends a fixed analyse
  request. There is no keyboard.

---

## KNOWN ISSUES

1. **The generated scene has not been opened by a human.** It was built and
   saved headlessly with zero warnings, every serialized field it writes was
   found, and it builds into a working APK — but nobody has looked at it in the
   Editor.

2. **TextMeshPro Essential Resources are not imported.** The import is async and
   cannot be driven from batch mode with `-quit`; both
   `TMP_PackageResourceImporter.ImportResources` and
   `AssetDatabase.ImportPackage` fail silently there. **This is not fatal** —
   `SpatialText` falls back to Unity's legacy `TextMesh` with a built-in font,
   so all annotation text still renders, just less prettily and without word
   wrapping (the panel's speech line is hard-wrapped in code to compensate).
   For nicer text: `Window > TextMeshPro > Import TMP Essential Resources`, one
   click, then re-run `1. Configure Project`.

3. **`ProjectSettings/` may have been overwritten.** Your Unity Editor was open
   on this project all night, and an open Editor can write `ProjectSettings` out
   from memory. If anything looks unconfigured, just run
   `1. Configure Project` — it is idempotent and takes seconds, and it now
   verifies its own work.

4. **Two overlapping git repositories.** `SpatialDebugger/.git` tracks
   `Unity/**` directly, *and* `Unity/.git` exists as a separate repo pointing at
   a different remote (`YheChen/Unity.git`). All work tonight went into the
   outer repo, which matches the remote you described. The inner one was left
   alone — but committing from inside `Unity/` hits a different repository.

5. **MRUK is deliberately not in the scene.** It needs Space Setup to have been
   run on the headset; its default prefab silently loads a *synthetic* room when
   there is no real scan (so things look like they work but annotations do not
   line up with anything); and `LoadSceneFromDevice` can pull the user into the
   system Space Setup flow mid-demo. `sceneSupport` and `anchorSupport` are now
   enabled and the permissions are in the manifest, so you *can* add it — but
   the physics-plus-projection fallback always works and needs nothing.

6. **The Unity and Python copies of the scenarios are duplicated by hand**
   (`Demo/DemoScenarios.cs` and `backend/app/scenarios.py`). A test on each side
   pins the id list, so drift is caught, but it is not generated.

7. **`applicationIdentifier` is now `com.htn2026.spatialdebugger`.** If you
   already have a build on the headset from the URP template default, they will
   not overwrite each other.

8. **The shader-pruning path in the setup tool is defensive and unexercised.**
   It removes `HideFlags.DontSave` shaders from Always Included Shaders (which
   otherwise fail the player build). The "don't add them in the first place"
   path is verified by a successful build; the removal path is not.

9. **`SampleScene.unity` is still in build settings** alongside
   `SpatialDebugger.unity`. Harmless — `SpatialDebugger.unity` is scene 0 and
   loads first — but you can remove it from `File > Build Settings`.

## Key findings from reading the installed SDK

Recorded because they contradict most tutorials and cost real time to establish.
All of these were confirmed against the installed package source, and several
were caught by the compile loop after an initial claim turned out to be wrong.

- **Meta XR SDK v205 ships no `XRLoader`.** It provides `Meta.XR.MetaXRFeature`
  (`com.meta.openxr.feature.metaxr`), an OpenXR *feature*, compiled only under
  `#if USING_XR_SDK_OPENXR`. Without `com.unity.xr.openxr` installed, that
  define never fires and none of it exists.
- **`com.unity.xr.oculus` is not an option** — the editor's own package manifest
  marks 4.5.5 as "no longer supported on this editor version". OpenXR 1.18.0 is
  the bundled, supported choice for 6000.6.2f1.
- **`OVRHand.HandType` is `internal`** to the Oculus.VR assembly. You cannot set
  handedness by assignment from project code; the setup tool writes it through
  `SerializedObject`.
- **`OVRProjectConfig` is in an Editor-only assembly.** Runtime code cannot
  reference it.
- **Building Block prefabs live under `Editor/` folders** and are excluded from
  player builds — a runtime `[SerializeField]` reference to one is null on
  device. Use the runtime prefabs (`Prefabs/OVRCameraRig.prefab`,
  `Prefabs/OVRHandPrefab.prefab`).
- **`EnvironmentRaycastManager.IsReady` is private.** The only honest readiness
  check is to cast and inspect `EnvironmentRaycastHit.status`.
- **`EnvironmentRaycastManager` is in namespace `Meta.XR`**, not
  `Meta.XR.MRUtilityKit`, despite shipping in the MRUK package.
- **MRUK's `RaycastHit` has no collider populated** — only `point`, `normal` and
  `distance` are meaningful.
- **`LabelFilter.Included()` / `.Excluded()` are obsolete** in v205; use
  `new LabelFilter(flags)`. A default `new LabelFilter()` allows everything.
- **`OVRPassthroughLayer.overlayType` is obsolete** — underlay is becoming the
  only mode. It already defaults to `Underlay`, so leave it alone.
- **Passthrough requires the centre-eye camera to clear to a fully transparent
  colour** under URP. Get this wrong and you see a black void — and it looks
  fine in the Editor.
- **`TMP_Text.enableWordWrapping` is obsolete**; use `textWrappingMode`.
- **`OVRManifestPreprocessor.GenerateManifestForSubmission()` is a trap in batch
  mode.** It calls the internal with `silentMode: false`, which shows a
  "replace the existing manifest?" dialog; `EditorUtility.DisplayDialog` returns
  false in batch, so it silently does nothing and looks like it worked. Use
  `GenerateOrUpdateAndroidManifest(silentMode: true)`.
- **OpenXR settings can hold two instances of the same feature for one build
  target.** Meta resolves `MetaXRFeature` via
  `FeatureHelpers.GetFeatureWithIdForBuildTarget`, so enabling a *different*
  instance achieves nothing. Enable every instance of the type.
- **Unity's OpenXR plugin and Meta's OVRPlugin both ship
  `libopenxr_loader.so`.** Meta's `OVRGradleGeneration.OnPreprocessBuild`
  disables Unity's copy — but only when it resolves an *enabled* `MetaXRFeature`,
  and it early-returns from `IsUsingMetaCoreSDK()` otherwise. Symptom is a
  Gradle failure at `mergeReleaseNativeLibs`, five minutes into the build.
- **Built-in shaders from `Library/unity default resources` (e.g.
  `GUI/Text Shader`) cannot be added to Always Included Shaders.** They carry
  `HideFlags.DontSave` and fail the player build with "An asset is marked with
  HideFlags.DontSave but is included in the build". Shaders from
  `Resources/unity_builtin_extra` (`Unlit/Color`, `Sprites/Default`) are fine.
- **TMP Essential Resources cannot be imported from batch mode with `-quit`.**
  Both `TMP_PackageResourceImporter.ImportResources` and
  `AssetDatabase.ImportPackage` are async and get torn down. Hence the legacy
  `TextMesh` fallback in `SpatialText`.
- **`UnityEngine.TextRenderingModule` IS available here**, so legacy `TextMesh`
  and `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` work — which is
  what makes annotation text independent of TMP.
- **`-quit` makes a failed `BuildPipeline.BuildPlayer` exit 0.** The build
  script calls `EditorApplication.Exit(1)` itself on failure, otherwise CI and
  shell scripts read a failed build as a success.
- **This project is Input-System-only** (`activeInputHandler: 1`), so
  `UnityEngine.Input` throws at runtime and Meta's `OVRInputModule` (which is
  `ENABLE_LEGACY_INPUT_MANAGER`-gated) does not exist.

---

## How the overnight verification loop worked

The Unity Editor was open on the project all night, and two Unity processes
cannot open the same project path. So the compile loop ran against an APFS
clone of the project in a scratch directory (`cp -Rc`, ~8 seconds, copy-on-write
so it costs almost no disk), with source synced in before each run. That clone
is throwaway — it is not part of the repository.

If you want to rebuild that loop:

```bash
cp -Rc Unity /tmp/UnityShadow
rm -rf /tmp/UnityShadow/Temp /tmp/UnityShadow/.git
rsync -a Unity/Assets/ /tmp/UnityShadow/Assets/   # before each run
/Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit -projectPath /tmp/UnityShadow -logFile /tmp/u.log
grep -E "error CS[0-9]+" /tmp/u.log
```

---

## MORNING CHECKLIST

Roughly in order. Steps 1–4 need no headset.

### 1. Start the backend (1 min)

```bash
cd backend && .venv/bin/python -m pytest        # expect: 54 passed
.venv/bin/python -m uvicorn app.main:app --host 0.0.0.0 --port 8000
```

Then note your Mac's LAN address — you will need it in the headset:

```bash
ipconfig getifaddr en0
```

### 2. Open the Unity project and run the tools (2 min)

Open `Unity/` in Unity **6000.6.2f1**. It will reimport for a minute.

Then, in order:

1. `Window > TextMeshPro > Import TMP Essential Resources` — optional, makes
   the text look much better.
2. `Tools > SpatialDebugger > 1. Configure Project`
3. `Tools > SpatialDebugger > 2. Build MR Scene`

Read the Console output of step 2. Every line should say "already" or
"verified". If any line says **VERIFY FAILED** or **NOT FOUND**, stop and read
it — it tells you exactly what to fix.

### 3. Try the whole pipeline in Play mode, no headset (3 min)

Open `Assets/Scenes/SpatialDebugger.unity` and press Play.

- There is a stand-in breadboard in front of the camera (Editor only).
- Point the **mouse** at it and **click** → a green selection bracket appears.
- On the floating panel, click **Demo Analysis**.
- You should get: a red marker and highlight on the point you clicked, a
  "GPIO 12" label, a red **✕ Possible incorrect connection** warning above it,
  and an amber arrow pointing down into the point.
- Click **Next Scenario** to cycle through all 8.
- Click **Clear** to remove them.

If that works, the entire `target → reasoning → annotation` pipeline is
working. That is the demo.

Then set the backend address and try the live path: select the
`SpatialDebuggerSystems` object, put your Mac's LAN IP in `AIClient`'s
**Host Override**, press Play, and click **Ask AI**. The panel's status line
shows `Backend: online` when it connects.

### 4. Put it on the headset

There is a ready APK at `Unity/Build/Android/SpatialDebugger.apk`:

```bash
adb install -r Unity/Build/Android/SpatialDebugger.apk
```

Or rebuild from the Editor: `Tools > SpatialDebugger > 3. Build Android APK`.

### 5. On the Quest — in this order

This is where the unverified things live. Check them in this order, because
each one depends on the last.

1. **Does it launch into XR?** If you see a flat 2D window, XR did not start —
   check `Project Settings > XR Plug-in Management > Android` has **OpenXR**
   ticked, and under **OpenXR** that **Meta XR** feature group is ticked.
2. **Do you see your room?** Passthrough should be on. A black void means the
   centre-eye camera's background alpha is not 0.
3. **Is hand tracking on in the headset's own settings?** The app permission is
   in the manifest, but the system setting is separate.
4. **Does the panel appear?** It follows you at ~0.75 m, 0.25 m below eye level.
5. **Can you hit a button with a pinch?** Look at a button, pinch index to
   thumb. It should light up on hover.
6. **Press "Target In Front"** — this places a target with no pointing at all.
   Then press **Demo Analysis**. If annotations appear, everything downstream
   works and any remaining problem is in targeting, not in the pipeline.
7. **Now try pointing.** Point at your real breadboard and pinch. The reticle
   should sit where you are pointing.

If the ray is offset or the reticle jitters, tune `MetaHandPointerSource`
(`requireHighConfidence`) and `SpatialRaycaster` (`fallbackDistance`).

If annotations are too small or too large, change `annotationScale` on
`Assets/SpatialDebugger/Resources/SpatialDebuggerConfig.asset` — it scales
everything at once.

### If the demo has to happen in 60 seconds

Press **Target In Front**, then **Demo Analysis**. No network, no CV, no
pointing accuracy, no sponsor APIs. That path is the one with the fewest moving
parts and it is the one to fall back to.
