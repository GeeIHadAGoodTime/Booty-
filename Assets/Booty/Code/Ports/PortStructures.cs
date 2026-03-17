// ---------------------------------------------------------------------------
// PortStructures.cs — Procedural port settlement visuals
// ---------------------------------------------------------------------------
// Builds a visual port settlement from primitive GameObjects. Attach this
// alongside PortVisual on a port root GameObject. Call Build() once after
// the port is created to instantiate all structures as children.
// ---------------------------------------------------------------------------

using UnityEngine;

namespace Booty.Ports
{
    /// <summary>
    /// Builds a visual port settlement using Unity primitive GameObjects.
    /// Manages faction-colored banner and dock glow light.
    /// Attach alongside PortVisual on each port GameObject.
    /// </summary>
    public class PortStructures : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Runtime References
        // ══════════════════════════════════════════════════════════════════

        /// <summary>The faction banner renderer — can be read by PortVisual or other systems.</summary>
        public Renderer FactionBannerRenderer => _factionBannerRenderer;

        private Renderer _factionBannerRenderer;
        private Light    _dockLight;

        private static readonly Color GoldGlow    = new Color(1.0f, 0.8f, 0.2f);
        private static readonly Color DefaultGlow = Color.white;

        // ══════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Instantiate all port structures as children of <paramref name="parent"/>.
        /// Safe to call once — repeated calls add duplicate geometry.
        /// </summary>
        /// <param name="parent">The port root transform.</param>
        public void Build(Transform parent)
        {
            BuildWarehouse(parent);
            BuildDock(parent);
            BuildDockPosts(parent);
            BuildFactionFlag(parent);
            BuildTower(parent);
            BuildDockLight(parent);
        }

        /// <summary>
        /// Update the dock glow light color and the faction banner to match
        /// a given faction color.
        /// </summary>
        /// <param name="color">The faction's representative color.</param>
        public void SetFactionColor(Color color)
        {
            if (_factionBannerRenderer != null)
            {
                ApplyColor(_factionBannerRenderer, color);
            }

            if (_dockLight != null)
            {
                _dockLight.color = color;
            }
        }

        /// <summary>
        /// Toggle a warm gold glow on the dock light to indicate the port
        /// can currently be captured.
        /// </summary>
        /// <param name="capturable">True = enable gold glow; false = restore normal light.</param>
        public void SetCapturableGlow(bool capturable)
        {
            if (_dockLight == null) return;

            _dockLight.color     = capturable ? GoldGlow : DefaultGlow;
            _dockLight.intensity = capturable ? 3.0f     : 1.5f;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Structure Builders
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Large storage building at the dock.</summary>
        private void BuildWarehouse(Transform parent)
        {
            var warehouse = GameObject.CreatePrimitive(PrimitiveType.Cube);
            warehouse.name = "Warehouse";
            warehouse.transform.SetParent(parent, worldPositionStays: false);
            warehouse.transform.localPosition = new Vector3(1.5f, 1.0f, 0.5f);
            warehouse.transform.localScale    = new Vector3(2.5f, 1.8f, 2.0f);

            ApplyColor(warehouse.GetComponent<Renderer>(), new Color(0.72f, 0.65f, 0.55f));
            RemoveCollider(warehouse);
        }

        /// <summary>Long wooden platform extending toward the water.</summary>
        private void BuildDock(Transform parent)
        {
            var dock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dock.name = "Dock";
            dock.transform.SetParent(parent, worldPositionStays: false);
            dock.transform.localPosition = new Vector3(0f, 0.2f, -3.5f);
            dock.transform.localScale    = new Vector3(0.8f, 0.3f, 3.0f);

            ApplyColor(dock.GetComponent<Renderer>(), new Color(0.45f, 0.35f, 0.22f));
            RemoveCollider(dock);
        }

        /// <summary>Two mooring posts at the end of the dock.</summary>
        private void BuildDockPosts(Transform parent)
        {
            CreatePost(parent, new Vector3(-0.3f, 0.5f, -4.8f));
            CreatePost(parent, new Vector3( 0.3f, 0.5f, -4.8f));
        }

        private void CreatePost(Transform parent, Vector3 localPos)
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "DockPost";
            post.transform.SetParent(parent, worldPositionStays: false);
            post.transform.localPosition = localPos;
            post.transform.localScale    = new Vector3(0.15f, 0.8f, 0.15f);

            ApplyColor(post.GetComponent<Renderer>(), new Color(0.4f, 0.3f, 0.2f));
            RemoveCollider(post);
        }

