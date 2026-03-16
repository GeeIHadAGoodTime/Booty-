using UnityEngine;
using UnityEngine.Rendering;

namespace Booty.World
{
    /// <summary>
    /// Configures the scene environment: skybox, directional light, fog, and ambient light.
    /// Call <see cref="Apply"/> once after the scene is loaded (e.g. from OceanPlane.Initialize).
    ///
    /// All settings produce a warm golden-hour / sunset look appropriate for the
    /// isometric naval setting of Booty! A Pirates Rise.
    /// </summary>
    public class EnvironmentSetup : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector overrides (optional — sensible defaults are baked in)
        // -----------------------------------------------------------------------
        [Header("Sky")]
        [SerializeField] private Color skyTopColor      = new Color(1.00f, 0.60f, 0.30f); // warm orange-pink
        [SerializeField] private Color skyEquatorColor  = new Color(0.40f, 0.60f, 0.90f); // warm blue horizon
        [SerializeField] private Color skyGroundColor   = new Color(0.30f, 0.20f, 0.10f); // dark warm earth

        [Header("Directional Light")]
        [SerializeField] private Color  lightColor      = new Color(1.00f, 0.80f, 0.50f); // warm amber
        [SerializeField] private float  lightIntensity  = 1.2f;
        [SerializeField] private Vector3 lightRotation  = new Vector3(45f, -30f, 0f);     // low sun angle

        [Header("Fog")]
        [SerializeField] private Color fogColor         = new Color(0.70f, 0.50f, 0.35f); // warm atmospheric haze
        [SerializeField] private float  fogDensity      = 0.008f;

        [Header("Ambient")]
        [SerializeField] private Color ambientColor     = new Color(0.40f, 0.35f, 0.30f); // warm fill

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Apply all environment settings to <see cref="RenderSettings"/> and the
        /// scene's directional light. Safe to call multiple times (idempotent).
        /// </summary>
        public void Apply()
        {
            ApplySkybox();
            ApplyDirectionalLight();
            ApplyFog();
            ApplyAmbient();

            Debug.Log("[EnvironmentSetup] Environment applied: sunset sky, warm light, ocean haze.");
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        private void ApplySkybox()
        {
            // Unity's built-in gradient skybox — always present, no import needed.
            Shader gradientShader = Shader.Find("Skybox/Gradient");
            if (gradientShader == null)
            {
                Debug.LogWarning("[EnvironmentSetup] Skybox/Gradient shader not found; skipping skybox.");
                return;
            }

            var skyMat = new Material(gradientShader)
            {
                name = "OceanSunsetSkybox"
            };

            skyMat.SetColor("_SkyColor",      skyTopColor);
            skyMat.SetColor("_EquatorColor",   skyEquatorColor);
            skyMat.SetColor("_GroundColor",    skyGroundColor);

            RenderSettings.skybox = skyMat;

            // Re-bake ambient from the new skybox
            DynamicGI.UpdateEnvironment();
        }

        private void ApplyDirectionalLight()
        {
            // Try the tagged light first; fall back to the first directional in scene.
            Light dirLight = FindDirectionalLight();

            if (dirLight == null)
            {
                // Create one if the scene doesn't have a directional light at all.
                var lightGO = new GameObject("SunLight");
                lightGO.transform.SetParent(transform);
                dirLight = lightGO.AddComponent<Light>();
                dirLight.type = LightType.Directional;
                Debug.Log("[EnvironmentSetup] No directional light found; created SunLight.");
            }

            dirLight.color     = lightColor;
            dirLight.intensity = lightIntensity;
            dirLight.transform.rotation = Quaternion.Euler(lightRotation);
        }

        private void ApplyFog()
        {
            RenderSettings.fog        = true;
            RenderSettings.fogMode    = FogMode.Exponential;
            RenderSettings.fogColor   = fogColor;
            RenderSettings.fogDensity = fogDensity;
        }

        private void ApplyAmbient()
        {
            RenderSettings.ambientMode  = AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor;
        }

        // -----------------------------------------------------------------------
        // Utility
        // -----------------------------------------------------------------------

        private static Light FindDirectionalLight()
        {
            // 1. Try a GameObject tagged "DirectionalLight"
            var tagged = GameObject.FindWithTag("DirectionalLight");
            if (tagged != null)
            {
                var l = tagged.GetComponent<Light>();
                if (l != null && l.type == LightType.Directional)
                    return l;
            }

            // 2. Scan all lights in the scene
#if UNITY_2023_1_OR_NEWER
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
#else
            var lights = FindObjectsOfType<Light>();
#endif
            foreach (var light in lights)
            {
                if (light.type == LightType.Directional)
                    return light;
            }

            return null;
        }
    }
}
