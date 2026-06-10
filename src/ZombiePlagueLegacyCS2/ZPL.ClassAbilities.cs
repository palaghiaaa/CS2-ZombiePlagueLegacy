using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using static ZombiePlagueLegacyCS2.ZPLZombieClassCFG;

namespace ZombiePlagueLegacyCS2;

/// <summary>
/// Handles all per-class zombie special abilities:
///   - Raptor          : +HP on infect
///   - Tight Zombie    : Double Jump (extra mid-air jumps)
///   - Mutant          : Temporary yellow glow on infect
///   - Predator Blue   : Temporary blue glow on infect
///   - Hunter          : Silent steps + temporary red glow on infect
/// All behavior is data-driven via ZombieAbilities fields in ZombieClassesCFG.jsonc.
/// </summary>
public class ZPLClassAbilities
{
    private readonly ISwiftlyCore _core;
    private readonly ZPLGlobals _globals;
    private readonly ZPLHelpers _helpers;
    private readonly ILogger<ZPLClassAbilities> _logger;
    private readonly IOptionsMonitor<ZPLZombieClassCFG> _zombieClassCFG;
    private readonly PlayerZombieState _zombieState;

    public ZPLClassAbilities(
        ISwiftlyCore core,
        ZPLGlobals globals,
        ZPLHelpers helpers,
        ILogger<ZPLClassAbilities> logger,
        IOptionsMonitor<ZPLZombieClassCFG> zombieClassCFG,
        PlayerZombieState zombieState)
    {
        _core = core;
        _globals = globals;
        _helpers = helpers;
        _logger = logger;
        _zombieClassCFG = zombieClassCFG;
        _zombieState = zombieState;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Called from posszombie() when a player becomes a zombie.
    // Applies passive abilities (silent steps, extra jumps) immediately.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies passive abilities for the zombie's class the moment they become a zombie.
    /// Call this at the end of posszombie(), after the class is already set.
    /// </summary>
    public void OnBecomeZombie(IPlayer zombie, ZombieClass zclass)
    {
        if (!zombie.IsValid || zombie.IsFakeClient) return;

        int id = zombie.PlayerID;
        var ab = zclass.Abilities;

        // ── Silent steps ──────────────────────────────────────────────────────
        if (ab.SilentSteps)
            _globals.SilentStepsActive.Add(id);
        else
            _globals.SilentStepsActive.Remove(id);

        // ── Extra jumps: seed the shared ExtraJumps dict (same one humans use) ──
        // Event_OnTickMultijump handles both humans and zombies via ExtraJumps.
        if (ab.ExtraJumps > 0)
            _globals.ExtraJumps[id] = ab.ExtraJumps;
        else
            _globals.ExtraJumps.Remove(id);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Called from Infect() after posszombie() has run on the victim.
    // Applies reactive abilities (infect heal, infect glow) to the INFECTOR.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called after a successful infect. Gives the infector any on-infect bonuses
    /// (extra HP, temporary glow) defined by their zombie class.
    /// </summary>
    public void OnInfectAbility(IPlayer infector)
    {
        if (!infector.IsValid || infector.IsFakeClient) return;

        int id = infector.PlayerID;

        var cfg = _zombieClassCFG.CurrentValue;
        var zclass = _zombieState.GetZombieClass(id, cfg.ZombieClassList);
        if (zclass == null) return;

        var ab = zclass.Abilities;

        // ── Infect heal (Raptor: +200 HP) ─────────────────────────────────────
        if (ab.InfectHealAmount > 0)
        {
            var pawn = infector.PlayerPawn;
            if (pawn != null && pawn.IsValid)
            {
                int newHp = Math.Min(pawn.Health + ab.InfectHealAmount, pawn.MaxHealth);
                pawn.Health = newHp;
                pawn.HealthUpdated();
            }
        }

        // ── Infect glow (Mutant: yellow, Predator Blue: blue, Hunter: red) ────
        if (!string.IsNullOrEmpty(ab.InfectGlowColor))
        {
            if (!TryParseGlowColor(ab.InfectGlowColor, out int r, out int g, out int b, out int a))
            {
                _logger.LogWarning("[ZPL-Abilities] Invalid InfectGlowColor '{0}' for class '{1}'",
                    ab.InfectGlowColor, zclass.Name);
                return;
            }

            // Cancel any existing infect-glow timer for this player
            if (_globals.InfectGlowTimers.TryGetValue(id, out var oldCts))
            {
                oldCts.Cancel();
                _globals.InfectGlowTimers.Remove(id);
            }

            // Apply glow
            _helpers.SetGlow(infector, r, g, b, a);

            // Schedule removal if duration > 0
            if (ab.GlowDurationSeconds > 0f)
            {
                var cts = _core.Scheduler.DelayBySeconds(ab.GlowDurationSeconds, () =>
                {
                    _globals.InfectGlowTimers.Remove(id);
                    if (infector.IsValid && !infector.IsFakeClient)
                        _helpers.RemoveGlow(infector);
                });
                _globals.InfectGlowTimers[id] = cts;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Cleanup helpers — call on death / disconnect / map unload
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Clears all ability state for a player (death, disconnect, unzombie).</summary>
    public void ClearPlayer(int playerId)
    {
        _globals.SilentStepsActive.Remove(playerId);

        if (_globals.InfectGlowTimers.TryGetValue(playerId, out var cts))
        {
            cts.Cancel();
            _globals.InfectGlowTimers.Remove(playerId);
        }
    }

    /// <summary>Clears ALL ability state — call on map unload.</summary>
    public void ClearAll()
    {
        _globals.SilentStepsActive.Clear();

        foreach (var cts in _globals.InfectGlowTimers.Values)
            cts.Cancel();
        _globals.InfectGlowTimers.Clear();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static bool TryParseGlowColor(string raw, out int r, out int g, out int b, out int a)
    {
        r = g = b = 0; a = 200;
        var parts = raw.Split(',');
        if (parts.Length < 3) return false;
        if (!int.TryParse(parts[0].Trim(), out r)) return false;
        if (!int.TryParse(parts[1].Trim(), out g)) return false;
        if (!int.TryParse(parts[2].Trim(), out b)) return false;
        if (parts.Length >= 4) int.TryParse(parts[3].Trim(), out a);
        return true;
    }
}
