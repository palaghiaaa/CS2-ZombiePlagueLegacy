using Economy.Contract;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared.Players;

namespace ZombiePlagueLegacyCS2;

/// <summary>
/// Thin wrapper over the Economy API's "ammo" wallet (v2 rewrite).
///
/// Key design decisions:
/// - No IPlayer cache. Economy exposes playerid (int) overloads for every
///   operation, so we never need to keep live IPlayer references.
/// - _balanceCache (int->int) avoids hammering the API in tight loops (menus,
///   HUD ticks). Kept in sync by OnPlayerLoad + OnPlayerBalanceChanged events.
/// - LoadData() is a stub: Economy automatically loads player data on connect.
///   We only record the steamId->slot mapping and do a direct read if the cache
///   is empty (handles UseSharedInterface late-arrival race condition).
/// - All mutating operations use the playerid (int) overloads; no IPlayer needed.
/// - _steamToSlot (ulong->int) is still required because OnPlayerBalanceChanged
///   only provides steamId, not playerid.
/// </summary>
public class AmmoPacksService
{
    private readonly ILogger<AmmoPacksService> _logger;
    private readonly IOptionsMonitor<ZPLMainCFG> _mainCFG;
    private IEconomyAPIv1? _api;

    // PlayerID -> cached balance (int AP)
    private readonly Dictionary<int, int> _balanceCache = new();
    // SteamID -> PlayerID, populated from OnPlayerLoad; needed for OnPlayerBalanceChanged
    private readonly Dictionary<ulong, int> _steamToSlot = new();

    private string WalletKind => _mainCFG.CurrentValue.EconomyWalletKind;

    public AmmoPacksService(
        ILogger<AmmoPacksService> logger,
        IOptionsMonitor<ZPLMainCFG> mainCFG)
    {
        _logger = logger;
        _mainCFG = mainCFG;
    }

    // -------------------------------------------------------------------------
    //  API wiring
    // -------------------------------------------------------------------------

    public void SetApi(IEconomyAPIv1 api)
    {
        if (_api != null)
            UnsubscribeEvents();

        _api = api;
        _api.OnPlayerLoad += OnEconomyPlayerLoad;
        _api.OnPlayerBalanceChanged += OnEconomyBalanceChanged;
        _logger.LogInformation("[ZPL-AP] Economy API connected. Wallet='{Kind}'.", WalletKind);
    }

    private void UnsubscribeEvents()
    {
        if (_api == null) return;
        _api.OnPlayerLoad -= OnEconomyPlayerLoad;
        _api.OnPlayerBalanceChanged -= OnEconomyBalanceChanged;
    }

    public void EnsureWalletKind()
    {
        if (_api == null) return;
        string kind = WalletKind;
        try
        {
            if (!_api.WalletKindExists(kind))
                _api.EnsureWalletKind(kind);
            _logger.LogInformation("[ZPL-AP] Wallet '{Kind}' ensured.", kind);
        }
        catch (Exception ex)
        {
            _logger.LogError("[ZPL-AP] EnsureWalletKind '{Kind}' failed: {Ex}", kind, ex.Message);
        }
    }

    // -------------------------------------------------------------------------
    //  Economy event handlers
    // -------------------------------------------------------------------------

    // Fired by Economy when a player's data is fully loaded from the DB.
    // This is the authoritative moment to populate the cache.
    private void OnEconomyPlayerLoad(IPlayer player)
    {
        if (player == null || !player.IsValid || player.IsFakeClient)
            return;

        int id = player.PlayerID;
        ulong steam = player.SteamID;

        if (steam != 0)
            _steamToSlot[steam] = id;

        try
        {
            int bal = Math.Max(0, (int)_api!.GetPlayerBalance(id, WalletKind));
            _balanceCache[id] = bal;
            _logger.LogInformation("[ZPL-AP] OnPlayerLoad: slot={Id} steam={Steam} balance={Bal}", id, steam, bal);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[ZPL-AP] OnPlayerLoad read failed slot={Id}: {Ex}", id, ex.Message);
            _balanceCache[id] = 0;
        }
    }

    // Fired whenever ANY balance change occurs (give, take, set, transfer, etc.)
    private void OnEconomyBalanceChanged(ulong steamId, string walletKind, decimal newBalance, decimal oldBalance)
    {
        if (!string.Equals(walletKind, WalletKind, StringComparison.OrdinalIgnoreCase))
            return;

        if (!_steamToSlot.TryGetValue(steamId, out int playerId))
            return;

        int newBal = Math.Max(0, (int)newBalance);
        _balanceCache[playerId] = newBal;
        _logger.LogDebug("[ZPL-AP] BalanceChanged: slot={Id} {Old}->{New}", playerId, (int)oldBalance, newBal);
    }

    // -------------------------------------------------------------------------
    //  Player lifecycle
    // -------------------------------------------------------------------------

