using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Seabright.Editor
{
    /// <summary>Dependency-free acceptance checks and reproducible standalone build entry points.</summary>
    public static class BuildPipelineTasks
    {
        const string ScenePath = "Assets/Scenes/Seabright.unity";
        const string ReportPath = "Artifacts/simulation-acceptance.json";

        [Serializable] public sealed class CheckResult
        {
            public string name;
            public bool passed;
            public string detail;
            public double milliseconds;
        }

        [Serializable] public sealed class AcceptanceReport
        {
            public string recordedAtUtc;
            public string unityVersion;
            public string platform;
            public bool passed;
            public int checksPassed;
            public int checksTotal;
            public int seededPopulation;
            public int seededJobs;
            public float seededNetIncome;
            public float seededPowerUse;
            public float seededWaterUse;
            public List<CheckResult> checks = new List<CheckResult>();
        }

        [Serializable] sealed class BuildResultRecord
        {
            public string recordedAtUtc;
            public string unityVersion;
            public string result;
            public string outputPath;
            public int errors;
            public int warnings;
            public long bytes;
            public double seconds;
        }

        [MenuItem("Seabright/Validate Simulation")]
        public static void Validate()
        {
            var report = RunAcceptance();
            Directory.CreateDirectory("Artifacts");
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            Debug.Log($"Seabright acceptance: {report.checksPassed}/{report.checksTotal} passed. {Path.GetFullPath(ReportPath)}");
            if (!report.passed)
                throw new InvalidOperationException("Simulation acceptance failed: " + string.Join("; ", report.checks.Where(c => !c.passed).Select(c => c.name + ": " + c.detail)));
        }

        [MenuItem("Seabright/Validate and Build macOS")]
        public static void ValidateAndBuild()
        {
            Validate();
            BuildStandalone();
        }

        public static void BuildStandalone()
        {
            PrepareScene();
            Directory.CreateDirectory("Builds");
            PlayerSettings.companyName = "Seabright Studio";
            PlayerSettings.productName = "Seabright";
            PlayerSettings.bundleVersion = "0.2.1";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 1000;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.seabright.citydemo");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetArchitecture(UnityEditor.Build.NamedBuildTarget.Standalone, 1); // Apple Silicon ARM64.
            var settings = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
            var input = settings.FindProperty("activeInputHandler") ?? settings.FindProperty("m_ActiveInputHandler");
            if (input != null) { input.intValue = 0; settings.ApplyModifiedPropertiesWithoutUndo(); }
            PrepareShaderReferences();
            var build = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Seabright.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            });
            var summary = build.summary;
            var record = new BuildResultRecord
            {
                recordedAtUtc = DateTime.UtcNow.ToString("o"), unityVersion = Application.unityVersion,
                result = summary.result.ToString(), outputPath = summary.outputPath,
                errors = (int)summary.totalErrors, warnings = (int)summary.totalWarnings,
                bytes = (long)summary.totalSize, seconds = summary.totalTime.TotalSeconds
            };
            File.WriteAllText("Artifacts/build-result.json", JsonUtility.ToJson(record, true));
            if (summary.result != BuildResult.Succeeded || summary.totalErrors > 0)
                throw new InvalidOperationException("Standalone build failed: " + summary.result + "; errors: " + summary.totalErrors);
            Debug.Log("Seabright standalone built: " + Path.GetFullPath("Builds/Seabright.app"));
        }

        [MenuItem("Seabright/Build Web")]
        public static void BuildWeb()
        {
            PrepareScene(); PrepareShaderReferences();
            Directory.CreateDirectory("Artifacts");
            PlayerSettings.companyName = "Seabright Studio";
            PlayerSettings.productName = "Seabright";
            PlayerSettings.bundleVersion = "0.2.1";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 1000;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.WebGL.template = "PROJECT:Seabright";
            // GitHub Pages cannot configure Content-Encoding per file; the loader handles gzip.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
            var build = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = "Builds/WebGL", target = BuildTarget.WebGL, options = BuildOptions.None
            });
            var summary = build.summary;
            File.WriteAllText("Artifacts/web-build-result.json", JsonUtility.ToJson(new BuildResultRecord {
                recordedAtUtc = DateTime.UtcNow.ToString("o"), unityVersion = Application.unityVersion,
                result = summary.result.ToString(), outputPath = summary.outputPath,
                errors = (int)summary.totalErrors, warnings = (int)summary.totalWarnings,
                bytes = (long)summary.totalSize, seconds = summary.totalTime.TotalSeconds
            }, true));
            if (summary.result != BuildResult.Succeeded || summary.totalErrors > 0)
                throw new InvalidOperationException("Web build failed: " + summary.result);
            File.WriteAllText("Builds/WebGL/.nojekyll", "");
            Debug.Log("SEABRIGHT_WEB_BUILD_COMPLETE Builds/WebGL");
        }

        [MenuItem("Seabright/Prepare Starter Scene")]
        public static void PrepareScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }

        static void PrepareShaderReferences()
        {
            // Runtime-created materials need asset references so their shaders survive player stripping.
            Directory.CreateDirectory("Assets/Resources/BuildReferences");
            foreach (var shaderName in new[] { "Standard", "Sprites/Default" })
            {
                var shader = Shader.Find(shaderName);
                if (shader == null) throw new InvalidOperationException("Required shader is missing: " + shaderName);
                string path = "Assets/Resources/BuildReferences/" + shaderName.Replace('/', '-') + ".mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(path) == null)
                    AssetDatabase.CreateAsset(new Material(shader), path);
            }
            const string emissivePath = "Assets/Resources/BuildReferences/Standard-Emissive.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(emissivePath) == null)
            {
                var material = new Material(Shader.Find("Standard"));
                material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", Color.white);
                AssetDatabase.CreateAsset(material, emissivePath);
            }
            AssetDatabase.SaveAssets();
        }

        public static AcceptanceReport RunAcceptance()
        {
            var report = new AcceptanceReport
            {
                recordedAtUtc = DateTime.UtcNow.ToString("o"),
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString()
            };
            Check(report, "Populated seed is connected, supplied, and economically viable", () =>
            {
                var s = Seed();
                report.seededPopulation = s.Population; report.seededJobs = s.Jobs;
                report.seededNetIncome = s.Income - s.Expenses;
                report.seededPowerUse = s.PowerUse; report.seededWaterUse = s.WaterUse;
                Require(s.Tiles.Length == CitySimulation.Size * CitySimulation.Size, "Map size changed.");
                Require(s.Population >= 1500 && s.Population <= 2500, "Seed population outside designed 1500–2500 range: " + s.Population);
                Require(s.Jobs > 0, "Seed has no jobs.");
                Require(s.Income > s.Expenses, "Seed must provide an initially healthy economy.");
                Require(Mathf.Abs(s.Money - 100000) < .01f, "Seed must begin with 100,000 credits.");
                var occupied = s.Tiles.Where(t => t.Residents > 0 || t.Jobs > 0).ToArray();
                Require(occupied.All(t => t.Connected && t.Powered && t.Watered), "A populated seed tile is unserved.");
                Require(s.PowerUse <= s.PowerCapacity && s.WaterUse <= s.WaterCapacity, "Initial utility demand exceeds capacity.");
                return $"Population {s.Population}, jobs {s.Jobs}, daily net {s.Income - s.Expenses:0.00}; {occupied.Length} occupied tiles served.";
            });
            Check(report, "Road placement charges each new tile exactly once", () =>
            {
                var s = Empty(); float before = s.Money;
                Require(s.BuildRoad(3, 24, 12, 24, out var reason), reason);
                Require(Mathf.Abs((before - s.Money) - 10 * s.Cost(TileKind.Road)) < .01f, "Road cost does not match its ten new tiles.");
                Require(s.Get(12, 24).Connected, "Road does not connect to the outside gateway.");
                before = s.Money;
                s.BuildRoad(3, 24, 12, 24, out reason);
                Require(Mathf.Abs(before - s.Money) < .01f, "Redrawing an existing road charged money.");
                return "10 new road cells charged; redraw charges zero; endpoint connected.";
            });
            Check(report, "Unaffordable and water-crossing roads are rejected atomically", () =>
            {
                var s = Empty(); s.Money = s.Cost(TileKind.Road) * 2;
                var money = s.Money; int roadCount = Count(s, TileKind.Road);
                Require(!s.BuildRoad(3, 24, 12, 24, out var reason), "Unaffordable road was accepted.");
                Require(!string.IsNullOrEmpty(reason), "Rejected construction gave no explanation.");
                Require(s.Money == money && Count(s, TileKind.Road) == roadCount, "Failure left partial road construction or charged money.");
                s.Money = 100000;
                Require(!s.BuildRoad(32, 24, 47, 24, out reason), "Road through sea/out-of-land cells was accepted.");
                Require(s.Money == 100000 && Count(s, TileKind.Road) == roadCount, "Invalid land path was only partially rejected.");
                return "Both rejected paths preserved funds and every road cell.";
            });
            Check(report, "Road severing disconnects a neighborhood and reconnecting repairs it", () =>
            {
                var s = Supplied();
                Place(s, 12, 23, TileKind.Residential);
                Require(s.Get(12, 23).Connected, "Connected frontage was not recognized.");
                Require(s.Bulldoze(7, 24, out var reason), reason);
                s.Recalculate();
                Require(!s.Get(12, 23).Connected, "Isolated neighborhood still has outside access.");
                Place(s, 7, 24, TileKind.Road); s.Recalculate();
                Require(s.Get(12, 23).Connected && s.Get(12, 23).Powered && s.Get(12, 23).Watered, "Reconnection did not recover neighborhood services.");
                return "Severed the only access road, observed disconnection, rebuilt and recovered access/services.";
            });
            Check(report, "New serviced housing develops while isolated zoning does not", () =>
            {
                var s = Supplied(); Place(s, 12, 23, TileKind.Residential);
                Place(s, 30, 10, TileKind.Residential);
                var connected = s.Get(12, 23); var isolated = s.Get(30, 10);
                Require(connected.Residents == 0, "Zoning must not create occupied housing instantly.");
                for (int day = 0; day < 30; day++) s.Tick(1f);
                Require(connected.Residents > 0 && connected.Level > 0, "Serviced housing did not grow within 30 days.");
                Require(isolated.Residents == 0 && !isolated.Connected, "Housing developed without outside road access.");
                return $"Served zone grew to level {connected.Level} with {connected.Residents} residents; isolated zone remained unoccupied.";
            });
            Check(report, "Power loss prevents development; rebuilding restores service", () => UtilityRecovery(TileKind.Power));
            Check(report, "Water loss prevents development; rebuilding restores service", () => UtilityRecovery(TileKind.Water));
            Check(report, "Computed road paths are continuous and respect broken links", () =>
            {
                var s = Empty();
                Require(s.BuildRoad(3, 24, 12, 24, out var reason), reason);
                Require(s.BuildRoad(12, 24, 16, 28, out reason), reason);
                var from = new Vector2Int(3, 24); var to = new Vector2Int(16, 28);
                var path = s.FindRoadPath(from, to);
                Require(path != null && path.Count == 18, "L-shaped connected route has wrong path length.");
                Require(path[0] == from && path[path.Count - 1] == to, "Route endpoints differ from requested endpoints.");
                for (int i = 0; i < path.Count; i++)
                {
                    Require(s.Get(path[i].x, path[i].y).Kind == TileKind.Road, "Route crosses a non-road tile.");
                    if (i > 0) Require(Mathf.Abs(path[i].x - path[i - 1].x) + Mathf.Abs(path[i].y - path[i - 1].y) == 1, "Route contains a diagonal/teleport step.");
                }
                Require(s.Bulldoze(7, 24, out reason), reason);
                path = s.FindRoadPath(from, to);
                Require(path == null || path.Count == 0, "Pathfinder crossed a severed link.");
                return "18-node cardinal route verified, then severed link made destination unreachable.";
            });
            Check(report, "Save and load preserve authored tiles, population, funds, date, and tax", () =>
            {
                var original = Seed(); original.TaxRate = 13;
                for (int day = 0; day < 5; day++) original.Tick(1);
                var save = original.SaveJson(); Require(!string.IsNullOrEmpty(save), "Save is empty.");
                var loaded = new CitySimulation(); loaded.LoadJson(save);
                Require(Mathf.Abs(original.Money - loaded.Money) < .05f && original.Day == loaded.Day, "Funds or day changed on load.");
                Require(original.TaxRate == loaded.TaxRate && original.Population == loaded.Population && original.Jobs == loaded.Jobs, "Tax/population/jobs changed on load.");
                Require(loaded.Tiles.Length == original.Tiles.Length, "Tile count changed.");
                for (int i = 0; i < original.Tiles.Length; i++)
                {
                    var a = original.Tiles[i]; var b = loaded.Tiles[i];
                    Require(a.X == b.X && a.Z == b.Z && a.Kind == b.Kind && a.Level == b.Level && a.Residents == b.Residents && a.Jobs == b.Jobs && Mathf.Abs(a.Growth - b.Growth) < .0001f,
                        "Persistent tile state changed at index " + i);
                }
                loaded.Tick(1);
                Require(loaded.Day > original.Day, "Loaded simulation cannot continue.");
                return $"Round-tripped {loaded.Tiles.Length} tiles, date, economy, tax and occupancy; loaded save advanced successfully.";
            });
            Check(report, "Malformed and incomplete saves are rejected without losing the active city", () =>
            {
                var s = Seed(); var original = s.SaveJson();
                foreach (var invalid in new[] { "", "{", "{}", "{\"Tiles\":[]}" })
                {
                    bool rejected = false;
                    try { s.LoadJson(invalid); } catch (Exception) { rejected = true; }
                    Require(rejected, "Invalid save was accepted: " + invalid);
                    Require(s.SaveJson() == original, "Rejected save damaged the active city's persistent state.");
                }
                return "Empty, malformed, missing-schema and empty-tile saves rejected transactionally.";
            });
            Check(report, "Higher taxes increase receipts and day progression books the operating budget", () =>
            {
                var low = Seed(); var high = Seed();
                low.TaxRate = 5; high.TaxRate = 17; low.Recalculate(); high.Recalculate();
                Require(high.Income > low.Income, "Tax change does not affect receipts.");
                var s = Seed(); var before = s.Money; var day = s.Day;
                s.Tick(1);
                Require(s.Day > day, "One-day tick did not advance the date.");
                Require(s.Money > before, "Healthy starting city's operating surplus did not enter the treasury.");
                return $"Tax receipts at 5%: {low.Income:0.00}; at 17%: {high.Income:0.00}; first-day treasury change {s.Money - before:0.00}.";
            });
            Check(report, "Four-month simulation remains finite and internally consistent", () =>
            {
                var s = Seed();
                for (int day = 0; day < 120; day++)
                {
                    s.Tick(1);
                    Require(Finite(s.Money) && Finite(s.Income) && Finite(s.Expenses), "Economy became non-finite on day " + day);
                    Require(s.Population == s.Tiles.Sum(t => t.Residents) && s.Jobs == s.Tiles.Sum(t => t.Jobs), "Aggregates do not match tiles on day " + day);
                    Require(s.Tiles.All(t => t.Residents >= 0 && t.Jobs >= 0 && t.Level >= 0 && Finite(t.Growth)), "Tile state corrupted on day " + day);
                    Require(s.Happiness >= 0 && s.Happiness <= 100 && s.TrafficFlow >= 0 && s.TrafficFlow <= 100, "City percentages escaped 0–100.");
                    Require(s.PowerUse >= 0 && s.WaterUse >= 0 && Finite(s.PowerUse) && Finite(s.WaterUse), "Utility metrics corrupted.");
                }
                return $"120 simulation days; final population {s.Population}, jobs {s.Jobs}, treasury {s.Money:0.00}; all daily invariants held.";
            });
            Check(report, "Starter town has room to grow and locked projects charge nothing", () =>
            {
                var s = new CitySimulation(); s.SeedStarterTown();
                Require(s.Population == 24 && s.Money == 30000, "Starter population or construction budget changed.");
                Require(s.Tiles.Count(t => t.Kind != TileKind.Empty) == 19, "Starter must be a small foothold, not a completed city.");
                Require(s.Tiles.Where(t => t.Residents > 0 || t.Jobs > 0).All(t => t.Connected && t.Powered && t.Watered), "Starter occupied buildings lack services.");
                string before = s.SaveJson();
                foreach (var kind in new[] { TileKind.Office, TileKind.HighResidential, TileKind.Stadium })
                {
                    Require(!s.IsUnlocked(kind) && !s.Build(20, 20, kind, out var reason), "A milestone project was available immediately.");
                    Require(s.SaveJson() == before, "Locked project altered city or treasury.");
                }
                Place(s, 14, 23, TileKind.Residential);
                s.Tick(2.5f);
                Require(s.Get(14, 23).Level == 1 && s.Get(14, 23).Residents > 0, "A serviced first home should emerge within about 20 real seconds at 1×.");
                return $"24 residents, 6 buildings, 13 roads, $30,000; locked projects atomic; first new home occupied after 2.5 simulation days.";
            });
            Check(report, "Unfunded starter grows through offices and towers to a viable stadium city", StarterGrowthJourney);
            Check(report, "Landmark footprints reject overlap and malformed saves transactionally", () =>
            {
                var s = Seed();
                Require(s.BuildRoad(13, 34, 13, 40, out var roadReason), roadReason);
                string before = s.SaveJson();
                Require(!s.Build(13, 37, TileKind.Stadium, out var overlap), "Stadium overlapped its road.");
                Require(s.SaveJson() == before, "Rejected stadium changed city state.");
                Place(s, 15, 37, TileKind.Stadium);
                var anchor = s.Get(15, 37);
                Require(s.Tiles.Count(t => t.Kind == TileKind.Stadium) == 9, "Stadium did not reserve all nine cells.");
                Require(s.GetAnchor(s.Get(14, 36)) == anchor, "Stadium corner did not resolve to its center.");
                before = s.SaveJson();
                var bad = new CitySimulation(); bad.LoadJson(before);
                bad.Get(14, 36).AnchorX++;
                bool rejected = false;
                try { s.LoadJson(bad.SaveJson()); } catch (ArgumentException) { rejected = true; }
                Require(rejected && s.SaveJson() == before, "Invalid stadium reservation was accepted or damaged active city.");
                Require(s.Bulldoze(14, 36, out var removed), removed);
                Require(s.Tiles.All(t => t.Kind != TileKind.Stadium), "Bulldozing a stadium corner left orphan reservations.");
                return "3×3 footprint prevents overlap, charges once, resolves corners to center, validates every saved reservation and clears atomically.";
            });
            report.checksTotal = report.checks.Count;
            report.checksPassed = report.checks.Count(c => c.passed);
            report.passed = report.checksPassed == report.checksTotal;
            return report;
        }

        static string StarterGrowthJourney()
        {
            var s = new CitySimulation(); s.SeedStarterTown();
            float lowestTreasury = s.Money;
            Require(s.BuildRoad(15, 18, 15, 30, out var reason), reason);
            Require(s.BuildRoad(15, 18, 26, 18, out reason), reason);
            Require(s.BuildRoad(15, 24, 26, 24, out reason), reason);
            Require(s.BuildRoad(15, 30, 26, 30, out reason), reason);
            Require(s.BuildRoad(26, 18, 26, 30, out reason), reason);
            Place(s, 22, 25, TileKind.Power); Place(s, 22, 23, TileKind.Water);
            Place(s, 20, 22, TileKind.Park); Place(s, 20, 27, TileKind.Park);
            for (int x = 16; x <= 21; x++)
                for (int z = 19; z <= 20; z++) Place(s, x, z, TileKind.Residential);
            AdvanceUntil(s, 150, 30, ref lowestTreasury);
            int officeDay = s.Day;
            Require(s.IsUnlocked(TileKind.Office), "150-resident office milestone did not unlock.");
            Place(s, 16, 23, TileKind.Office); Place(s, 17, 23, TileKind.Office);
            for (int x = 9; x <= 14; x++) Place(s, x, 26, TileKind.Industrial);
            for (int x = 16; x <= 21; x++)
                for (int z = 28; z <= 29; z++) Place(s, x, z, TileKind.Residential);
            AdvanceUntil(s, 350, 40, ref lowestTreasury);
            int towerDay = s.Day;
            Require(s.IsUnlocked(TileKind.HighResidential), "350-resident tower milestone did not unlock.");
            for (int x = 22; x <= 25; x++) Place(s, x, 19, TileKind.HighResidential);
            Place(s, 18, 23, TileKind.Office);
            AdvanceUntil(s, 600, 40, ref lowestTreasury);
            int stadiumDay = s.Day;
            Require(s.IsUnlocked(TileKind.Stadium), "600-resident stadium milestone did not unlock.");
            float beforeStadium = s.Money;
            Place(s, 22, 32, TileKind.Stadium);
            Require(Mathf.Abs(beforeStadium - s.Money - s.Cost(TileKind.Stadium)) < .05f, "Stadium was not charged exactly once.");
            Require(s.Tiles.Any(t => t.Kind == TileKind.HighResidential && t.Residents >= 32), "City reached 600 without occupied modern towers.");
            Require(s.Tiles.Any(t => t.Kind == TileKind.Office && t.Jobs >= 48), "Office district did not provide jobs.");
            var stadium = s.Get(22, 32);
            Require(stadium.Connected && stadium.Powered && stadium.Watered && stadium.Jobs == 64, "Stadium failed to operate with road and utilities.");
            Require(s.Income > s.Expenses, "Completed starter journey is not financially sustainable.");
            var saved = s.SaveJson(); var loaded = new CitySimulation(); loaded.LoadJson(saved);
            Require(loaded.SaveJson() == saved && loaded.IsUnlocked(TileKind.Stadium), "Growing city's landmark or milestone state changed on load.");
            foreach (var t in loaded.Tiles) if (CitySimulation.IsResidential(t.Kind)) t.Residents = 0;
            float funds = loaded.Money; loaded.Recalculate();
            Require(loaded.IsUnlocked(TileKind.Stadium) && loaded.Money == funds, "Population loss relocked earned projects or repeated grant money.");
            return $"No injected funds: 24→{s.Population} residents; offices day {officeDay}, towers day {towerDay}, stadium day {stadiumDay}; lowest treasury ${lowestTreasury:0}, final ${s.Money:0}, net +${s.Income - s.Expenses:0}/day; occupied towers, working offices, served 3×3 stadium and save round-trip verified.";
        }

        static void AdvanceUntil(CitySimulation s, int population, int maxDays, ref float lowestTreasury)
        {
            for (int day = 0; day < maxDays && s.Population < population; day++)
            {
                lowestTreasury = Mathf.Min(lowestTreasury, s.Money);
                Require(s.Money >= 0, "Growing city ran out of funds before population " + population + ".");
                s.Tick(1f);
                Require(s.Tiles.Where(t => t.Residents > 0 || t.Jobs > 0).All(t => t.Connected && t.Powered && t.Watered), "Occupied growth district lost utilities on day " + s.Day + ".");
            }
            Require(s.Population >= population, $"Growth stalled at {s.Population}/{population} residents by day {s.Day}; jobs {s.Jobs}, residential demand {s.ResidentialDemand:0}, money {s.Money:0}.");
        }

        static CitySimulation Seed() { var s = new CitySimulation(); s.SeedCity(); s.Recalculate(); return s; }
        static CitySimulation Empty()
        {
            var s = Seed();
            foreach (var t in s.Tiles)
            {
                t.Kind = TileKind.Empty; t.Level = t.Residents = t.Jobs = 0;
                t.Growth = t.LandValue = t.Pollution = 0;
                t.Connected = t.Powered = t.Watered = false;
            }
            s.Money = 100000; s.Recalculate(); return s;
        }
        static CitySimulation Supplied()
        {
            var s = Empty();
            Require(s.BuildRoad(3, 24, 12, 24, out var reason), reason);
            Place(s, 8, 23, TileKind.Power); Place(s, 10, 23, TileKind.Water);
            s.Recalculate(); return s;
        }
        static void Place(CitySimulation s, int x, int z, TileKind kind)
        { Require(s.Build(x, z, kind, out var reason), $"Build {kind} ({x}, {z}): {reason}"); s.Recalculate(); }
        static string UtilityRecovery(TileKind kind)
        {
            var s = Supplied(); Place(s, 12, 23, TileKind.Residential);
            int utilityX = kind == TileKind.Power ? 8 : 10;
            var home = s.Get(12, 23);
            Require(home.Powered && home.Watered, "Fixture failed to supply its new home.");
            Require(s.Bulldoze(utilityX, 23, out var reason), reason); s.Recalculate();
            Require(kind == TileKind.Power ? !home.Powered : !home.Watered, "Removing the only utility did not remove service.");
            for (int day = 0; day < 10; day++) s.Tick(1);
            Require(home.Residents == 0, "New housing developed without " + kind + ".");
            Place(s, utilityX, 23, kind);
            Require(home.Powered && home.Watered, "Rebuilding failed to restore service.");
            for (int day = 0; day < 30; day++) s.Tick(1);
            Require(home.Residents > 0, "Restored utility did not enable housing growth.");
            return kind + " removal stopped development for 10 days; restoration yielded " + home.Residents + " residents.";
        }
        static int Count(CitySimulation s, TileKind kind) => s.Tiles.Count(t => t.Kind == kind);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static void Check(AcceptanceReport report, string name, Func<string> test)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var result = new CheckResult { name = name };
            try { result.detail = test(); result.passed = true; }
            catch (Exception ex) { result.detail = ex.GetType().Name + ": " + ex.Message; result.passed = false; }
            timer.Stop(); result.milliseconds = timer.Elapsed.TotalMilliseconds;
            report.checks.Add(result);
        }
    }
}
