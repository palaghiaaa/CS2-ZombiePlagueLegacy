using ZombiePlagueLegacyCS2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Helpers;

public class ZPLGameMode
{
    private readonly ILogger<ZPLGameMode> _logger;
    private readonly ISwiftlyCore _core;
    private readonly IOptionsMonitor<ZPLMainCFG> _mainCFG;
    private readonly IOptionsMonitor<ZPLVoxCFG> _voxCFG;
    private readonly ZPLGlobals _globals;

    public GameModeType CurrentMode { get; private set; } = GameModeType.Normal;

    public ZPLGameMode(ISwiftlyCore core, ILogger<ZPLGameMode> logger,
        IOptionsMonitor<ZPLMainCFG> mainCFG, IOptionsMonitor<ZPLVoxCFG> voxCFG,
        ZPLGlobals globals)
    {
        _core = core;
        _logger = logger;
        _mainCFG = mainCFG;
        _voxCFG = voxCFG;
        _globals = globals;
    }

    public GameModeType PickRandomMode()
    {
        var config = _mainCFG.CurrentValue;

        // If a minimum number of normal rounds between custom rounds is configured,
        // and the streak has not yet been reached, force Normal Infection this round.
        if (config.NormalRoundsInterval > 0 && _globals.NormalRoundsStreak < config.NormalRoundsInterval)
        {
            CurrentMode = GameModeType.NormalInfection;
            _globals.NormalRoundsStreak++;
            return GameModeType.NormalInfection;
        }

        var modes = new List<(GameModeType type, int weight, bool enable, int minPlayers)>
    {
        (GameModeType.NormalInfection, config.NormalInfection.Weight, config.NormalInfection.Enable, config.NormalInfection.MinPlayers),
        (GameModeType.MultiInfection, config.MultiInfection.Weight, config.MultiInfection.Enable, config.MultiInfection.MinPlayers),
        (GameModeType.Nemesis, config.Nemesis.Weight, config.Nemesis.Enable, config.Nemesis.MinPlayers),
        (GameModeType.Survivor, config.Survivor.Weight, config.Survivor.Enable, config.Survivor.MinPlayers),
        (GameModeType.Swarm, config.Swarm.Weight, config.Swarm.Enable, config.Swarm.MinPlayers),
        (GameModeType.Plague, config.Plague.Weight, config.Plague.Enable, config.Plague.MinPlayers),
        (GameModeType.Assassin, config.Assassin.Weight, config.Assassin.Enable, config.Assassin.MinPlayers),
        (GameModeType.Sniper, config.Sniper.Weight, config.Sniper.Enable, config.Sniper.MinPlayers),
        (GameModeType.AVS, config.AVS.Weight, config.AVS.Enable, config.AVS.MinPlayers),
        (GameModeType.Hero, config.Hero.Weight, config.Hero.Enable, config.Hero.MinPlayers)
    };

        // Count real (non-bot) connected players for the MinPlayers check.
        int currentPlayerCount = _core.PlayerManager.GetAllPlayers()
            .Count(p => p != null && p.IsValid && !p.IsFakeClient);

        var enabledModes = modes.Where(m => m.enable && currentPlayerCount >= m.minPlayers).ToList();

        if (enabledModes.Count == 0)
        {
            _globals.NormalRoundsStreak++;
            CurrentMode = GameModeType.Normal;
            return GameModeType.Normal;
        }

        int totalWeight = enabledModes.Sum(m => m.weight);
        int randomWeight = Random.Shared.Next(totalWeight);

        int currentWeight = 0;
        foreach (var mode in enabledModes)
        {
            currentWeight += mode.weight;
            if (randomWeight < currentWeight)
            {
                CurrentMode = mode.type;
                bool isNormalMode = mode.type == GameModeType.Normal || mode.type == GameModeType.NormalInfection;
                if (isNormalMode)
                    _globals.NormalRoundsStreak++;
                else
                    _globals.NormalRoundsStreak = 0;
                return mode.type;
            }
        }

        _globals.NormalRoundsStreak++;
        CurrentMode = GameModeType.Normal;
        return GameModeType.Normal;
    }

    public void SetMode(GameModeType mode)
    {
        CurrentMode = mode;
    }

    public void ResetMode()
    {
        CurrentMode = GameModeType.Normal;
    }

