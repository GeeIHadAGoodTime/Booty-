// ---------------------------------------------------------------------------
// S3SystemTests.cs — EditMode smoke tests for S3 visual/audio systems.
// Verifies CameraShake, FloatingDamageNumber, AudioManager, CombatVFX, and
// ShipVisual can be instantiated and their key public methods do not throw.
// ---------------------------------------------------------------------------
// NOTE: These are EditMode [Test] methods (synchronous, no game loop).
//   - Awake() IS called when AddComponent<T>() is called.
//   - Start() is NOT called in EditMode [Test] — any self-wiring in Start()
//     is bypassed, which is safe for these smoke tests.
//   - Object.DestroyImmediate() is required in EditMode (not Destroy()).
// ---------------------------------------------------------------------------

using NUnit.Framework;
using UnityEngine;
using Booty.World;
using Booty.UI;
using Booty.Audio;
using Booty.VFX;
using Booty.Ships;
using Booty.Combat;

namespace Booty.Tests
{
    /// <summary>
    /// Smoke tests for S3 visual and audio systems.
    /// Verifies instantiation and key public-method contracts do not throw.
    /// </summary>
    [TestFixture]
    public class S3SystemTests
    {
        // ══════════════════════════════════════════════════════════════════
        //  CameraShake
        // ══════════════════════════════════════════════════════════════════

        // ── Test 1 ────────────────────────────────────────────────────────
        /// <summary>
        /// CameraShake can be added to a GameObject without throwing.
        /// Start() (which wires to GameRoot.Instance) is NOT called in EditMode
        /// [Test] — null GameRoot is safe.
        /// </summary>
        [Test]
        public void CameraShake_Instantiate_IsNotNull()
        {
            var go = new GameObject("TestCameraShake");

            CameraShake cs = null;
            Assert.DoesNotThrow(() => cs = go.AddComponent<CameraShake>(),
                "Adding CameraShake component must not throw.");
            Assert.IsNotNull(cs, "CameraShake component must not be null after AddComponent.");

            Object.DestroyImmediate(go);
        }

        // ══════════════════════════════════════════════════════════════════
        //  FloatingDamageNumber
        // ══════════════════════════════════════════════════════════════════

        // ── Test 2 ────────────────────────────────────────────────────────
        /// <summary>
        /// FloatingDamageNumber.Spawn() returns a non-null instance for enemy damage
        /// (isPlayer=false → yellow colour).
        /// </summary>
        [Test]
        public void FloatingDamageNumber_Spawn_EnemyDamage_ReturnsNonNull()
        {
            FloatingDamageNumber result = null;
            Assert.DoesNotThrow(() => result = FloatingDamageNumber.Spawn(Vector3.zero, 42, false),
                "FloatingDamageNumber.Spawn(enemy) must not throw.");
            Assert.IsNotNull(result, "Spawn must return a non-null FloatingDamageNumber.");

            if (result != null)
                Object.DestroyImmediate(result.gameObject);
        }

        // ── Test 3 ────────────────────────────────────────────────────────
        /// <summary>
        /// FloatingDamageNumber.Spawn() returns a non-null instance for player damage
        /// (isPlayer=true → red colour).
        /// </summary>
        [Test]
        public void FloatingDamageNumber_Spawn_PlayerDamage_ReturnsNonNull()
        {
            FloatingDamageNumber result = null;
            Assert.DoesNotThrow(() => result = FloatingDamageNumber.Spawn(Vector3.zero, 10, true),
                "FloatingDamageNumber.Spawn(player) must not throw.");
            Assert.IsNotNull(result, "Spawn must return a non-null FloatingDamageNumber.");

            if (result != null)
                Object.DestroyImmediate(result.gameObject);
        }

        // ══════════════════════════════════════════════════════════════════
        //  AudioManager
        // ══════════════════════════════════════════════════════════════════

