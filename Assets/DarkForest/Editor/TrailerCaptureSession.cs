using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class TrailerCaptureSession
{
    static readonly string[] Scenes = { "AreaA_BurningVillage", "AreaB_DarkForest", "AreaC_ClockTower" };
    static double readyAt;
    static TrailerCaptureSession() { EditorApplication.update += Tick; }
    public static void Begin()
    {
        if (EditorApplication.isPlaying) throw new System.Exception("Exit play mode before capture.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.isDirty) {
            Directory.CreateDirectory("TrailerCaptures/Recovery");
            EditorSceneManager.SaveScene(scene, "TrailerCaptures/Recovery/BeforeTrailer.unity", true);
        }
        SessionState.SetString("TrailerOriginalScene", scene.path);
        SessionState.SetInt("TrailerIndex", 0);
        SessionState.SetInt("TrailerPhase", 0);
        SessionState.SetBool("TrailerActive", true);
    }
    static void Tick()
    {
        if (!SessionState.GetBool("TrailerActive", false) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying) return;
        int index = SessionState.GetInt("TrailerIndex", 0);
        int phase = SessionState.GetInt("TrailerPhase", 0);
        if (phase == 0 && !EditorApplication.isPlaying) {
            if (index >= 3) {
                SessionState.SetBool("TrailerActive", false);
                EditorSceneManager.OpenScene(SessionState.GetString("TrailerOriginalScene", "Assets/DarkForest/Scenes/AreaA_BurningVillage.unity"));
                File.WriteAllText("TrailerCaptures/capture-complete.txt", "Captured 300 frames per scene at 30 fps.");
                return;
            }
            EditorSceneManager.OpenScene("Assets/DarkForest/Scenes/" + Scenes[index] + ".unity");
            SessionState.SetInt("TrailerPhase", 1);
            EditorApplication.isPlaying = true;
            readyAt = 0;
        } else if (phase == 1 && EditorApplication.isPlaying) {
            if (readyAt == 0) readyAt = EditorApplication.timeSinceStartup + 2;
            if (EditorApplication.timeSinceStartup < readyAt || !Camera.main) return;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.gameObject.SetActive(false);
            var recorder = Camera.main.gameObject.AddComponent<FlythroughRecorder>();
            recorder.startX = index == 2 ? 20 : 18;
            recorder.endX = index == 2 ? 135 : 140;
            recorder.seconds = 10; recorder.fps = 30;
            recorder.width = 1920; recorder.height = 1080;
            recorder.zoom = new Vector2(8.5f, 10f);
            recorder.folder = Path.GetFullPath("TrailerCaptures/" + Scenes[index]);
            SessionState.SetInt("TrailerPhase", 2);
        } else if (phase == 2 && EditorApplication.isPlaying) {
            var recorder = Object.FindFirstObjectByType<FlythroughRecorder>();
            if (recorder && recorder.Done) {
                SessionState.SetInt("TrailerIndex", index + 1);
                SessionState.SetInt("TrailerPhase", 0);
                EditorApplication.isPlaying = false;
            }
        }
    }
}
