using System;
using System.IO;
using UnityEngine;

namespace Seabright
{
    public class CityGame : MonoBehaviour
    {
        public static CityGame Instance;
        [NonSerialized] public CitySimulation Sim;
        public CityView View;
        public CityCamera Rig;
        public CityHUD HUD;
        public CityAudio Audio;
        public Camera Cam;
        public int Speed = 1, Overlay;
        public bool Night, Photo, Help, Budget;
        public TileKind Tool = TileKind.Empty;
        public bool Demolish;
        public Vector2Int Hover = new Vector2Int(-1,-1), Selected = new Vector2Int(-1,-1);
        public string Notice = "Welcome to Seabright. Your next neighborhood starts with a road.";
        public float NoticeUntil = 12;
        public int RoadsBuilt, ZonesPainted;
        public bool AutoplayEnabled => Array.IndexOf(Environment.GetCommandLineArgs(), "-autoplay") >= 0;
        public bool AudioTestEnabled => Array.IndexOf(Environment.GetCommandLineArgs(), "-audioTest") >= 0;
        public bool InputDiagnostics => Array.IndexOf(Environment.GetCommandLineArgs(), "-debugInput") >= 0;
        public string ArtifactDirectory {
            get {
                string[] args = Environment.GetCommandLineArgs();
                int i = Array.IndexOf(args,"-artifacts");
                return Path.GetFullPath(i >= 0 && i + 1 < args.Length ? args[i+1] : Path.Combine(Application.dataPath, Application.isEditor ? "../Artifacts" : "../../../../../Artifacts"));
            }
        }
        public string SavePath => AutoplayEnabled || InputDiagnostics || AudioTestEnabled ? Path.Combine(ArtifactDirectory, "playtest-save.json") : Path.Combine(Application.persistentDataPath, "seabright-city.json");
        Light sun;
        int seenRevision = -1;
        Vector2Int roadStart = new Vector2Int(-1,-1), lastPaint = new Vector2Int(-1,-1);
        LineRenderer roadPreview;
        float nextTick;
        bool worldStroke;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (FindAnyObjectByType<CityGame>() == null) new GameObject("Seabright").AddComponent<CityGame>(); }