        // ── Test 4 ────────────────────────────────────────────────────────
        /// <summary>
        /// AudioManager instantiates with correct default volume values.
        /// Awake() creates two AudioSources and generates procedural AudioClips.
        /// </summary>
        [Test]
        public void AudioManager_Instantiate_HasCorrectDefaultVolumes()
        {
            var go = new GameObject("TestAudioManager_Defaults");
            var am = go.AddComponent<AudioManager>();

            Assert.AreEqual(1.0f, am.SFXVolume,
                "Default SFX volume must be 1.0.");
            Assert.AreEqual(0.5f, am.MusicVolume,
                "Default music volume must be 0.5.");

            Object.DestroyImmediate(go);
        }

        // ── Test 5 ────────────────────────────────────────────────────────
        /// <summary>
        /// SetSFXVolume clamps values to the [0, 1] range.
        /// </summary>
        [Test]
        public void AudioManager_SetSFXVolume_ClampsToRange()
        {
            var go = new GameObject("TestAudioManager_SFXVol");
            var am = go.AddComponent<AudioManager>();

            am.SetSFXVolume(5.0f);
            Assert.AreEqual(1.0f, am.SFXVolume,
                "SetSFXVolume(5.0) must clamp to 1.0.");

            am.SetSFXVolume(-1.0f);
            Assert.AreEqual(0.0f, am.SFXVolume,
                "SetSFXVolume(-1.0) must clamp to 0.0.");

            Object.DestroyImmediate(go);
        }

        // ── Test 6 ────────────────────────────────────────────────────────
        /// <summary>
        /// SetMusicVolume clamps values to the [0, 1] range.
        /// </summary>
        [Test]
        public void AudioManager_SetMusicVolume_ClampsToRange()
        {
            var go = new GameObject("TestAudioManager_MusicVol");
            var am = go.AddComponent<AudioManager>();

            am.SetMusicVolume(5.0f);
            Assert.AreEqual(1.0f, am.MusicVolume,
                "SetMusicVolume(5.0) must clamp to 1.0.");

            am.SetMusicVolume(-1.0f);
            Assert.AreEqual(0.0f, am.MusicVolume,
                "SetMusicVolume(-1.0) must clamp to 0.0.");

            Object.DestroyImmediate(go);
        }

        // ── Test 7 ────────────────────────────────────────────────────────
        /// <summary>PlayClick must not throw (calls PlaySFX on a procedural clip).</summary>
        [Test]
        public void AudioManager_PlayClick_DoesNotThrow()
        {
            var go = new GameObject("TestAudioManager_Click");
            var am = go.AddComponent<AudioManager>();

            Assert.DoesNotThrow(() => am.PlayClick(),
                "PlayClick() must not throw.");

            Object.DestroyImmediate(go);
        }

        // ── Test 8 ────────────────────────────────────────────────────────
        /// <summary>PlayGoldCoin must not throw.</summary>
        [Test]
        public void AudioManager_PlayGoldCoin_DoesNotThrow()
        {
            var go = new GameObject("TestAudioManager_GoldCoin");
            var am = go.AddComponent<AudioManager>();

            Assert.DoesNotThrow(() => am.PlayGoldCoin(),
                "PlayGoldCoin() must not throw.");

            Object.DestroyImmediate(go);
        }

        // ── Test 9 ────────────────────────────────────────────────────────
        /// <summary>PlayChime must not throw.</summary>
        [Test]
        public void AudioManager_PlayChime_DoesNotThrow()
        {
            var go = new GameObject("TestAudioManager_Chime");
            var am = go.AddComponent<AudioManager>();

            Assert.DoesNotThrow(() => am.PlayChime(),
                "PlayChime() must not throw.");

            Object.DestroyImmediate(go);
        }

