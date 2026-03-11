using System.Numerics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Helpers;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using static ZombiePlagueLegacyCS2.ZPLZombieClassCFG;

namespace ZombiePlagueLegacyCS2;

public partial class ZombiePlagueLegacyAPI : IZombiePlagueLegacyAPI, IDisposable
{
    private bool _disposed = false;

    private ILogger<ZombiePlagueLegacyAPI> _logger = null!;
    private ISwiftlyCore _core = null!;
    private ZPLGlobals _globals = null!;
    private ZPLHelpers _helpers = null!;
    private ZPLServices _services = null!;
    private PlayerZombieState _zombieState = null!;
    private IOptionsMonitor<ZPLMainCFG> _mainCFG = null!;
    private IOptionsMonitor<ZPLZombieClassCFG> _zombieClassCFG = null!;
    private IOptionsMonitor<ZPLSpecialClassCFG> _specialClassCFG = null!;
    private ZPLGameMode _gameMode = null!;

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ZombiePlagueLegacyAPI));
        }
    }
    internal void Initialize(
        ISwiftlyCore core, ILogger<ZombiePlagueLegacyAPI> logger,
        ZPLGlobals globals, ZPLHelpers helpers, ZPLServices services,
        IOptionsMonitor<ZPLMainCFG> mainCFG, PlayerZombieState zombieState,
        IOptionsMonitor<ZPLZombieClassCFG> zombieClassCFG,
        IOptionsMonitor<ZPLSpecialClassCFG> specialClassCFG,
        ZPLGameMode gameMode)
    {
        _core = core;
        _logger = logger;
        _globals = globals;
        _helpers = helpers;
        _services = services;
        _mainCFG = mainCFG;
        _zombieState = zombieState;
        _zombieClassCFG = zombieClassCFG;
        _specialClassCFG = specialClassCFG;
        _gameMode = gameMode;
    }

    public ZombiePlagueLegacyAPI() { 
    }

    
    public bool GameStart { get { ThrowIfDisposed(); return _globals.GameStart; } }

    public bool ZPL_IsZombie(int playerId)
    {
        ThrowIfDisposed();
        return _globals.IsZombie.TryGetValue(playerId, out var v) && v;
    }
    public bool ZPL_IsMotherZombie(int playerId)
    {
        ThrowIfDisposed();
        return _globals.IsMother.TryGetValue(playerId, out var v) && v;
    }
    public bool ZPL_IsSurvivor(int playerId)
    {
        ThrowIfDisposed();
        return _globals.IsSurvivor.TryGetValue(playerId, out var v) && v;
    }
    public bool ZPL_IsSniper(int playerId)
    {
        ThrowIfDisposed();
        return _globals.IsSniper.TryGetValue(playerId, out var v) && v;
    }
    public bool ZPL_IsNemesis(int playerId)
    {
        ThrowIfDisposed();
        return _globals.IsNemesis.TryGetValue(playerId, out var v) && v;
    }
    public bool ZPL_IsAssassin(int playerId)
    {
        ThrowIfDisposed();
        return _globals.IsAssassin.TryGetValue(playerId, out var v) && v;
    }
    public bool ZPL_IsHero(int playerId)
    {
        ThrowIfDisposed();
        return _globals.IsHero.TryGetValue(playerId, out var v) && v;
    }


    public bool ZPL_PlayerHaveScbaSuit(int playerId)
    {
        ThrowIfDisposed();
        return _globals.ScbaSuit.TryGetValue(playerId, out var v) && v;
    }
    public bool ZPL_PlayerHaveGodState(int playerId)
    {
        ThrowIfDisposed();
        return _globals.GodState.TryGetValue(playerId, out var v) && v;
    }
    public bool ZPL_PlayerHaveInfiniteAmmoState(int playerId)
    {
        ThrowIfDisposed();
        return _globals.InfiniteAmmoState.TryGetValue(playerId, out var v) && v;
    }

    public void ZPL_SetTargetZombie(IPlayer target)
    {
        ThrowIfDisposed();
        if (target == null || !target.IsValid)
            return;

        var id = target.PlayerID;

        _helpers.RemoveSHumanClass(id);
        _helpers.RemoveSZombieClass(id);
        _helpers.RemoveGlow(target);

        _services.SetPlayerZombie(target);
    }

    public void ZPL_SetTargetHuman(IPlayer target)
    {
        ThrowIfDisposed();
        if (target == null || !target.IsValid)
            return;

        var id = target.PlayerID;
        _helpers.RemoveSHumanClass(id);
        _helpers.RemoveSZombieClass(id);
        _helpers.RemoveGlow(target);

        _services.SetPlayerHuman(target);
    }

    public void ZPL_InfectMotherZombie(IPlayer target)
    {
        ThrowIfDisposed();
        if (target == null || !target.IsValid)
            return;

        var id = target.PlayerID;
        _globals.IsZombie.TryGetValue(id, out bool IsZombie);
        if (IsZombie)
            return;

        _helpers.RemoveSHumanClass(id);
        _helpers.RemoveSZombieClass(id);
        _helpers.RemoveGlow(target);

        _services.InfectMotherPlayer(target, true);
    }

    public void ZPL_InfectPlayer(IPlayer target, bool IgnoreScbaSuit)
    {
        ThrowIfDisposed();
        if (target == null || !target.IsValid)
            return;

        var id = target.PlayerID;
        _globals.IsZombie.TryGetValue(id, out bool IsZombie);
        if (IsZombie)
            return;

        _services.ForceCommandInfect(target, IgnoreScbaSuit);
    }

    public void ZPL_SetTargetNemesis(IPlayer target)
    {
        ThrowIfDisposed();
        if (target == null || !target.IsValid)
            return;

        var id = target.PlayerID;
        _globals.IsZombie.TryGetValue(id, out bool IsZombie);
        if (IsZombie)
            return;

        _helpers.RemoveSHumanClass(id);
        _helpers.RemoveSZombieClass(id);
        _helpers.RemoveGlow(target);

        _services.SetupNemesis(target);
    }

    public void ZPL_SetTargetAssassin(IPlayer target)
    {
        ThrowIfDisposed();
        if (target == null || !target.IsValid)
            return;

        var id = target.PlayerID;
        _globals.IsZombie.TryGetValue(id, out bool IsZombie);
        if (IsZombie)
            return;

        _helpers.RemoveSHumanClass(id);
        _helpers.RemoveSZombieClass(id);
        _helpers.RemoveGlow(target);

        _services.SetupAssassin(target);
    }
    public void ZPL_SetTargetTVaccine(IPlayer target)
    {
        ThrowIfDisposed();
        if (target == null || !target.IsValid)
            return;

        var CFG = _mainCFG.CurrentValue;
        var id = target.PlayerID;

        _globals.IsHero.TryGetValue(id, out bool IsHero);
        _globals.IsSniper.TryGetValue(id, out bool IsSniper);
        _globals.IsSurvivor.TryGetValue(id, out bool IsSurvivor);

        int maxHealth;
        if (IsHero)
        {
            maxHealth = CFG.Hero.HeroHealth;
        }
        else if (IsSniper)
        {
            maxHealth = CFG.Sniper.SniperHealth;
        }
        else if (IsSurvivor)
        {
            maxHealth = CFG.Survivor.SurvivorHealth;
        }
        else
        {
            maxHealth = CFG.HumanMaxHealth;
        }

        string Default = "characters/models/ctm_st6/ctm_st6_variante.vmdl";
        string Custom = string.IsNullOrEmpty(CFG.HumandefaultModel) ? Default : CFG.HumandefaultModel;

        _helpers.TVaccine(target, maxHealth, CFG.HumanInitialSpeed, Custom, CFG.TVaccineSound, 1.0f);
    }

    public void ZPL_SetTargetSniper(IPlayer target)
    {
        ThrowIfDisposed();
        if (target == null || !target.IsValid)
            return;

        var id = target.PlayerID;
        _globals.IsZombie[id] = false;
        _globals.IsZombie.TryGetValue(id, out bool IsZombie);
        if (IsZombie)
            return;

        _helpers.RemoveSHumanClass(id);
        _helpers.RemoveSZombieClass(id);
        _helpers.RemoveGlow(target);

        _services.SetupSniper(target);
    }

    public void ZPL_SetTargetSurvivor(IPlayer target)
    {
        ThrowIfDisposed();
        if (target == null || !target.IsValid)
            return;

        var id = target.PlayerID;
        _globals.IsZombie[id] = false;
        _globals.IsZombie.TryGetValue(id, out bool IsZombie);
        if (IsZombie)
            return;

        _helpers.RemoveSHumanClass(id);
        _helpers.RemoveSZombieClass(id);
        _helpers.RemoveGlow(target);

        _services.SetupSurvivor(target);
    }
    public void ZPL_SetTargetHero(IPlayer target)
    {
        ThrowIfDisposed();
        if (target == null || !target.IsValid)
            return;

        var id = target.PlayerID;
        _globals.IsZombie[id] = false;
        _globals.IsZombie.TryGetValue(id, out bool IsZombie);
        if (IsZombie)
            return;

        _helpers.RemoveSHumanClass(id);
        _helpers.RemoveSZombieClass(id);
        _helpers.RemoveGlow(target);

        _services.posshero(target);
    }

    public void ZPL_GiveTVirusGrenade(IPlayer target)
    {
        ThrowIfDisposed();
        if (target == null || !target.IsValid)
            return;

        var id = target.PlayerID;
        _globals.IsZombie.TryGetValue(id, out bool IsZombie);
        if (!IsZombie)
            return;

        _helpers.TVirusGrenade(target);

    }

    public void ZPL_GiveScbaSuit(IPlayer target)
    {
        ThrowIfDisposed();
        if (target == null || !target.IsValid)
            return;

        var id = target.PlayerID;
        _globals.IsZombie.TryGetValue(id, out bool IsZombie);
        if (IsZombie)
            return;

        _globals.ScbaSuit.TryGetValue(id, out bool IsHaveScbaSuit);
        if (IsHaveScbaSuit)
            return;

        var CFG = _mainCFG.CurrentValue;

        _helpers.GiveScbaSuit(target, CFG.ScbaSuitGetSound);

    }

    public void ZPL_GiveGodState(IPlayer target, float time)
    {
        ThrowIfDisposed();
        if (target == null || !target.IsValid)
            return;

        var id = target.PlayerID;
        _globals.GodState.TryGetValue(id, out bool IsGodState);
        if (IsGodState)
            return;

        _helpers.SetGodState(target, time);

    }

    public void ZPL_GiveInfiniteAmmo(IPlayer target, float time)
    {
        ThrowIfDisposed();
        if (target == null || !target.IsValid)
            return;

        var id = target.PlayerID;
        _globals.IsZombie.TryGetValue(id, out bool IsZombie);
        if (IsZombie)
            return;

        _globals.InfiniteAmmoState.TryGetValue(id, out bool IsInfiniteAmmoState);
        if (IsInfiniteAmmoState)
            return;

        _helpers.SetInfiniteAmmoState(target, time);

    }

    public void ZPL_HumanAddHealth(IPlayer target, int valve)
    {
        ThrowIfDisposed();
        if (target == null || !target.IsValid)
            return;

        var pawn = target.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return;

        if(pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE)
            return;

        var CFG = _mainCFG.CurrentValue;

        var id = target.PlayerID;
        _globals.IsZombie.TryGetValue(id, out bool IsZombie);
        if (IsZombie)
            return;

        _globals.IsHero.TryGetValue(id, out bool IsHero);
        _globals.IsSniper.TryGetValue(id, out bool IsSniper);
        _globals.IsSurvivor.TryGetValue(id, out bool IsSurvivor);


        int maxHealth;
        if (IsHero)
        {
            maxHealth = CFG.Hero.HeroHealth;
        }
        else if (IsSniper)
        {
            maxHealth = CFG.Sniper.SniperHealth;
        }
        else if (IsSurvivor)
        {
            maxHealth = CFG.Survivor.SurvivorHealth;
        }
        else
        {
            maxHealth = CFG.HumanMaxHealth;
        }

        var currentHealth = pawn.Health;
        var newHealth = currentHealth + valve;

        if (currentHealth >= maxHealth)
        {
            _helpers.SendChatT(target, "ItemAddHelathMax", maxHealth); 
            return;
        }

        _helpers.AddHealth(target, maxHealth, valve, CFG.AddHealthSound);

    }

    public string ZPL_GetZombieClassname(IPlayer player)
    {
        ThrowIfDisposed();
        if (!player.IsValid)
            return string.Empty;

        var id = player.PlayerID;
        if (!_globals.IsZombie.TryGetValue(id, out bool isZombie) || !isZombie)
            return string.Empty;

        var zombieClassName = _zombieState.GetPlayerZombieClass(id);
        return string.IsNullOrEmpty(zombieClassName) ? string.Empty : zombieClassName;
    }

    public int ZPL_GetZombieMaxHealth(IPlayer player, bool original)
    {
        ThrowIfDisposed();
        if (!player.IsValid)
            return 0;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return 0;

        var id = player.PlayerID;
        if (!_globals.IsZombie.TryGetValue(id, out bool isZombie) || !isZombie)
            return 0;

        var classname = _zombieState.GetPlayerZombieClass(id);
        if (string.IsNullOrEmpty(classname))
            return 0;

        var zombieConfig = _zombieClassCFG.CurrentValue;
        var zombieClasses = zombieConfig?.ZombieClassList;
        var selectedClass = zombieClasses?.FirstOrDefault(c => c.Name == classname);
        if (selectedClass == null)
            return 0;

        bool isMother = _globals.IsMother.TryGetValue(id, out bool v) && v;

        int result;
        if (isMother)
            result = original ? selectedClass.Stats.MotherZombieHealth : pawn.MaxHealth;
        else
            result = original ? selectedClass.Stats.Health : pawn.MaxHealth;

        return result;
    }

    public string ZPL_GetCurrentModeName()
    {
        ThrowIfDisposed();
        var modeName = _gameMode.GetModeName();
        return string.IsNullOrEmpty(modeName) ? string.Empty : modeName;
    }

    public void ZPL_GiveFireGrenade(IPlayer player)
    {
        ThrowIfDisposed();
        if (!player.IsValid)
            return;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return;

        if(pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE)
            return;

        _helpers.GiveFireGrenade(player);

    }

    public void ZPL_GiveLightGrenade(IPlayer player)
    {
        ThrowIfDisposed();
        if (!player.IsValid)
            return;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return;

        if (pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE)
            return;

        _helpers.GiveLightGrenade(player);

    }

    public void ZPL_GiveFreezeGrenade(IPlayer player)
    {
        ThrowIfDisposed();
        if (!player.IsValid)
            return;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return;

        if (pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE)
            return;

        _helpers.GiveFreezeGrenade(player);

    }

    public void ZPL_GiveTeleportGrenade(IPlayer player)
    {
        ThrowIfDisposed();
        if (!player.IsValid)
            return;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return;

        if (pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE)
            return;

        _helpers.GiveTeleprotGrenade(player);

    }

    public void ZPL_GiveIncGrenade(IPlayer player)
    {
        ThrowIfDisposed();
        if (!player.IsValid)
            return;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return;

        if (pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE)
            return;

        _helpers.GiveIncGrenade(player);

    }

    public void ZPL_CheckRoundWinConditions()
    {
        ThrowIfDisposed();
        _services.CheckRoundWinConditions();
    }
    public void ZPL_SetZombieWin()
    {
        ThrowIfDisposed();
        _services.FakeZombieWins();
    }

    public void ZPL_SetHumanWin()
    {
        ThrowIfDisposed();
        _services.FakeHumanWins();
    }

    public void ZPL_SetPlayerGlow(IPlayer player, int R, int G, int B, int A)
    {
        ThrowIfDisposed();
        _helpers.SetGlow(player, R, G, B, A);
    }

    public void ZPL_RemovePlayerGlow(IPlayer player)
    {
        ThrowIfDisposed();
        _helpers.RemoveGlow(player);
    }

    public void ZPL_SetPlayerFov(IPlayer player, int fov)
    {
        ThrowIfDisposed();
        _helpers.SetFov(player, fov);
    }

    public string? ZPL_GetZombieNameBySteamid(ulong steamId)
    {
        if (_zombieState.ExternalPreferences.TryGetValue(steamId, out var className))
        {
            return className;
        }

        return null;
    }

    public void ZPL_SetExternalPreference(ulong steamId, string? className)
    {
        if (string.IsNullOrEmpty(className))
        {
            _zombieState.ExternalPreferences.Remove(steamId);
        }
        else
        {
            _zombieState.ExternalPreferences[steamId] = className;
        }
    }


    public ZombiePropertySnapshot? ZPL_GetZombieProperties(string zombieName)
    {
        ThrowIfDisposed();
        if (string.IsNullOrEmpty(zombieName)) 
            return null;

        // 内部查询逻辑
        var normal = _zombieClassCFG.CurrentValue.ZombieClassList.FirstOrDefault(x => x.Name == zombieName);
        var zombieClass = normal;

        if (zombieClass == null)
        {
            var special = _specialClassCFG.CurrentValue.SpecialClassList.FirstOrDefault(x => x.Name == zombieName);
            if (special != null) 
                zombieClass = _zombieState.ConvertSpecialToZombieClass(special);
        }

        if (zombieClass == null) 
            return null;

        //返回快照数据
        return new ZombiePropertySnapshot
        {
            Name = zombieClass.Name,
            Health = zombieClass.Stats.Health,
            MotherHealth = zombieClass.Stats.MotherZombieHealth,
            Speed = zombieClass.Stats.Speed,
            Damage = zombieClass.Stats.Damage,
            Gravity = zombieClass.Stats.Gravity,
            EnableRegen = zombieClass.Stats.EnableRegen,
            HpRegenSec = zombieClass.Stats.HpRegenSec,
            HpRegenHp = zombieClass.Stats.HpRegenHp,
            ModelPath = zombieClass.Models.ModelPath
        };
    }

    public void Dispose()
    {
        OnGameStart = null;
        OnPlayerInfect = null;
        OnMotherZombieSelected = null;
        OnNemesisSelected = null;
        OnAssassinSelected = null;
        OnHeroSelected = null; 
        OnSurvivorSelected = null;
        OnSniperSelected = null;
        OnHumanWin = null;

        _disposed = true;

    }


}
