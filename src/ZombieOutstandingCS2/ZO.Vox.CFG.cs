namespace ZombieOutstandingCS2;

public class ZOVoxCFG
{
    public class RoundVox
    {
        public string Name { get; set; } = string.Empty;
        public bool Enable { get; set; } = true;
        public float Volume { get; set; } = 1.0f;
        public string RoundMusicVox { get; set; } = string.Empty;
        public string SecRemainVox { get; set; } = string.Empty;
        public string CoundDownVox { get; set; } = string.Empty;
        public string ZombieSpawnVox { get; set; } = string.Empty;
        public string NormalInfectionVox { get; set; } = string.Empty;
        public string MultiInfectionVox { get; set; } = string.Empty;
        public string NemesisVox { get; set; } = string.Empty;
        public string SurvivorVox { get; set; } = string.Empty;
        public string SwarmVox { get; set; } = string.Empty;
        public string PlagueVox { get; set; } = string.Empty;
        public string AssassinVox { get; set; } = string.Empty;
        public string SniperVox { get; set; } = string.Empty;
        public string AVSVox { get; set; } = string.Empty;
        public string HeroVox { get; set; } = string.Empty;
        public string HumanWinVox { get; set; } = string.Empty;
        public string ZombieWinVox { get; set; } = string.Empty;
        public string PrecacheSoundEvent { get; set; } = string.Empty;
    }
    public List<RoundVox> VoxList { get; set; } = new List<RoundVox>();

}