using UnityEngine;

namespace Booty.World
{
    /// <summary>
    /// Ocean visual surface setup.
    ///
    /// P1 → S3.2 upgrade: flat URP/Lit placeholder replaced with a subdivided procedural
    /// mesh driven by the custom <c>Booty/OceanWater</c> HLSL shader that animates vertex
    /// waves, foam, colour depth gradient, and specular highlights entirely on the GPU.
    ///
    /// Per PRD A3: "Water = visual/shader-based only. No physical waves or buoyancy."
    /// </summary>
    public class OceanPlane : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector fields
        // -----------------------------------------------------------------------
        [Header("Ocean Settings")]
        [SerializeField] private float oceanSize   = 500f;
        [SerializeField] private float yPosition   = -0.5f;
        [SerializeField] private int   subdivisions = 50;   // vertices per edge

        [Header("Water Shader Properties")]
        [SerializeField] private Color shallowColor    = new Color(0.10f, 0.40f, 0.70f, 0.85f);
        [SerializeField] private Color deepColor       = new Color(0.02f, 0.10f, 0.35f, 1.00f);
        [SerializeField] private Color foamColor       = new Color(0.90f, 0.95f, 1.00f, 1.00f);
        [SerializeField] private float waveAmplitude   = 0.3f;
        [SerializeField] private float waveFrequency   = 0.1f;
        [SerializeField] private float waveSpeed       = 1.5f;
        [SerializeField] private float foamThreshold   = 0.6f;
        [SerializeField] private float smoothness      = 0.85f;

        // Legacy colour kept for SetOceanColor() back-compat
        [Header("Legacy")]
        [SerializeField] private Color oceanColor      = new Color(0.1f, 0.3f, 0.6f, 1f);

        // -----------------------------------------------------------------------
        // Private state
        // -----------------------------------------------------------------------
        private GameObject  _oceanObject;
        private Renderer    _oceanRenderer;
        private Material    _oceanMaterial;

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Initialize the ocean. Called by GameRoot / RegionSetup during bootstrap.
        /// Idempotent — safe to call more than once.
        /// </summary>
        public void Initialize()
        {
            if (_oceanObject != null)
                return;

            CreateOcean();
            SetupEnvironment();

            Debug.Log($"[OceanPlane] Ocean initialised. Size={oceanSize}  Subdivisions={subdivisions}x{subdivisions}");
        }

        /// <summary>
        /// Update the shallow-water colour at runtime (preserves existing public API).
        /// </summary>
        public void SetOceanColor(Color color)
        {
            oceanColor    = color;
            shallowColor  = color;
            if (_oceanMaterial != null)
                _oceanMaterial.SetColor("_ShallowColor", color);
        }

        /// <returns>World-space Y of the nominal (unperturbed) ocean surface.</returns>
        public float GetSurfaceY() => yPosition;

        /// <returns>Half-size of the ocean plane in world units.</returns>
        public float GetWorldExtent() => oceanSize * 0.5f;

        /// <summary>Check whether <paramref name="worldPos"/> lies inside the ocean boundary.</summary>
        public bool IsWithinBounds(Vector3 worldPos)
        {
            float ext = oceanSize * 0.5f;
            return Mathf.Abs(worldPos.x) <= ext && Mathf.Abs(worldPos.z) <= ext;
        }

        // -----------------------------------------------------------------------
        // Internal — mesh creation
        // -----------------------------------------------------------------------

        private void CreateOcean()
        {
            _oceanObject = new GameObject("OceanSurface");
            _oceanObject.transform.SetParent(transform);
            _oceanObject.transform.position = new Vector3(0f, yPosition, 0f);

            // Attach mesh components
            var meshFilter   = _oceanObject.AddComponent<MeshFilter>();
            _oceanRenderer   = _oceanObject.AddComponent<MeshRenderer>();

            // Build subdivided plane mesh
            meshFilter.sharedMesh = CreateOceanMesh(subdivisions, oceanSize);

            // Build and assign material
            _oceanMaterial = CreateOceanMaterial();
            _oceanRenderer.material = _oceanMaterial;

            // Water layer (best-effort; falls back to Default if layer absent)
            int waterLayer = LayerMask.NameToLayer("Water");
            _oceanObject.layer = waterLayer >= 0 ? waterLayer : 0;
        }

