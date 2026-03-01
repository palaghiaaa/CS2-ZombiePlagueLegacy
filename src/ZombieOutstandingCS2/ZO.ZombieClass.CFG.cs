using static ZombieOutstandingCS2.ZOZombieClassCFG;

namespace ZombieOutstandingCS2;

public class ZOZombieClassCFG
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

    // 丧尸类定义（组合）
    public class ZombieClass
    {
        public string Name { get; set; } = string.Empty;
        public bool Enable { get; set; } = true;
        public string PrecacheSoundEvent { get; set; } = string.Empty;

        public ZombieStats Stats { get; set; } = new();
        public ZombieModels Models { get; set; } = new();
        public ZombieSounds Sounds { get; set; } = new();
    }

}


