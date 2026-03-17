// ---------------------------------------------------------------------------
// MainMenuSceneSetup.cs — Editor utility to build the MainMenu scene
// ---------------------------------------------------------------------------
// Invoke from Unity menu: Booty > Setup MainMenu Scene
// Creates an empty scene with a single "MainMenu" GameObject + MainMenuManager,
// then saves to Assets/Booty/Scenes/MainMenu.unity.
// ---------------------------------------------------------------------------

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Booty.UI;

namespace Booty.Editor
{
    /// <summary>
    /// One-click scene scaffolding for the P1 MainMenu scene.
    /// Run once per dev machine or after a clean clone.
    /// </summary>
    public static class MainMenuSceneSetup
    {
        private const string SceneSavePath = "Assets/Booty/Scenes/MainMenu.unity";
        private const string WorldMainPath = "Assets/Booty/Scenes/World_Main.unity";

        [MenuItem("Booty/Setup MainMenu Scene")]
        public static void SetupMainMenuScene()
        {
            // 1. Ensure the Scenes directory exists
            string fullScenesDir = Path.Combine(Application.dataPath, "Booty", "Scenes");
            if (!Directory.Exists(fullScenesDir))
            {
                Directory.CreateDirectory(fullScenesDir);
                Debug.Log("[MainMenuSceneSetup] Created directory: " + fullScenesDir);
            }

            // 2. Create a new empty scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 3. Create the MainMenu root GameObject and attach the manager
            var menuGO = new GameObject("MainMenu");
            menuGO.AddComponent<MainMenuManager>();

            // 4. Save the scene
            EditorSceneManager.SaveScene(scene, SceneSavePath);

            // 5. Add scenes to Build Settings
            //    MainMenu = index 0, World_Main = index 1
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            scenes.Add(new EditorBuildSettingsScene(SceneSavePath, true));

            if (File.Exists(Path.Combine(Application.dataPath, "..",
                WorldMainPath)))
            {
                scenes.Add(new EditorBuildSettingsScene(WorldMainPath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log("[MainMenuSceneSetup] MainMenu scene created at " + SceneSavePath +
                      " and registered in Build Settings (index 0).");

            // 6. Ping the new scene asset in the Project window
            AssetDatabase.Refresh();
            var asset = AssetDatabase.LoadAssetAtPath<Object>(SceneSavePath);
            if (asset != null)
                EditorGUIUtility.PingObject(asset);
        }
    }
}
