// ---------------------------------------------------------------------------
// BroadsideArcUI.cs — Runtime broadside arc visualizer using LineRenderer.
// ---------------------------------------------------------------------------
// Draws port (left) and starboard (right) broadside sector arcs on the XZ
// plane around the owning ship. Arc colour reflects reload status:
//   • Green (semi-transparent) when the side is ready to fire.
//   • Orange-red (semi-transparent) when the side is still reloading.
//
// Attach this component to the same GameObject as BroadsideSystem and
// ShipController.  No external wiring needed — self-resolves on Awake.
//
// Also provides OnDrawGizmosSelected arcs for editor layout validation.
// ---------------------------------------------------------------------------

using UnityEngine;
using Booty.Combat;
using Booty.Ships;

namespace Booty.UI
{
    /// <summary>
    /// Visualises the port and starboard broadside firing sectors using
    /// LineRenderer ghost arcs and shows reload status via color tint.
    /// </summary>
    public class BroadsideArcUI : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Inspector Tuning
        // ══════════════════════════════════════════════════════════════════

        [Header("Arc Resolution")]
        [Tooltip("Number of points sampled along each arc edge. Higher = smoother.")]
        [SerializeField] private int arcResolution = 24;

        [Header("Line Width")]
        [SerializeField] private float lineWidth = 0.15f;

        [Header("Colors — Port (left)")]
        [SerializeField] private Color portReadyColor   = new Color(0.15f, 1.00f, 0.25f, 0.50f);
        [SerializeField] private Color portReloadColor  = new Color(1.00f, 0.35f, 0.05f, 0.30f);

        [Header("Colors — Starboard (right)")]
        [SerializeField] private Color stbdReadyColor   = new Color(0.15f, 1.00f, 0.25f, 0.50f);
        [SerializeField] private Color stbdReloadColor  = new Color(1.00f, 0.35f, 0.05f, 0.30f);

        // ══════════════════════════════════════════════════════════════════
        //  Private State
        // ══════════════════════════════════════════════════════════════════

        private BroadsideSystem _broadsideSystem;
        private ShipController  _shipController;

        private LineRenderer _portRenderer;
        private LineRenderer _stbdRenderer;
        private Material     _arcMaterial;

        // Elevation offset so arcs hover just above the ocean plane.
        private const float ArcYOffset = 0.08f;

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            _broadsideSystem = GetComponent<BroadsideSystem>();
            _shipController  = GetComponent<ShipController>();

            // Shared transparent material for both arcs.
            _arcMaterial = new Material(Shader.Find("Sprites/Default"));

            _portRenderer = CreateArcRenderer("BroadsideArc_Port");
            _stbdRenderer = CreateArcRenderer("BroadsideArc_Stbd");
        }

        private void Update()
        {
            if (_broadsideSystem == null || _shipController == null)
                return;

            UpdateArc(_portRenderer,
                      _shipController.Port,
                      _broadsideSystem.PortReady,
                      portReadyColor,
                      portReloadColor);

            UpdateArc(_stbdRenderer,
                      _shipController.Starboard,
                      _broadsideSystem.StarboardReady,
                      stbdReadyColor,
                      stbdReloadColor);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Arc Construction
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a child GameObject with a configured LineRenderer for one
        /// broadside arc.
        /// </summary>
        private LineRenderer CreateArcRenderer(string childName)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);

            var lr = child.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop          = true;   // auto-closes sector: arcRight → origin
            lr.startWidth    = lineWidth;
            lr.endWidth      = lineWidth;
            lr.material      = _arcMaterial;
            lr.sortingOrder  = 5;      // render above gameplay geometry

            return lr;
        }

        /// <summary>
        /// Rebuilds the world-space point list for one broadside sector each frame.
        /// The sector is traced as:  origin → leftEdge → (arc) → rightEdge → (loop to origin).
        /// </summary>
        /// <param name="lr">The LineRenderer to update.</param>
        /// <param name="centerDirection">XZ direction toward the broadside centre.</param>
        /// <param name="isReady">True if the side has finished reloading.</param>
        /// <param name="readyColor">Color when ready to fire.</param>
        /// <param name="reloadColor">Color while reloading.</param>
        private void UpdateArc(LineRenderer   lr,
                                Vector3        centerDirection,
                                bool           isReady,
                                Color          readyColor,
                                Color          reloadColor)
        {
            Color tint = isReady ? readyColor : reloadColor;
            lr.startColor = tint;
            lr.endColor   = tint;

            float range     = _broadsideSystem.FiringRange;
            float halfAngle = _broadsideSystem.HalfAngle;

            // Sector origin sits slightly above the ocean plane.
            Vector3 origin = transform.position;
            origin.y = ArcYOffset;

            // Point layout:  [0] = origin,  [1..arcResolution] = arc from left to right.
            // lr.loop=true adds the implicit edge from the last arc point back to origin.
            int totalPoints = arcResolution + 1;
            lr.positionCount = totalPoints;

            // Position 0: sector tip (ship centre).
            lr.SetPosition(0, origin);

            // Positions 1..arcResolution: fan from -halfAngle to +halfAngle.
            for (int i = 0; i < arcResolution; i++)
            {
                float t        = (float)i / Mathf.Max(1, arcResolution - 1);
                float angleDeg = Mathf.Lerp(-halfAngle, halfAngle, t);

                Quaternion rot = Quaternion.Euler(0f, angleDeg, 0f);
                Vector3    dir = rot * centerDirection;

                Vector3 point = origin + dir * range;
                point.y = ArcYOffset;

                lr.SetPosition(1 + i, point);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Editor Gizmos
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Draws broadside sectors in the Scene view when this object is selected.
        /// Port arc is drawn in green, starboard in cyan.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            var bs = GetComponent<BroadsideSystem>();
            var sc = GetComponent<ShipController>();
            if (bs == null || sc == null)
                return;

            DrawGizmoSector(sc.Port,      bs.FiringRange, bs.HalfAngle, Color.green);
            DrawGizmoSector(sc.Starboard, bs.FiringRange, bs.HalfAngle, Color.cyan);
        }

        /// <summary>
        /// Draws a single sector (two edge rays + arc) in the Scene view.
        /// </summary>
        private void DrawGizmoSector(Vector3 centerDir, float range, float halfAngle, Color color)
        {
            Gizmos.color = color;

            // Edge rays.
            Vector3 origin    = transform.position;
            Vector3 leftEdge  = origin + Quaternion.Euler(0f, -halfAngle, 0f) * centerDir * range;
            Vector3 rightEdge = origin + Quaternion.Euler(0f,  halfAngle, 0f) * centerDir * range;

            Gizmos.DrawLine(origin, leftEdge);
            Gizmos.DrawLine(origin, rightEdge);

            // Arc segment lines.
            const int GizmoResolution = 12;
            for (int i = 0; i < GizmoResolution - 1; i++)
            {
                float t0 = (float)i       / (GizmoResolution - 1);
                float t1 = (float)(i + 1) / (GizmoResolution - 1);

                float a0 = Mathf.Lerp(-halfAngle, halfAngle, t0);
                float a1 = Mathf.Lerp(-halfAngle, halfAngle, t1);

                Vector3 p0 = origin + Quaternion.Euler(0f, a0, 0f) * centerDir * range;
                Vector3 p1 = origin + Quaternion.Euler(0f, a1, 0f) * centerDir * range;

                Gizmos.DrawLine(p0, p1);
            }
        }
    }
}