        void Start()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;
            QualitySettings.antiAliasing = 4;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowDistance = 700;
            QualitySettings.shadowCascades = 4;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.59f,.69f,.79f);
            RenderSettings.ambientEquatorColor = new Color(.46f,.52f,.52f);
            RenderSettings.ambientGroundColor = new Color(.26f,.3f,.27f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .00115f;
            RenderSettings.fogColor = new Color(.64f,.76f,.79f);
            sun = new GameObject("Late afternoon sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.35f;
            sun.color = new Color(1f,.9f,.75f);
            sun.transform.rotation = Quaternion.Euler(43,-36,0);
            sun.shadows = LightShadows.Soft; sun.shadowStrength = .82f;
            sun.shadowBias = .12f; sun.shadowNormalBias = .35f;
            RenderSettings.sun = sun;
            Cam = new GameObject("City camera").AddComponent<Camera>();
            Cam.tag = "MainCamera"; Cam.nearClipPlane = .8f; Cam.farClipPlane = 2200;
            Cam.fieldOfView = 43; Cam.allowHDR = true; Cam.allowMSAA = true;
            Cam.backgroundColor = RenderSettings.fogColor;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.gameObject.AddComponent<AudioListener>();
            Cam.gameObject.AddComponent<CityAtmosphere>();
            Rig = Cam.gameObject.AddComponent<CityCamera>(); Rig.Game = this;
            Sim = new CitySimulation();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-showcase") >= 0) Sim.SeedCity();
            else Sim.SeedStarterTown();
            Rig.ResetView();
            View = new GameObject("City presentation").AddComponent<CityView>(); View.Initialize(Sim);
            HUD = gameObject.AddComponent<CityHUD>(); HUD.Game = this;
            Audio = gameObject.AddComponent<CityAudio>(); Audio.Initialize(this);
            roadPreview = new GameObject("Road placement preview").AddComponent<LineRenderer>();
            roadPreview.material = new Material(Shader.Find("Sprites/Default"));
            roadPreview.startColor = roadPreview.endColor = new Color(.35f,1,.82f,.72f);
            roadPreview.startWidth = roadPreview.endWidth = 7.5f;
            roadPreview.positionCount = 0;
            if (AutoplayEnabled) gameObject.AddComponent<CityRuntimePlaytest>();
            if (AudioTestEnabled) gameObject.AddComponent<CityAudioPlaytest>();
            if (InputDiagnostics || AudioTestEnabled) Speed=0;
        }

        void Update()
        {
            if (Sim == null) return;
            if(InputDiagnostics&&(Input.GetMouseButtonDown(0)||Input.GetMouseButtonUp(0))) Debug.Log("SEABRIGHT_POINTER "+Input.mousePosition+" down="+Input.GetMouseButtonDown(0)+" up="+Input.GetMouseButtonUp(0)+" focused="+Application.isFocused);
            float dt = Mathf.Min(Time.unscaledDeltaTime,.1f);
            if (Speed > 0) { nextTick += dt * Speed; if (nextTick >= .5f) { Sim.Tick(nextTick * .12f); nextTick = 0; } }
            if (Sim.Revision != seenRevision) { View.Refresh(); seenRevision = Sim.Revision; }
            View.UpdateTraffic(dt * Speed);
            if (Input.GetKeyDown(KeyCode.Escape)) { SetTool(TileKind.Empty); Help = Budget = false; HUD.CloseModal(); }
            if (Input.GetKeyDown(KeyCode.M)) { Audio.SetMuted(!Audio.Muted); Toast(Audio.Muted ? "Sound muted. Press M to hear your city." : "City sound is on."); }
            if (Help || HUD.ModalOpen) { worldStroke = false; return; }
            if (Input.GetKeyDown(KeyCode.Space)) { Speed = Speed == 0 ? 1 : 0; Audio.Play(CityAudio.Cue.Ui); }
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetTool(TileKind.Road);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetTool(TileKind.Residential);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetTool(TileKind.Commercial);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetTool(TileKind.Industrial);
            if (Input.GetKeyDown(KeyCode.Alpha5)) SetTool(TileKind.Park);
            if (Input.GetKeyDown(KeyCode.B)) { SetTool(TileKind.Empty); Demolish = true; }
            if (Input.GetKeyDown(KeyCode.F)) Photo = !Photo;
            if (Input.GetKeyDown(KeyCode.N)) ToggleNight();
            if (Input.GetKeyDown(KeyCode.Home)) Rig.ResetView();
            if (Input.GetKeyDown(KeyCode.F5)) Save();
            if (Input.GetKeyDown(KeyCode.F9)) Load();
            HandlePointer();
        }

        void HandlePointer()
        {
            ProcessPointer(Input.mousePosition,Input.GetMouseButtonDown(0),Input.GetMouseButton(0),Input.GetMouseButtonUp(0),Input.GetMouseButtonDown(1));
        }

        // Both live input and the runtime acceptance scenario use this exact world-edit path.
        public void ProcessPointer(Vector3 mousePosition,bool down,bool held,bool up,bool cancel=false)
        {
            bool blocked = Photo || Help || HUD.ModalOpen || HUD.PointerOverUI(mousePosition);
            Ray ray = Cam.ScreenPointToRay(mousePosition);
            Plane ground = new Plane(Vector3.up, Vector3.zero);
            bool valid = !blocked && ground.Raycast(ray, out float d);
            if (valid) {
                ground.Raycast(ray, out float distance);
                Vector3 p = ray.GetPoint(distance);
                Hover = new Vector2Int(Mathf.FloorToInt(p.x / CitySimulation.CellSize + 24), Mathf.FloorToInt(p.z / CitySimulation.CellSize + 24));
                if((Tool==TileKind.Empty||Demolish)&&Physics.Raycast(ray,out RaycastHit hit,Cam.farClipPlane,1<<6)) {
                    var lot=hit.collider.GetComponent<CityLot>();
                    if(lot!=null)Hover=new Vector2Int(lot.X,lot.Z);
                }
                valid = Hover.x >= 0 && Hover.y >= 0 && Hover.x < CitySimulation.Size && Hover.y < CitySimulation.Size;
            }
            if (!valid) Hover = new Vector2Int(-1,-1);
            bool land = valid && CitySimulation.IsLand(Hover.x,Hover.y);
            bool siteClear=land;
            if(Tool==TileKind.Stadium&&valid)for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++) {
                var part=Sim.Get(Hover.x+x,Hover.y+z);
                siteClear&=CitySimulation.IsLand(Hover.x+x,Hover.y+z)&&part!=null&&part.Kind==TileKind.Empty;
            }
            View.ShowHover(Hover.x, Hover.y, !siteClear || Demolish ? new Color(1,.4f,.3f) : CityHUD.Teal, valid && (Tool != TileKind.Empty || Demolish),Tool==TileKind.Stadium?3:1);
            if (cancel) { SetTool(TileKind.Empty); return; }
            if (Tool == TileKind.Road && roadStart.x >= 0 && valid) {
                roadPreview.positionCount = 3;
                roadPreview.SetPosition(0, CitySimulation.World(roadStart.x,roadStart.y) + Vector3.up*.8f);
                roadPreview.SetPosition(1, CitySimulation.World(Hover.x,roadStart.y) + Vector3.up*.8f);
                roadPreview.SetPosition(2, CitySimulation.World(Hover.x,Hover.y) + Vector3.up*.8f);
            }
            if (up) {
                if (Tool == TileKind.Road && roadStart.x >= 0 && valid) {
                    int revision = Sim.Revision;
                    if (Sim.BuildRoad(roadStart.x,roadStart.y,Hover.x,Hover.y,out string why)) {
                        if (Sim.Revision != revision) { RoadsBuilt++; Audio.Play(CityAudio.Cue.Road); Toast("Road completed. Zone the adjoining land to invite new residents."); }
                    } else { Toast(why); Audio.Play(CityAudio.Cue.Denied); }
                }
                roadStart.x = -1; roadPreview.positionCount = 0; lastPaint.x = -1; worldStroke = false;
            }
            if (down) worldStroke = valid;
            if (!valid) return;
            if (down) {
                if (Tool == TileKind.Road) roadStart = Hover;
                else if (Tool == TileKind.Empty && !Demolish) {
                    var picked=Sim.GetAnchor(Sim.Get(Hover.x,Hover.y));
                    Selected=picked!=null?new Vector2Int(picked.X,picked.Z):Hover;
                    Audio.Play(CityAudio.Cue.Ui);
                }
            }
            if (worldStroke && held && Hover != lastPaint && (Tool != TileKind.Empty || Demolish) && Tool != TileKind.Road) {
                lastPaint = Hover;
                bool ok; string why; int revision = Sim.Revision;
                if (Demolish) ok = Sim.Bulldoze(Hover.x,Hover.y,out why);
                else ok = Sim.Build(Hover.x,Hover.y,Tool,out why);
                if (ok && Sim.Revision != revision) {
                    ZonesPainted++;
                    Audio.Play(Demolish ? CityAudio.Cue.Bulldoze : CitySimulation.IsZone(Tool) ? CityAudio.Cue.Zone : CityAudio.Cue.Facility);
                    if (Tool >= TileKind.Park || Demolish) Toast(Demolish ? "Site cleared." : CityHUD.ToolName(Tool) + " built. Services updated.");
                } else if (!ok && down) { Toast(why); Audio.Play(CityAudio.Cue.Denied); }
            }
        }

        public void SetTool(TileKind kind) {
            if (Sim!=null&&!Sim.IsUnlocked(kind)) { Toast(CityHUD.ToolName(kind)+" unlocks at "+Sim.UnlockRequirement(kind)+" residents."); Audio?.Play(CityAudio.Cue.Denied); return; }
            if (Tool != kind || Demolish) Audio?.Play(CityAudio.Cue.Ui);
            Tool = kind; Demolish = false; worldStroke = false; roadStart.x = -1; if (roadPreview != null) roadPreview.positionCount = 0;
        }
        public void NewCity() {
            Sim.SeedStarterTown(); Audio.ResetCitySnapshot(); seenRevision=-1; RoadsBuilt=ZonesPainted=0; nextTick=0;
            Selected=Hover=new Vector2Int(-1,-1); SetTool(TileKind.Empty); SetOverlay(0);
            Help=Budget=Photo=false; if(Night)ToggleNight(); Speed=1; Rig.ResetView();
            Toast("A new beginning. Extend the road and zone your first neighborhood. Your previous save is still available.");
        }
        public void SetOverlay(int mode) { if (Overlay != mode) Audio?.Play(CityAudio.Cue.Ui); Overlay = mode; View.SetOverlay(mode); }
        public void Toast(string message) { Notice = message; NoticeUntil = Time.unscaledTime + 6; }
        public void ToggleNight() {
            Night = !Night; View.SetNight(Night); Audio?.Play(CityAudio.Cue.Ui);
            sun.intensity = Night ? .34f : 1.35f;
            sun.color = Night ? new Color(.44f,.58f,1f) : new Color(1,.9f,.75f);
            RenderSettings.ambientSkyColor = Night ? new Color(.13f,.19f,.32f) : new Color(.59f,.69f,.79f);
            RenderSettings.ambientEquatorColor = Night ? new Color(.09f,.12f,.21f) : new Color(.46f,.52f,.52f);
            RenderSettings.ambientGroundColor = Night ? new Color(.05f,.08f,.13f) : new Color(.26f,.3f,.27f);
            RenderSettings.fogColor = Night ? new Color(.07f,.13f,.23f) : new Color(.64f,.76f,.79f);
            Cam.backgroundColor = RenderSettings.fogColor;
        }
        public bool Save() { try { Directory.CreateDirectory(Path.GetDirectoryName(SavePath)); File.WriteAllText(SavePath,Sim.SaveJson()); Toast("City saved. Your council can rest easy."); Audio.Play(CityAudio.Cue.Save); return true; } catch (Exception ex) { Toast("Save failed: " + ex.Message); Audio.Play(CityAudio.Cue.Denied); return false; } }
        public void Load() { try { if (!File.Exists(SavePath)) { Toast("No saved city yet. Use Save first."); Audio.Play(CityAudio.Cue.Denied); return; } Sim.LoadJson(File.ReadAllText(SavePath)); Audio.ResetCitySnapshot(); seenRevision = -1; nextTick=0; SetTool(TileKind.Empty); Selected=Hover=new Vector2Int(-1,-1); Rig.ResetView(); Toast("Saved city restored."); Audio.Play(CityAudio.Cue.Load); } catch (Exception ex) { Toast("Could not load city: " + ex.Message); Audio.Play(CityAudio.Cue.Denied); } }

    }
}
