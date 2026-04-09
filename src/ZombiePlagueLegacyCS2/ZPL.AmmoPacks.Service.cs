using Economy.Contract;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared.Players;

namespace ZombiePlagueLegacyCS2;

public class AmmoPacksService
{
    private readonly ILogger<AmmoPacksService> _logger;
    private readonly IOptionsMonitor<ZPLMainCFG> _mainCFG;
    private IEconomyAPIv1? _api;

    private readonly Dictionary<int, int> _balanceCache = new();
    private readonly Dictionary<int, IPlayer> _playerMap = new();
    // SteamID → PlayerID pentru lookup rapid în OnPlayerLoad
    private readonly Dictionary<ulong, int> _steamToSlot = new();

    public AmmoPacksService(
        ILogger<AmmoPacksService> logger,
        IOptionsMonitor<ZPLMainCFG> mainCFG)
    {
        _logger = logger;
        _mainCFG = mainCFG;
    }

    public void SetApi(IEconomyAPIv1 api)
    {
        if (_api != null)
            UnsubscribeEconomyEvents();

        _api = api;
        _api.OnPlayerLoad += OnEconomyPlayerLoad;
        _api.OnPlayerBalanceChanged += OnEconomyPlayerBalanceChanged;
        _api.OnPlayerFundsTransferred += OnEconomyPlayerFundsTransferred;
    }

    private string WalletKind => _mainCFG.CurrentValue.EconomyWalletKind;

    private static int ConvertBalance(decimal balance)
        => (int)Math.Max(0, Math.Floor(balance));

    private void UnsubscribeEconomyEvents()
    {
        if (_api == null) return;
        _api.OnPlayerLoad -= OnEconomyPlayerLoad;
        _api.OnPlayerBalanceChanged -= OnEconomyPlayerBalanceChanged;
        _api.OnPlayerFundsTransferred -= OnEconomyPlayerFundsTransferred;
    }

    private void OnEconomyPlayerBalanceChanged(ulong steamId, string walletKind, decimal newBalance, decimal oldBalance)
    {
        if (!string.Equals(walletKind, WalletKind, StringComparison.OrdinalIgnoreCase))
            return;

        if (!_steamToSlot.TryGetValue(steamId, out int playerId))
            return;

        _balanceCache[playerId] = ConvertBalance(newBalance);
        _logger.LogDebug("[ZPL-AP] OnPlayerBalanceChanged: slot={Id} wallet={Kind} old={Old} new={New}", playerId, walletKind, oldBalance, newBalance);
    }

    private void OnEconomyPlayerFundsTransferred(ulong fromSteamId, ulong toSteamId, string walletKind, decimal amount)
    {
        if (!string.Equals(walletKind, WalletKind, StringComparison.OrdinalIgnoreCase))
            return;

        int change = ConvertBalance(amount);

        if (_steamToSlot.TryGetValue(fromSteamId, out int fromId))
            _balanceCache[fromId] = Math.Max(0, _balanceCache.GetValueOrDefault(fromId) - change);

        if (_steamToSlot.TryGetValue(toSteamId, out int toId))
            _balanceCache[toId] = _balanceCache.GetValueOrDefault(toId) + change;
    }

    // Apelat de Economy când datele unui jucător sunt complet încărcate din DB
    private void OnEconomyPlayerLoad(IPlayer player)
    {
        if (player == null || !player.IsValid || player.IsFakeClient)
            return;

        int id = player.PlayerID;
        // Always refresh mappings: a player may be seen earlier with SteamID=0,
        // then receive the real SteamID after authorization.
        _playerMap[id] = player;
        if (player.SteamID != 0)
            _steamToSlot[player.SteamID] = id;

        // Acum datele SUNT gata — citim balanța
        try
        {
            _balanceCache[id] = Math.Max(0, (int)_api!.GetPlayerBalance(player, WalletKind));
            _logger.LogDebug("[ZPL-AP] OnPlayerLoad: slot={Id} balance={Bal}", id, _balanceCache[id]);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[ZPL-AP] OnPlayerLoad balance read failed for slot {Id}: {Ex}", id, ex.Message);
            _balanceCache[id] = 0;
        }
    }

