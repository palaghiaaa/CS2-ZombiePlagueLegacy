using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.SchemaDefinitions;
using NativeColor = SwiftlyS2.Shared.Natives.Color;

namespace ZombieOutstandingCS2;

/// <summary>
/// Applies a server-wide default skybox (material, brightness, tint colour)
/// to the map's <c>env_sky</c> entity on every map load.
/// Configured entirely through the <see cref="ZOMainCFG"/> section of
/// ZombieOutstandingCFG.jsonc — no player commands or menus.
/// </summary>
public class ZOSkyboxManager
{
    private const string OwnedTag = "zo_sky_default";
    private const int MaterialFuncVtableIndex = 14;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint FindOrCreateMaterialDelegate(nint a1, nint a2, string name);

    private readonly ISwiftlyCore _core;
    private readonly ILogger<ZOSkyboxManager> _logger;
    private readonly IOptionsMonitor<ZOMainCFG> _mainCFG;

    private nint _matSysPtr;
    /// <summary>Set to true once the material-system lookup has been attempted.</summary>
    private bool _matSysInitialized;

    public ZOSkyboxManager(
        ISwiftlyCore core,
        ILogger<ZOSkyboxManager> logger,
        IOptionsMonitor<ZOMainCFG> mainCFG)
    {
        _core     = core;
        _logger   = logger;
        _mainCFG  = mainCFG;
    }

    // ── Public surface ────────────────────────────────────────────────────────

    /// <summary>
    /// Called on map load: removes every native <c>env_sky</c>, then creates
    /// one with the configured material/brightness/tint (if configured).
    /// </summary>
    public void OnMapLoad()
    {
        _matSysPtr = 0;
        _matSysInitialized = false;

        var cfg = _mainCFG.CurrentValue;
        if (string.IsNullOrWhiteSpace(cfg.SkyboxMaterial))
            return; // nothing to do — keep map's default

        RemoveNativeSkyboxes();
        SpawnDefaultSkybox(cfg);
    }

    /// <summary>
    /// Called from the entity-spawned hook so map-native <c>env_sky</c>
    /// entities created after our own setup are removed immediately.
    /// </summary>
    public void OnEntitySpawned(CEntityInstance entity)
    {
        if (entity == null || !entity.IsValidEntity) return;

        var cfg = _mainCFG.CurrentValue;
        if (string.IsNullOrWhiteSpace(cfg.SkyboxMaterial))
            return; // not managing skyboxes

        var tag = entity.PrivateVScripts;
        if (tag == OwnedTag) return; // it's ours — keep it

        var sky = entity.As<CEnvSky>();
        if (sky == null || !sky.IsValidEntity) return;

        _core.Scheduler.NextTick(() =>
        {
            if (sky.IsValidEntity)
                sky.Despawn();
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RemoveNativeSkyboxes()
    {
        try
        {
            foreach (var sky in _core.EntitySystem
                         .GetAllEntitiesByDesignerName<CEnvSky>("env_sky")
                         .ToList())
            {
                if (!sky.IsValidEntity) continue;
                if (sky.PrivateVScripts == OwnedTag) continue;
                sky.Despawn();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ZOSkybox] Failed to remove native env_sky entities.");
        }
    }

    private void SpawnDefaultSkybox(ZOMainCFG cfg)
    {
        try
        {
            var sky = _core.EntitySystem.CreateEntity<CEnvSky>();
            sky.PrivateVScripts = OwnedTag;
            sky.DispatchSpawn();

            _core.Scheduler.NextTick(() =>
            {
                if (!sky.IsValidEntity) return;

                // Brightness
                sky.BrightnessScale = cfg.SkyboxBrightness;
                sky.BrightnessScaleUpdated();

                // Tint colour
                var tint = ParseTintColor(cfg.SkyboxTintColor, _logger);
                sky.TintColor = tint;
                sky.TintColorUpdated();

                // Material (requires unsafe pointer write)
                var matPtr = FindMaterialByPath(cfg.SkyboxMaterial);
                if (matPtr != 0)
                    ApplyMaterial(sky, matPtr);
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ZOSkybox] Failed to spawn default env_sky.");
        }
    }

    private static NativeColor ParseTintColor(string raw, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new NativeColor(255, 255, 255, 255);

        var parts = raw.Trim().Split(' ');
        if (parts.Length < 3)
        {
            logger.LogWarning("[ZOSkybox] SkyboxTintColor '{Raw}' is invalid; expected 'R G B' or 'R G B A'. Using white.", raw);
            return new NativeColor(255, 255, 255, 255);
        }

        bool ok = byte.TryParse(parts[0], out byte r)
               & byte.TryParse(parts[1], out byte g)
               & byte.TryParse(parts[2], out byte b);
        byte a = 255;
        if (parts.Length >= 4) ok &= byte.TryParse(parts[3], out a);

        if (!ok)
            logger.LogWarning("[ZOSkybox] SkyboxTintColor '{Raw}' contains non-byte values; partial parse applied.", raw);

        return new NativeColor(r, g, b, a);
    }

    private unsafe void ApplyMaterial(CEnvSky sky, nint materialPtr)
    {
        try
        {
            ref var mat = ref sky.SkyMaterial;
            *(nint*)Unsafe.AsPointer(ref mat) = materialPtr;
            sky.SkyMaterialUpdated();

            ref var matLo = ref sky.SkyMaterialLightingOnly;
            *(nint*)Unsafe.AsPointer(ref matLo) = materialPtr;
            sky.SkyMaterialLightingOnlyUpdated();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ZOSkybox] Failed to write material pointer.");
        }
    }

    private unsafe nint FindMaterialByPath(string material)
    {
        try
        {
            if (material.EndsWith("_c", StringComparison.OrdinalIgnoreCase))
                material = material[..^2];

            // Cache the material-system pointer; retry each map load if it
            // failed previously (reset in OnMapLoad resets _matSysInitialized).
            if (!_matSysInitialized)
            {
                _matSysInitialized = true;
                var ptr = _core.Memory.GetInterfaceByName("VMaterialSystem2_001");
                if (ptr == null)
                {
                    _logger.LogWarning("[ZOSkybox] VMaterialSystem2_001 interface not found.");
                    return 0;
                }
                _matSysPtr = ptr.Value;
            }

            if (_matSysPtr == 0) return 0; // interface lookup failed earlier

            nint vtable  = *(nint*)_matSysPtr;
            nint funcPtr = *(nint*)(vtable + MaterialFuncVtableIndex * IntPtr.Size);
            var  fn      = Marshal.GetDelegateForFunctionPointer<FindOrCreateMaterialDelegate>(funcPtr);

            nint outMaterial  = 0;
            nint pOutMaterial = (nint)(&outMaterial);
            nint result;

            // The function uses different argument order per platform:
            //   Linux (System V ABI):   FindOrCreate(pOut, 0 /*unused*/, name)
            //   Windows (thiscall/x64): FindOrCreate(pThis, pOut, name)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                result = fn(_matSysPtr, pOutMaterial, material);
            else
                result = fn(pOutMaterial, 0, material);

            if (result == 0) return 0;
            return *(nint*)result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ZOSkybox] FindMaterialByPath failed for '{Mat}'.", material);
            return 0;
        }
    }
}
