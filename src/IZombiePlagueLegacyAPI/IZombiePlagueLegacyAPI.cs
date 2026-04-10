using SwiftlyS2.Shared.Players;


namespace ZombiePlagueLegacyCS2;

/// <summary>
/// Han Zombie Plague API.
/// Han 僵尸瘟疫 API.
/// </summary>
public interface IZombiePlagueLegacyAPI
{
    /// <summary>
    /// Gets whether the game has started.
    /// 获取游戏是否已经开始。
    /// </summary>
    bool GameStart { get; }

    /// <summary>
    /// Checks whether the specified player is a zombie.
    /// 检查指定玩家是否为丧尸。
    /// </summary>
    bool ZPL_IsZombie(int playerId);

    /// <summary>
    /// Checks whether the specified player is a mother zombie.
    /// 检查指定玩家是否为母体丧尸。
    /// </summary>
    bool ZPL_IsMotherZombie(int playerId);

    /// <summary>
    /// Checks whether the specified player is a Nemesis.
    /// 检查指定玩家是否为复仇之神（Nemesis）。
    /// </summary>
    bool ZPL_IsNemesis(int playerId);

    /// <summary>
    /// Checks whether the specified player is an Assassin zombie.
    /// 检查指定玩家是否为暗杀者丧尸（Assassin）。
    /// </summary>
    bool ZPL_IsAssassin(int playerId);

    /// <summary>
    /// Checks whether the specified player is a Survivor.
    /// 检查指定玩家是否为幸存者（幸存者职业）。
    /// </summary>
    bool ZPL_IsSurvivor(int playerId);

    /// <summary>
    /// Checks whether the specified player is a Sniper.
    /// 检查指定玩家是否为狙击手（狙击手职业）。
    /// </summary>
    bool ZPL_IsSniper(int playerId);

    /// <summary>
    /// Checks whether the specified player is a Hero.
    /// 检查指定玩家是否为英雄（英雄职业）。
    /// </summary>
    bool ZPL_IsHero(int playerId);

    /// <summary>
    /// Checks whether the specified player has purchased and owns the SCBA suit (chemical protective suit).
    /// 检查指定玩家是否已购买并拥有防化服（SCBA Suit）。
    /// </summary>
    bool ZPL_PlayerHaveScbaSuit(int playerId);

    /// <summary>
    /// Checks whether the specified player is currently in God Mode (invincibility) purchased state.
    /// 检查指定玩家是否处于已购买的无敌状态（God Mode）。
    /// </summary>
    bool ZPL_PlayerHaveGodState(int playerId);

    /// <summary>
    /// Checks whether the specified player is currently in Infinite Ammo state purchased.
    /// 检查指定玩家是否处于已购买的无限子弹状态。
    /// </summary>
    bool ZPL_PlayerHaveInfiniteAmmoState(int playerId);

    /// <summary>
    /// Gets the name of the current game mode/round type.
    /// 获取当前回合的模式名称。
    /// 可用于在不同模式下实现自定义逻辑。
    /// </summary>
    string ZPL_GetCurrentModeName();

    /// <summary>
    /// Directly turns the target human player into a zombie (non-infection method).
    /// 将目标人类玩家直接设置为丧尸（非感染方式）。
    /// 可用于实现道具如“T病毒试剂”等非感染转化效果。
    /// </summary>
    void ZPL_SetTargetZombie(IPlayer player);

    /// <summary>
    /// Directly turns the target zombie player back into a human.
    /// 将目标丧尸玩家直接设置回人类。
    /// （直接转换，非通过感染或解药方式）
    /// </summary>
    void ZPL_SetTargetHuman(IPlayer player);

    /// <summary>
    /// Infects the target human player and turns them into a Mother Zombie.
    /// 将目标人类玩家感染并转化为母体丧尸。
    /// 感染逻辑与游戏寻找母体丧尸的规则一致。
    /// </summary>
    void ZPL_InfectMotherZombie(IPlayer player);

    /// <summary>
    /// Infects the target human player and turns them into a regular zombie.
    /// 将目标人类玩家感染并转化为普通丧尸。
    /// 感染逻辑与游戏中玩家被感染的规则一致。
    /// </summary>
    /// <param name="player">The player to infect.</param>
    /// <param name="IgnoreScbaSuit">Whether to ignore the target's SCBA suit (chemical protective suit) protection.</param>
    void ZPL_InfectPlayer(IPlayer player, bool IgnoreScbaSuit);