    public void LoadData(IPlayer player)
    {
        int id = player.PlayerID;
        _playerMap[id] = player;
        if (player.SteamID != 0)
            _steamToSlot[player.SteamID] = id;

        if (_api == null) return;

        // Setăm 0 ca valoare temporară — va fi actualizat în OnEconomyPlayerLoad
        _balanceCache[id] = 0;

        // Avoid loading against SteamID=0; wait for Economy's own player-load flow.
        if (player.SteamID == 0)
            return;

        try
        {
            // Aceasta declanșează încărcarea async; OnPlayerLoad va fi apelat când e gata
            _api.LoadData(player);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[ZPL-AP] LoadData({Id}) failed: {Ex}", id, ex.Message);
        }
    }

    public void RemovePlayer(int playerId)
    {
        if (_playerMap.TryGetValue(playerId, out var p) && p.IsValid && p.SteamID != 0)
            _steamToSlot.Remove(p.SteamID);

        // Also purge any stale steam->slot entries that still point to this slot.
        foreach (var steamId in _steamToSlot.Where(kv => kv.Value == playerId).Select(kv => kv.Key).ToArray())
            _steamToSlot.Remove(steamId);

        _balanceCache.Remove(playerId);
        _playerMap.Remove(playerId);
    }

    public void RefreshBalance(IPlayer player)
    {
        if (_api == null || player == null || !player.IsValid || player.IsFakeClient)
            return;

        if (player.SteamID == 0)
            return;

        int id = player.PlayerID;
        _playerMap[id] = player;
        _steamToSlot[player.SteamID] = id;

        try
        {
            _balanceCache[id] = Math.Max(0, ConvertBalance(_api.GetPlayerBalance(player, WalletKind)));
            _logger.LogDebug("[ZPL-AP] RefreshBalance: slot={Id} balance={Bal}", id, _balanceCache[id]);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[ZPL-AP] RefreshBalance({Id}) failed: {Ex}", id, ex.Message);
        }
    }

    public int GetBalance(int playerId)
    {
        if (_balanceCache.TryGetValue(playerId, out int cached))
            return cached;

        // Fallback: încearcă direct dacă nu e în cache
        if (_api == null || !_playerMap.TryGetValue(playerId, out var player))
            return 0;

        try
        {
            int balance = Math.Max(0, (int)_api.GetPlayerBalance(player, WalletKind));
            _balanceCache[playerId] = balance;
            return balance;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[ZPL-AP] GetBalance({Id}) failed: {Ex}", playerId, ex.Message);
            _balanceCache[playerId] = 0;
            return 0;
        }
    }

    public void SetBalance(int playerId, int amount)
    {
        if (_api == null || !_playerMap.TryGetValue(playerId, out var player)) return;
        try
        {
            int clamped = Math.Max(0, amount);
            _api.SetPlayerBalance(player, WalletKind, clamped);
            _api.SaveData(player);
            // Read the actual balance after setting to ensure cache is accurate
            _balanceCache[playerId] = Math.Max(0, (int)_api.GetPlayerBalance(player, WalletKind));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[ZPL-AP] SetBalance({Id},{Amount}) failed: {Ex}", playerId, amount, ex.Message);
        }
    }

    public void AddBalance(int playerId, int amount)
    {
        if (_api == null || amount <= 0) return;
        if (!_playerMap.TryGetValue(playerId, out var player)) return;
        try
        {
            _api.AddPlayerBalance(player, WalletKind, amount);
            _api.SaveData(player);
            // Read the actual balance after addition to ensure cache is accurate
            _balanceCache[playerId] = Math.Max(0, (int)_api.GetPlayerBalance(player, WalletKind));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[ZPL-AP] AddBalance({Id},{Amount}) failed: {Ex}", playerId, amount, ex.Message);
        }
    }

    public bool SpendBalance(int playerId, int cost)
    {
        if (_api == null) return false;
        if (cost <= 0) return true;
        if (!_playerMap.TryGetValue(playerId, out var player)) return false;
        try
        {
            if (!_api.HasSufficientFunds(player, WalletKind, cost)) return false;
            _api.SubtractPlayerBalance(player, WalletKind, cost);
            _api.SaveData(player);
            // Read the actual balance after subtraction to ensure cache is accurate
            _balanceCache[playerId] = Math.Max(0, (int)_api.GetPlayerBalance(player, WalletKind));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[ZPL-AP] SpendBalance({Id},{Cost}) failed: {Ex}", playerId, cost, ex.Message);
            return false;
        }
    }

    public void EnsureWalletKind()
    {
        if (_api == null) return;
        var walletKind = WalletKind;
        if (!_api.WalletKindExists(walletKind))
            _api.EnsureWalletKind(walletKind);
    }

    // Dezabonare la cleanup
    public void Dispose()
    {
        UnsubscribeEconomyEvents();
        _api = null;
    }
}