    /// <summary>
    /// Economy loads player data automatically on connect, so this is a stub.
    /// We record the steamId->slot mapping and do a direct cache read if the
    /// slot is still empty (covers the late UseSharedInterface race condition).
    /// </summary>
    public void LoadData(IPlayer player)
    {
        if (player == null || !player.IsValid || player.IsFakeClient)
            return;

        int id = player.PlayerID;
        ulong steam = player.SteamID;

        if (steam != 0)
            _steamToSlot[steam] = id;

        // If the cache is already populated (OnPlayerLoad already fired) do nothing.
        if (_balanceCache.ContainsKey(id))
            return;

        // Cache miss: Economy may already have the data in its own cache but
        // OnPlayerLoad has not fired for us yet (or fired before we subscribed).
        // Try a direct read; OnPlayerLoad will update the cache when it fires.
        if (_api != null && steam != 0)
        {
            try
            {
                int bal = Math.Max(0, (int)_api.GetPlayerBalance(id, WalletKind));
                _balanceCache[id] = bal;
                _logger.LogInformation("[ZPL-AP] LoadData direct-read: slot={Id} balance={Bal}", id, bal);
            }
            catch
            {
                // Data not yet available -- OnPlayerLoad will set the cache when ready.
            }
        }
    }

    /// <summary>Forces a fresh read from Economy for already-connected players.</summary>
    public void RefreshBalance(IPlayer player)
    {
        if (_api == null || player == null || !player.IsValid || player.IsFakeClient || player.SteamID == 0)
            return;

        int id = player.PlayerID;
        _steamToSlot[player.SteamID] = id;

        try
        {
            int bal = Math.Max(0, (int)_api.GetPlayerBalance(id, WalletKind));
            _balanceCache[id] = bal;
            _logger.LogDebug("[ZPL-AP] RefreshBalance: slot={Id} balance={Bal}", id, bal);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[ZPL-AP] RefreshBalance slot={Id} failed: {Ex}", id, ex.Message);
        }
    }

    public void RemovePlayer(int playerId)
    {
        var toRemove = _steamToSlot
            .Where(kv => kv.Value == playerId)
            .Select(kv => kv.Key)
            .ToArray();
        foreach (var s in toRemove)
            _steamToSlot.Remove(s);

        _balanceCache.Remove(playerId);
    }

    // -------------------------------------------------------------------------
    //  Balance operations -- all use the playerid (int) overload
    // -------------------------------------------------------------------------

    public int GetBalance(int playerId)
    {
        if (_balanceCache.TryGetValue(playerId, out int cached))
            return cached;

        // Cache miss: read directly via playerid overload.
        if (_api == null)
            return 0;

        try
        {
            int bal = Math.Max(0, (int)_api.GetPlayerBalance(playerId, WalletKind));
            _balanceCache[playerId] = bal;
            return bal;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("[ZPL-AP] GetBalance cache-miss read failed slot={Id}: {Ex}", playerId, ex.Message);
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
            // Do NOT call _api.SaveData() or _api.GetPlayerBalance() here --
            // both paths invoke Dommel's SqlExpression visitor which causes a
            // JIT SIGSEGV.  Economy auto-saves on disconnect and fires
            // OnPlayerBalanceChanged to keep our cache consistent.
            _balanceCache[playerId] = clamped;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[ZPL-AP] SetBalance slot={Id} amount={A} failed: {Ex}", playerId, amount, ex.Message);
        }
    }

    public void AddBalance(int playerId, int amount)
    {
        if (_api == null || amount <= 0) return;
        try
        {
            _api.AddPlayerBalance(playerId, WalletKind, amount);
            // Do NOT call _api.SaveData() or _api.GetPlayerBalance() here --
            // both paths invoke Dommel's SqlExpression visitor which causes a
            // JIT SIGSEGV.  Economy auto-saves on disconnect and fires
            // OnPlayerBalanceChanged to keep our cache consistent.
            // cur defaults to 0 when the player has no cached entry yet.
            _balanceCache.TryGetValue(playerId, out int cur);
            _balanceCache[playerId] = Math.Max(0, cur + amount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[ZPL-AP] AddBalance slot={Id} amount={A} failed: {Ex}", playerId, amount, ex.Message);
        }
    }

    public bool SpendBalance(int playerId, int cost)
    {
        if (_api == null) return false;
        if (cost <= 0) return true;
        try
        {
            if (!_api.HasSufficientFunds(playerId, WalletKind, cost))
                return false;

            _api.SubtractPlayerBalance(playerId, WalletKind, cost);
            // Do NOT call _api.SaveData() or _api.GetPlayerBalance() here --
            // both paths invoke Dommel's SqlExpression visitor which causes a
            // JIT SIGSEGV.  Economy auto-saves on disconnect and fires
            // OnPlayerBalanceChanged to keep our cache consistent.
            _balanceCache.TryGetValue(playerId, out int cur);
            _balanceCache[playerId] = Math.Max(0, cur - cost);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[ZPL-AP] SpendBalance slot={Id} cost={C} failed: {Ex}", playerId, cost, ex.Message);
            return false;
        }
    }

    // -------------------------------------------------------------------------
    //  Cleanup
    // -------------------------------------------------------------------------

    public void Dispose()
    {
        UnsubscribeEvents();
        _balanceCache.Clear();
        _steamToSlot.Clear();
        _api = null;
    }
}