    /// <summary>
    /// Directly turns the target human player into a Nemesis zombie.
    /// 将目标人类玩家直接设置为复仇之神（Nemesis）。
    /// </summary>
    /// <param name="player">The player to set as Nemesis.</param>
    void ZPL_SetTargetNemesis(IPlayer player);

    /// <summary>
    /// Directly turns the target human player into an Assassin zombie.
    /// 将目标人类玩家直接设置为暗杀者丧尸（Assassin）。
    /// </summary>
    /// <param name="player">The player to set as Assassin.</param>
    void ZPL_SetTargetAssassin(IPlayer player);

    /// <summary>
    /// Administers T-Virus vaccine/serum to the target zombie player, turning them back to human (if possible).
    /// 让目标丧尸玩家服用T病毒血清，将其变回人类（特殊丧尸无法改变）。
    /// </summary>
    /// <param name="player">The zombie player to administer the vaccine to.</param>
    void ZPL_SetTargetTVaccine(IPlayer player);

    /// <summary>
    /// Directly turns the target human player into a Sniper.
    /// 将目标人类玩家直接设置为狙击手（Sniper）。
    /// </summary>
    /// <param name="player">The player to set as Sniper.</param>
    void ZPL_SetTargetSniper(IPlayer player);

    /// <summary>
    /// Directly turns the target human player into a Survivor.
    /// 将目标人类玩家直接设置为幸存者（Survivor）。
    /// </summary>
    /// <param name="player">The player to set as Survivor.</param>
    void ZPL_SetTargetSurvivor(IPlayer player);

    /// <summary>
    /// Directly turns the target human player into a Hero.
    /// 将目标人类玩家直接设置为英雄（Hero）。
    /// </summary>
    /// <param name="player">The player to set as Hero.</param>
    void ZPL_SetTargetHero(IPlayer player);

    /// <summary>
    /// Gives the specified zombie player a T-Virus Grenade.
    /// 给予指定丧尸玩家一枚T病毒炸弹。
    /// </summary>
    /// <param name="player">The zombie player to give the grenade to.</param>
    void ZPL_GiveTVirusGrenade(IPlayer player);

    /// <summary>
    /// Gives the specified human player an SCBA suit (chemical protective suit).
    /// 给予指定人类玩家一件防化服（SCBA Suit）。
    /// </summary>
    /// <param name="player">The human player to give the SCBA suit to.</param>
    void ZPL_GiveScbaSuit(IPlayer player);

    /// <summary>
    /// Grants the specified player God Mode (invincibility) for a custom duration in seconds.
    /// 给予指定玩家无敌状态（God Mode），持续自定义的秒数。
    /// </summary>
    /// <param name="player">The player to grant God Mode to.</param>
    /// <param name="time">Duration of invincibility in seconds (e.g. 30f for 30 seconds).</param>
    void ZPL_GiveGodState(IPlayer player, float time);

    /// <summary>
    /// Grants the specified player Infinite Ammo mode for a custom duration in seconds.
    /// 给予指定玩家无限子弹状态，持续自定义的秒数。
    /// </summary>
    /// <param name="player">The player to grant Infinite Ammo to.</param>
    /// <param name="time">Duration of infinite ammo in seconds.</param>
    void ZPL_GiveInfiniteAmmo(IPlayer player, float time);

    /// <summary>
    /// Adds a specified amount of health to the human player (cannot exceed the max health defined for their class/role in config).
    /// 为指定人类玩家增加指定数值血量（无法超过配置中该职业/角色的最大血量上限）。
    /// </summary>
    /// <param name="player">The human player to add health to.</param>
    /// <param name="valve">The amount of health to add (positive integer).</param>
    void ZPL_HumanAddHealth(IPlayer player, int valve);

    /// <summary>
    /// Gets the internal classname/string identifier of the current zombie class/type for the player.
    /// 获取指定玩家的丧尸职业/类型名称（Classname）。
    /// 可用于制作技能组、判断特定丧尸类型等逻辑。
    /// </summary>
    /// <param name="player">The zombie player whose class name to retrieve.</param>
    /// <returns>The zombie class name as a string.</returns>
    string ZPL_GetZombieClassname(IPlayer player);

    /// <summary>
    /// Gets the maximum health value for the player's current zombie class.
    /// 获取指定玩家的丧尸职业最大血量值。
    /// </summary>
    /// <param name="player">The zombie player to query.</param>
    /// <param name="original">
    /// true  → Returns the max health value from the plugin's configuration.<br/>
    /// false → Returns the current pawn.MaxHealth value (may be modified in-game).
    /// </param>
    /// <returns>The maximum health value for this zombie class/type.</returns>
    int ZPL_GetZombieMaxHealth(IPlayer player, bool original);

