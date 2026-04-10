using System.Drawing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Helpers;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using DrawingColor = System.Drawing.Color;

namespace ZombiePlagueLegacyCS2;

public class ZPLExtraItemsMenu
{
    private readonly ILogger<ZPLExtraItemsMenu> _logger;
    private readonly ISwiftlyCore _core;
    private readonly ZPLGlobals _globals;
    private readonly ZPLHelpers _helpers;
    private readonly ZPLMenuHelper _menuHelper;
    private readonly IOptionsMonitor<ZPLExtraItemsCFG> _extraItemsCFG;
    private readonly IOptionsMonitor<ZPLMainCFG> _mainCFG;
    private readonly AmmoPacksService _ammoPacks;
    private readonly ZPLGameMode _gameMode;
    private readonly ZPLMineMenu _mineMenu;

    // Injected post-construction to break circular dependency with ZPLServices
    private ZPLServices? _services;
    public void SetServices(ZPLServices services) => _services = services;

    public ZPLExtraItemsMenu(
        ISwiftlyCore core,
        ILogger<ZPLExtraItemsMenu> logger,
        ZPLGlobals globals,
        ZPLHelpers helpers,
        ZPLMenuHelper menuHelper,
        IOptionsMonitor<ZPLExtraItemsCFG> extraItemsCFG,
        IOptionsMonitor<ZPLMainCFG> mainCFG,
        AmmoPacksService ammoPacks,
        ZPLGameMode gameMode,
        ZPLMineMenu mineMenu)
    {
        _core = core;
        _logger = logger;
        _globals = globals;
        _helpers = helpers;
        _menuHelper = menuHelper;
        _extraItemsCFG = extraItemsCFG;
        _mainCFG = mainCFG;
        _ammoPacks = ammoPacks;
        _gameMode = gameMode;
        _mineMenu = mineMenu;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Ammo-pack helpers
    // ─────────────────────────────────────────────────────────────────────────

    public int GetAmmoPacks(int playerId)
    {
        return _ammoPacks.GetBalance(playerId);
    }

    public void SetAmmoPacks(int playerId, int amount)
    {
        _ammoPacks.SetBalance(playerId, amount);
    }

    public bool SpendAmmoPacks(int playerId, int cost)
    {
        return _ammoPacks.SpendBalance(playerId, cost);
    }

    public void AddAmmoPacks(int playerId, int amount)
    {
        _ammoPacks.AddBalance(playerId, amount);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Team / eligibility helpers
    // ─────────────────────────────────────────────────────────────────────────

    private bool IsZombie(int playerId)
    {
        _globals.IsZombie.TryGetValue(playerId, out bool v);
        return v;
    }

    private bool IsSpecialRole(int playerId)
    {
        _globals.IsNemesis.TryGetValue(playerId, out bool isNemesis);
        _globals.IsAssassin.TryGetValue(playerId, out bool isAssassin);
        _globals.IsSurvivor.TryGetValue(playerId, out bool isSurvivor);
        _globals.IsSniper.TryGetValue(playerId, out bool isSniper);
        _globals.IsHero.TryGetValue(playerId, out bool isHero);
        return isNemesis || isAssassin || isSurvivor || isSniper || isHero;
    }

    /// <summary>Returns the number of real (non-bot) connected players.</summary>
    private int GetRealPlayerCount()
    {
        return _core.PlayerManager.GetAllPlayers()
            .Count(p => p != null && p.IsValid && !p.IsFakeClient);
    }

    /// <summary>Parses a "R,G,B,A" string (0–255 each) into a SwiftlyS2 Color.</summary>
    private static SwiftlyS2.Shared.Natives.Color ParseColor(string rgba, byte defaultR, byte defaultG, byte defaultB, byte defaultA)
    {
        try
        {
            var parts = rgba.Split(',');
            if (parts.Length >= 4)
                return new SwiftlyS2.Shared.Natives.Color(
                    byte.Parse(parts[0].Trim()),
                    byte.Parse(parts[1].Trim()),
                    byte.Parse(parts[2].Trim()),
                    byte.Parse(parts[3].Trim()));
        }
        catch { }
        return new SwiftlyS2.Shared.Natives.Color(defaultR, defaultG, defaultB, defaultA);
    }

    private bool ItemAllowedForPlayer(ExtraItemEntry item, int playerId)
    {
        bool zombie = IsZombie(playerId);
        return item.Team switch
        {
            ExtraItemTeam.Human => !zombie,
            ExtraItemTeam.Zombie => zombie,
            ExtraItemTeam.Both => true,
            _ => false
        };
    }

    /// <summary>Returns false when the ZPLMainCFG feature toggle for this item is disabled.</summary>

    private bool IsSpecialOrCustomModeActive()
    {
        if (_globals.AdminForcedModeThisRound)
            return true;

        return _gameMode.CurrentMode is GameModeType.Nemesis
            or GameModeType.Survivor
            or GameModeType.Sniper
            or GameModeType.Swarm
            or GameModeType.Plague
            or GameModeType.Assassin
            or GameModeType.AVS
            or GameModeType.Hero;
    }

    private bool IsToggleEnabled(ExtraItemEntry item)
    {
        var cfg = _mainCFG.CurrentValue;
        return item.Key switch
        {
            "he_grenade"       => cfg.FireGrenade,
            "flash_grenade"    => cfg.LightGrenade,
            "smoke_grenade"    => cfg.FreezeGrenade,
            "inc_grenade"      => cfg.SpawnGiveIncGrenade,
            "teleport_grenade" => cfg.TelportGrenade,
            "scba_suit"        => cfg.CanUseScbaSuit,
            _                  => true
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Menu
    // ─────────────────────────────────────────────────────────────────────────

    public void OpenExtraItemsMenu(IPlayer player)
    {
        if (!player.IsValid) return;

        var controller = player.Controller;
        if (controller == null || !controller.IsValid || !controller.PawnIsAlive)
        {
            _helpers.SendChatT(player, "ExtraItemsMustBeAlive");
            return;
        }

        if (IsSpecialOrCustomModeActive())
        {
            _helpers.SendChatT(player, "ExtraItemsDisabledInMode");
            return;
        }

        var cfg = _extraItemsCFG.CurrentValue;
        int playerId = player.PlayerID;
        int ap = GetAmmoPacks(playerId);

        IMenuAPI menu = _menuHelper.CreateMenu(_helpers.T(player, "ExtraItemsMenuTitle"));

        menu.AddOption(new TextMenuOption(
            HtmlGradient.GenerateGradientText(
                _helpers.T(player, "ExtraItemsMenuAP", ap),
                DrawingColor.Gold, DrawingColor.LightGoldenrodYellow, DrawingColor.Gold),
            updateIntervalMs: 800, pauseIntervalMs: 100)
        {
            TextStyle = MenuOptionTextStyle.ScrollLeftLoop
        });

        bool anyVisible = false;
        foreach (var item in cfg.Items)
        {
            if (!item.Enable) continue;
            if (!IsToggleEnabled(item)) continue;
            if (!ItemAllowedForPlayer(item, playerId)) continue;

            anyVisible = true;
            string label = $"{item.Name}  [{item.Price} AP]";

            var btn = new ButtonMenuOption(label)
            {
                TextStyle = MenuOptionTextStyle.ScrollLeftLoop,
                CloseAfterClick = false
            };
            btn.Tag = "extend";

            // Capture loop variables
            var capturedItem = item;

            btn.Click += async (_, args) =>
            {
                var clicker = args.Player;
                _core.Scheduler.NextTick(() => HandleItemPurchase(clicker, capturedItem));
            };

            menu.AddOption(btn);
        }

        if (!anyVisible)
        {
            menu.AddOption(new TextMenuOption(_helpers.T(player, "ExtraItemsNoneAvailable")));
        }

        _core.MenusAPI.OpenMenuForPlayer(player, menu);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Purchase handling
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleItemPurchase(IPlayer player, ExtraItemEntry item)
    {
        if (!player.IsValid) return;

        var controller = player.Controller;
        if (controller == null || !controller.IsValid || !controller.PawnIsAlive)
        {
            _helpers.SendChatT(player, "ExtraItemsMustBeAlive");
            return;
        }

        int playerId = player.PlayerID;

        if (!ItemAllowedForPlayer(item, playerId))
        {
            _helpers.SendChatT(player, "ExtraItemsWrongTeam");
            return;
        }

        int ap = GetAmmoPacks(playerId);
        if (ap < item.Price)
        {
            _helpers.SendChatT(player, "ExtraItemsNotEnoughAP", item.Price, ap);
            return;
        }

        if (!SpendAmmoPacks(playerId, item.Price))
        {
            _helpers.SendChatT(player, "ExtraItemsNotEnoughAP", item.Price, ap);
            return;
        }

        // Safety check: if the feature toggle was disabled after the menu was opened, refund silently.
        if (!IsToggleEnabled(item))
        {
            AddAmmoPacks(playerId, item.Price);
            return;
        }

        // Per-item purchase limit check (zp_extra_*_limit equivalent).
        if (item.PurchaseLimit > 0)
        {
            if (!_globals.ItemPurchaseCount.TryGetValue(playerId, out var counts))
            {
                counts = new Dictionary<string, int>();
                _globals.ItemPurchaseCount[playerId] = counts;
            }
            counts.TryGetValue(item.Key, out int timesUsed);
            if (timesUsed >= item.PurchaseLimit)
            {
                AddAmmoPacks(playerId, item.Price);
                _helpers.SendChatT(player, "ExtraItemsPurchaseLimit", item.Name, item.PurchaseLimit);
                return;
            }
            counts[item.Key] = timesUsed + 1;
        }

        int newAp = GetAmmoPacks(playerId);

        switch (item.Key)
        {
            case "armor":
                ApplyArmor(player, newAp);
                break;
            case "he_grenade":
                ApplyHEGrenade(player, newAp);
                break;
            case "flash_grenade":
                ApplyFlashGrenade(player, newAp);
                break;
            case "smoke_grenade":
                ApplySmokeGrenade(player, newAp);
                break;
            case "antidote":
                ApplyAntidote(player, newAp);
                break;
            case "zombie_madness":
                ApplyZombieMadness(player, newAp);
                break;
            case "multijump":
                ApplyMultijump(player, newAp);
                break;
            case "knife_blink":
                ApplyKnifeBlink(player, newAp);
                break;
            case "jetpack":
                ApplyJetpack(player, newAp);
                break;
            case "laser_mine":
                ApplyLaserMine(player, newAp);
                break;
            case "revive_token":
                ApplyReviveToken(player, newAp);
                break;
            case "t_virus_grenade":
                ApplyTVirusGrenade(player, newAp);
                break;
            case "inc_grenade":
                ApplyIncGrenadeItem(player, newAp);
                break;
            case "teleport_grenade":
                ApplyTeleportGrenadeItem(player, newAp);
                break;
            case "scba_suit":
                ApplyScbaSuit(player, newAp);
                break;
            case "unlimited_clip":
                ApplyUnlimitedClip(player, newAp);
                break;
            case "no_recoil":
                ApplyNoRecoil(player, newAp);
                break;
            case "tryder":
                ApplyTryder(player, newAp);
                break;
            case "buy_survivor":
                ApplyBuySurvivor(player, newAp);
                break;
            case "buy_nemesis":
                ApplyBuyNemesis(player, newAp);
                break;
            case "buy_sniper":
                ApplyBuySniper(player, newAp);
                break;
            case "buy_assassin":
                ApplyBuyAssassin(player, newAp);
                break;
            default:
                // Unknown item – refund
                AddAmmoPacks(playerId, item.Price);
                _logger.LogWarning("[ZOExtraItems] Unknown item key: {Key}", item.Key);
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Item effects
    // ─────────────────────────────────────────────────────────────────────────

    private void ApplyArmor(IPlayer player, int remainingAP)
    {
        var cfg = _extraItemsCFG.CurrentValue;
        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid) return;

        pawn.ArmorValue = cfg.ArmorAmount;
        pawn.ArmorValueUpdated();

        _helpers.SendChatT(player, "ExtraItemsArmorSuccess", cfg.ArmorAmount, remainingAP);
    }

    private void ApplyHEGrenade(IPlayer player, int remainingAP)
    {
        _helpers.GiveFireGrenade(player);
        _helpers.SendChatT(player, "ExtraItemsGrenadeSuccess",
            _helpers.T(player, "ItemFireGrenade"), remainingAP);
    }

    private void ApplyFlashGrenade(IPlayer player, int remainingAP)
    {
        _helpers.GiveLightGrenade(player);
        _helpers.SendChatT(player, "ExtraItemsGrenadeSuccess",
            _helpers.T(player, "ItemLightGrenade"), remainingAP);
    }

    private void ApplySmokeGrenade(IPlayer player, int remainingAP)
    {
        _helpers.GiveFreezeGrenade(player);
        _helpers.SendChatT(player, "ExtraItemsGrenadeSuccess",
            _helpers.T(player, "ItemFreezeGrenade"), remainingAP);
    }

    private void ApplyAntidote(IPlayer player, int remainingAP)
    {
        int playerId = player.PlayerID;
        if (!IsZombie(playerId))
        {
            AddAmmoPacks(playerId, _extraItemsCFG.CurrentValue.Items
                .FirstOrDefault(i => i.Key == "antidote")?.Price ?? 0);
            _helpers.SendChatT(player, "ExtraItemsAntidoteNotZombie");
            return;
        }

        if (IsSpecialRole(playerId))
        {
            AddAmmoPacks(playerId, _extraItemsCFG.CurrentValue.Items
                .FirstOrDefault(i => i.Key == "antidote")?.Price ?? 0);
            _helpers.SendChatT(player, "ItemTVaccineError");
            return;
        }

        var mainCFG = _mainCFG.CurrentValue;
        string defaultModel = string.IsNullOrEmpty(mainCFG.HumandefaultModel)
            ? "characters/models/ctm_st6/ctm_st6_variante.vmdl"
            : mainCFG.HumandefaultModel;

        _helpers.TVaccine(player, mainCFG.HumanMaxHealth, mainCFG.HumanInitialSpeed,
            defaultModel, mainCFG.TVaccineSound, 1.0f);

        _helpers.SendChatT(player, "ExtraItemsAntidoteSuccess", remainingAP);
        _helpers.SendChatToAllT("ExtraItemsAntidoteSuccessToAll", player.Name);
    }

    private void ApplyZombieMadness(IPlayer player, int remainingAP)
    {
        int playerId = player.PlayerID;
        if (!IsZombie(playerId))
        {
            AddAmmoPacks(playerId, _extraItemsCFG.CurrentValue.Items
                .FirstOrDefault(i => i.Key == "zombie_madness")?.Price ?? 0);
            _helpers.SendChatT(player, "ItemHumanCantUse");
            return;
        }

        _globals.ZombieMadnessActive.TryGetValue(playerId, out bool alreadyActive);
        if (alreadyActive)
        {
            AddAmmoPacks(playerId, _extraItemsCFG.CurrentValue.Items
                .FirstOrDefault(i => i.Key == "zombie_madness")?.Price ?? 0);
            _helpers.SendChatT(player, "ExtraItemsMadnessAlready");
            return;
        }

        float duration = _extraItemsCFG.CurrentValue.MadnessDuration;
        _globals.ZombieMadnessActive[playerId] = true;

        _helpers.SendChatT(player, "ExtraItemsMadnessSuccess", duration, remainingAP);

        _core.Scheduler.DelayBySeconds(duration, () =>
        {
            if (!player.IsValid) return;
            _globals.ZombieMadnessActive[playerId] = false;
            _helpers.SendChatT(player, "ExtraItemsMadnessEnd");
        });
    }

    private void ApplyMultijump(IPlayer player, int remainingAP)
    {
        int playerId = player.PlayerID;
        var cfg = _extraItemsCFG.CurrentValue;

        _globals.ExtraJumps.TryGetValue(playerId, out int currentExtra);
        if (currentExtra >= cfg.MultijumpMax)
        {
            AddAmmoPacks(playerId, cfg.Items.FirstOrDefault(i => i.Key == "multijump")?.Price ?? 0);
            _helpers.SendChatT(player, "ExtraItemsMultijumpMax", cfg.MultijumpMax);
            return;
        }

        int newExtra = Math.Min(currentExtra + cfg.MultijumpIncrement, cfg.MultijumpMax);
        _globals.ExtraJumps[playerId] = newExtra;

        _helpers.SendChatT(player, "ExtraItemsMultijumpSuccess", newExtra, remainingAP);
    }

    private void ApplyKnifeBlink(IPlayer player, int remainingAP)
    {
        int playerId = player.PlayerID;
        var cfg = _extraItemsCFG.CurrentValue;

        _globals.KnifeBlinkCharges.TryGetValue(playerId, out int currentCharges);
        int newCharges = currentCharges + cfg.KnifeBlinkCharges;
        _globals.KnifeBlinkCharges[playerId] = newCharges;

        _helpers.SendChatT(player, "ExtraItemsKnifeBlinkSuccess", cfg.KnifeBlinkCharges, newCharges, remainingAP);
    }

    private void ApplyJetpack(IPlayer player, int remainingAP)
    {
        int playerId = player.PlayerID;
        var cfg = _extraItemsCFG.CurrentValue;

        _globals.HasJetpack.TryGetValue(playerId, out bool alreadyHas);
        if (alreadyHas)
        {
            // Refuel instead of blocking purchase
            _globals.JetpackFuel[playerId] = cfg.JetpackMaxFuel;
            _helpers.SendChatT(player, "ExtraItemsJetpackRefueled", cfg.JetpackMaxFuel, remainingAP);
            return;
        }

        _globals.HasJetpack[playerId] = true;
        _globals.JetpackFuel[playerId] = cfg.JetpackMaxFuel;
        _globals.JetpackLastFuelTime[playerId] = 0f;

        _helpers.SendChatT(player, "ExtraItemsJetpackSuccess", cfg.JetpackMaxFuel, remainingAP);
    }

    private void ApplyLaserMine(IPlayer player, int remainingAP)
    {
        // Purchasing this extra item opens the mine selection menu directly.
        // The player picks a mine type from the menu and places it on a surface.
        _helpers.SendChatT(player, "ExtraItemsLaserMineSuccess", remainingAP);
        _mineMenu.OpenMineMenu(player);
    }

    private void ApplyReviveToken(IPlayer player, int remainingAP)
    {
        int playerId = player.PlayerID;

        _globals.HasReviveToken.TryGetValue(playerId, out bool alreadyHas);
        if (alreadyHas)
        {
            AddAmmoPacks(playerId, _extraItemsCFG.CurrentValue.Items
                .FirstOrDefault(i => i.Key == "revive_token")?.Price ?? 0);
            _helpers.SendChatT(player, "ExtraItemsReviveTokenAlready");
            return;
        }

        _globals.HasReviveToken[playerId] = true;

        _helpers.SendChatT(player, "ExtraItemsReviveTokenSuccess", remainingAP);
    }

    private void ApplyTVirusGrenade(IPlayer player, int remainingAP)
    {
        int playerId = player.PlayerID;
        if (!IsZombie(playerId))
        {
            AddAmmoPacks(playerId, _extraItemsCFG.CurrentValue.Items
                .FirstOrDefault(i => i.Key == "t_virus_grenade")?.Price ?? 0);
            _helpers.SendChatT(player, "ItemHumanCantUse");
            return;
        }

        _helpers.TVirusGrenade(player);
        _helpers.SendChatT(player, "ExtraItemsGrenadeSuccess",
            _helpers.T(player, "ItemTVirusGrenade"), remainingAP);
    }

    private void ApplyIncGrenadeItem(IPlayer player, int remainingAP)
    {
        _helpers.GiveIncGrenade(player);
        _helpers.SendChatT(player, "ExtraItemsGrenadeSuccess",
            _helpers.T(player, "ItemIncGrenade"), remainingAP);
    }

    private void ApplyTeleportGrenadeItem(IPlayer player, int remainingAP)
    {
        _helpers.GiveTeleprotGrenade(player);
        _helpers.SendChatT(player, "ExtraItemsGrenadeSuccess",
            _helpers.T(player, "ItemTeleportGrenade"), remainingAP);
    }

    private void ApplyScbaSuit(IPlayer player, int remainingAP)
    {
        int playerId = player.PlayerID;
        if (_globals.ScbaSuit.GetValueOrDefault(playerId))
        {
            AddAmmoPacks(playerId, _extraItemsCFG.CurrentValue.Items
                .FirstOrDefault(i => i.Key == "scba_suit")?.Price ?? 0);
            _helpers.SendChatT(player, "ItemSCBASuitAlready");
            return;
        }

        var mainCFG = _mainCFG.CurrentValue;
        _globals.ScbaSuit[playerId] = true;
        _helpers.EmitSoundFormPlayer(player, mainCFG.ScbaSuitGetSound, 1.0f);
        _helpers.SendChatT(player, "ExtraItemsScbaSuitSuccess", remainingAP);
        _helpers.SendChatToAllT("ItemSCBASuitSuccessToAll", player.Name);
    }

    private void ApplyUnlimitedClip(IPlayer player, int remainingAP)
    {
        int playerId = player.PlayerID;
        if (_globals.InfiniteClipState.GetValueOrDefault(playerId))
        {
            AddAmmoPacks(playerId, _extraItemsCFG.CurrentValue.Items
                .FirstOrDefault(i => i.Key == "unlimited_clip")?.Price ?? 0);
            _helpers.SendChatT(player, "ExtraItemsUnlimitedClipAlready");
            return;
        }
        _globals.InfiniteClipState[playerId] = true;
        _helpers.SendChatT(player, "ExtraItemsUnlimitedClipSuccess", remainingAP);
    }

    private void ApplyNoRecoil(IPlayer player, int remainingAP)
    {
        int playerId = player.PlayerID;
        if (_globals.ExtraNoRecoilState.GetValueOrDefault(playerId))
        {
            AddAmmoPacks(playerId, _extraItemsCFG.CurrentValue.Items
                .FirstOrDefault(i => i.Key == "no_recoil")?.Price ?? 0);
            _helpers.SendChatT(player, "ExtraItemsNoRecoilAlready");
            return;
        }
        _globals.ExtraNoRecoilState[playerId] = true;
        _helpers.SendChatT(player, "ExtraItemsNoRecoilSuccess", remainingAP);
    }

    private void ApplyTryder(IPlayer player, int remainingAP)
    {
        int playerId = player.PlayerID;
        if (_globals.TryderState.GetValueOrDefault(playerId))
        {
            AddAmmoPacks(playerId, _extraItemsCFG.CurrentValue.Items
                .FirstOrDefault(i => i.Key == "tryder")?.Price ?? 0);
            _helpers.SendChatT(player, "ExtraItemsTryderAlready");
            return;
        }

        var cfg = _extraItemsCFG.CurrentValue;
        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid) return;

        _globals.TryderState[playerId] = true;
        _globals.InfiniteClipState[playerId] = true;

        pawn.MaxHealth = cfg.TryderHealth;
        pawn.MaxHealthUpdated();
        pawn.Health = cfg.TryderHealth;
        pawn.HealthUpdated();

        pawn.ArmorValue = cfg.TryderArmor;
        pawn.ArmorValueUpdated();

        _helpers.SetGlow(player, cfg.TryderGlowR, cfg.TryderGlowG, cfg.TryderGlowB, 255);

        _helpers.SendChatT(player, "ExtraItemsTryderSuccess", remainingAP);
    }

    private void ApplyBuySurvivor(IPlayer player, int remainingAP)
    {
        int playerId = player.PlayerID;
        int price = _extraItemsCFG.CurrentValue.Items
            .FirstOrDefault(i => i.Key == "buy_survivor")?.Price ?? 0;
        if (_services == null)
        {
            _logger.LogError("[ZOExtraItems] ZPLServices not wired – refunding buy_survivor.");
            AddAmmoPacks(playerId, price);
            return;
        }
        // Survivors are a human-only role; zombies and existing special roles cannot buy.
        if (IsZombie(playerId) || IsSpecialRole(playerId))
        {
            AddAmmoPacks(playerId, price);
            _helpers.SendChatT(player, "ExtraItemsBuyRoleNotEligible");
            return;
        }
        // Require the same minimum player count as the Survivor round mode.
        int minPlayers = _mainCFG.CurrentValue.Survivor.MinPlayers;
        int currentPlayers = GetRealPlayerCount();
        if (minPlayers > 0 && currentPlayers < minPlayers)
        {
            AddAmmoPacks(playerId, price);
            _helpers.SendChatT(player, "ExtraItemsBuyRoleNotEnoughPlayers", minPlayers, currentPlayers);
            return;
        }
        _services.SetupSurvivor(player);
        _helpers.SendChatT(player, "ExtraItemsBuySurvivorSuccess", remainingAP);
    }

    private void ApplyBuyNemesis(IPlayer player, int remainingAP)
    {
        int playerId = player.PlayerID;
        int price = _extraItemsCFG.CurrentValue.Items
            .FirstOrDefault(i => i.Key == "buy_nemesis")?.Price ?? 0;
        if (_services == null)
        {
            _logger.LogError("[ZOExtraItems] ZPLServices not wired – refunding buy_nemesis.");
            AddAmmoPacks(playerId, price);
            return;
        }
        // Nemesis is a zombie role — only active zombies without a special role can buy.
        if (!IsZombie(playerId) || IsSpecialRole(playerId))
        {
            AddAmmoPacks(playerId, price);
            _helpers.SendChatT(player, "ExtraItemsBuyRoleNotEligible");
            return;
        }
        // Require the same minimum player count as the Nemesis round mode.
        int minPlayers = _mainCFG.CurrentValue.Nemesis.MinPlayers;
        int currentPlayers = GetRealPlayerCount();
        if (minPlayers > 0 && currentPlayers < minPlayers)
        {
            AddAmmoPacks(playerId, price);
            _helpers.SendChatT(player, "ExtraItemsBuyRoleNotEnoughPlayers", minPlayers, currentPlayers);
            return;
        }
        _services.SetupNemesis(player);
        _helpers.SendChatT(player, "ExtraItemsBuyNemesisSuccess", remainingAP);
    }

    private void ApplyBuySniper(IPlayer player, int remainingAP)
    {
        int playerId = player.PlayerID;
        int price = _extraItemsCFG.CurrentValue.Items
            .FirstOrDefault(i => i.Key == "buy_sniper")?.Price ?? 0;
        if (_services == null)
        {
            _logger.LogError("[ZOExtraItems] ZPLServices not wired – refunding buy_sniper.");
            AddAmmoPacks(playerId, price);
            return;
        }
        // Snipers are a human-only role; zombies and existing special roles cannot buy.
        if (IsZombie(playerId) || IsSpecialRole(playerId))
        {
            AddAmmoPacks(playerId, price);
            _helpers.SendChatT(player, "ExtraItemsBuyRoleNotEligible");
            return;
        }
        // Require the same minimum player count as the Sniper round mode.
        int minPlayers = _mainCFG.CurrentValue.Sniper.MinPlayers;
        int currentPlayers = GetRealPlayerCount();
        if (minPlayers > 0 && currentPlayers < minPlayers)
        {
            AddAmmoPacks(playerId, price);
            _helpers.SendChatT(player, "ExtraItemsBuyRoleNotEnoughPlayers", minPlayers, currentPlayers);
            return;
        }
        _services.SetupSniper(player);
        _helpers.SendChatT(player, "ExtraItemsBuySniperSuccess", remainingAP);
    }

    private void ApplyBuyAssassin(IPlayer player, int remainingAP)
    {
        int playerId = player.PlayerID;
        int price = _extraItemsCFG.CurrentValue.Items
            .FirstOrDefault(i => i.Key == "buy_assassin")?.Price ?? 0;
        if (_services == null)
        {
            _logger.LogError("[ZOExtraItems] ZPLServices not wired – refunding buy_assassin.");
            AddAmmoPacks(playerId, price);
            return;
        }
        // Assassin is a zombie role — only active zombies without a special role can buy.
        if (!IsZombie(playerId) || IsSpecialRole(playerId))
        {
            AddAmmoPacks(playerId, price);
            _helpers.SendChatT(player, "ExtraItemsBuyRoleNotEligible");
            return;
        }
        // Require the same minimum player count as the Assassin round mode.
        int minPlayers = _mainCFG.CurrentValue.Assassin.MinPlayers;
        int currentPlayers = GetRealPlayerCount();
        if (minPlayers > 0 && currentPlayers < minPlayers)
        {
            AddAmmoPacks(playerId, price);
            _helpers.SendChatT(player, "ExtraItemsBuyRoleNotEnoughPlayers", minPlayers, currentPlayers);
            return;
        }
        _services.SetupAssassin(player);
        _helpers.SendChatT(player, "ExtraItemsBuyAssassinSuccess", remainingAP);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Knife-blink execution (triggered from EventWeaponFire when knife is fired)
    // ─────────────────────────────────────────────────────────────────────────

    public void TryExecuteKnifeBlink(IPlayer player)
    {
        if (!player.IsValid) return;

        var controller = player.Controller;
        if (controller == null || !controller.IsValid || !controller.PawnIsAlive) return;

        int playerId = player.PlayerID;

        if (IsZombie(playerId)) return;

        _globals.KnifeBlinkCharges.TryGetValue(playerId, out int charges);
        if (charges <= 0) return;

        // Use Environment.TickCount64 (ms) for cooldown tracking
        long nowMs = Environment.TickCount64;
        _globals.KnifeBlinkCooldownEnd.TryGetValue(playerId, out long cooldownEndMs);
        if (nowMs < cooldownEndMs) return;

        var cfg = _extraItemsCFG.CurrentValue;
        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid) return;

        var origin = pawn.AbsOrigin;
        if (origin == null) return;

        // Compute destination: eye forward direction * distance
        QAngle eyeAngles = pawn.EyeAngles;
        float yawRad = eyeAngles.Y * MathF.PI / 180f;
        float pitchRad = eyeAngles.X * MathF.PI / 180f;

        float cosPitch = MathF.Cos(pitchRad);
        float fwdX = cosPitch * MathF.Cos(yawRad);
        float fwdY = cosPitch * MathF.Sin(yawRad);
        float fwdZ = -MathF.Sin(pitchRad);

        var dest = new Vector(
            origin.Value.X + fwdX * cfg.KnifeBlinkDistance,
            origin.Value.Y + fwdY * cfg.KnifeBlinkDistance,
            origin.Value.Z + fwdZ * cfg.KnifeBlinkDistance
        );

        pawn.Teleport(dest, eyeAngles, Vector.Zero);

        _globals.KnifeBlinkCharges[playerId] = charges - 1;
        _globals.KnifeBlinkCooldownEnd[playerId] = nowMs + (long)(cfg.KnifeBlinkCooldown * 1000);

        _helpers.SendChatT(player, "ExtraItemsKnifeBlinkUsed", charges - 1, cfg.KnifeBlinkCooldown);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Jetpack – omnidirectional thrust (upward + WASD-based horizontal)
    // ─────────────────────────────────────────────────────────────────────────

    // Named constants for internal physics values
    private const float MaxDeltaTime      = 0.15f; // clamp dt to avoid large jumps
    private const float DefaultDeltaTime  = 0.05f; // fallback dt on first tick / long gap

    public void TryExecuteJetpackThrust(IPlayer player)
    {
        if (!player.IsValid) return;

        int id = player.PlayerID;
        if (!_globals.HasJetpack.TryGetValue(id, out bool hasJetpack) || !hasJetpack) return;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid) return;

        bool duckPressed  = (player.PressedButtons & GameButtonFlags.Ctrl)  != 0;
        bool spacePressed = (player.PressedButtons & GameButtonFlags.Space) != 0;

        if (!duckPressed || !spacePressed) return;

        _globals.JetpackFuel.TryGetValue(id, out float fuel);
        if (fuel <= 0) return;

        float now = _core.Engine.GlobalVars.CurrentTime;
        _globals.JetpackLastFuelTime.TryGetValue(id, out float lastTime);

        float dt = now - lastTime;
        if (dt <= 0f || dt > MaxDeltaTime) dt = DefaultDeltaTime; // clamp: first tick / long gap

        var cfg = _extraItemsCFG.CurrentValue;
        float fuelUsed = cfg.JetpackFuelConsumeRate * dt;
        _globals.JetpackFuel[id] = Math.Max(0f, fuel - fuelUsed);
        _globals.JetpackLastFuelTime[id] = now;

        // ── Horizontal thrust from WASD keys relative to eye yaw ──────────────
        bool wPressed = (player.PressedButtons & GameButtonFlags.W) != 0;
        bool sPressed = (player.PressedButtons & GameButtonFlags.S) != 0;
        bool aPressed = (player.PressedButtons & GameButtonFlags.A) != 0;
        bool dPressed = (player.PressedButtons & GameButtonFlags.D) != 0;

        var vel = pawn.AbsVelocity;
        float newVelX = vel.X;
        float newVelY = vel.Y;

        if (wPressed || sPressed || aPressed || dPressed)
        {
            QAngle eyeAngles = pawn.EyeAngles;
            float yawRad = eyeAngles.Y * MathF.PI / 180f;
            float cosYaw = MathF.Cos(yawRad);
            float sinYaw = MathF.Sin(yawRad);

            // Forward/backward along eye yaw, strafe perpendicular
            float dirX = 0f, dirY = 0f;
            if (wPressed) { dirX += cosYaw; dirY += sinYaw; }
            if (sPressed) { dirX -= cosYaw; dirY -= sinYaw; }
            if (dPressed) { dirX += sinYaw; dirY -= cosYaw; }
            if (aPressed) { dirX -= sinYaw; dirY += cosYaw; }

            float mag = MathF.Sqrt(dirX * dirX + dirY * dirY);
            if (mag > 0.001f)
            {
                float hForce = cfg.JetpackHorizontalForce;
                newVelX = (dirX / mag) * hForce;
                newVelY = (dirY / mag) * hForce;
            }
        }

        // Apply thrust: upward override + horizontal (WASD-driven or preserve existing)
        float force = cfg.JetpackThrustForce;

        var newVel = new Vector(
            newVelX,
            newVelY,
            force  // fixed upward velocity override (counters gravity + lifts)
        );

        pawn.Teleport(null, null, newVel);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Cleanup helpers (called from Events on death / disconnect / round-end)
    // ─────────────────────────────────────────────────────────────────────────

    public void CleanupJetpack(int playerId)
    {
        _globals.HasJetpack.Remove(playerId);
        _globals.JetpackFuel.Remove(playerId);
        _globals.JetpackLastFuelTime.Remove(playerId);
    }
}
