using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Seabright.Editor
{
    /// <summary>Capture current game art without repeating the growth acceptance journey.</summary>
    [InitializeOnLoad]
    public static class ReviewCaptureTasks
    {
        const string Active = "Seabright.ReviewCapture.Active";
        const string Index = "Seabright.ReviewCapture.Index";
        const string Prepared = "Seabright.ReviewCapture.Prepared";
        static double captureAt;
        static readonly string[] Names = { "starter", "grown-city", "stadium", "skyline", "night" };
        static readonly Vector3[] Targets = {
            CitySimulation.World(12,24), CitySimulation.World(20,25), CitySimulation.World(22,31),
            CitySimulation.World(21,22)+Vector3.up*18, CitySimulation.World(20,25)
        };
        static readonly float[] Distances = { 185, 340, 160, 255, 340 };
        static ReviewCaptureTasks() { EditorApplication.update += Update; }

        [MenuItem("Seabright/Capture Review Images")]
        public static void Capture()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before capturing review images.");
            if (!File.Exists("Artifacts/grown-city-save.json")) throw new FileNotFoundException("Run the growth playtest once to create Artifacts/grown-city-save.json.");
            Directory.CreateDirectory("Documentation/Images");
            EditorSceneManager.OpenScene("Assets/Scenes/Seabright.unity");
            SessionState.SetInt(Index, 0); SessionState.SetBool(Prepared, false); SessionState.SetBool(Active, true);
            EditorApplication.isPlaying = true;
        }

        static void Update()
        {
            if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying) return;
            var game = CityGame.Instance;
            if (!game || game.Sim == null || !game.Audio || !game.Audio.Ready) return;
            try {
                int index = SessionState.GetInt(Index, 0);
                if (!SessionState.GetBool(Prepared, false)) {
                    game.Speed = 0; game.Photo = true; game.Help = game.Budget = false; game.HUD.CloseModal();
                    // Apply output gain directly so editor captures do not change persisted player preferences.
                    AudioListener.volume = 0;
                    if (index == 0) game.Sim.SeedStarterTown();
                    else game.Sim.LoadJson(File.ReadAllText("Artifacts/grown-city-save.json"));
                    game.Audio.ResetCitySnapshot(); game.SetTool(TileKind.Empty); game.SetOverlay(0);
                    game.Selected = game.Hover = new Vector2Int(-1, -1);
                    if (game.Night != (index == 4)) game.ToggleNight();
                    game.View.Refresh(); game.Rig.Focus = Targets[index]; game.Rig.Distance = Distances[index];
                    game.Rig.Yaw = -28; game.Rig.Pitch = 48;
                    captureAt = EditorApplication.timeSinceStartup + .8;
                    SessionState.SetBool(Prepared, true);
                    return;
                }
                if (EditorApplication.timeSinceStartup < captureAt) return;
                Quaternion rotation = Quaternion.Euler(48,-28,0);
                game.Cam.transform.SetPositionAndRotation(Targets[index]-rotation*Vector3.forward*Distances[index],rotation);
                CaptureCamera(game.Cam, "Documentation/Images/" + Names[index] + ".png");
                SessionState.SetInt(Index, index+1); SessionState.SetBool(Prepared, false);
                if (index+1 >= Names.Length) {
                    File.WriteAllText("Documentation/Images/capture-info.json", JsonUtility.ToJson(new CaptureInfo {
                        recordedAtUtc=DateTime.UtcNow.ToString("O"), unityVersion=Application.unityVersion,
                        sourceVersion=PlayerSettings.bundleVersion, width=1600, height=1000,
                        method="Unity Editor Play mode, live CityGame camera render with normal shaders and image effects; interface hidden. Starter seed and previously earned grown-city save; no repeated growth simulation."
                    }, true));
                    SessionState.SetBool(Active, false);
                    Debug.Log("SEABRIGHT_REVIEW_CAPTURE_COMPLETE Documentation/Images");
                    if (Application.isBatchMode) EditorApplication.Exit(0); else EditorApplication.isPlaying=false;
                }
            } catch (Exception ex) {
                SessionState.SetBool(Active, false); Debug.LogException(ex);
                if (Application.isBatchMode) EditorApplication.Exit(1); else EditorApplication.isPlaying=false;
            }
        }

        static void CaptureCamera(Camera camera, string path)
        {
            var target = RenderTexture.GetTemporary(1600,1000,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB,4);
            var previousTarget=camera.targetTexture; var previousActive=RenderTexture.active;
            Texture2D image=null;
            try {
                camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
                image=new Texture2D(1600,1000,TextureFormat.RGB24,false,false);
                image.ReadPixels(new Rect(0,0,1600,1000),0,0); image.Apply(); File.WriteAllBytes(path,image.EncodeToPNG());
            } finally {
                camera.targetTexture=previousTarget; RenderTexture.active=previousActive;
                if(image)UnityEngine.Object.DestroyImmediate(image); RenderTexture.ReleaseTemporary(target);
            }
        }
        [Serializable] sealed class CaptureInfo { public string recordedAtUtc,unityVersion,sourceVersion,method; public int width,height; }
    }
}
