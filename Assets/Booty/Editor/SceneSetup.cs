// ---------------------------------------------------------------------------
// SceneSetup.cs — Editor utility to build the World_Main scene from scratch
// ---------------------------------------------------------------------------
// Invoke from Unity menu: Booty > Setup World_Main Scene
// Creates ocean plane, Bootstrap GO, port markers, isometric camera, and
// a directional light, then saves to Assets/Booty/Scenes/World_Main.unity.
// ---------------------------------------------------------------------------

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Booty.Bootstrap;

namespace Booty.Editor
{
    /// <summary>
    /// One-click scene scaffolding for the P1 World_Main scene.
    /// Run once per dev machine or after a clean clone.
    /// </summary>
    public static class SceneSetup
    {
        private const string SceneSavePath = "Assets/Booty/Scenes/World_Main.unity";

        [MenuItem("Booty/Setup World_Main Scene")]
        public static void SetupWorldMainScene()
        {
            // 1. Create a new empty scene
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── Ocean Plane ───────────────────────────────────────────────
            GameObject ocean = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ocean.name = "OceanPlane";
            ocean.transform.localScale = new Vector3(100f, 1f, 100f);

            var oceanRenderer = ocean.GetComponent<Renderer>();
            if (oceanRenderer != null)
            {
                // Blue-ish ocean material
                oceanRenderer.sharedMaterial = new Material(Shader.Find("Standard"));
                oceanRenderer.sharedMaterial.color = new Color(0.10f, 0.35f, 0.65f, 1f);
            }

            // ── Bootstrap GO ─────────────────────────────────────────────
            GameObject bootstrap = new GameObject("Bootstrap");
            bootstrap.AddComponent<BootyBootstrap>();

            // ── Port Markers ─────────────────────────────────────────────
            // Positions are hardcoded to match ports.json (TextAssets are not
            // available at edit time, so we embed the values directly).
            CreatePortMarker("Port_port_haven",    new Vector3(-40f, 0f,  30f), new Color(1.00f, 0.85f, 0.00f)); // yellow
            CreatePortMarker("Port_fort_imperial", new Vector3( 50f, 0f,  40f), new Color(0.80f, 0.15f, 0.15f)); // red
            CreatePortMarker("Port_smugglers_cove",new Vector3( 10f, 0f, -50f), new Color(0.60f, 0.60f, 0.60f)); // grey
            CreatePortMarker("Port_isla_del_oro",  new Vector3(-30f, 0f, -30f), new Color(0.80f, 0.15f, 0.15f)); // red

            // ── Isometric Camera ─────────────────────────────────────────
            GameObject camGO = new GameObject("IsometricCamera");
            camGO.transform.rotation = Quaternion.Euler(45f, 45f, 0f);

            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 20f;

            // ── Directional Light ─────────────────────────────────────────
            GameObject lightGO = new GameObject("DirectionalLight");
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;

            // ── Save Scene ────────────────────────────────────────────────
            EditorSceneManager.SaveScene(scene, SceneSavePath);

            Debug.Log("Booty World_Main setup complete!");
        }

        /// <summary>
        /// Create a cylinder port marker at the given world position with a tinted material.
        /// </summary>
        private static GameObject CreatePortMarker(string markerName, Vector3 position, Color tint)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = markerName;
            marker.transform.position = position;
            marker.transform.localScale = new Vector3(3f, 2f, 3f);

            var rend = marker.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = new Material(Shader.Find("Standard"));
                rend.sharedMaterial.color = tint;
            }

            return marker;
        }
    }
}