    /// <summary>
    /// Immediately checks the round win conditions for both teams.
    /// Calls this method to force-check if any win condition is met (e.g., team member count).
    /// If a winning condition is satisfied, the round will end immediately.
    /// 立即检查当前回合的胜利条件。
    /// 调用后会立刻判断双方阵营人数或其他条件是否满足胜利要求，
    /// 如果满足任一结束条件，则立即结束回合。
    /// </summary>
    void ZPL_CheckRoundWinConditions();

    /// <summary>
    /// Forces the human team to win the current round immediately.
    /// 强制让人类阵营立即获胜（结束当前回合）。
    /// </summary>
    void ZPL_SetHumanWin();

    /// <summary>
    /// Forces the zombie team to win the current round immediately.
    /// 强制让丧尸阵营立即获胜（结束当前回合）。
    /// </summary>
    void ZPL_SetZombieWin();

    /// <summary>
    /// Sets a glowing outline effect on the specified player (visible through walls).
    /// The glow is automatically removed when the player dies.
    /// Uses RGBA color values (0–255 for each channel).
    /// 为指定玩家设置外发光效果（可透视墙体）。
    /// 玩家死亡后发光效果会自动移除。
    /// 使用 RGBA 值（每个通道 0–255）。
    /// </summary>
    /// <param name="player">The player to apply the glow effect to.</param>
    /// <param name="R">Red channel value (0–255).</param>
    /// <param name="G">Green channel value (0–255).</param>
    /// <param name="B">Blue channel value (0–255).</param>
    /// <param name="A">Alpha (opacity) value (0–255, 0 = fully transparent, 255 = fully opaque).</param>
    void ZPL_SetPlayerGlow(IPlayer player, int R, int G, int B, int A);

    /// <summary>
    /// Removes the glowing outline effect from the specified player (if any exists).
    /// 删除指定玩家的外发光效果（如果存在）。
    /// </summary>
    /// <param name="player">The player whose glow effect should be removed.</param>
    void ZPL_RemovePlayerGlow(IPlayer player);

    /// <summary>
    /// Sets a custom Field of View (FOV) for the specified player.
    /// The FOV will automatically reset to 90 when the player dies.
    /// 为指定玩家设置自定义视野范围（FOV）。
    /// 玩家死亡后会自动恢复为默认值 90。
    /// </summary>
    /// <param name="player">The player to change FOV for.</param>
    /// <param name="fov">The desired FOV value (typically between 70–120, depending on game limits).</param>
    void ZPL_SetPlayerFov(IPlayer player, int fov);

    /// <summary>
    /// Gives the specified player a fire/incendiary grenade (burning damage over time).
    /// 给予指定玩家一枚燃烧/高爆手雷（造成持续燃烧伤害）。
    /// </summary>
    /// <param name="player">The player to receive the fire grenade.</param>
    void ZPL_GiveFireGrenade(IPlayer player);

    /// <summary>
    /// Gives the specified player a flashbang / illumination grenade (light effect).
    /// 给予指定玩家一枚照明弹（照明效果）。
    /// </summary>
    /// <param name="player">The player to receive the light grenade.</param>
    void ZPL_GiveLightGrenade(IPlayer player);

    /// <summary>
    /// Gives the specified player a Freeze Grenade (freezes targets on explosion).
    /// 给予指定玩家一枚冰冻弹（Freeze Grenade，爆炸后可冻结目标）。
    /// </summary>
    /// <param name="player">The player to receive the freeze grenade.</param>
    void ZPL_GiveFreezeGrenade(IPlayer player);

    /// <summary>
    /// Gives the specified player a Teleport Grenade (allows teleportation on explosion).
    /// 给予指定玩家一枚传送手雷（Teleport Grenade，爆炸后可实现传送效果）。
    /// </summary>
    /// <param name="player">The player to receive the teleport grenade.</param>
    void ZPL_GiveTeleportGrenade(IPlayer player);

    /// <summary>
    /// Gives the specified player an Incendiary Grenade (causes burning/fire damage over time).
    /// 给予指定玩家一枚火焰弹/燃烧弹（Incendiary Grenade，造成持续火焰/燃烧伤害）。
    /// </summary>
    /// <param name="player">The player to receive the incendiary grenade.</param>
    void ZPL_GiveIncGrenade(IPlayer player);

