using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Seabright
{
    /// <summary>
    /// Opt-in player acceptance harness. Construction uses the same pointer handler as
    /// live play; the report explicitly distinguishes this from synthesized GUI events.
    /// </summary>
    public sealed class CityRuntimePlaytest : MonoBehaviour
    {
        CityGame game;
        Report report;
        string artifactDirectory, seedJson;
        int seedRoadsBuilt, seedZonesPainted;
        readonly List<Check> checks = new List<Check>();
        readonly List<string> screenshots = new List<string>();
        readonly List<string> runtimeErrors = new List<string>();
        int errorCount, exceptionCount, assertCount, warningCount;
        float started;

        void Awake() { Application.logMessageReceived += OnLog; }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }

        void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Warning) warningCount++;
            if (type == LogType.Error) errorCount++;
            if (type == LogType.Exception) exceptionCount++;
            if (type == LogType.Assert) assertCount++;
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && runtimeErrors.Count < 20)
                runtimeErrors.Add(type + ": " + condition + "\n" + stackTrace);
        }

        IEnumerator Start()
        {
            game = CityGame.Instance;
            started = Time.realtimeSinceStartup;
            string[] args = Environment.GetCommandLineArgs();
            int directoryArg = Array.IndexOf(args, "-artifacts");
            artifactDirectory = directoryArg >= 0 && directoryArg + 1 < args.Length
                ? Path.GetFullPath(args[directoryArg + 1])
                : Path.GetFullPath(Path.Combine(Application.dataPath, Application.isEditor ? "../Artifacts" : "../../../../../Artifacts"));
            report = new Report
            {
                startedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                applicationVersion = Application.version,
                buildGuid = Application.buildGUID,
                isEditor = Application.isEditor,
                platform = Application.platform.ToString(),
                operatingSystem = SystemInfo.operatingSystem,
                graphicsDevice = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                processor = SystemInfo.processorType,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                qualityLevel = QualitySettings.names[QualitySettings.GetQualityLevel()],
                inputScope = "Programmatic down/held/up through CityGame.ProcessPointer, the live world pointer handler; no GUI button event synthesis.",
                logCaptureScope = "From playtest component Awake through completion, including screenshot rendering and restoration."
            };

            // Flatten our nested routines so an unexpected exception still produces a
            // failed report and restores the seeded city instead of abandoning the run.
            var routines = new Stack<IEnumerator>();
            routines.Push(Run());
            while (routines.Count > 0)
            {
                bool more = false;
                object instruction = null;
                Exception failure = null;
                try
                {
                    IEnumerator current = routines.Peek();
                    more = current.MoveNext();
                    if (more) instruction = current.Current;
                }
                catch (Exception ex) { failure = ex; }
                if (failure != null)
                {
                    Add("harness completed without exception", false, failure.ToString());
                    routines.Clear();
                    break;
                }
                if (!more) { routines.Pop(); continue; }
                if (instruction is IEnumerator nested && !(instruction is CustomYieldInstruction))
                { routines.Push(nested); continue; }
                yield return instruction;
            }

            bool restored = false;
            try
            {
                if (game != null && seedJson != null)
                {
                    game.Speed = 0;
                    game.Sim.LoadJson(seedJson);
                    game.RoadsBuilt = seedRoadsBuilt;
                    game.ZonesPainted = seedZonesPainted;
                    game.SetTool(TileKind.Empty);
                    game.Selected = game.Hover = new Vector2Int(-1, -1);
                    game.SetOverlay(0);
                    game.Help = game.Budget = game.Photo = false;
                    if (game.Night) game.ToggleNight();
                    game.Rig.ResetView();
                    game.View.ShowHover(-1, -1, Color.clear, false);
                    restored = game.Sim.SaveJson() == seedJson;
                }
            }
            catch (Exception ex) { Add("seed restoration exception", false, ex.ToString()); }
            yield return new WaitForSecondsRealtime(1.5f);
            yield return new WaitForEndOfFrame();
            Add("seed restored and city controls reset", restored && game.Tool == TileKind.Empty && !game.Demolish &&
                game.Selected.x < 0 && game.Overlay == 0 && !game.Night && !game.Photo && !game.Help && !game.Budget,
                "Full seed JSON restored; home camera, natural daylight, empty tool and selection; onboarding counters restored.");
            report.seedRestored = restored;
            report.errorCount = errorCount;
            report.exceptionCount = exceptionCount;
            report.assertCount = assertCount;
            report.warningCount = warningCount;
            report.runtimeErrors = runtimeErrors.ToArray();
            Add("no runtime errors, exceptions or assertions", errorCount + exceptionCount + assertCount == 0,
                "Errors " + errorCount + ", exceptions " + exceptionCount + ", assertions " + assertCount + ", warnings " + warningCount + ".");
            report.screenshots = screenshots.ToArray();
            report.checks = checks.ToArray();
            report.passed = checks.Count > 0 && checks.TrueForAll(c => c.passed);
            report.completedUtc = DateTime.UtcNow.ToString("O");
            report.durationSeconds = Time.realtimeSinceStartup - started;
            try
            {
                Directory.CreateDirectory(artifactDirectory);
                File.WriteAllText(Path.Combine(artifactDirectory, "runtime-playtest.json"), JsonUtility.ToJson(report, true));
            }
            catch (Exception ex)
            {
                report.passed = false;
                Debug.LogError("SEABRIGHT_AUTOPLAY_REPORT_FAILED " + ex);
            }
            if (game != null)
            {
                game.Speed = 1;
                game.Toast(report.passed ? "Autonomous playtest complete. Seabright is ready to explore." :
                    "Autonomous playtest found a problem. See runtime-playtest.json.");
            }
            Debug.Log((report.passed ? "SEABRIGHT_AUTOPLAY_COMPLETE " : "SEABRIGHT_AUTOPLAY_FAILED ") + artifactDirectory);
            if (Array.IndexOf(args, "-quitAfterAutoplay") >= 0) Application.Quit(report.passed ? 0 : 1);
        }

        IEnumerator Run()
        {
            Directory.CreateDirectory(artifactDirectory);
            if (game == null || game.Sim == null || game.View == null || game.Cam == null || game.HUD == null)
                throw new InvalidOperationException("City runtime was not initialized before autoplay.");
            game.Speed=0; seedJson=game.Sim.SaveJson(); seedRoadsBuilt=game.RoadsBuilt; seedZonesPainted=game.ZonesPainted;
            report.populationBefore=game.Sim.Population; report.lowestTreasury=game.Sim.Money;
            game.Help=game.Budget=game.Photo=false;game.SetTool(TileKind.Empty);game.Selected=new Vector2Int(-1,-1);
            game.Rig.ResetView();
            Add("starts with a small supplied settlement",game.Sim.Population==24&&Count(TileKind.Residential)==2&&Count(TileKind.Road)==13,
                game.Sim.Population+" residents, "+Count(TileKind.Road)+" road cells, $"+game.Sim.Money+". No established skyline.");
            yield return new WaitForSecondsRealtime(2f);
            yield return Capture("01-starter-settlement.png");

            float before=game.Sim.Money;string why;
            bool locked=!game.Sim.Build(18,23,TileKind.Office,out why)&&game.Sim.Money==before;
            Add("locked office rejects placement without charging money",locked,why);

            yield return FrameAt(15,24,180);
            TestUiBlock();
            int roadsBefore=Count(TileKind.Road);float roadMoney=game.Sim.Money;
            yield return DragRoad(15,24,20,24);
            report.newRoadTiles=Count(TileKind.Road)-roadsBefore;report.roadCost=roadMoney-game.Sim.Money;
            report.roadBuilt=report.newRoadTiles==5&&Mathf.Abs(report.roadCost-400)<.01f;
            Add("first road drag builds five connected segments for $400",report.roadBuilt,report.newRoadTiles+" segments; $"+report.roadCost+".");
            yield return Paint(TileKind.Residential,16,23);
            yield return Paint(TileKind.Commercial,17,23);
            yield return Paint(TileKind.Industrial,10,26);
            Add("three zone types remain distinct in simulation",game.Sim.Get(16,23).Kind==TileKind.Residential&&game.Sim.Get(17,23).Kind==TileKind.Commercial&&game.Sim.Get(10,26).Kind==TileKind.Industrial,
                "Homes (16,23), shops (17,23), industry (10,26), all designated through the live pointer handler.");
            game.SetTool(TileKind.Empty);game.Selected=new Vector2Int(-1,-1);
            yield return Capture("02-distinct-zone-sites.png");
            game.Speed=1;
            float wait=Time.realtimeSinceStartup;
            while(Time.realtimeSinceStartup-wait<25f && (game.Sim.Get(16,23).Level==0||game.Sim.Get(17,23).Level==0||game.Sim.Get(10,26).Level==0))yield return null;
            report.firstDevelopmentRealSeconds=Time.realtimeSinceStartup-wait;
            game.Speed=0;
            report.developedNewLots=(game.Sim.Get(16,23).Level>0?1:0)+(game.Sim.Get(17,23).Level>0?1:0)+(game.Sim.Get(10,26).Level>0?1:0);
            report.newNeighborhoodResidents=game.Sim.Get(16,23).Residents;
            Add("homes shops and industry develop during normal 1x play",game.Sim.Get(16,23).Level>0&&game.Sim.Get(17,23).Level>0&&game.Sim.Get(10,26).Level>0,
                "Observed real Update ticks for "+report.firstDevelopmentRealSeconds.ToString("F1")+" seconds; levels "+game.Sim.Get(16,23).Level+"/"+game.Sim.Get(17,23).Level+"/"+game.Sim.Get(10,26).Level+".");
            yield return Capture("03-first-neighborhood.png");
            yield return RenderVisibility(8,"04-visible-vehicles.png","vehicle meshes render in the standalone game");
            yield return RenderVisibility(9,"05-visible-pedestrians.png","pedestrian meshes render in the standalone game");
            Vector3 carBefore=game.View.TrafficMotionSample,walkerBefore=game.View.PedestrianMotionSample;
            report.trafficVehicles=game.View.TrafficCount;report.pedestrians=game.View.PedestrianCount;
            game.Speed=1;yield return new WaitForSecondsRealtime(2f);game.Speed=0;
            report.trafficMotionMeters=Vector3.Distance(carBefore,game.View.TrafficMotionSample);
            report.pedestrianMotionMeters=Vector3.Distance(walkerBefore,game.View.PedestrianMotionSample);
            Add("cars and pedestrians move during normal play",report.trafficMotionMeters>1&&report.pedestrianMotionMeters>.5f,
                report.trafficVehicles+" cars, "+report.pedestrians+" walkers; sampled motion "+report.trafficMotionMeters.ToString("F2")+"m and "+report.pedestrianMotionMeters.ToString("F2")+"m over 2s at 1x.");
            game.Photo=true;yield return FrameAt(13,24,90);yield return Capture("12-street-life.png");game.Photo=false;

            // Every purchase uses live construction input. Only waiting is compressed;
            // neither funds, unlocks, building levels nor population are injected.
            yield return DragRoad(15,18,15,30);
            yield return DragRoad(15,18,26,18);
            yield return DragRoad(20,24,26,24);
            yield return DragRoad(15,30,26,30);
            yield return DragRoad(26,18,26,30);
            yield return Paint(TileKind.Power,22,25);
            yield return Paint(TileKind.Water,22,23);
            yield return Paint(TileKind.Park,20,22);
            yield return Paint(TileKind.Park,20,27);
            for(int x=16;x<=21;x++)for(int z=19;z<=20;z++)yield return Paint(TileKind.Residential,x,z);
            yield return GrowTo(150,30);
            report.officeUnlockDay=game.Sim.Day;
            Add("town growth unlocks offices at 150 residents",game.Sim.IsUnlocked(TileKind.Office),"Day "+game.Sim.Day+", "+game.Sim.Population+" residents, $"+game.Sim.Money.ToString("F0")+".");
            yield return Paint(TileKind.Office,16,23);yield return Paint(TileKind.Office,17,23);
            for(int x=9;x<=14;x++)if(game.Sim.Get(x,26).Kind==TileKind.Empty)yield return Paint(TileKind.Industrial,x,26);
            for(int x=16;x<=21;x++)for(int z=28;z<=29;z++)yield return Paint(TileKind.Residential,x,z);
            yield return GrowTo(350,40);
            report.towerUnlockDay=game.Sim.Day;
            Add("growing town unlocks residential towers at 350 residents",game.Sim.IsUnlocked(TileKind.HighResidential),"Day "+game.Sim.Day+", "+game.Sim.Population+" residents.");
            yield return FrameAt(20,24,285);
            yield return Capture("06-growing-town.png");
            for(int x=22;x<=25;x++)yield return Paint(TileKind.HighResidential,x,19);
            yield return Paint(TileKind.Office,18,23);
            yield return GrowTo(600,40);
            report.stadiumUnlockDay=game.Sim.Day;
            Add("occupied towers grow the city to the stadium milestone",game.Sim.IsUnlocked(TileKind.Stadium)&&game.Sim.Get(22,19).Residents>=32,
                game.Sim.Population+" residents by day "+game.Sim.Day+"; first tower houses "+game.Sim.Get(22,19).Residents+".");
            float stadiumMoney=game.Sim.Money;
            yield return Paint(TileKind.Stadium,22,32);
            var stadium=game.Sim.Get(22,32);
            Add("stadium reserves nine tiles and opens with services",Count(TileKind.Stadium)==9&&stadium.Connected&&stadium.Powered&&stadium.Watered&&stadium.Jobs==64&&Mathf.Abs(stadiumMoney-game.Sim.Money-game.Sim.Cost(TileKind.Stadium))<.1f,
                "3x3 stadium; "+stadium.Jobs+" jobs; cost $"+(stadiumMoney-game.Sim.Money).ToString("F0")+"; served="+(stadium.Connected&&stadium.Powered&&stadium.Watered)+".");
            report.populationAfter=game.Sim.Population;report.money=game.Sim.Money;report.happiness=game.Sim.Happiness;
            Add("growth journey remains affordable and financially sustainable",report.lowestTreasury>=0&&game.Sim.Income>game.Sim.Expenses,
                "Lowest treasury $"+report.lowestTreasury.ToString("F0")+"; final $"+game.Sim.Money.ToString("F0")+"; net $"+(game.Sim.Income-game.Sim.Expenses).ToString("F0")+"/day; no injected funds.");
            game.SetTool(TileKind.Empty);game.Selected=new Vector2Int(-1,-1);
            yield return FrameAt(20,25,340);
            yield return Capture("07-grown-city.png");
            yield return FrameAt(22,31,160);
            game.Selected=new Vector2Int(22,32);
            yield return Capture("08-stadium-detail.png");
            game.Selected=new Vector2Int(-1,-1);game.Photo=true;
            yield return FrameAt(21,22,255);game.Rig.Focus+=Vector3.up*18;yield return new WaitForSecondsRealtime(.7f);
            yield return Capture("09-modern-skyline.png");
            game.Photo=false;yield return FrameAt(20,25,340);
            game.ToggleNight();yield return Capture("10-grown-city-night.png");game.ToggleNight();
            game.SetOverlay(2);yield return Capture("11-supply-overlay.png");game.SetOverlay(0);
            int originalWidth=Screen.width,originalHeight=Screen.height;
            Screen.SetResolution(1920,1080,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(1);
            yield return Capture("13-toolbar-resized.png");
            Add("HUD renders after window resize",Screen.width==1920&&Screen.height==1080,"Cached icon textures captured at 1920x1080 with a non-unit GUI scale; image reviewed separately.");
            Screen.SetResolution(originalWidth,originalHeight,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(1);

            report.savePath=Path.GetFullPath(game.SavePath);
            bool isolated=report.savePath==Path.Combine(artifactDirectory,"playtest-save.json");
            Add("growth save uses isolated artifact slot",isolated,report.savePath);
            if(isolated) {
                string saved=game.Sim.SaveJson();bool wrote=game.Save();
                yield return FrameAt(22,32,150);game.Demolish=true;Click(ScreenPoint(21,31));game.SetTool(TileKind.Empty);
                bool removed=Count(TileKind.Stadium)==0;game.Load();
                report.saveLoadPassed=wrote&&removed&&game.Sim.SaveJson()==saved&&game.Sim.IsUnlocked(TileKind.Stadium);
                Add("save restores landmark footprint and earned unlocks",report.saveLoadPassed,"Saved city, bulldozed stadium through its footprint, loaded exact city JSON and permanent milestones.");
                File.WriteAllText(Path.Combine(artifactDirectory,"grown-city-save.json"),saved);
                yield return FrameAt(22,19,185);game.Rig.Focus+=Vector3.up*18;yield return new WaitForSecondsRealtime(.7f);
                CityLot roofLot=null;
                foreach(var lot in FindObjectsByType<CityLot>())if(lot.X==22&&lot.Z==19){roofLot=lot;break;}
                bool roofSelected=false,roofDemolished=false;
                if(roofLot!=null) {
                    var bounds=roofLot.GetComponent<BoxCollider>().bounds;
                    Vector3 roof=game.Cam.WorldToScreenPoint(bounds.center+Vector3.up*(bounds.extents.y-.4f));
                    if(VisibleWorldPoint(roof)) {
                        Click(roof);roofSelected=game.Selected==new Vector2Int(22,19);
                        game.Demolish=true;Click(roof);game.SetTool(TileKind.Empty);
                        roofDemolished=game.Sim.Get(22,19).Kind==TileKind.Empty&&Count(TileKind.HighResidential)==3;
                    }
                }
                game.Load();report.pointerSelected=roofSelected;
                Add("tower roof selection and bulldozing target the visible building",roofSelected&&roofDemolished&&game.Sim.SaveJson()==saved,
                    "Clicked the actual tower collider roof: selected="+roofSelected+", exactly one tower removed="+roofDemolished+", saved city restored.");
                yield return FrameAt(13,30,180);game.SetTool(TileKind.Road);
                Vector3 roadStart=ScreenPoint(15,30),roadEnd=ScreenPoint(10,30);
                game.ProcessPointer(roadStart,true,true,false);game.Load();game.ProcessPointer(roadEnd,false,false,true);
                Add("loading cancels an unfinished road stroke",game.Tool==TileKind.Empty&&game.Sim.SaveJson()==saved,
                    "Loaded while a road drag was active, then released; restored city remained byte-for-byte unchanged.");
            }
            // Measure the actual grown city with live simulation and moving agents.
            game.Selected=new Vector2Int(-1,-1);game.SetTool(TileKind.Empty);game.Speed=1;
            yield return FrameAt(20,25,340);yield return new WaitForSecondsRealtime(2);
            var frameMs=new List<float>();float seconds=0;
            while(seconds<5){yield return null;float dt=Time.unscaledDeltaTime;if(dt>0){seconds+=dt;frameMs.Add(dt*1000);}}
            frameMs.Sort();report.performance=new Performance{scenario="Player-built city over 600 residents; HUD visible; simulation 1x; cars and pedestrians moving.",sampleFrames=frameMs.Count,sampleSeconds=seconds,meanFps=frameMs.Count/seconds,p95FrameMs=frameMs[Mathf.Clamp(Mathf.CeilToInt(frameMs.Count*.95f)-1,0,frameMs.Count-1)],worstFrameMs=frameMs[frameMs.Count-1],targetFrameRate=Application.targetFrameRate,vSyncCount=QualitySettings.vSyncCount,warmupSecondsAfterScreenshot=2};
            Add("active grown city sustains interactive performance",!Application.isEditor&&report.performance.meanFps>=45,
                report.performance.meanFps.ToString("F1")+" FPS; p95 "+report.performance.p95FrameMs.ToString("F2")+"ms; 1600x1000 Metal, active city.");
            game.Speed=0;
        }

        IEnumerator FrameAt(int x,int z,float distance) {
            game.Rig.Focus=CitySimulation.World(x,z);game.Rig.Distance=distance;game.Rig.Yaw=-28;game.Rig.Pitch=48;
            yield return new WaitForSecondsRealtime(.65f);
        }
        IEnumerator DragRoad(int x0,int z0,int x1,int z1) {
            game.Selected=new Vector2Int(-1,-1);game.SetTool(TileKind.Road);
            yield return FrameAt((x0+x1)/2,(z0+z1)/2,Mathf.Max(180,Mathf.Max(Mathf.Abs(x1-x0),Mathf.Abs(z1-z0))*16+90));
            var a=ScreenPoint(x0,z0);var b=ScreenPoint(x1,z1);
            if(!VisibleWorldPoint(a)||!VisibleWorldPoint(b))throw new InvalidOperationException("Road endpoints obscured by HUD: "+x0+","+z0+" to "+x1+","+z1);
            game.ProcessPointer(a,true,true,false);yield return null;game.ProcessPointer(b,false,true,false);yield return null;game.ProcessPointer(b,false,false,true);
            if(game.Sim.Get(x1,z1).Kind!=TileKind.Road)throw new InvalidOperationException("Road input failed: "+game.Notice);
            game.SetTool(TileKind.Empty);report.lowestTreasury=Mathf.Min(report.lowestTreasury,game.Sim.Money);
            yield return null;
        }
        IEnumerator Paint(TileKind kind,int x,int z) {
            game.Selected=new Vector2Int(-1,-1);game.SetTool(kind);
            if(game.Tool!=kind)throw new InvalidOperationException("Cannot select "+kind+": "+game.Notice);
            Vector3 point=ScreenPoint(x,z);
            if(!VisibleWorldPoint(point)) {yield return FrameAt(x,z,180);point=ScreenPoint(x,z);}
            if(!VisibleWorldPoint(point))throw new InvalidOperationException("Tile behind UI: "+x+","+z);
            Click(point);yield return null;
            if(game.Sim.Get(x,z).Kind!=kind)throw new InvalidOperationException("Pointer build "+kind+" at "+x+","+z+" failed: "+game.Notice);
            if(CitySimulation.IsZone(kind))report.zonesPainted++;
            report.lowestTreasury=Mathf.Min(report.lowestTreasury,game.Sim.Money);
            game.SetTool(TileKind.Empty);
        }
        IEnumerator GrowTo(int target,int maxDays) {
            game.SetTool(TileKind.Empty);game.Selected=new Vector2Int(-1,-1);
            int day=0;
            while(day<maxDays&&game.Sim.Population<target) {
                game.Sim.Tick(.5f);yield return null;game.Sim.Tick(.5f);yield return null;day++;
                report.lowestTreasury=Mathf.Min(report.lowestTreasury,game.Sim.Money);
                if(game.Sim.Money<0)throw new InvalidOperationException("City ran out of money on day "+game.Sim.Day);
            }
            report.daysAdvancedForGrowth+=day;
            if(game.Sim.Population<target)throw new InvalidOperationException("Growth stalled at "+game.Sim.Population+"/"+target+" on day "+game.Sim.Day+". Jobs="+game.Sim.Jobs+" demand="+game.Sim.ResidentialDemand);
        }
        IEnumerator RenderVisibility(int layer,string filename,string checkName) {
            int mask=game.Cam.cullingMask;Color bg=game.Cam.backgroundColor;bool hud=game.HUD.enabled;
            var atmosphere=game.Cam.GetComponent<CityAtmosphere>();bool effects=atmosphere.enabled;
            try {
                game.HUD.enabled=false;atmosphere.enabled=false;game.Cam.cullingMask=1<<layer;game.Cam.backgroundColor=Color.black;
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                Texture2D pixels=ScreenCapture.CaptureScreenshotAsTexture();int colored=0;
                foreach(var c in pixels.GetPixels32())if(c.r>18||c.g>18||c.b>18)colored++;
                File.WriteAllBytes(Path.Combine(artifactDirectory,filename),pixels.EncodeToPNG());Destroy(pixels);
                Add(checkName,colored>100,colored+" non-background pixels from isolated layer "+layer+" in actual standalone render readback. Positions alone do not establish visibility.");
                if(layer==8)report.visibleVehiclePixels=colored;else report.visiblePedestrianPixels=colored;
            } finally {game.HUD.enabled=hud;atmosphere.enabled=effects;game.Cam.cullingMask=mask;game.Cam.backgroundColor=bg;}
            yield return null;
        }

        void TestUiBlock()
        {
            game.SetTool(TileKind.Park);
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 960f);
            Vector3 point = Vector3.zero;
            Vector2Int groundTile = new Vector2Int(-1, -1);
            bool found = false;
            // Locate real, buildable land underneath an actual HUD panel. A click on
            // empty screen/sea could pass accidentally without exercising GUI blocking.
            for (int column = 3; column <= 7 && !found; column++)
            {
                Vector3 candidate = new Vector3(Screen.width * column / 10f, Screen.height - 62f * scale, 0);
                if (!game.HUD.PointerOverUI(candidate) || !GroundTile(candidate, out Vector2Int tile)) continue;
                if (!CitySimulation.IsLand(tile.x, tile.y) || game.Sim.Get(tile.x, tile.y).Kind != TileKind.Empty) continue;
                found = true; point = candidate; groundTile = tile;
            }
            string before = game.Sim.SaveJson();
            if (found) Click(point);
            report.uiBlocksWorldPointer = found && game.Hover.x < 0 && before == game.Sim.SaveJson();
            Add("HUD intercepts a construction click over buildable world land", report.uiBlocksWorldPointer,
                "Buildable empty tile beneath header (" + groundTile.x + "," + groundTile.y + "); valid fixture=" + found +
                "; hover suppressed and simulation JSON unchanged=" + report.uiBlocksWorldPointer + ".");
            game.SetTool(TileKind.Empty);
        }

        bool GroundTile(Vector3 point, out Vector2Int tile)
        {
            tile = new Vector2Int(-1, -1);
            Ray ray = game.Cam.ScreenPointToRay(point);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)) return false;
            Vector3 world = ray.GetPoint(distance);
            tile = new Vector2Int(Mathf.FloorToInt(world.x / CitySimulation.CellSize + 24), Mathf.FloorToInt(world.z / CitySimulation.CellSize + 24));
            return game.Sim.Get(tile.x, tile.y) != null;
        }

        Vector3 ScreenPoint(int x, int z) { return game.Cam.WorldToScreenPoint(CitySimulation.World(x, z) + Vector3.up * .05f); }
        bool VisibleWorldPoint(Vector3 point) { return point.z > 0 && point.x >= 0 && point.x < Screen.width && point.y >= 0 && point.y < Screen.height && !game.HUD.PointerOverUI(point); }
        void Click(Vector3 point) { game.ProcessPointer(point, true, true, false); game.ProcessPointer(point, false, false, true); }
        int Count(TileKind kind) { int count = 0; foreach (CityTile tile in game.Sim.Tiles) if (tile.Kind == kind) count++; return count; }
        void Add(string name, bool passed, string detail) { checks.Add(new Check { name = name, passed = passed, detail = detail }); }

        IEnumerator Capture(string filename)
        {
            // Synchronous render readback avoids racing Unity's asynchronous PNG writer:
            // a file can exist before its image bytes have finished being written.
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(artifactDirectory, filename);
            Texture2D capture=ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path,capture.EncodeToPNG());Destroy(capture);
            bool saved = File.Exists(path) && new FileInfo(path).Length > 1024;
            if (saved) screenshots.Add(filename);
            Add("screenshot " + filename, saved, saved ? new FileInfo(path).Length + " bytes written; visual quality requires image review." : "Screenshot was not written.");
        }

        [Serializable] sealed class Check { public string name, detail; public bool passed; }
        [Serializable] sealed class Performance
        {
            public string scenario;
            public int sampleFrames, targetFrameRate, vSyncCount;
            public float warmupSecondsAfterScreenshot, sampleSeconds, meanFps, p95FrameMs, worstFrameMs;
        }
        [Serializable] sealed class Report
        {
            public bool passed, isEditor, roadBuilt, pointerSelected, uiBlocksWorldPointer, saveLoadPassed, seedRestored;
            public string startedUtc, completedUtc, unityVersion, applicationVersion, buildGuid, platform, operatingSystem,
                graphicsDevice, graphicsApi, processor, qualityLevel, inputScope, logCaptureScope, roadResult, savePath;
            public int screenWidth, screenHeight, populationBefore, populationAfter, newRoadTiles, zonesPainted,
                developedNewLots, newNeighborhoodResidents, trafficVehicles, trafficSampleDayAdvance,
                errorCount, exceptionCount, assertCount, warningCount;
            public float durationSeconds, roadCost, zoningCost, daysAdvancedForGrowth, money, happiness, trafficMotionMeters, pedestrianMotionMeters, firstDevelopmentRealSeconds, lowestTreasury;
            public int pedestrians, visibleVehiclePixels, visiblePedestrianPixels, officeUnlockDay, towerUnlockDay, stadiumUnlockDay;
            public Performance performance;
            public Check[] checks;
            public string[] screenshots, runtimeErrors;
        }
    }
}