        /// <summary>
        /// Generate a subdivided quad-plane mesh lying in the XZ plane, centred at origin.
        /// </summary>
        /// <param name="subdivs">Number of quads per axis (total verts = (subdivs+1)²).</param>
        /// <param name="size">Total side length in world units.</param>
        private static Mesh CreateOceanMesh(int subdivs, float size)
        {
            subdivs = Mathf.Max(2, subdivs);

            int   vertCount = (subdivs + 1) * (subdivs + 1);
            int   triCount  = subdivs * subdivs * 6;           // 2 tris per quad × 3 indices

            var vertices  = new Vector3[vertCount];
            var normals   = new Vector3[vertCount];
            var uvs       = new Vector2[vertCount];
            var triangles = new int[triCount];

            float step     = size / subdivs;
            float halfSize = size * 0.5f;

            int vi = 0;
            for (int z = 0; z <= subdivs; z++)
            {
                for (int x = 0; x <= subdivs; x++)
                {
                    float px = x * step - halfSize;
                    float pz = z * step - halfSize;

                    vertices[vi] = new Vector3(px, 0f, pz);
                    normals[vi]  = Vector3.up;
                    uvs[vi]      = new Vector2((float)x / subdivs, (float)z / subdivs);
                    vi++;
                }
            }

            int ti = 0;
            for (int z = 0; z < subdivs; z++)
            {
                for (int x = 0; x < subdivs; x++)
                {
                    int bl = z * (subdivs + 1) + x;         // bottom-left
                    int br = bl + 1;                         // bottom-right
                    int tl = bl + (subdivs + 1);             // top-left
                    int tr = tl + 1;                         // top-right

                    // Triangle 1 (bl, tl, tr)
                    triangles[ti++] = bl;
                    triangles[ti++] = tl;
                    triangles[ti++] = tr;

                    // Triangle 2 (bl, tr, br)
                    triangles[ti++] = bl;
                    triangles[ti++] = tr;
                    triangles[ti++] = br;
                }
            }

            var mesh = new Mesh
            {
                name      = "OceanMesh",
                indexFormat = vertCount > 65535
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16,
            };

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        // -----------------------------------------------------------------------
        // Internal — material creation
        // -----------------------------------------------------------------------

        private Material CreateOceanMaterial()
        {
            Shader oceanShader = Shader.Find("Booty/OceanWater");

            if (oceanShader == null || oceanShader.name == "Hidden/InternalErrorShader")
            {
                Debug.LogWarning("[OceanPlane] Booty/OceanWater shader not found — falling back to URP/Lit.");
                oceanShader = Shader.Find("Universal Render Pipeline/Lit")
                           ?? Shader.Find("Standard");
            }

            var mat = new Material(oceanShader) { name = "OceanWaterMat" };

            // Only set our custom properties when we have the real shader
            if (mat.shader.name == "Booty/OceanWater")
            {
                mat.SetColor("_ShallowColor",  shallowColor);
                mat.SetColor("_DeepColor",     deepColor);
                mat.SetColor("_FoamColor",     foamColor);
                mat.SetFloat("_WaveAmplitude", waveAmplitude);
                mat.SetFloat("_WaveFrequency", waveFrequency);
                mat.SetFloat("_WaveSpeed",     waveSpeed);
                mat.SetFloat("_FoamThreshold", foamThreshold);
                mat.SetFloat("_Smoothness",    smoothness);
            }
            else
            {
                // Fallback: best-effort plain colour
                mat.color = oceanColor;
            }

            return mat;
        }

        // -----------------------------------------------------------------------
        // Internal — environment
        // -----------------------------------------------------------------------

        private void SetupEnvironment()
        {
            // Reuse existing EnvironmentSetup on this GameObject, or add one.
            var env = GetComponent<EnvironmentSetup>()
                   ?? gameObject.AddComponent<EnvironmentSetup>();
            env.Apply();
        }
    }
}
