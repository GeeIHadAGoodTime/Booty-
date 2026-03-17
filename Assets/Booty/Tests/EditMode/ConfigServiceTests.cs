// ---------------------------------------------------------------------------
// ConfigServiceTests.cs — EditMode smoke tests for ConfigService.
// Verifies that ConfigService initializes non-null collection properties
// even when Inspector-assigned TextAssets are absent (graceful degradation).
// ---------------------------------------------------------------------------

using NUnit.Framework;
using UnityEngine;
using Booty.Config;

namespace Booty.Tests
{
    /// <summary>
    /// Smoke tests for <see cref="ConfigService"/>.
    /// Confirms that Ports, Ships, and Factions are never null after Awake().
    /// </summary>
    [TestFixture]
    public class ConfigServiceTests
    {
        // ── Test 1 ────────────────────────────────────────────────────────
        /// <summary>
        /// ConfigService.Ports must not be null after Awake(), even when no
        /// TextAsset is assigned (falls back to empty list, not null).
        /// </summary>
        [Test]
        public void ConfigService_Ports_NotNullAfterAwake()
        {
            var go = new GameObject("TestConfigService_Ports");
            var configService = go.AddComponent<ConfigService>();   // triggers Awake()

            Assert.IsNotNull(configService.Ports,
                "ConfigService.Ports must not be null after Awake().");

            Object.DestroyImmediate(go);
        }

        // ── Test 2 ────────────────────────────────────────────────────────
        /// <summary>
        /// ConfigService.Ships must not be null after Awake().
        /// </summary>
        [Test]
        public void ConfigService_Ships_NotNullAfterAwake()
        {
            var go = new GameObject("TestConfigService_Ships");
            var configService = go.AddComponent<ConfigService>();

            Assert.IsNotNull(configService.Ships,
                "ConfigService.Ships must not be null after Awake().");

            Object.DestroyImmediate(go);
        }

        // ── Test 3 ────────────────────────────────────────────────────────
        /// <summary>
        /// ConfigService.Factions must not be null after Awake().
        /// </summary>
        [Test]
        public void ConfigService_Factions_NotNullAfterAwake()
        {
            var go = new GameObject("TestConfigService_Factions");
            var configService = go.AddComponent<ConfigService>();

            Assert.IsNotNull(configService.Factions,
                "ConfigService.Factions must not be null after Awake().");

            Object.DestroyImmediate(go);
        }
    }
}