    public string GetModeName()
    {
        var config = _mainCFG.CurrentValue;

        return CurrentMode switch
        {
            GameModeType.Normal => $"{config.NormalInfection.Name}",
            GameModeType.NormalInfection => $"{config.NormalInfection.Name}",
            GameModeType.MultiInfection => $"{config.MultiInfection.Name}",
            GameModeType.Nemesis => $"{config.Nemesis.Name}",
            GameModeType.Survivor => $"{config.Survivor.Name}",
            GameModeType.Swarm => $"{config.Swarm.Name}",
            GameModeType.Plague => $"{config.Plague.Name}",
            GameModeType.Assassin => $"{config.Assassin.Name}",
            GameModeType.Sniper => $"{config.Sniper.Name}",
            GameModeType.AVS => $"{config.AVS.Name}",
            GameModeType.Hero => $"{config.Hero.Name}",
            _ => "ERROR"
        };
    }

    public string GetTramslationsModeName()
    {
        return CurrentMode switch
        {
            GameModeType.Normal => "NormalInfection",
            GameModeType.NormalInfection => "NormalInfection",
            GameModeType.MultiInfection => "MultiInfection",
            GameModeType.Nemesis => "NemesisMode",
            GameModeType.Survivor => "SurvivorMode",
            GameModeType.Swarm => "SwarmMode",
            GameModeType.Plague => "PlagueMode",
            GameModeType.Assassin => "AssassinMode",
            GameModeType.Sniper => "SniperMode",
            GameModeType.AVS => "AssassinVSSniper",
            GameModeType.Hero => "HeroMode",
            _ => "ERROR"
        };
    }

    public bool CanZombieReborn()
    {
        var mode = CurrentMode;
        var config = _mainCFG.CurrentValue;

        return mode switch
        {
            GameModeType.Normal => config.NormalInfection.ZombieCanReborn,
            GameModeType.MultiInfection => config.MultiInfection.ZombieCanReborn,
            GameModeType.Nemesis => config.Nemesis.ZombieCanReborn,
            GameModeType.Survivor => config.Survivor.ZombieCanReborn,
            GameModeType.Swarm => config.Swarm.ZombieCanReborn,
            GameModeType.Plague => config.Plague.ZombieCanReborn,
            GameModeType.Assassin => config.Assassin.ZombieCanReborn,
            GameModeType.Sniper => config.Sniper.ZombieCanReborn,
            GameModeType.AVS => config.AVS.ZombieCanReborn,
            GameModeType.Hero => config.Hero.ZombieCanReborn,
            _ => true // 默认允许复活
        };
    }

    public bool InfiniteClipMode()
    {
        var mode = CurrentMode;
        var config = _mainCFG.CurrentValue;

        return mode switch
        {
            GameModeType.Normal => config.NormalInfection.EnableInfiniteClipMode,
            GameModeType.MultiInfection => config.MultiInfection.EnableInfiniteClipMode,
            GameModeType.Nemesis => config.Nemesis.EnableInfiniteClipMode,
            GameModeType.Survivor => config.Survivor.EnableInfiniteClipMode,
            GameModeType.Swarm => config.Swarm.EnableInfiniteClipMode,
            GameModeType.Plague => config.Plague.EnableInfiniteClipMode,
            GameModeType.Assassin => config.Assassin.EnableInfiniteClipMode,
            GameModeType.Sniper => config.Sniper.EnableInfiniteClipMode,
            GameModeType.AVS => config.AVS.EnableInfiniteClipMode,
            GameModeType.Hero => config.Hero.EnableInfiniteClipMode,
            _ => true // 默认开启无限子弹
        };
    }


    public string? SelectModeVox()
    {
        var mode = CurrentMode;
        var VoxCFG = _voxCFG.CurrentValue;
        var VoxList = VoxCFG.VoxList;

        if (_globals.RoundVoxGroup == null)
            return null;

        return mode switch
        {
            GameModeType.Normal => _globals.RoundVoxGroup.NormalInfectionVox,
            GameModeType.MultiInfection => _globals.RoundVoxGroup.NormalInfectionVox,
            GameModeType.Nemesis => _globals.RoundVoxGroup.NemesisVox,
            GameModeType.Survivor => _globals.RoundVoxGroup.SurvivorVox,
            GameModeType.Swarm => _globals.RoundVoxGroup.SwarmVox,
            GameModeType.Plague => _globals.RoundVoxGroup.PlagueVox,
            GameModeType.Assassin => _globals.RoundVoxGroup.AssassinVox,
            GameModeType.Sniper => _globals.RoundVoxGroup.SniperVox,
            GameModeType.AVS => _globals.RoundVoxGroup.AVSVox,
            GameModeType.Hero => _globals.RoundVoxGroup.HeroVox,
            _ => _globals.RoundVoxGroup.NormalInfectionVox
        };
    }
}