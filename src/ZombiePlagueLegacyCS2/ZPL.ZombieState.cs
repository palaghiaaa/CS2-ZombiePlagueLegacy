using SwiftlyS2.Shared.Players;
using static ZombiePlagueLegacyCS2.ZPLSpecialClassCFG;
using static ZombiePlagueLegacyCS2.ZPLZombieClassCFG;

namespace ZombiePlagueLegacyCS2;

public class PlayerZombieState
{
    public Dictionary<int, string> PlayerZombieClass { get; set; } = new(); // <PlayerSlot, ZombieClassName>

    public Dictionary<int, ZombiePreferenceConfig> PlayerPreferences { get; set; } = new();

    public Dictionary<ulong, string> ExternalPreferences { get; set; } = new();

    public ZombieClass? PickRandomZombieClass(List<ZombieClass> zombieClasses)
    {
        if (zombieClasses == null) return null;

        var enabled = zombieClasses.Where(z => z.Enable).ToList();
        if (enabled.Count == 0) return null;

        return enabled[Random.Shared.Next(enabled.Count)];
    }
    public void SetPlayerZombieClass(int slot, string className)
    {
        PlayerZombieClass[slot] = className;
    }

    public string? GetPlayerZombieClass(int slot)
    {
        return PlayerZombieClass.TryGetValue(slot, out var className) ? className : null;
    }

    public void ClearSpecialAndSetPlayerZombie(IPlayer player, List<ZombieClass> classList, List<SpecialZombieClass> specialClassList)
    {
        if(!player.IsValid)
            return;

        var slot = player.PlayerID;
        var steamId = player.SteamID;
        // 1. 获取玩家当前僵尸类名
        var currentClassName = GetPlayerZombieClass(slot);
        if (string.IsNullOrEmpty(currentClassName))
        {
            // 没有类名记录，直接随机选择普通僵尸
            var randomClass = PickRandomZombieClass(classList);
            if (randomClass != null)
            {
                SetPlayerZombieClass(slot, randomClass.Name);
            }
            return;
        }

        // 2. 检查是否在普通列表中找到
        var inClassList = classList.FirstOrDefault(c => c.Name == currentClassName && c.Enable);
        if (inClassList != null)
        {
            // 在普通列表中，继续沿用
            return;
        }

        // 3. 检查是否在特殊列表中找到
        var inSpecialList = specialClassList.FirstOrDefault(c => c.Name == currentClassName && c.Enable);
        if (inSpecialList != null)
        {
            // 是特殊僵尸，需要重新分配普通僵尸

            // 4. 检查玩家是否有偏好
            var preference = GetPlayerPreference(slot, steamId);
            if (preference != null && preference.Preference == ZombiePreference.Fixed)
            {
                // 有偏好，设置为偏好
                var preferredClass = classList.FirstOrDefault(c => c.Name == preference.FixedZombieName && c.Enable);
                if (preferredClass != null)
                {
                    SetPlayerZombieClass(slot, preferredClass.Name);
                    return;
                }
            }

            // 5. 没有偏好或偏好无效，随机选择普通僵尸
            var randomClass = PickRandomZombieClass(classList);
            if (randomClass != null)
            {
                SetPlayerZombieClass(slot, randomClass.Name);
            }
        }
        else
        {
            // 既不在普通列表也不在特殊列表，随机选择普通僵尸
            var randomClass = PickRandomZombieClass(classList);
            if (randomClass != null)
            {
                SetPlayerZombieClass(slot, randomClass.Name);
            }
        }
    }

    public ZombieClass? GetZombieClass(int slot, List<ZombieClass> classList, List<SpecialZombieClass>? specialClassList = null)
    {
        var className = GetPlayerZombieClass(slot);
        if (string.IsNullOrEmpty(className))
            return null;

        // 先从普通列表查找
        var zombieClass = classList.FirstOrDefault(c => c.Name == className);
        if (zombieClass != null)
            return zombieClass;

        // 再从特殊列表查找并转换
        if (specialClassList != null)
        {
            var specialClass = specialClassList.FirstOrDefault(c => c.Name == className);
            if (specialClass != null)
            {
                return ConvertSpecialToZombieClass(specialClass);
            }
        }

        return null;
    }

    public ZombieClass ConvertSpecialToZombieClass(SpecialZombieClass specialClass)
    {
        return new ZombieClass
        {
            Name = specialClass.Name,
            Enable = specialClass.Enable,
            PrecacheSoundEvent = specialClass.PrecacheSoundEvent,
            Stats = new ZPLZombieClassCFG.ZombieStats
            {
                Health = specialClass.Stats.Health,
                MotherZombieHealth = specialClass.Stats.MotherZombieHealth,
                Speed = specialClass.Stats.Speed,
                Damage = specialClass.Stats.Damage,
                Gravity = specialClass.Stats.Gravity,
                EnableRegen = specialClass.Stats.EnableRegen,
                HpRegenSec = specialClass.Stats.HpRegenSec,
                HpRegenHp = specialClass.Stats.HpRegenHp,
                ZombieSoundVolume = specialClass.Stats.ZombieSoundVolume,
                IdleInterval = specialClass.Stats.IdleInterval
            },
            Models = new ZPLZombieClassCFG.ZombieModels
            {
                ModelPath = specialClass.Models.ModelPath,
                CustomKinfeModelPath = specialClass.Models.CustomKinfeModelPath
            },
            Sounds = new ZPLZombieClassCFG.ZombieSounds
            {
                SoundInfect = specialClass.Sounds.SoundInfect,
                SoundPain = specialClass.Sounds.SoundPain,
                SoundHurt = specialClass.Sounds.SoundHurt,
                SoundDeath = specialClass.Sounds.SoundDeath,
                IdleSound = specialClass.Sounds.IdleSound,
                RegenSound = specialClass.Sounds.RegenSound,
                BurnSound = specialClass.Sounds.BurnSound,
                ExplodeSound = specialClass.Sounds.ExplodeSound,
                HitSound = specialClass.Sounds.HitSound,
                HitWallSound = specialClass.Sounds.HitWallSound,
                SwingSound = specialClass.Sounds.SwingSound
            }
        };
    }

    // 添加偏好设置方法
    public void SetPlayerPreference(int slot, ZombiePreferenceConfig config)
    {
        PlayerPreferences[slot] = config;
    }

    public ZombiePreferenceConfig? GetPlayerPreference(int slot, ulong steamId = 0)
    {

        if (steamId > 0 && ExternalPreferences.TryGetValue(steamId, out var className))
        {
            return new ZombiePreferenceConfig
            {
                Preference = ZombiePreference.Fixed,
                FixedZombieName = className
            };
        }

        if (PlayerPreferences.TryGetValue(slot, out var localConfig))
        {
            return localConfig;
        }

        return new ZombiePreferenceConfig { Preference = ZombiePreference.Random };
    }



    public void SetPlayerPreference(ulong steamId, string? className)
    {
        if (string.IsNullOrEmpty(className))
        {
            ExternalPreferences.Remove(steamId);
        }
        else
        {
            ExternalPreferences[steamId] = className;
        }
    }


}
public enum ZombiePreference
{
    Random,
    Fixed
}
public class ZombiePreferenceConfig
{
    public ZombiePreference Preference { get; set; } = ZombiePreference.Random;
    public string FixedZombieName { get; set; } = string.Empty;
}




