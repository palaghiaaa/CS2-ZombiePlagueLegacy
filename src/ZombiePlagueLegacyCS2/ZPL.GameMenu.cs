using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using Vector = SwiftlyS2.Shared.Natives.Vector;

namespace ZombiePlagueLegacyCS2;

/// <summary>
/// Main game menu accessible via !menu / !zp chat commands or the zo_menu console command.
/// Replicates the classic CS1.6 Zombie Plague: Legacy menu structure:
///   1. Buy Weapons
///   2. Buy Extra Items
///   3. Choose Zombie Class
///   4. Unstuck
///   5. Join Spectator
/// </summary>
public class ZPLGameMenu
{
    private readonly ILogger<ZPLGameMenu> _logger;
    private readonly ISwiftlyCore _core;
    private readonly ZPLGlobals _globals;
    private readonly ZPLHelpers _helpers;
    private readonly ZPLMenuHelper _menuHelper;
    private readonly ZPLZombieClassMenu _zombieClassMenu;
    private readonly ZPLExtraItemsMenu _extraItemsMenu;
    private readonly ZPLWeaponsMenu _weaponsMenu;
    private readonly ZPLUserSettingsMenu _userSettingsMenu;
    private readonly ZPLAdminItemMenu _adminMenu;

    public ZPLGameMenu(
        ISwiftlyCore core,
        ILogger<ZPLGameMenu> logger,
        ZPLGlobals globals,
        ZPLHelpers helpers,
        ZPLMenuHelper menuHelper,
        ZPLZombieClassMenu zombieClassMenu,
        ZPLExtraItemsMenu extraItemsMenu,
        ZPLWeaponsMenu weaponsMenu,
        ZPLUserSettingsMenu userSettingsMenu,
        ZPLAdminItemMenu adminMenu)
    {
        _core = core;
        _logger = logger;
        _globals = globals;
        _helpers = helpers;
        _menuHelper = menuHelper;
        _zombieClassMenu = zombieClassMenu;
        _extraItemsMenu = extraItemsMenu;
        _weaponsMenu = weaponsMenu;
        _userSettingsMenu = userSettingsMenu;
        _adminMenu = adminMenu;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Open the main menu
    // ─────────────────────────────────────────────────────────────────────────

    public void OpenGameMenu(IPlayer player)
    {
        if (!player.IsValid) return;

        IMenuAPI menu = _menuHelper.CreateMenu(_helpers.T(player, "GameMenuTitle"));

        menu.AddOption(ZPLMenuHelper.LargeText(_helpers.T(player, "GameMenuHint")));

        // 1 – Buy Weapons
        var buyWeaponsBtn = ZPLMenuHelper.LargeButton(_helpers.T(player, "GameMenuBuyWeapons"));
        buyWeaponsBtn.Click += async (_, args) =>
        {
            var clicker = args.Player;
            _core.Scheduler.NextTick(() =>
            {
                if (!clicker.IsValid) return;
                _weaponsMenu.OpenWeaponsMenuIfAllowed(clicker);
            });
        };
        menu.AddOption(buyWeaponsBtn);

        // 2 – Buy Extra Items
        var extraItemsBtn = ZPLMenuHelper.LargeButton(_helpers.T(player, "GameMenuExtraItems"));
        extraItemsBtn.Click += async (_, args) =>
        {
            var clicker = args.Player;
            _core.Scheduler.NextTick(() =>
            {
                if (!clicker.IsValid) return;
                _extraItemsMenu.OpenExtraItemsMenu(clicker);
            });
        };
        menu.AddOption(extraItemsBtn);

        // 3 – Choose Zombie Class
        var zombieClassBtn = ZPLMenuHelper.LargeButton(_helpers.T(player, "GameMenuZombieClass"));
        zombieClassBtn.Click += async (_, args) =>
        {
            var clicker = args.Player;
            _core.Scheduler.NextTick(() =>
            {
                if (!clicker.IsValid) return;
                _zombieClassMenu.OpenZombieClassMenu(clicker);
            });
        };
        menu.AddOption(zombieClassBtn);

        // 4 – Unstuck
        var unstuckBtn = ZPLMenuHelper.LargeButton(_helpers.T(player, "GameMenuUnstuck"));
        unstuckBtn.Click += async (_, args) =>
        {
            var clicker = args.Player;
            _core.Scheduler.NextTick(() =>
            {
                if (!clicker.IsValid) return;
                TryUnstuck(clicker);
            });
        };
        menu.AddOption(unstuckBtn);

        // 5 – Join Spectator
        var specBtn = ZPLMenuHelper.LargeButton(_helpers.T(player, "GameMenuJoinSpectator"));
        specBtn.Click += async (_, args) =>
        {
            var clicker = args.Player;
            _core.Scheduler.NextTick(() =>
            {
                if (!clicker.IsValid) return;
                TryJoinSpectator(clicker);
            });
        };
        menu.AddOption(specBtn);

        // 6 – Admin Menu (only shown to admins)
        var settingsBtn = ZPLMenuHelper.LargeButton(_helpers.T(player, "GameMenuUserSettings"), ZPLMenuHelper.ColSelected);
        settingsBtn.Click += async (_, args) =>
        {
            var clicker = args.Player;
            _core.Scheduler.NextTick(() =>
            {
                if (!clicker.IsValid) return;
                _userSettingsMenu.OpenUserSettingsMenu(clicker);
            });
        };
        menu.AddOption(settingsBtn);

        if (_helpers.HasAdminMenuPermission(player))
        {
            var adminBtn = ZPLMenuHelper.LargeButton(_helpers.T(player, "GameMenuAdminMenu"), ZPLMenuHelper.ColAmber);
            adminBtn.Click += async (_, args) =>
            {
                var clicker = args.Player;
                _core.Scheduler.NextTick(() =>
                {
                    if (!clicker.IsValid) return;
                    if (!_helpers.HasAdminMenuPermission(clicker))
                    {
                        _helpers.SendChatT(clicker, "NoPermission");
                        return;
                    }

                    _adminMenu.OpenAdminItemMenu(clicker);
                });
            };
            menu.AddOption(adminBtn);
        }

        _core.MenusAPI.OpenMenuForPlayer(player, menu);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Unstuck logic
    // ─────────────────────────────────────────────────────────────────────────

    private const float MinSafeWorldZ = -8192f;
    private const float MaxSafeWorldZ = 16384f;
    private const float MaxUnstuckStepDrop = 96f;

    private bool IsSafeTeleportDestination(IPlayer player, Vector origin, Vector candidate)
    {
        if (candidate.Z < MinSafeWorldZ || candidate.Z > MaxSafeWorldZ)
            return false;

        float dx = candidate.X - origin.X;
        float dy = candidate.Y - origin.Y;
        float dz = candidate.Z - origin.Z;

        // Avoid giant jumps; unstuck should only do local corrections.
        float horizontalDist = MathF.Sqrt(dx * dx + dy * dy);
        if (horizontalDist > 180f)
            return false;

        // Never push player too far below their current floor level.
        if (dz < -MaxUnstuckStepDrop)
            return false;

        // Avoid teleporting inside another player, which often results in immediate re-stuck.
        foreach (var other in _core.PlayerManager.GetAllPlayers())
        {
            if (other == null || !other.IsValid || other.PlayerID == player.PlayerID)
                continue;

            var otherPawn = other.PlayerPawn;
            if (otherPawn == null || !otherPawn.IsValid)
                continue;

            var otherPos = otherPawn.AbsOrigin;
            if (otherPos == null)
                continue;

            float ox = otherPos.Value.X - candidate.X;
            float oy = otherPos.Value.Y - candidate.Y;
            float oz = MathF.Abs(otherPos.Value.Z - candidate.Z);
            if ((ox * ox + oy * oy) <= (24f * 24f) && oz <= 72f)
                return false;
        }

        return true;
    }

    private void TryUnstuck(IPlayer player)
    {
        if (!player.IsValid) return;

        var controller = player.Controller;
        if (controller == null || !controller.IsValid || !controller.PawnIsAlive)
        {
            _helpers.SendChatT(player, "UnstuckMustBeAlive");
            return;
        }

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid) return;

        var origin = pawn.AbsOrigin;
        if (origin == null) return;

        var angles = pawn.EyeAngles;
        angles.ToDirectionVectors(out Vector forward, out Vector right, out _);

        // Prioritize small forward/side corrections before wider attempts.
        var attempts = new (float fwd, float side, float up)[]
        {
            (28f, 0f, 18f),
            (20f, 22f, 18f),
            (20f, -22f, 18f),
            (-20f, 0f, 20f),
            (36f, 0f, 28f),
            (0f, 34f, 24f),
            (0f, -34f, 24f),
            (-32f, 28f, 24f),
            (-32f, -28f, 24f),
            (58f, 0f, 36f)
        };

        var current = origin.Value;
        foreach (var (fwd, side, up) in attempts)
        {
            var candidate = new Vector(
                current.X + (forward.X * fwd) + (right.X * side),
                current.Y + (forward.Y * fwd) + (right.Y * side),
                current.Z + up);

            if (!IsSafeTeleportDestination(player, current, candidate))
                continue;

            pawn.Teleport(candidate, angles, Vector.Zero);
            _helpers.SendChatT(player, "UnstuckSuccess");
            return;
        }

        _helpers.SendChatT(player, "UnstuckNoSafePosition");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Join Spectator logic
    // ─────────────────────────────────────────────────────────────────────────

    private void TryJoinSpectator(IPlayer player)
    {
        if (!player.IsValid) return;

        try
        {
            player.SwitchTeam(Team.Spectator);
            _helpers.SendChatT(player, "JoinSpectatorSuccess");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ZPLGameMenu] Failed to move {Name} to spectator.", player.Name);
            _helpers.SendChatT(player, "JoinSpectatorFailed");
        }
    }
}
