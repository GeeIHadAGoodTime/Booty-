// ---------------------------------------------------------------------------
// ShipVisual.cs — Procedural ship visual layer
// ---------------------------------------------------------------------------
// Builds a ship mesh at runtime from three parts: hull, mast, sail.
// Adds a wake TrailRenderer at the stern and animates the sail each frame.
//
// Usage:
//   var sv = shipGO.AddComponent<ShipVisual>();
//   sv.Initialize();
//   sv.Configure("player_pirates", ShipTier.Sloop);
// ---------------------------------------------------------------------------

using UnityEngine;

namespace Booty.Ships
{
    // =========================================================================
    //  ShipTier enum
    // =========================================================================

    /// <summary>Size / class of a ship, affects visual scale.</summary>
    public enum ShipTier
    {
        Sloop      = 0,   // scale 1.0 — small, fast
        Brigantine = 1,   // scale 1.4 — medium
        Galleon    = 2,   // scale 2.0 — large, heavy
    }

    // =========================================================================
    //  ShipVisual MonoBehaviour
    // =========================================================================

    /// <summary>
    /// Procedurally assembles the visual representation of a ship:
    /// hull mesh, mast cylinder, sail quad, wake trail, and per-frame animation.
    /// </summary>
    public class ShipVisual : MonoBehaviour
    {
        // ── Hull geometry constants ──────────────────────────────────────────
        private const float HullLength      = 4.0f;
        private const float HullWidth       = 1.2f;
        private const float HullHeight      = 0.55f;
        private const int   LongSegments    = 8;    // slices along Z
        private const int   CrossPoints     = 6;    // points per cross-section

        // ── Mast constants ───────────────────────────────────────────────────
        private const float MastHeight      = 3.0f;
        private const float MastRadius      = 0.08f;

        // ── Sail constants ───────────────────────────────────────────────────
        private const float SailWidth       = 1.4f;
        private const float SailHeight      = 2.2f;

        // ── Sail animation ───────────────────────────────────────────────────
        private const float SailSwaySpeed   = 1.2f;
        private const float SailSwayAmount  = 3.0f;   // degrees

        // ── Runtime references ───────────────────────────────────────────────
        private ShipController _shipController;
        private GameObject     _modelGO;
        private GameObject     _mastGO;
        private GameObject     _sailGO;
        private TrailRenderer  _wakeTrail;
        private Material       _hullMaterial;
        private Material       _sailMaterial;

        private float          _sailPhaseOffset;
        private float          _tierScale = 1.0f;

        // =====================================================================
        //  Public API
        // =====================================================================

        /// <summary>
        /// Build all mesh components (hull, mast, sail, wake).
        /// Call once after AddComponent.
        /// </summary>
        public void Initialize()
        {
            _shipController   = GetComponent<ShipController>();
            _sailPhaseOffset  = Random.Range(0f, Mathf.PI * 2f);

            BuildHull();
            BuildMast();
            BuildSail();
            BuildWakeTrail();
        }

        /// <summary>
        /// Apply faction colours and tier scale.
        /// Safe to call after Initialize().
        /// </summary>
        public void Configure(string factionId, ShipTier tier)
        {
            // ── Tier scale ───────────────────────────────────────────────────
            _tierScale = tier switch
            {
                ShipTier.Sloop      => 1.0f,
                ShipTier.Brigantine => 1.4f,
                ShipTier.Galleon    => 2.0f,
                _                   => 1.0f,
            };
            transform.localScale = Vector3.one * _tierScale;

            // ── Faction colours ──────────────────────────────────────────────
            Color hullColor;
            Color sailTint;

            switch (factionId)
            {
                case "player_pirates":
                    hullColor = new Color(0.12f, 0.22f, 0.45f);
                    sailTint  = new Color(0.90f, 0.85f, 0.70f);
                    break;
                case "crown_navy":
                    hullColor = new Color(0.15f, 0.30f, 0.65f);
                    sailTint  = new Color(0.95f, 0.95f, 1.00f);
                    break;
                case "merchant_guild":
                    hullColor = new Color(0.45f, 0.30f, 0.15f);
                    sailTint  = new Color(0.85f, 0.80f, 0.70f);
                    break;
                default: // enemy / unknown
                    hullColor = new Color(0.50f, 0.12f, 0.12f);
                    sailTint  = new Color(0.80f, 0.75f, 0.70f);
                    break;
            }

            if (_hullMaterial != null) _hullMaterial.color = hullColor;
            if (_sailMaterial != null) _sailMaterial.color = sailTint;
        }