    /// <summary>
    /// Externally sets the player's preferred zombie class (for database persistence).
    /// Used by external systems/plugins to save player preferences to their own database,
    /// then sync the value to the plugin's internal dictionary.
    /// If className is null or empty string, it means "random" preference.
    /// 外部设置玩家的丧尸职业偏好（用于数据库持久化保存）。
    /// 供外部插件/系统调用，将玩家偏好保存到自己的数据库后，
    /// 再同步到本插件内部的字典中。
    /// 如果 className 为 null 或空字符串，则代表“随机”偏好。
    /// </summary>
    /// <param name="steamId">The player's SteamID64.</param>
    /// <param name="className">The preferred zombie class name, or null/empty for random.</param>
    void ZPL_SetExternalPreference(ulong steamId, string? className);

    /// <summary>
    /// Retrieves the player's pre-set zombie class name based on SteamID (for external plugins).
    /// 根据 SteamID 获取玩家预设的丧尸职业名称（供外部插件调用）。
    /// </summary>
    /// <param name="steamId">The player's SteamID64.</param>
    /// <returns>
    /// The zombie class name the player has set as preference;
    /// returns null if the player has set it to random or has no record.
    /// 返回玩家预设的丧尸职业名称；若设为随机或无记录，则返回 null。
    /// </returns>
    string? ZPL_GetZombieNameBySteamid(ulong steamId);

    /// <summary>
    /// Gets a snapshot of all core properties for the specified zombie class/type by name.
    /// 根据丧尸职业名称获取该职业的所有核心属性快照（当前配置值）。
    /// </summary>
    /// <param name="zombieName">The internal name of the zombie class/type.</param>
    /// <returns>
    /// A ZombiePropertySnapshot containing the core properties, or null if the class does not exist.
    /// 返回包含核心属性的快照对象；若该职业名称不存在，则返回 null。
    /// </returns>
    ZombiePropertySnapshot? ZPL_GetZombieProperties(string zombieName);

    /// <summary>
    /// Event triggered when the game round officially starts or ends.
    /// 游戏回合正式开始或结束时触发的事件。
    /// </summary>
    /// <remarks>
    /// The bool parameter indicates the game start state:<br/>
    /// true  → Game/round has started<br/>
    /// false → Game/round has ended
    /// </remarks>
    event Action<bool>? ZPL_OnGameStart;

    /// <summary>
    /// 广播玩家被感染事件（玩家转化为丧尸时触发）。<br/>
    /// Broadcasts the player infection event (triggered when a player turns into a zombie).<br/>
    /// <br/>
    /// 参数顺序 / Parameter order:<br/>
    /// 1. IPlayer? 感染者（attacker，可能为 null，如果没有明确感染来源）<br/>
    ///    IPlayer? Infector (attacker, may be null if no clear infection source)<br/>
    /// 2. IPlayer 被感染者（victim，被转化为丧尸的玩家）<br/>
    ///    IPlayer Infected player (victim, the player becoming a zombie)<br/>
    /// 3. bool 是否由手雷/爆炸物感染（true = 手雷感染，false = 普通感染）<br/>
    ///    bool Whether caused by grenade/explosive (true = grenade infection, false = normal infection)<br/>
    /// 4. string 被感染后转化为的丧尸职业名称（zombie class name）<br/>
    ///    string Zombie class name the infected player is turned into (zombie class name)
    /// </summary>
    event Action<IPlayer, IPlayer, bool, string>? ZPL_OnPlayerInfect;

    /// <summary>
    /// 广播母体丧尸被选择事件（回合开始时母体丧尸确定）。<br/>
    /// Broadcasts the Mother Zombie selection event (when Mother Zombie is chosen at round start).<br/>
    /// <br/>
    /// 参数 / Parameter:<br/>
    /// IPlayer 母体丧尸玩家（被选为母体丧尸的玩家对象）<br/>
    /// IPlayer Mother Zombie player (the player selected as Mother Zombie)
    /// </summary>
    event Action<IPlayer>? ZPL_OnMotherZombieSelected;

    /// <summary>
    /// 广播复仇之神被选择事件（回合开始时 Nemesis 确定）。<br/>
    /// Broadcasts the Nemesis selection event (when Nemesis is chosen at round start).<br/>
    /// <br/>
    /// 参数 / Parameter:<br/>
    /// IPlayer 复仇之神玩家（被选为 Nemesis 的玩家对象）<br/>
    /// IPlayer Nemesis player (the player selected as Nemesis)
    /// </summary>
    event Action<IPlayer>? ZPL_OnNemesisSelected;

    /// <summary>
    /// 广播暗杀者被选择事件（回合开始时 Assassin 确定）。<br/>
    /// Broadcasts the Assassin selection event (when Assassin is chosen at round start).<br/>
    /// <br/>
    /// 参数 / Parameter:<br/>
    /// IPlayer 暗杀者玩家（被选为 Assassin 的玩家对象）<br/>
    /// IPlayer Assassin player (the player selected as Assassin)
    /// </summary>
    event Action<IPlayer>? ZPL_OnAssassinSelected;

