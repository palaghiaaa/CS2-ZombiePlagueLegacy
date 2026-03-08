#include <amxmodx>
#include <fun>
#include <hamsandwich>

native zp_get_user_zombie(id);
native zp_get_user_nemesis(id);
native zp_get_user_assassin(id);
native zp_get_user_zombie_class(id);
native zp_register_zombie_class(const name[], const info[], const model[], const clawmodel[], hp, speed, Float:gravity, Float:knockback);
forward zp_user_infected_post(id, infector, nemesis);

#define FADE_IN      0x0000
#define FADE_OUT      0x0001
#define FADE_MODULATE   0x0002
#define FADE_STAYOUT   0x0004

new g_zclass_hunter;

new const zclass1_name[] =      {"Hunter"};
new const zclass1_info[] =      {"\r(Silent Killer)"};
new const zclass1_model[] =      {"z_out_hunter"};
new const zclass1_clawmodel[] =   {"z_out_hunter_claws.mdl"};

const zclass1_health =   4700;
const zclass1_speed =   274;

const Float:zclass1_gravity =      0.62;
const Float:zclass1_knockback =   0.84;

public plugin_init()
{
	register_plugin( "[CS16] ZClass Hunter", "1.0", "CS16 Team" );
	RegisterHam( Ham_Killed, "player", "fw_PlayerKilled" );
	g_zclass_hunter = zp_register_zombie_class( zclass1_name, zclass1_info, zclass1_model, zclass1_clawmodel, zclass1_health, zclass1_speed, zclass1_gravity, zclass1_knockback );
}

public fw_PlayerKilled( victim, attacker, shouldgib )
{
	if (is_user_alive(attacker)  &&  zp_get_user_zombie(attacker)  &&  zp_get_user_zombie_class(attacker)  &&  zp_get_user_nemesis(attacker)  &&  zp_get_user_assassin(attacker) == g_zclass_hunter)
	{
		do_screen_fade( attacker, 0.60, 1.15, 255, 10, 10, 94 );
		SetHamParamInteger( 3, 2 );
	}
	return PLUGIN_HANDLED;
}


/* ======================================
   When HUNTER infect other human
====================================== */
public zp_user_infected_post( id, infector )
{
	if( is_user_connected( infector ) && zp_get_user_zombie_class( infector ) == g_zclass_hunter )
	{
		do_screen_fade( infector, 0.60, 1.15, 255, 10, 10, 94 );
		set_user_rendering( infector, kRenderFxGlowShell, 255, 10, 10, kRenderNormal, 0 );
		set_task( 2.9, "TakeGlow", infector );
		set_user_footsteps( infector, 1 );
	}
}


/* ==============================
   Custom screen fade
============================== */
stock do_screen_fade( id, Float:fadeTime, Float:holdTime, red, green, blue, alpha, type = FADE_IN )
{
	static msgScreenFade;
	if( !msgScreenFade ) 
	{ 
		msgScreenFade = get_user_msgid( "ScreenFade" ); 
	}
	
	new fade, hold;
	fade = clamp( floatround( fadeTime * float(1<<12)), 0, 0xFFFF );
	hold = clamp( floatround( holdTime * float(1<<12)), 0, 0xFFFF );
	message_begin( MSG_ONE_UNRELIABLE, msgScreenFade, _, id );
	write_short( fade );
	write_short( hold );
	write_short( type );
	write_byte( red );
	write_byte( green );
	write_byte( blue );
	write_byte( alpha );
	message_end( );
}

public TakeGlow( infector ) 
{ 
	set_user_rendering( infector, kRenderFxNone, 0, 0, 0, kRenderNormal, 0 );
}