        /// <summary>Show or hide the entire visual layer.</summary>
        public void SetVisible(bool visible)
        {
            if (_modelGO  != null) _modelGO.SetActive(visible);
            if (_mastGO   != null) _mastGO.SetActive(visible);
            if (_sailGO   != null) _sailGO.SetActive(visible);
            if (_wakeTrail != null) _wakeTrail.enabled = visible;
        }

        // =====================================================================
        //  Unity Lifecycle
        // =====================================================================

        private void Update()
        {
            AnimateSail();
            UpdateWakeEmission();
        }

        // =====================================================================
        //  Hull Construction
        // =====================================================================

        /// <summary>
        /// Builds a tapered boat-hull mesh and assigns it to the "Model" child.
        /// The hull is elongated along Z, widest at centre, tapering to near-zero
        /// at both ends. Cross-sections are semi-ellipses (flat bottom, curved sides).
        /// </summary>
        private void BuildHull()
        {
            // Find or create "Model" child
            Transform modelTransform = transform.Find("Model");
            if (modelTransform == null)
            {
                _modelGO = new GameObject("Model");
                _modelGO.transform.SetParent(transform, false);
            }
            else
            {
                _modelGO = modelTransform.gameObject;
            }

            var meshFilter   = _modelGO.GetComponent<MeshFilter>()
                               ?? _modelGO.AddComponent<MeshFilter>();
            var meshRenderer = _modelGO.GetComponent<MeshRenderer>()
                               ?? _modelGO.AddComponent<MeshRenderer>();

            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogWarning("[ShipVisual] No shader found — using fallback diffuse.");
                shader = Shader.Find("Hidden/InternalErrorShader");
            }
            _hullMaterial = new Material(shader)
            {
                color = new Color(0.50f, 0.12f, 0.12f) // default until Configure()
            };
            meshRenderer.material = _hullMaterial;

            meshFilter.mesh = BuildHullMesh();
        }