    /// <summary>
    /// 广播英雄被选择事件（回合开始时 Hero 确定）。<br/>
    /// Broadcasts the Hero selection event (when Hero is chosen at round start).<br/>
    /// <br/>
    /// 参数 / Parameter:<br/>
    /// IPlayer 英雄玩家（被选为 Hero 的玩家对象）<br/>
    /// IPlayer Hero player (the player selected as Hero)
    /// </summary>
    event Action<IPlayer>? ZPL_OnHeroSelected;

    /// <summary>
    /// 广播幸存者被选择事件（回合开始时 Survivor 确定）。<br/>
    /// Broadcasts the Survivor selection event (when Survivor is chosen at round start).<br/>
    /// <br/>
    /// 参数 / Parameter:<br/>
    /// IPlayer 幸存者玩家（被选为 Survivor 的玩家对象）<br/>
    /// IPlayer Survivor player (the player selected as Survivor)
    /// </summary>
    event Action<IPlayer>? ZPL_OnSurvivorSelected;

    /// <summary>
    /// 广播狙击手被选择事件（回合开始时 Sniper 确定）。<br/>
    /// Broadcasts the Sniper selection event (when Sniper is chosen at round start).<br/>
    /// <br/>
    /// 参数 / Parameter:<br/>
    /// IPlayer 狙击手玩家（被选为 Sniper 的玩家对象）<br/>
    /// IPlayer Sniper player (the player selected as Sniper)
    /// </summary>
    event Action<IPlayer>? ZPL_OnSniperSelected;

    /// <summary>
    /// 广播人类阵营胜利事件（回合结束时宣布胜利阵营）。<br/>
    /// Broadcasts the human team victory event (triggered at round end when declaring the winner).<br/>
    /// <br/>
    /// 参数 / Parameter:<br/>
    /// bool true = 人类胜利 / Humans win<br/>
    ///     false = 丧尸胜利 / Zombies win
    /// </summary>
    event Action<bool>? ZPL_OnHumanWin;

    /// <summary>
    /// 广播当前游戏模式/回合类型被选择事件。<br/>
    /// Broadcasts the game mode / round type selection event.<br/>
    /// <br/>
    /// 参数 / Parameter:<br/>
    /// string 配置中的模式名称（当前选中的游戏模式名称，可用于自定义模式逻辑）<br/>
    /// string Game mode name from configuration (the selected mode name, can be used for custom mode logic)
    /// </summary>
    event Action<string>? ZPL_OnGameModeSelect;

    /// <summary>
    /// 当玩家通过菜单更改丧尸职业偏好后广播的事件。<br/>
    /// Broadcasts the event when a player changes their zombie class preference via menu.<br/>
    /// 外部插件可监听此事件来将玩家的偏好保存到数据库。<br/>
    /// External plugins can listen to this event to save the player's preference to database.<br/>
    /// <br/>
    /// 参数 / Parameters:<br/>
    /// 1. ulong steamid - 更改偏好的玩家的 SteamID64<br/>
    ///    ulong steamid - SteamID64 of the player who changed preference<br/>
    /// 2. string? 丧尸名字 - 新选择的丧尸职业名称；null 或空字符串表示“随机”<br/>
    ///    string? Zombie class name - New preferred zombie class name; null or empty string means "random"
    /// </summary>
    event Action<ulong, string?>? ZPL_OnPreferenceChanged;

    // ── Ammo Packs helpers (safe, cache-backed, no Economy DB calls) ──────────

    /// <summary>
    /// Returns the cached Ammo Pack balance for the given player slot.<br/>
    /// Safe to call at any time – reads from the local cache, never hits the Economy DB.
    /// </summary>
    int ZPL_GetAmmoPacks(int playerId);

    /// <summary>
    /// Adds <paramref name="amount"/> Ammo Packs to the player's balance.<br/>
    /// Uses the internal cache; never calls Economy.SaveData, avoiding the Dommel SIGSEGV.
    /// </summary>
    void ZPL_AddAmmoPacks(int playerId, int amount);

    /// <summary>
    /// Deducts <paramref name="cost"/> Ammo Packs from the player's balance.<br/>
    /// Returns <c>true</c> on success, <c>false</c> if the player has insufficient funds
    /// or the Economy service is unavailable.
    /// </summary>
    bool ZPL_SpendAmmoPacks(int playerId, int cost);
}