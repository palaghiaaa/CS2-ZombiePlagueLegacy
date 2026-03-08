#include <amxmodx>
#include <fakemeta>
#include <fun>
#include <hamsandwich>
#include <zombieplague>

native zp_get_user_assassin(player);


forward zp_user_infected_post(id, infector, nemesis);

#define FADE_IN      0x0000
#define MAX_HEALTH		6300

const TASKID_REG = 1000001

new g_zclass_regen_toggle, g_time, g_amount

new const zclass1_name[] = { "Regenerator" }
new const zclass1_info[] = { "\r(Health Regeneration)" }
new const zclass1_model[] = { "z_out_regenerator" }
new const zclass1_clawmodel[] = { "z_out_raptor_claws.mdl" }
const zclass1_health = 4750
const zclass1_speed = 250
const Float:zclass1_gravity = 1.0
const Float:zclass1_knockback = 0.90

new g_zclass_Regen
new g_MaxPlayers
new g_MsgSync

public plugin_init()
{
	g_zclass_regen_toggle = register_cvar("zp_zclass_regen", "1")

	g_time = register_cvar("zp_regen_time", "5")
	g_amount = register_cvar("zp_regen_amount", "350")

	g_MaxPlayers = get_maxplayers()
	
	g_MsgSync = CreateHudSyncObj()
	
}

public plugin_precache()
{
	register_plugin("[CS16] ZClass Regenerator", "1.0", "CS16 Team")

	g_zclass_Regen = zp_register_zombie_class(zclass1_name, zclass1_info, zclass1_model, zclass1_clawmodel, zclass1_health, zclass1_speed, zclass1_gravity, zclass1_knockback)
}

public zp_round_ended()
{
	for(new id = 1; id <= g_MaxPlayers; id++)
	{
		if(task_exists(id + TASKID_REG)) remove_task(id + TASKID_REG)
	}
}

public zp_user_infected_post(infector)
{
	if (zp_get_user_zombie_class(infector) == g_zclass_Regen)
	{
		set_user_health(infector, get_user_health(infector));
		set_task(get_pcvar_float(g_time), "Regenerate", infector + TASKID_REG, _, _, "b")
		set_user_footsteps(infector, 1);
	}
}

public Regenerate(id)
{

	new player = id - TASKID_REG

	if (!get_pcvar_num(g_zclass_regen_toggle) || !is_user_connected(player) || !is_user_alive(player) || !zp_get_user_zombie(player) || zp_get_user_nemesis(player) || zp_get_user_assassin(player))
	{
		remove_task(player + TASKID_REG)

		return
	}

	if(pev(player, pev_health) <= 10.0)
	{
		remove_task(player + TASKID_REG)

		return
	}

	new ZMaxHealth = MAX_HEALTH

	if(pev(player, pev_health) < ZMaxHealth)
	{
		new RegenHealth = pev(player, pev_health) + get_pcvar_num(g_amount)
		set_pev(player, pev_health, float(min(RegenHealth, ZMaxHealth)))
		set_hudmessage(0, 127, 255, -1.0, 0.1);
		ShowSyncHudMsg(player, g_MsgSync, " [ => REGENERATION <= ]^n[ => +350 HP <= ]");
		do_screen_fade( player, 0.10, 0.20, 0, 255, 0, 94 );
		
		static origin[3]
		get_user_origin(player, origin)
		message_begin(MSG_PVS, SVC_TEMPENTITY, origin)
		write_byte(TE_PARTICLEBURST) // TE id
		write_coord(origin[0]) // x
		write_coord(origin[1]) // y
		write_coord(origin[2]) // z
		write_short(50) // radius
		write_byte(70) // color
		write_byte(3) // duration (will be randomized a bit)
		message_end()
	}
}

stock do_screen_fade(id, Float:fadeTime, Float:holdTime, red, green, blue, alpha, type = FADE_IN)
{
	static msgScreenFade;
	
	if (!msgScreenFade) 
	{ 
		msgScreenFade = get_user_msgid("ScreenFade");
	}
	
	new fade, hold;
	fade = clamp(floatround(fadeTime * float(1<<12)), 0, 0xFFFF);
	hold = clamp(floatround(holdTime * float(1<<12)), 0, 0xFFFF);
	
	message_begin(MSG_ONE_UNRELIABLE, msgScreenFade, _, id);
	write_short(fade);
	write_short(hold);
	write_short(type);
	write_byte(red);
	write_byte(green);
	write_byte(blue);
	write_byte(alpha);
	message_end();
}