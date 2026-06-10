using static ZombiePlagueLegacyCS2.ZPLZombieClassCFG;

namespace ZombiePlagueLegacyCS2;

public class ZPLZombieClassCFG
{
    public List<ZombieClass> ZombieClassList { get; set; } = new List<ZombieClass>();
    public class ZombieStats
    {
        public int Health { get; set; }
        public int MotherZombieHealth { get; set; }
        public float Speed { get; set; }
        public float Damage { get; set; }
        public float Gravity { get; set; }
        public int Fov { get; set; }
        public bool EnableRegen { get; set; }
        public float HpRegenSec { get; set; }
        public int HpRegenHp { get; set; }
        public float ZombieSoundVolume { get; set; } = 1.0f;
        public float IdleInterval { get; set; } = 140.0f;
    }

    // 丧尸外观
    public class ZombieModels
    {
        public string ModelPath { get; set; } = string.Empty;
        public string CustomKinfeModelPath { get; set; } = string.Empty;
    }

    // 丧尸音效
    public class ZombieSounds
    {
        public string SoundInfect { get; set; } = string.Empty;
        public string SoundPain { get; set; } = string.Empty;
        public string SoundHurt { get; set; } = string.Empty;
        public string SoundDeath { get; set; } = string.Empty;
        public string IdleSound { get; set; } = string.Empty;
        public string RegenSound { get; set; } = string.Empty;
        public string BurnSound { get; set; } = string.Empty;
        public string ExplodeSound { get; set; } = string.Empty;
        public string HitSound { get; set; } = string.Empty;
        public string HitWallSound { get; set; } = string.Empty;
        public string SwingSound { get; set; } = string.Empty;
    }

    // ── Per-class special abilities ──────────────────────────────────────────
    public class ZombieAbilities
    {
        /// <summary>HP given to the infector each time they infect a human. 0 = disabled.</summary>
        public int InfectHealAmount { get; set; } = 0;

        /// <summary>Extra mid-air jumps available to this zombie class. 0 = disabled.</summary>
        public int ExtraJumps { get; set; } = 0;

        /// <summary>
        /// Glow color applied to this zombie for GlowDurationSeconds after infecting someone.
        /// Format: "R,G,B,A"  e.g. "255,215,0,180". Empty = no glow-on-infect.
        /// </summary>
        public string InfectGlowColor { get; set; } = string.Empty;

        /// <summary>How many seconds the infect-glow lasts. 0 = permanent until removed.</summary>
        public float GlowDurationSeconds { get; set; } = 3.0f;

        /// <summary>When true, this zombie class moves silently (no audible footsteps).</summary>
        public bool SilentSteps { get; set; } = false;
    }

    // 丧尸类定义（组合）
    public class ZombieClass
    {
        public string Name { get; set; } = string.Empty;
        public bool Enable { get; set; } = true;
        public string PrecacheSoundEvent { get; set; } = string.Empty;

        public ZombieStats Stats { get; set; } = new();
        public ZombieModels Models { get; set; } = new();
        public ZombieSounds Sounds { get; set; } = new();
        public ZombieAbilities Abilities { get; set; } = new();
    }

}


