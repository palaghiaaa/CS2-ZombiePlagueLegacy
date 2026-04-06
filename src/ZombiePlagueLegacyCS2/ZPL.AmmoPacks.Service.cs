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
        _api = api;
        // Abonare la evenimentul OnPlayerLoad — se apelează când datele sunt GATA
        _api.OnPlayerLoad += OnEconomyPlayerLoad;
    }

    private string WalletKind => _mainCFG.CurrentValue.EconomyWalletKind;

    // Apelat de Economy când datele unui jucător sunt complet încărcate din DB
    private void OnEconomyPlayerLoad(IPlayer player)
    {
        if (player == null || !player.IsValid || player.IsFakeClient)
            return;

        int id = player.PlayerID;
        if (!_playerMap.ContainsKey(id))
        {
            // Jucătorul nu e în map-ul nostru → înregistrează-l
            _playerMap[id] = player;
            _steamToSlot[player.SteamID] = id;
        }

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
        _steamToSlot[player.SteamID] = id;

        if (_api == null) return;

        // Setăm 0 ca valoare temporară — va fi actualizat în OnEconomyPlayerLoad
        _balanceCache[id] = 0;

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
        if (_playerMap.TryGetValue(playerId, out var p) && p.IsValid)
            _steamToSlot.Remove(p.SteamID);

        _balanceCache.Remove(playerId);
        _playerMap.Remove(playerId);
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
            _balanceCache[playerId] = clamped;
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
            int before = GetBalance(playerId);
            _api.AddPlayerBalance(player, WalletKind, amount);
            _api.SaveData(player);
            _balanceCache[playerId] = before + amount;
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
            int before = GetBalance(playerId);
            _api.SubtractPlayerBalance(player, WalletKind, cost);
            _api.SaveData(player);
            _balanceCache[playerId] = Math.Max(0, before - cost);
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
        if (_api != null)
            _api.OnPlayerLoad -= OnEconomyPlayerLoad;
    }
}