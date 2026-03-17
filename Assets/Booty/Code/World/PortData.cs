// ---------------------------------------------------------------------------
// PortData.cs — ScriptableObject: defines a port's static configuration
// ---------------------------------------------------------------------------
// Each port in the Caribbean world has a PortData asset assigned in the
// WorldMapManager Inspector. PortData feeds NavigationUI with port names,
// positions, and available services.
//
// Create via:  Assets → Create → Booty → World → PortData
// ---------------------------------------------------------------------------

using UnityEngine;

namespace Booty.World
{
    /// <summary>
    /// Services available at a port. Use bitwise flags to combine.
    /// </summary>
    [System.Flags]
    public enum PortService
    {
        None    = 0,
        Trade   = 1,
        Upgrade = 2,
        Crew    = 4,
        Quest   = 8,
    }

    /// <summary>
    /// ScriptableObject that defines a single port's static configuration:
    /// id, display name, world position, faction ownership, and available services.
    ///
    /// Assign PortData instances to <see cref="WorldMapManager.portDataAssets"/>.
    /// Runtime mutable state (faction capture, etc.) lives in PortSystem.
    /// </summary>
    [CreateAssetMenu(fileName = "PortData_New", menuName = "Booty/World/PortData")]
    public class PortData : ScriptableObject
    {
        // ══════════════════════════════════════════════════════════════════
        //  Identity
        // ══════════════════════════════════════════════════════════════════

        [Header("Identity")]
        [Tooltip("Unique ID matching PortSystem portId (e.g. 'nassau').")]
        public string portId;

        [Tooltip("Display name shown in UI (e.g. 'Nassau').")]
        public string portName;

        // ══════════════════════════════════════════════════════════════════
        //  World
        // ══════════════════════════════════════════════════════════════════

        [Header("World")]
        [Tooltip("World-space position on the XZ nav plane (Y must be 0).")]
        public Vector3 worldPosition;

        [Tooltip("Starting faction: 'player_pirates', 'spain', 'england', 'france', 'neutral'.")]
        public string faction = "neutral";

        // ══════════════════════════════════════════════════════════════════
        //  Services
        // ══════════════════════════════════════════════════════════════════

        [Header("Services")]
        [Tooltip("Which services are available when docked here.")]
        public PortService availableServices = PortService.Trade | PortService.Crew;

        // ══════════════════════════════════════════════════════════════════
        //  Flavour
        // ══════════════════════════════════════════════════════════════════

        [Header("Flavour")]
        [Tooltip("Brief description shown in the port interaction UI.")]
        [TextArea(2, 4)]
        public string description;

        // ══════════════════════════════════════════════════════════════════
        //  Convenience helpers
        // ══════════════════════════════════════════════════════════════════

        /// <summary>True if this port offers Trade services.</summary>
        public bool HasTrade   => (availableServices & PortService.Trade)   != 0;

        /// <summary>True if this port offers Ship Upgrade services.</summary>
        public bool HasUpgrade => (availableServices & PortService.Upgrade) != 0;

        /// <summary>True if this port offers Crew recruiting.</summary>
        public bool HasCrew    => (availableServices & PortService.Crew)    != 0;

        /// <summary>True if this port has available Quests.</summary>
        public bool HasQuest   => (availableServices & PortService.Quest)   != 0;

        public override string ToString() => $"PortData({portId}, {faction})";
    }
}
