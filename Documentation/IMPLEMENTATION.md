# Seabright implementation

**Version 0.2.1 · Unity 6000.6.0f1 · built-in render pipeline**

Seabright is a runtime-generated Unity/C# project with no external gameplay code packages. The verified distribution is a native macOS Apple Silicon app using the Mono scripting backend. The minimal scene boots `CityGame`; architecture, terrain, interface and sound are assembled from code and Resources assets. Other platforms are possible Unity targets, but have not been built or tested here.

## Build and verification commands

Run these from the repository root with Unity 6000.6.0f1 and macOS build support installed. The helper's default editor location is the standard Unity Hub path; override `UNITY_EDITOR` if needed. Close another Unity editor instance using this project before invoking batch mode.

```sh
bash Tools/build.sh validate   # Simulation acceptance only
bash Tools/build.sh build      # Acceptance, native build, local signing and verification
bash Tools/build.sh package    # Native build and signing without repeating acceptance
bash Tools/playtest.sh         # Standalone starter-to-stadium scenario
bash Tools/audio-playtest.sh   # Standalone audio output and control checks
python3 Tools/package.py       # ZIP archives and integrity/provenance manifest
bash Tools/capture-review.sh   # Fresh review images from the existing growth fixture
```

`build.sh package` rebuilds the app; `package.py` creates the downloadable archives from existing tested output. The packager requires passing reports and a locally verified app signature. Builds and reports appear under `Builds/` and `Artifacts/`. The app is ad-hoc signed for local use, without Developer ID notarization. Standalone playtests need a graphical session; audio tests also need an available Unity audio output path.

The audio generator is optional for ordinary builds: all source WAVs are committed. Regenerate them with `python3 Tools/generate_audio.py` using Python 3 and NumPy. It uses seed `54821`; floating-point differences between dependency/platform versions may change final PCM bytes.

## Module map

| Area | Source | Responsibility |
|---|---|---|
| Runtime coordinator | `Assets/Scripts/CityGame.cs` | Boot, clock, shared pointer actions, tool state, new city, save/load, audio feedback |
| Simulation | `Assets/Scripts/Simulation/CitySimulation.cs` | Grid, transactions, road connectivity, growth, demand, utilities, budget, milestones and save validation |
| Camera and picking | `CityCamera.cs`, `CityLot.cs` | Smoothed orbit/translation/zoom; per-property coordinate colliders |
| Interface | `Assets/Scripts/UI/CityHUD.cs` | Management panels, cached icon masks, overlays, inspector, modal input and audio settings |
| Presentation | `Assets/Scripts/Presentation/CityView.cs` and partial files | Ordinary buildings, modern architecture, stadiums, terrain and detail placement |
| Geometry and effects | `MeshBatches.cs`, `CityAtmosphere.cs`, `CitySteam.cs` | Material batches, post-processing and industrial effects |
| Moving agents | `CityTraffic.cs`, `CityPedestrians.cs` | Instanced vehicles and animated walkers on roadside routes |
| Audio | `CityAudio.cs`, `Assets/Resources/Audio/` | Loop mixing, pooled effects, reactive ambience and persistent player preferences |
| Runtime acceptance | `CityRuntimePlaytest.cs`, `CityAudioPlaytest.cs`, `AudioOutputProbe.cs` | Gameplay input scenario, image readbacks, bounded audio measurements and report writing |
| Editor tooling | `Assets/Editor/BuildPipelineTasks.cs`, `ReviewCaptureTasks.cs`, importer scripts | Review images, simulation acceptance, scene/build preparation, readable model import and lossless audio import |
| Materials and models | `Assets/Resources/*.shader`, `Assets/ThirdParty/` | Runtime shader references, licensed source models/materials and provenance |

## State and input contracts

`CitySimulation` owns the 48 × 48 tile array and all financial/progression state. `Tick(days)` consumes fixed 0.1-day steps; `CityGame` supplies real-time advances. `Build`, `BuildRoad` and `Bulldoze` validate complete edits before charging or mutating tiles. `Recalculate` derives connectivity, service allocation, pollution, jobs, happiness, demand and operating results. Road connectivity and paths use breadth-first searches; traffic pressure samples residential-to-work routes.