        // ── Test 10 ───────────────────────────────────────────────────────
        /// <summary>StopAll must not throw (stops both SFX and music sources).</summary>
        [Test]
        public void AudioManager_StopAll_DoesNotThrow()
        {
            var go = new GameObject("TestAudioManager_StopAll");
            var am = go.AddComponent<AudioManager>();

            Assert.DoesNotThrow(() => am.StopAll(),
                "StopAll() must not throw.");

            Object.DestroyImmediate(go);
        }

        // ══════════════════════════════════════════════════════════════════
        //  CombatVFX
        // ══════════════════════════════════════════════════════════════════

        // ── Test 11 ───────────────────────────────────────────────────────
        /// <summary>
        /// CombatVFX.Awake() sets the static Instance. After instantiation,
        /// Instance must be the newly created component.
        /// </summary>
        [Test]
        public void CombatVFX_Instantiate_SetsSingleton()
        {
            var go  = new GameObject("TestCombatVFX_Singleton");
            var vfx = go.AddComponent<CombatVFX>();

            Assert.IsNotNull(CombatVFX.Instance,
                "CombatVFX.Instance must not be null after AddComponent.");
            Assert.AreEqual(vfx, CombatVFX.Instance,
                "CombatVFX.Instance must equal the newly created component.");

            Object.DestroyImmediate(go);
        }

        // ── Test 12 ───────────────────────────────────────────────────────
        /// <summary>
        /// CombatVFX.OnDestroy() clears the static Instance.
        /// After DestroyImmediate the Instance must be null.
        /// </summary>
        [Test]
        public void CombatVFX_OnDestroy_ClearsSingleton()
        {
            var go = new GameObject("TestCombatVFX_OnDestroy");
            go.AddComponent<CombatVFX>();

            Object.DestroyImmediate(go);

            Assert.IsNull(CombatVFX.Instance,
                "CombatVFX.Instance must be null after the component is destroyed.");
        }

        // ── Test 13 ───────────────────────────────────────────────────────
        /// <summary>PlayCannonSmoke must not throw at Vector3.zero.</summary>
        [Test]
        public void CombatVFX_PlayCannonSmoke_DoesNotThrow()
        {
            var go  = new GameObject("TestCombatVFX_CannonSmoke");
            var vfx = go.AddComponent<CombatVFX>();

            Assert.DoesNotThrow(() => vfx.PlayCannonSmoke(Vector3.zero),
                "PlayCannonSmoke(Vector3.zero) must not throw.");

            Object.DestroyImmediate(go);
        }

        // ── Test 14 ───────────────────────────────────────────────────────
        /// <summary>PlayWaterSplash must not throw at Vector3.zero.</summary>
        [Test]
        public void CombatVFX_PlayWaterSplash_DoesNotThrow()
        {
            var go  = new GameObject("TestCombatVFX_WaterSplash");
            var vfx = go.AddComponent<CombatVFX>();

            Assert.DoesNotThrow(() => vfx.PlayWaterSplash(Vector3.zero),
                "PlayWaterSplash(Vector3.zero) must not throw.");

            Object.DestroyImmediate(go);
        }

        // ── Test 15 ───────────────────────────────────────────────────────
        /// <summary>
        /// RegisterShip with a valid ship GO (with HPSystem) must not throw.
        /// RegisterShip subscribes to OnDamaged and OnDestroyed events.
        /// </summary>
        [Test]
        public void CombatVFX_RegisterShip_WithHPSystem_DoesNotThrow()
        {
            var vfxGO  = new GameObject("TestCombatVFX_Register");
            var vfx    = vfxGO.AddComponent<CombatVFX>();

            var shipGO = new GameObject("TestShip_ForRegister");
            var hp     = shipGO.AddComponent<HPSystem>();
            hp.Configure(100);

            Assert.DoesNotThrow(() => vfx.RegisterShip(shipGO),
                "RegisterShip with a valid HPSystem ship must not throw.");

            Object.DestroyImmediate(vfxGO);
            Object.DestroyImmediate(shipGO);
        }