        /// <summary>Flag pole + faction-colored banner at the warehouse corner.</summary>
        private void BuildFactionFlag(Transform parent)
        {
            // Flag pole
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "FlagPole";
            pole.transform.SetParent(parent, worldPositionStays: false);
            pole.transform.localPosition = new Vector3(-1.0f, 1.5f, 1.0f);
            pole.transform.localScale    = new Vector3(0.08f, 2.5f, 0.08f);

            ApplyColor(pole.GetComponent<Renderer>(), new Color(0.6f, 0.55f, 0.45f));
            RemoveCollider(pole);

            // Flag banner (faction-colored)
            var banner = GameObject.CreatePrimitive(PrimitiveType.Cube);
            banner.name = "FactionBanner";
            banner.transform.SetParent(parent, worldPositionStays: false);
            banner.transform.localPosition = new Vector3(-0.6f, 2.8f, 1.0f);
            banner.transform.localScale    = new Vector3(0.8f, 0.4f, 0.05f);

            _factionBannerRenderer = banner.GetComponent<Renderer>();
            // Default banner color — will be overwritten by SetFactionColor or PortVisual
            ApplyColor(_factionBannerRenderer, new Color(0.63f, 0.63f, 0.63f));
            RemoveCollider(banner);

            // Let PortVisual take ownership of banner coloring if present
            var portVisual = GetComponent<PortVisual>();
            if (portVisual != null)
            {
                // Trigger a refresh so PortVisual colors the banner via its existing ApplyColor path.
                // PortVisual.RefreshVisuals() only applies color to its serialised renderer fields,
                // so we push the current faction color directly here instead.
                string faction = "neutral_traders"; // safe default before Configure()
                Color factionColor = portVisual.GetFactionColor(faction);
                ApplyColor(_factionBannerRenderer, factionColor);
            }
        }

        /// <summary>Tall stone tower for visual silhouette variety.</summary>
        private void BuildTower(Transform parent)
        {
            var tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tower.name = "WatchTower";
            tower.transform.SetParent(parent, worldPositionStays: false);
            tower.transform.localPosition = new Vector3(-1.5f, 1.5f, -0.5f);
            tower.transform.localScale    = new Vector3(0.6f, 2.5f, 0.6f);

            ApplyColor(tower.GetComponent<Renderer>(), new Color(0.65f, 0.58f, 0.48f));
            RemoveCollider(tower);
        }

        /// <summary>Dock glow indicator — point light above dock, faction-colored.</summary>
        private void BuildDockLight(Transform parent)
        {
            var lightGO = new GameObject("DockLight");
            lightGO.transform.SetParent(parent, worldPositionStays: false);
            lightGO.transform.localPosition = new Vector3(0f, 2.0f, -2.0f);

            _dockLight           = lightGO.AddComponent<Light>();
            _dockLight.type      = LightType.Point;
            _dockLight.range     = 15f;
            _dockLight.intensity = 1.5f;
            _dockLight.color     = DefaultGlow;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Helpers
        // ══════════════════════════════════════════════════════════════════

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>
        /// Apply a color to a renderer using MaterialPropertyBlock (avoids material instance leak).
        /// Handles both URP (_BaseColor) and Built-in (_Color) shaders.
        /// </summary>
        private static void ApplyColor(Renderer rend, Color color)
        {
            if (rend == null) return;

            var block = new MaterialPropertyBlock();
            rend.GetPropertyBlock(block);

            if (rend.sharedMaterial != null && rend.sharedMaterial.HasProperty(BaseColorId))
                block.SetColor(BaseColorId, color);
            else
                block.SetColor("_Color", color);

            rend.SetPropertyBlock(block);
        }

        /// <summary>
        /// Remove the default collider from a decorative object.
        /// Port trigger is on the parent SphereCollider; decorative meshes must not block it.
        /// </summary>
        private static void RemoveCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null)
                Object.Destroy(col);
        }
    }
}