`TileKind` values 0–8 remain compatible with the first save format; `HighResidential=9`, `Office=10` and `Stadium=11` are appended. `PeakPopulation`, `IsUnlocked(kind)` and `UnlockRequirement(kind)` expose permanent milestones. A stadium's nine tiles store a shared center. `GetAnchor(tile)` resolves selection, and `IsFootprintAnchor(tile)` prevents duplicated geometry, jobs, utility demand or upkeep.

Save version 2 records tile state, money, clock, tax, peak population and anchors. Loading checks schema, dimensions, finite values, capacities and complete landmark reservations before committing. Version-1 saves migrate in memory. Runtime Load cancels unfinished placement and clears selection; New City reuses the simulation instance so presentation references remain valid. Normal saves use Unity's persistent-data directory and filename `seabright-city.json`; diagnostics and both test modes use `Artifacts/playtest-save.json` instead.

`CityGame.ProcessPointer` is the common construction path for normal play and runtime acceptance. A stroke must start over valid world space; roads commit on release. Inspection and demolition pick layer-6 `CityLot` colliders, including visible roofs, before falling back to the ground plane. HUD/modal hit testing blocks edits behind controls. Save returns a success value so Save & Start cannot discard the active city after a failed write.

## Rendering and audio details

`CityView.Refresh` follows simulation revisions but rebuilds architecture only when tile kinds or levels change. `MeshBatches` groups geometry by material, and runtime property colliders are rebuilt with the visual footprint. Surface shaders use consistent world-space scales, normal/roughness maps and generated tangents. The project uses linear color, HDR, shadows, MSAA and restrained bloom/vignette.