        private Mesh BuildHullMesh()
        {
            // LongSegments+1 rings, each with CrossPoints vertices
            // Plus separate top-cap vertices for the flat deck edge
            int totalSlices  = LongSegments + 1;           // 0..8  → 9 slices
            int vertsPerSlice = CrossPoints;               // 6 per ring
            int totalVerts   = totalSlices * vertsPerSlice;

            var vertices  = new Vector3[totalVerts];
            var normals   = new Vector3[totalVerts];
            var uv        = new Vector2[totalVerts];

            // Build vertex grid
            for (int s = 0; s < totalSlices; s++)
            {
                float t          = (float)s / LongSegments;           // 0..1
                float zPos       = (t - 0.5f) * HullLength;           // -half..+half

                // Width envelope: sin curve — widest at centre (t=0.5), pointed at ends
                float widthScale = Mathf.Sin(Mathf.PI * t);
                float sliceWidth = HullWidth * widthScale;
                // Ensure bow/stern have a minimum so they form a visible cap
                sliceWidth = Mathf.Max(sliceWidth, HullWidth * 0.05f);

                for (int c = 0; c < CrossPoints; c++)
                {
                    // c goes from 0 (port waterline) to CrossPoints-1 (starboard waterline)
                    // via the keel at c = CrossPoints/2 - 0.5 … use a half-ellipse
                    float angle = Mathf.PI * c / (CrossPoints - 1); // 0..PI
                    float xNorm = Mathf.Cos(angle);   // +1 → -1 across starboard to port
                    float yNorm = Mathf.Sin(angle);   // 0 at ends, +1 at keel bottom

                    // x: half-width, y: depth below waterline (negative = down)
                    float x = xNorm * sliceWidth * 0.5f;
                    float y = -yNorm * HullHeight;    // keel dips below y=0

                    int idx = s * vertsPerSlice + c;
                    vertices[idx] = new Vector3(x, y, zPos);

                    // Normal: approximate outward from hull centre column
                    Vector3 n = new Vector3(xNorm, yNorm, 0f).normalized;
                    normals[idx] = n;

                    uv[idx] = new Vector2(t, (float)c / (CrossPoints - 1));
                }
            }

            // Build triangles for the side surface (quad strips between adjacent slices)
            var triangles = new System.Collections.Generic.List<int>();

            for (int s = 0; s < LongSegments; s++)
            {
                for (int c = 0; c < CrossPoints - 1; c++)
                {
                    int i0 = s       * vertsPerSlice + c;
                    int i1 = s       * vertsPerSlice + c + 1;
                    int i2 = (s + 1) * vertsPerSlice + c;
                    int i3 = (s + 1) * vertsPerSlice + c + 1;

                    // Two triangles per quad
                    triangles.Add(i0); triangles.Add(i2); triangles.Add(i1);
                    triangles.Add(i1); triangles.Add(i2); triangles.Add(i3);
                }
            }

            // End caps (bow and stern) — fan from centre point
            // Bow cap: s = LongSegments (stern: +Z end)
            AddEndCap(triangles, vertices, normals, uv, LongSegments * vertsPerSlice, true);
            // Stern cap: s = 0 (bow end, −Z)
            AddEndCap(triangles, vertices, normals, uv, 0, false);

            var mesh = new Mesh { name = "ProceduralHull" };
            mesh.vertices  = vertices;
            mesh.normals   = normals;
            mesh.uv        = uv;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Appends a fan-cap for the first or last ring of the hull.
        /// Because the ring vertices are already near the centreline at ends,
        /// we just fan between adjacent edge verts using the first ring vert as hub.
        /// </summary>
        private static void AddEndCap(
            System.Collections.Generic.List<int> tris,
            Vector3[] verts, Vector3[] normals, Vector2[] uvs,
            int ringStart, bool facingPositiveZ)
        {
            // Fan from ringStart vertex 0 through all others
            int hub = ringStart;
            for (int c = 1; c < CrossPoints - 1; c++)
            {
                int a = ringStart + c;
                int b = ringStart + c + 1;
                if (facingPositiveZ)
                {
                    tris.Add(hub); tris.Add(b); tris.Add(a);
                }
                else
                {
                    tris.Add(hub); tris.Add(a); tris.Add(b);
                }
            }
        }

        // =====================================================================
        //  Mast Construction
        // =====================================================================

        private void BuildMast()
        {
            _mastGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _mastGO.name = "Mast";
            _mastGO.transform.SetParent(transform, false);

            // Position: slightly forward of centre, raised above hull
            _mastGO.transform.localPosition = new Vector3(
                0f,
                HullHeight + MastHeight * 0.5f,
                HullLength * 0.10f
            );
            _mastGO.transform.localScale = new Vector3(MastRadius, MastHeight * 0.5f, MastRadius);

            // Remove the collider — purely visual
            var col = _mastGO.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            var mastShader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Standard");
            if (mastShader == null)
            {
                Debug.LogWarning("[ShipVisual] Mast: no shader found — using fallback.");
                mastShader = Shader.Find("Hidden/InternalErrorShader");
            }
            var mastMat = new Material(mastShader)
            {
                color = new Color(0.35f, 0.22f, 0.12f)
            };
            _mastGO.GetComponent<Renderer>().material = mastMat;
        }

        // =====================================================================
        //  Sail Construction
        // =====================================================================

        private void BuildSail()
        {
            _sailGO = new GameObject("Sail");
            _sailGO.transform.SetParent(transform, false);

            // Hang sail from mast, slightly above mid-height
            _sailGO.transform.localPosition = new Vector3(
                0f,
                HullHeight + MastHeight * 0.35f,
                HullLength * 0.10f
            );

            var mf = _sailGO.AddComponent<MeshFilter>();
            var mr = _sailGO.AddComponent<MeshRenderer>();

            var sailShader = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Standard");
            if (sailShader == null)
            {
                Debug.LogWarning("[ShipVisual] Sail: no shader found — using fallback.");
                sailShader = Shader.Find("Hidden/InternalErrorShader");
            }
            _sailMaterial = new Material(sailShader)
            {
                color = new Color(0.95f, 0.92f, 0.85f) // default off-white; Configure() overrides
            };
            mr.material = _sailMaterial;

            mf.mesh = BuildSailMesh();
        }

        /// <summary>
        /// Quad sail mesh (4 verts, 2 tris, double-sided via 4+4 verts).
        /// Top edge vertices are offset in +Z to simulate a small wind billow.
        /// </summary>
        private static Mesh BuildSailMesh()
        {
            float hw     = SailWidth  * 0.5f;
            float hh     = SailHeight * 0.5f;
            float billow = 0.18f; // +Z offset on top edge for wind-fill illusion

            // Front face
            var verts = new Vector3[8];
            verts[0] = new Vector3(-hw, -hh, 0f);         // BL
            verts[1] = new Vector3( hw, -hh, 0f);         // BR
            verts[2] = new Vector3(-hw,  hh, billow);     // TL (bowed forward)
            verts[3] = new Vector3( hw,  hh, billow);     // TR (bowed forward)

            // Back face (reversed winding)
            verts[4] = new Vector3( hw, -hh, 0f);
            verts[5] = new Vector3(-hw, -hh, 0f);
            verts[6] = new Vector3( hw,  hh, billow);
            verts[7] = new Vector3(-hw,  hh, billow);

            var uvs = new Vector2[]
            {
                new(0, 0), new(1, 0), new(0, 1), new(1, 1),
                new(0, 0), new(1, 0), new(0, 1), new(1, 1),
            };

            var tris = new int[]
            {
                0, 2, 1,  1, 2, 3,   // front
                4, 6, 5,  5, 6, 7,   // back
            };

            var mesh = new Mesh { name = "ProceduralSail" };
            mesh.vertices  = verts;
            mesh.uv        = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // =====================================================================
        //  Wake Trail
        // =====================================================================

        private void BuildWakeTrail()
        {
            var wakeGO = new GameObject("WakeTrail");
            wakeGO.transform.SetParent(transform, false);
            wakeGO.transform.localPosition = new Vector3(0f, -0.2f, -HullLength * 0.4f);

            _wakeTrail = wakeGO.AddComponent<TrailRenderer>();
            _wakeTrail.time        = 1.5f;
            _wakeTrail.startWidth  = 0.8f;
            _wakeTrail.endWidth    = 0.05f;
            _wakeTrail.emitting    = false;

            var wakeShader = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Standard");
            if (wakeShader == null)
            {
                Debug.LogWarning("[ShipVisual] Wake: no shader found — using fallback.");
                wakeShader = Shader.Find("Hidden/InternalErrorShader");
            }
            var wakeMat = new Material(wakeShader)
            {
                color = new Color(0.85f, 0.90f, 0.95f, 0.60f)
            };
            _wakeTrail.material = wakeMat;
        }

        // =====================================================================
        //  Per-Frame Animation
        // =====================================================================

        private void AnimateSail()
        {
            if (_sailGO == null) return;

            // Yaw sway
            float swayAngle = SailSwayAmount
                            * Mathf.Sin(Time.time * SailSwaySpeed + _sailPhaseOffset);
            _sailGO.transform.localRotation = Quaternion.Euler(0f, swayAngle, 0f);

            // Z-scale oscillation to simulate wind billowing
            float billowScale = 1.0f + 0.05f * Mathf.Sin(Time.time * 0.8f + _sailPhaseOffset);
            Vector3 s = _sailGO.transform.localScale;
            s.z = billowScale;
            _sailGO.transform.localScale = s;
        }

        private void UpdateWakeEmission()
        {
            if (_wakeTrail == null || _shipController == null) return;
            _wakeTrail.emitting = _shipController.SpeedNormalized > 0.05f;
        }
    }
}
