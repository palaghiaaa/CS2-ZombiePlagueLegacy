#include amxmodx
#include hamsandwich
#include fakemeta_util
#include engine
#include zombieplague
//#include colorchat

native zp_get_user_frozen(id)
native zp_set_user_frozen(id, set)

new cvar_distance, cvar_freeze_time, cvar_cooldown, cvar_max_frost_nemesis
new bool:g_has_frost[33], g_frost_left[33], Float:g_last_use[33]

public plugin_init() 
{
	RegisterHam(Ham_Killed, "player", "fw_PlayerKilled") 
        register_event("HLTV", "event_round_start", "a", "1=0", "2=0")
        register_forward(FM_CmdStart, "fw_Start") 

        cvar_max_frost_nemesis = register_cvar("frost_ball_nemesis", "35")
	cvar_distance = register_cvar("frost_distance", "650")
	cvar_freeze_time = register_cvar("freeze_time", "3.0")
	cvar_cooldown = register_cvar( "frost_cooldown", "5.0")
	
}

public plugin_natives()	register_native("zp_get_user_frost_balls", "_get_user_frost_balls", 1);
public _get_user_frost_balls(id) {
	return g_frost_left[id];
}
public event_round_start(player) 
{ 
	arrayset(g_has_frost, false, 33)
}
public client_disconnect(player) 
{
	g_has_frost[player] = false
}
public fw_PlayerKilled(victim, attacker, shouldgib)
{
	g_has_frost[victim] = false
}
        
public zp_user_infected_post(player, infector) 
{	
        if(zp_get_user_nemesis(player))
	{
            g_has_frost[player] = true 
	    g_frost_left[player] = get_pcvar_num(cvar_max_frost_nemesis)
	    zp_colored_print(player, "^x04[Zombie OutStanding]^x01 HIT a player then press^x03 R^x01 to freeze them!");
          //  ColorChat(player, NORMAL, "^x04[Zombie OutStanding]^x01 You has frost ball! for freeze press R to target!")
        }
} 
public fw_Start(player)
{	
	if (g_has_frost[player] == true && zp_get_user_nemesis(player))
	{
		new button = get_user_button(player)
		new oldbutton = get_user_oldbutton(player)
		if(!(oldbutton & IN_RELOAD) && (button & IN_RELOAD)) 
			use_cmd(player) 
	}return PLUGIN_CONTINUE;
}
public use_cmd(player)
{
	if (g_frost_left[player] <= 0) 
	{
		//ColorChat(player, NORMAL, "^x04[Zombie OutStanding]^x01 You not have amore frost ball!")
		return PLUGIN_HANDLED
	}
	if (get_gametime() - g_last_use[player] < get_pcvar_float(cvar_cooldown))
	{ 
		//ColorChat(player, NORMAL, "^x04[Zombie OutStanding]^x01 You have to wait %..f seconds to freeze again", get_pcvar_float(cvar_cooldown) - (get_gametime() - g_last_use[player]))
		return PLUGIN_HANDLED 
	}

        new target, body
	if (get_user_aiming( player, target, body, get_pcvar_num(cvar_distance)))
	{
	        if(!is_user_alive(target) || !is_user_alive(target))
                       return PLUGIN_HANDLED;
						  
		if (zp_get_user_frozen(target))
		{
			//ColorChat(player, NORMAL, "^x04[Zombie OutStanding]^x01 Can't freeze ^x03(Freezed)^x01 target!")
			return PLUGIN_HANDLED
		}
		if (zp_get_user_nemesis(target) || zp_get_user_zombie(target)) 
		{ 
			//ColorChat(player, NORMAL, "^x04[Zombie OutStanding]^x01 Can't freeze temate!") 
			return PLUGIN_HANDLED 
		}
		if(is_user_alive(target))
		{
		        g_last_use[player] = get_gametime()
			g_frost_left[player]--
			start_frost(target)
			g_has_frost[player] = true 
		}
	}
	return PLUGIN_CONTINUE
}

public start_frost(target) 
{
        if(is_user_alive(target))
	{
	    zp_set_user_frozen(target, 1)
	    set_task(get_pcvar_float(cvar_freeze_time), "unfreeze_target", target)
	}	
}

public unfreeze_target(target) 
{
	if(is_user_alive(target))
	{
	    zp_set_user_frozen(target, 0)
	}
}

zp_colored_print(target, const message[], any:...)
{
	static buffer[512], i, argscount
	argscount = numargs()
	
	// Send to everyone
	if (!target)
	{
		static player
		for (player = 1; player <= get_maxplayers(); player++)
		{
			// Not connected
			if (!!is_user_connected(player))
				continue;
			
			// Remember changed arguments
			static changed[5], changedcount // [5] = max LANG_PLAYER occurencies
			changedcount = 0
			
			// Replace LANG_PLAYER with player id
			for (i = 2; i < argscount; i++)
			{
				if (getarg(i) == LANG_PLAYER)
				{
					setarg(i, 0, player)
					changed[changedcount] = i
					changedcount++
				}
			}
			
			// Format message for player
			vformat(buffer, charsmax(buffer), message, 3)
			//replace_all(buffer, charsmax(buffer), "[ZP]", CHAT_TAG);
			
			// Send it
			message_begin(MSG_ONE_UNRELIABLE, get_user_msgid("SayText"), _, player)
			write_byte(player)
			write_string(buffer)
			message_end()
			
			// Replace back player id's with LANG_PLAYER
			for (i = 0; i < changedcount; i++)
				setarg(changed[i], 0, LANG_PLAYER)
		}
	}
	// Send to specific target
	else
	{
		/*
		// Not needed since you should set the ML argument
		// to the player's id for a targeted print message
		
		// Replace LANG_PLAYER with player id
		for (i = 2; i < argscount; i++)
		{
			if (getarg(i) == LANG_PLAYER)
				setarg(i, 0, target)
		}
		*/
		
		// Format message for player
		vformat(buffer, charsmax(buffer), message, 3)
		//replace_all(buffer, charsmax(buffer), "[ZP]", CHAT_TAG);
		
		// Send it
		message_begin(MSG_ONE, get_user_msgid("SayText"), _, target)
		write_byte(target)
		write_string(buffer)
		message_end()
	}
}