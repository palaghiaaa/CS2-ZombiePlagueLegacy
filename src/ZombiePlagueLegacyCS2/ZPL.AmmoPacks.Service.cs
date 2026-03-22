using Economy.Contract;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared.Players;

namespace ZombiePlagueLegacyCS2;

/// <summary>
/// Manages ammo-pack balances exclusively via the Economy plugin
/// (https://github.com/SwiftlyS2-Plugins/Economy).
/// All persistence is delegated to Economy; a local per-player cache is
/// maintained so that high-frequency reads (e.g. 1-second HUD timer) never
/// call Economy.GetPlayerBalance → LoadFromDatabase, which triggers a
/// Dommel DateOnlyTypeHandler JIT crash (SIGSEGV at 0x0) seen in the
/// crash files.  The cache is primed on connect (LoadData) and kept in sync
/// by every balance mutation (Add/Set/Spend).
/// </summary>
public class AmmoPacksService
{
    private readonly ILogger<AmmoPacksService> _logger;
    private readonly IOptionsMonitor<ZPLMainCFG> _mainCFG;
    private IEconomyAPIv1? _api;

    // Local balance cache – keyed by PlayerId.
    // All game logic runs on the single game-server thread, so no locking
    // is required.
    private readonly Dictionary<int, int> _balanceCache = new();

    public AmmoPacksService(
        ILogger<AmmoPacksService> logger,
        IOptionsMonitor<ZPLMainCFG> mainCFG)
    {
        _logger = logger;
        _mainCFG = mainCFG;
    }

    /// <summary>Injects the Economy API reference after it is resolved via shared interface.</summary>
    public void SetApi(IEconomyAPIv1 api) => _api = api;

    private string WalletKind => _mainCFG.CurrentValue.EconomyWalletKind;

    /// <summary>
    /// Loads the player's economy data from persistent storage and primes
    /// the local balance cache with a single Economy round-trip.
    /// Must be called when a player connects.
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
            _logger.LogWarning("[ZPL-AP] LoadData({PlayerId}) failed: {Ex}", player.PlayerID, ex.Message);
        }

        // Prime the local cache immediately after loading so that subsequent
        // GetBalance calls (e.g. from the 1-second HUD timer) never need to
        // call GetPlayerBalance → LoadFromDatabase again.
        int id = player.PlayerID;
        try
        {
            _balanceCache[id] = Math.Max(0, _api.GetPlayerBalance(id, WalletKind));
        }
        catch
        {
            _balanceCache[id] = 0;
        }
    }

    /// <summary>
    /// Removes the cached balance entry when the player disconnects.
    /// Call this from the OnClientDisconnected handler.
    /// </summary>
    public void RemovePlayer(int playerId) => _balanceCache.Remove(playerId);

    /// <summary>
    /// Returns the player's AP balance.
    /// Reads from the local cache when available to avoid calling
    /// Economy.GetPlayerBalance → LoadFromDatabase on every HUD tick
    /// (which triggers the Dommel DateOnlyTypeHandler SIGSEGV).
    /// </summary>
    public int GetBalance(int playerId)
    {
        // Fast path: serve from local cache – no Economy API call needed.
        if (_balanceCache.TryGetValue(playerId, out int cached))
            return cached;

        // Slow path: player not yet in cache (e.g. connected before the
        // Economy API was available).  Query once and cache the result.
        if (_api == null) return 0;
        try
        {
            int balance = Math.Max(0, _api.GetPlayerBalance(playerId, WalletKind));
            _balanceCache[playerId] = balance;
            return balance;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[ZPL-AP] GetBalance({PlayerId}) failed: {Ex}", playerId, ex.Message);
            _balanceCache[playerId] = 0;
            return 0;
        }
    }

    public void SetBalance(int playerId, int amount)
    {
        if (_api == null) return;
        try
        {
            int clamped = Math.Max(0, amount);
            _api.SetPlayerBalance(playerId, WalletKind, clamped);
            _api.SaveData(playerId);
            _balanceCache[playerId] = clamped;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[ZPL-AP] SetBalance({PlayerId},{Amount}) failed: {Ex}", playerId, amount, ex.Message);
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
            // Snapshot the current balance from cache (or Economy on first call)
            // BEFORE the mutation so the post-mutation cache entry is accurate.
            int before = GetBalance(playerId);
            _api.AddPlayerBalance(playerId, WalletKind, amount);
            _api.SaveData(playerId);
            _balanceCache[playerId] = before + amount;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[ZPL-AP] AddBalance({PlayerId},{Amount}) failed: {Ex}", playerId, amount, ex.Message);
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
            // Snapshot the current balance from cache (or Economy on first call)
            // BEFORE the mutation so the post-mutation cache entry is accurate.
            int before = GetBalance(playerId);
            _api.SubtractPlayerBalance(playerId, WalletKind, cost);
            _api.SaveData(playerId);
            _balanceCache[playerId] = Math.Max(0, before - cost);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[ZPL-AP] SpendBalance({PlayerId},{Cost}) failed: {Ex}", playerId, cost, ex.Message);
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
        }
    }
}
