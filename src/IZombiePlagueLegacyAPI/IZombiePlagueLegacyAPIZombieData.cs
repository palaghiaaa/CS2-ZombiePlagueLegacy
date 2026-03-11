namespace ZombiePlagueLegacyCS2;

/// <summary>
/// 丧尸职业的核心属性快照（从配置中读取的原始值快照）。
/// Snapshot of core properties for a zombie class/type (original values read from configuration).
/// </summary>
public class ZombiePropertySnapshot
{
    /// <summary>
    /// 丧尸职业名称（内部唯一标识）。
    /// Zombie class name (internal unique identifier).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 普通状态下的最大血量。
    /// Maximum health in normal zombie state.
    /// </summary>
    public int Health { get; set; }

    /// <summary>
    /// 作为母体丧尸（Mother Zombie）时的最大血量。
    /// Maximum health when this zombie is selected as Mother Zombie.
    /// </summary>
    public int MotherHealth { get; set; }

    /// <summary>
    /// 移动速度（通常为 1.0f 为默认人类速度，丧尸可能更高）。
    /// Movement speed (1.0f is typically default human speed; zombies may have higher values).
    /// </summary>
    public float Speed { get; set; }

    /// <summary>
    /// 伤害加成。
    /// Damage multiplier.
    /// </summary>
    public float Damage { get; set; }

    /// <summary>
    /// 重力缩放值（影响跳跃高度、掉落速度等，越小跳得越高）。
    /// Gravity scale .
    /// </summary>
    public float Gravity { get; set; }

    /// <summary>
    /// 是否启用自动回血功能。
    /// Whether automatic health regeneration is enabled.
    /// </summary>
    public bool EnableRegen { get; set; }

    /// <summary>
    /// 自动回血的间隔时间（秒）。
    /// Interval time between each health regeneration tick (in seconds).
    /// </summary>
    public float HpRegenSec { get; set; }

    /// <summary>
    /// 每次自动回血恢复的血量值。
    /// Amount of health restored per regeneration tick.
    /// </summary>
    public int HpRegenHp { get; set; }

    /// <summary>
    /// 该丧尸职业使用的模型路径（游戏中显示的外观模型）。
    /// Model path used for this zombie class (the visual model in-game).
    /// </summary>
    public string ModelPath { get; set; } = string.Empty;
}