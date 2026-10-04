using IdleGrounds.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace IdleGrounds.Editor
{
    /// <summary>Creates Assets/_Project/Scenes/Game.unity from scratch (reproducible) and registers it as build index 0.</summary>
    public static class GameSceneBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/Game.unity";

        const string ActionsPath = "Assets/_Project/Input/IdleGroundsControls.inputactions";

        static void AssignUiActions(InputSystemUIInputModule m)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(ActionsPath);
            if (asset == null) { Debug.LogWarning("[IdleGrounds] Missing " + ActionsPath); return; }
            UnityEngine.InputSystem.InputActionReference Ref(string n)
            {
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ActionsPath))
                    if (o is UnityEngine.InputSystem.InputActionReference r && r.name == n) return r;
                return null;
            }
            m.actionsAsset = asset;
            m.point = Ref("UI/Point");
            m.leftClick = Ref("UI/Click");
            m.rightClick = Ref("UI/RightClick");
            m.middleClick = Ref("UI/MiddleClick");
            m.scrollWheel = Ref("UI/ScrollWheel");
            m.move = Ref("UI/Navigate");
            m.submit = Ref("UI/Submit");
            m.cancel = Ref("UI/Cancel");
        }

        [MenuItem("Idle Grounds/Scene/Build Game Scene")]
        public static void BuildGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);

            // --- Systems
            var systems = new GameObject("--- Systems");
            new GameObject("GameRunner").transform.SetParent(systems.transform, false);

            // Main Camera
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 9.84f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = IslandArtBuilder.SkyBase;     // the sky gradient's base (ADR 0003)
            cam.nearClipPlane = -50f;
            cam.farClipPlane = 50f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<UniversalAdditionalCameraData>();
            camGo.AddComponent<CameraController>();
            camGo.transform.position = new Vector3(144.5f, -12.5f, -10f);

            // World
            WorldBuilder.BuildIslands();

            // Runtime containers
            var runtime = new GameObject("Runtime");
            foreach (var n in new[] { "Nodes", "Ground", "Buildings", "Enemies", "Wisps", "FX" })
                new GameObject(n).transform.SetParent(runtime.transform, false);

            // UI
            var ui = new GameObject("UI");
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(ui.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var es = new GameObject("EventSystem");
            es.transform.SetParent(ui.transform, false);
            es.AddComponent<EventSystem>();
            var module = es.AddComponent<InputSystemUIInputModule>();
            AssignUiActions(module);

            // M2 core loop: prefabs, runner, views, input, HUD
            CoreLoopBuilder.BuildCorePrefabs();
            CoreLoopBuilder.InstallIntoActiveScene();
            IslandsBuilder.InstallIntoActiveScene();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[IdleGrounds] Game scene saved to " + ScenePath);
        }
    }
}
