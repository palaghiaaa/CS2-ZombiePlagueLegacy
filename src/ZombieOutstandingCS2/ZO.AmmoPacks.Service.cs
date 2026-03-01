using Economy.Contract;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared.Players;

namespace ZombieOutstandingCS2;

/// <summary>
/// Manages ammo-pack balances exclusively via the Economy plugin
/// (https://github.com/SwiftlyS2-Plugins/Economy).
/// All persistence is delegated to Economy; no local caching or database is used.
/// </summary>
public class AmmoPacksService
{
    private readonly ILogger<AmmoPacksService> _logger;
    private readonly IOptionsMonitor<ZOMainCFG> _mainCFG;
    private IEconomyAPIv1? _api;

    public AmmoPacksService(
        ILogger<AmmoPacksService> logger,
        IOptionsMonitor<ZOMainCFG> mainCFG)
    {
        _logger = logger;
        _mainCFG = mainCFG;
    }

    /// <summary>Injects the Economy API reference after it is resolved via shared interface.</summary>
    public void SetApi(IEconomyAPIv1 api) => _api = api;

    private string WalletKind => _mainCFG.CurrentValue.EconomyWalletKind;

    /// <summary>
    /// Loads the player's economy data from persistent storage.
    /// Must be called when a player connects so that subsequent balance queries are reliable.
    /// </summary>
    public void LoadData(IPlayer player)
    {
        if (_api == null) return;
        try
        {
            _api.LoadData(player);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[ZO-AP] LoadData({PlayerId}) failed: {Ex}", player.PlayerID, ex.Message);
        }
    }

    public int GetBalance(int playerId)
    {
        if (_api == null) return 0;
        try
        {
            return Math.Max(0, _api.GetPlayerBalance(playerId, WalletKind));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[ZO-AP] GetBalance({PlayerId}) failed: {Ex}", playerId, ex.Message);
            return 0;
        }
    }

    public void SetBalance(int playerId, int amount)
    {
        if (_api == null) return;
        try
        {
            _api.SetPlayerBalance(playerId, WalletKind, Math.Max(0, amount));
            _api.SaveData(playerId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[ZO-AP] SetBalance({PlayerId},{Amount}) failed: {Ex}", playerId, amount, ex.Message);
        }
    }

    /// <summary>
    /// Adds <paramref name="amount"/> ammo packs to the player's balance.
    /// Use <see cref="SpendBalance"/> for deductions.
    /// All balance mutations execute on the game server's single event thread,
    /// so no additional synchronization is needed.
    /// </summary>
    public void AddBalance(int playerId, int amount)
    {
        if (_api == null || amount <= 0) return;
        try
        {
            _api.AddPlayerBalance(playerId, WalletKind, amount);
            _api.SaveData(playerId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[ZO-AP] AddBalance({PlayerId},{Amount}) failed: {Ex}", playerId, amount, ex.Message);
        }
    }

    /// <summary>
    /// Deducts <paramref name="cost"/> ammo packs from the player's balance.
    /// Returns <c>false</c> when the player has insufficient funds.
    /// All balance mutations execute on the game server's single event thread,
    /// so no additional synchronization is needed.
    /// </summary>
    public bool SpendBalance(int playerId, int cost)
    {
        if (_api == null) return false;
        if (cost <= 0) return true;
        try
        {
            if (!_api.HasSufficientFunds(playerId, WalletKind, cost)) return false;
            _api.SubtractPlayerBalance(playerId, WalletKind, cost);
            _api.SaveData(playerId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[ZO-AP] SpendBalance({PlayerId},{Cost}) failed: {Ex}", playerId, cost, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Ensures the wallet kind exists in the Economy plugin.
    /// Called once after the Economy API is resolved.
    /// </summary>
    public void EnsureWalletKind()
    {
        if (_api == null) return;
        var walletKind = WalletKind;
        if (!_api.WalletKindExists(walletKind))
        {
            _api.EnsureWalletKind(walletKind);
            _logger.LogInformation("[ZO-AP] Registered wallet kind '{Kind}' in Economy.", walletKind);
        }
    }
}
