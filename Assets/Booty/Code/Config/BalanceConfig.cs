// ---------------------------------------------------------------------------
// BalanceConfig.cs — DEPRECATED: All constants moved to GameBalance.cs
// ---------------------------------------------------------------------------
// GameBalance (ScriptableObject) is the single source of truth for all
// tunable gameplay values. DifficultyManager clones and modifies GameBalance
// at runtime to apply Easy / Normal / Hard presets.
//
// This file retains only port-defender tiers and enemy-scaling unlock times,
// which are not yet surfaced in the GameBalance ScriptableObject inspector.
// All other constants have been removed — they were duplicating GameBalance
// fields without being read at runtime.
//
// To tune balance values: open Assets/Booty/Balance/GameBalance.asset in the
// Unity Inspector, or modify the defaults in GameBalance.cs.
// ---------------------------------------------------------------------------

namespace Booty.Config
{
    /// <summary>
    /// Compile-time constants NOT yet in GameBalance.
    /// All tunable values live in <see cref="Booty.Balance.GameBalance"/>.
    /// </summary>
    public static class BalanceConfig
    {
        // ══════════════════════════════════════════════════════════════════
        //  Port Defenders Per Port Tier
        //  (Not yet in GameBalance; kept here until ScriptableObject updated)
        // ══════════════════════════════════════════════════════════════════

        public const int PortDefendersTier1 = 3;  // small outpost
        public const int PortDefendersTier2 = 5;  // established port
        public const int PortDefendersTier3 = 8;  // fortified stronghold

        // ══════════════════════════════════════════════════════════════════
        //  Enemy Scaling Unlock Times (minutes)
        //  (Not yet in GameBalance; kept here until ScriptableObject updated)
        // ══════════════════════════════════════════════════════════════════

        public const float EnemyTier2UnlockMinutes = 10f;
        public const float EnemyTier3UnlockMinutes = 25f;
    }
}