        // ── Test 16 ───────────────────────────────────────────────────────
        /// <summary>
        /// RegisterShip(null) must not throw — the method has an internal null guard.
        /// </summary>
        [Test]
        public void CombatVFX_RegisterShip_NullArg_DoesNotThrow()
        {
            var go  = new GameObject("TestCombatVFX_NullArg");
            var vfx = go.AddComponent<CombatVFX>();

            Assert.DoesNotThrow(() => vfx.RegisterShip(null),
                "RegisterShip(null) must not throw (internal null guard).");

            Object.DestroyImmediate(go);
        }

        // ══════════════════════════════════════════════════════════════════
        //  ShipVisual
        // ══════════════════════════════════════════════════════════════════

        // ── Test 17 ───────────────────────────────────────────────────────
        /// <summary>
        /// ShipVisual.Initialize() must not throw.
        /// NOTE: Initialize() calls Shader.Find() which may return null in
        /// headless/batch EditMode — the code handles null gracefully (Material
        /// constructor falls back to default shader).
        /// </summary>
        [Test]
        public void ShipVisual_Initialize_DoesNotThrow()
        {
            var go = new GameObject("TestShipVisual_Init");
            var sv = go.AddComponent<ShipVisual>();

            Assert.DoesNotThrow(() => sv.Initialize(),
                "ShipVisual.Initialize() must not throw.");
            Assert.IsNotNull(sv,
                "ShipVisual component must not be null after Initialize().");

            Object.DestroyImmediate(go);
        }

        // ── Test 18 ───────────────────────────────────────────────────────
        /// <summary>
        /// ShipVisual.Configure("player_pirates", Sloop) must not throw after Initialize().
        /// </summary>
        [Test]
        public void ShipVisual_Configure_PlayerFaction_DoesNotThrow()
        {
            var go = new GameObject("TestShipVisual_PlayerCfg");
            var sv = go.AddComponent<ShipVisual>();
            sv.Initialize();

            Assert.DoesNotThrow(() => sv.Configure("player_pirates", ShipTier.Sloop),
                "Configure(player_pirates, Sloop) must not throw.");

            Object.DestroyImmediate(go);
        }

        // ── Test 19 ───────────────────────────────────────────────────────
        /// <summary>
        /// Configure must not throw for any combination of faction ID and tier.
        /// Covers all 3 ShipTier enum values and known + unknown factions.
        /// </summary>
        [Test]
        public void ShipVisual_Configure_AllFactionsAndTiers_DoesNotThrow()
        {
            var go = new GameObject("TestShipVisual_AllCfg");
            var sv = go.AddComponent<ShipVisual>();
            sv.Initialize();

            string[] factions = { "player_pirates", "crown_navy", "merchant_guild", "unknown_faction" };
            ShipTier[] tiers  = { ShipTier.Sloop, ShipTier.Brigantine, ShipTier.Galleon };

            foreach (string faction in factions)
            {
                foreach (ShipTier tier in tiers)
                {
                    string label = string.Format("Configure({0}, {1})", faction, tier);
                    Assert.DoesNotThrow(() => sv.Configure(faction, tier), label + " must not throw.");
                }
            }

            Object.DestroyImmediate(go);
        }

        // ── Test 20 ───────────────────────────────────────────────────────
        /// <summary>
        /// SetVisible(false) and SetVisible(true) must not throw after Initialize().
        /// </summary>
        [Test]
        public void ShipVisual_SetVisible_TogglesWithoutThrow()
        {
            var go = new GameObject("TestShipVisual_Visible");
            var sv = go.AddComponent<ShipVisual>();
            sv.Initialize();

            Assert.DoesNotThrow(() => sv.SetVisible(false),
                "SetVisible(false) must not throw.");
            Assert.DoesNotThrow(() => sv.SetVisible(true),
                "SetVisible(true) must not throw.");

            Object.DestroyImmediate(go);
        }
    }
}