Cars and walkers are GPU-instanced on separate layers, 8 and 9. **Keep instancing shader variants enabled in GraphicsSettings:** their materials are created at runtime, so Unity cannot infer all required variants from the minimal scene. Earlier variants were stripped despite valid agent positions; actual rendered-pixel checks now cover that failure. The requirement follows [Unity's instancing documentation](https://docs.unity3d.com/6000.0/Documentation/Manual/gpu-instancing-strip-variants.html).

Icons use cached antialiased texture masks. This avoids the old per-line GUI rotations that displaced icon strokes when the window scale changed. The UI remains immediate-mode GUI rather than a reusable UI Toolkit component system.

`CityAudio` owns five permanent 2D loops and eight reused effect sources. Unscaled crossfades follow scene context; music ducks for milestones, while simulation speed never repitches it. Revision changes gate construction sounds, and level/population snapshots detect development and milestones. Resetting those snapshots on new/load prevents cue bursts. Master/mute operate on listener gain; category changes affect already-playing sources. Debounced PlayerPrefs writes flush on focus loss/quit, and test modes disable persistence.

The 15 source files are 44.1 kHz stereo 16-bit PCM, approximately 27 MB in total. Lossless, preloaded DecompressOnLoad preserves generated loop seams at a modest memory cost. Source tails wrap around a circular timeline, loop endpoints match, one-shots fade to zero and the measured source files do not clip. The opt-in audio probe copies listener samples into preallocated memory; the main thread computes statistics and writes WAV. Ordinary gameplay has no capture component.

## Verified status and limits of the evidence

These are retained executions from **9 September 2026**, not a claim that every scenario was rerun on the latest binary. See [Review](REVIEW.md) for image review and the comparison to the design target.

| Record | Version and result | Scope |
|---|---|---|
| [Simulation acceptance](../Artifacts/simulation-acceptance.json) | 15/15 passed in Unity; recorded before the audio revision | Atomic roads, outages/recovery, paths, saves, finance, 120-day consistency, starter progression and stadium reservations |
| [Growth runtime](../Artifacts/runtime-playtest.json) | **0.2.0**, 33/33 passed; zero errors/warnings | Real world-input purchases; first development at 1×; later waiting compressed through `Tick`; no injected funds/population/levels/unlocks |
| [Audio runtime](../Artifacts/audio-playtest.json) | **0.2.1**, 36/36 passed; zero errors/warnings | Live action feedback, category gains, mute, modal behavior and actual listener PCM |
| [Build](../Artifacts/build-result.json) | **0.2.1**, succeeded with zero errors/warnings | Native ARM64 payload, 121,569,985 bytes before archive compression |

The retained growth journey reached **24 → 609 residents**, with offices on day 7, towers on day 12 and a supplied stadium on day 20. First homes, shops and industry developed in 17.1 real seconds at 1×. Lowest treasury was $11,590; the completed city had $17,307 and approximately +$197/day net income. Actual agent pixels and movement, roof picking/demolition, load cancellation, exact save restoration and window resizing were checked.

The active-city sample measured 118.2 mean FPS and 9.03 ms p95 on Apple M5 Pro/Metal at 1600 × 1000; its slowest frame was 66.35 ms. This five-second sample does not establish maximum-city or long-session performance. The audio revision left simulation/growth/rendering algorithms unchanged, so the full growth scenario was retained rather than repeated. Current screenshots alone do not constitute a new growth run.

The 0.2.1 listener recording is 12 seconds of stereo 48 kHz PCM, with RMS 0.058016, peak 0.422362, zero clipped samples and no non-finite samples. Mute and all-category-zero tests measured zero output. These measurements validate the mixer; there was no subjective speaker-listening evaluation. Computer-control mouse coordinates were offset in Unity on the test Retina display, so programmatic world input and public panel methods are identified explicitly. They do not prove successful native slider dragging or every GUI click path.

## Assets and redistribution records

- **Kenney, CC0:** Commercial 2.1, Suburban 2.0, Industrial 2.0, Car Kit 3.1 and Watercraft Kit 2.1. Pack licenses and original download records remain in [Kenney licenses](../Assets/ThirdParty/Kenney/Licenses/) and [provenance](../Assets/ThirdParty/Kenney/PROVENANCE.json).
- **Poly Haven, CC0:** `grass_path_2`, `leafy_grass`, `asphalt_02`, `concrete_wall_008` and `red_brick_03`, using 1K diffuse, OpenGL normal and roughness maps. Provider MD5 values were checked; authors, source URLs and local SHA-256 hashes are in [provenance](../Assets/ThirdParty/PolyHaven/PROVENANCE.json), with the [asset license](../Assets/ThirdParty/PolyHaven/LICENSE.txt).
- **Liberation Sans:** the interface font retains its SIL Open Font License. The full text and third-party material notices are included in [StreamingAssets notices](../Assets/StreamingAssets/THIRD_PARTY_NOTICES.txt), which are bundled with the app.
- **Original project work:** procedural architecture/details, shaders, interface and audio source generation. Audio uses no external samples or downloaded songs; [source metrics](../Assets/Resources/Audio/audio_metrics.json) accompany the generator and WAV files. No Mesh AI key or credits were used.

## Prioritized next steps

1. **Refine ordinary play with observed user sessions.** Exercise the new-city decision, failed-save path, budget and sound sliders, tool switching and camera at common window scales. Resolve the Retina automation coordinate mismatch before treating native GUI automation as coverage; add tests only for changed or failing behavior.
2. **Reduce growth rebuild hitches.** Profile the full-map recalculation and whole-city mesh/collider rebuilds, then partition geometry into dirty chunks and reuse simulation scratch storage. Accept improvements against active-city p95/worst-frame samples and memory growth, not only an average FPS number.
3. **Deepen services and explain tradeoffs.** Make the renewable power presentation consistent with its current pollution model, expose clearer causes of unmet demand, and add a small progression tier with a testable budget effect. Preserve an affordable starter-to-landmark route without grants masking persistent operating losses.
4. **Improve street behavior before expanding the network tools.** Add intersection waiting and basic car separation to the existing routes, then explore curved roads and pedestrian crossings. Keep rendered-pixel and movement checks when replacing instancing or agent paths.
5. **Expand architecture and landmark activity.** Add a few materially different house/shop variants and better stadium scale, entrance activity and scheduled events. Validate silhouettes and material scale in actual street/overview captures; model counts alone are not a quality measure.
6. **Harden distribution and long-term saves.** Add multiple named save slots, explicit migration fixtures and a longer varied-city soak test. Build and verify another target before advertising support; Developer ID signing/notarization is separate release work for broad macOS distribution.
