using UnityEngine;

namespace Booty.Ports
{
    /// <summary>
    /// Manages visual representation of a port based on faction ownership.
    /// Changes flag color and banner material when ownership changes.
    /// Attached to each port GameObject alongside PortInteraction.
    /// </summary>
    public class PortVisual : MonoBehaviour
    {
        [Header("Visual References")]
        [SerializeField] private Renderer flagRenderer;
        [SerializeField] private Renderer bannerRenderer;
        [SerializeField] private Transform flagPole;

        [Header("Port Marker")]
        [SerializeField] private Renderer portMarkerRenderer;

        [Header("Faction Colors")]
        [SerializeField] private Color playerColor = new Color(1f, 0.84f, 0f, 1f);   // Gold
        [SerializeField] private Color enemyColor = new Color(0.78f, 0.12f, 0.12f, 1f); // Red
        [SerializeField] private Color neutralColor = new Color(0.63f, 0.63f, 0.63f, 1f); // Gray

        private string _portId;
        private PortSystem _portSystem;
        private string _currentFaction = "";

        private static readonly int ColorProperty = Shader.PropertyToID("_BaseColor");

        /// <summary>
        /// Configure this visual component with system references.
        /// Called by RegionSetup during world initialization.
        /// </summary>
        /// <param name="portId">The port identifier.</param>
        /// <param name="portSystem">Reference to the port system.</param>
        public void Configure(string portId, PortSystem portSystem)
        {
            _portId = portId;
            _portSystem = portSystem;

            // Subscribe to ownership changes
            if (_portSystem != null)
            {
                _portSystem.OnPortCaptured += HandlePortCaptured;
            }

            // Apply initial visuals
            RefreshVisuals();
        }

        /// <summary>
        /// Manually set faction colors. Useful for custom faction definitions
        /// loaded from factions.json.
        /// </summary>
        /// <param name="player">Player faction color.</param>
        /// <param name="enemy">Enemy faction color.</param>
        /// <param name="neutral">Neutral faction color.</param>
        public void SetFactionColors(Color player, Color enemy, Color neutral)
        {
            playerColor = player;
            enemyColor = enemy;
            neutralColor = neutral;
            RefreshVisuals();
        }

        /// <summary>
        /// Force refresh of all visual elements based on current port ownership.
        /// </summary>
        public void RefreshVisuals()
        {
            if (_portSystem == null || string.IsNullOrEmpty(_portId))
                return;

            var port = _portSystem.GetPort(_portId);
            if (port == null)
                return;

            // Only update if faction actually changed
            if (port.factionOwner == _currentFaction)
                return;

            _currentFaction = port.factionOwner;
            Color factionColor = GetFactionColor(_currentFaction);

            ApplyColor(flagRenderer, factionColor);
            ApplyColor(bannerRenderer, factionColor);
            ApplyColor(portMarkerRenderer, factionColor);

            Debug.Log($"[PortVisual] Port '{_portId}' visuals updated to faction '{_currentFaction}'.");
        }

        /// <summary>
        /// Get the display color for a faction.
        /// </summary>
        /// <param name="factionId">Faction identifier.</param>
        /// <returns>The associated color.</returns>
        public Color GetFactionColor(string factionId)
        {
            switch (factionId)
            {
                case "player_pirates":
                    return playerColor;
                case "neutral_traders":
                    return neutralColor;
                default:
                    return enemyColor;
            }
        }

        /// <summary>
        /// Apply a color to a renderer's material. Handles both URP (_BaseColor)
        /// and standard (_Color) shader properties.
        /// </summary>
        /// <param name="rend">The renderer to color.</param>
        /// <param name="color">The color to apply.</param>
        private void ApplyColor(Renderer rend, Color color)
        {
            if (rend == null)
                return;

            // Use MaterialPropertyBlock to avoid creating material instances
            var block = new MaterialPropertyBlock();
            rend.GetPropertyBlock(block);

            if (rend.sharedMaterial != null && rend.sharedMaterial.HasProperty(ColorProperty))
            {
                block.SetColor(ColorProperty, color);
            }
            else
            {
                block.SetColor("_Color", color);
            }

            rend.SetPropertyBlock(block);
        }

        private void HandlePortCaptured(string capturedPortId, string newFaction)
        {
            if (capturedPortId == _portId)
            {
                RefreshVisuals();
            }
        }

        private void OnDestroy()
        {
            if (_portSystem != null)
            {
                _portSystem.OnPortCaptured -= HandlePortCaptured;
            }
        }
    }
}
