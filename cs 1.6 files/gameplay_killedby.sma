#include <amxmodx>
#include <amxmisc>
#include <hamsandwich>
#include <dhudmessage>

// #define PLUGIN "Killed by ca in CSGO"

public plugin_init()	RegisterHam(Ham_Killed, "player", "fw_PlayerKilled_Post", 1);
public fw_PlayerKilled_Post(id, killer) {
	if(is_user_connected(id) && is_user_connected(killer) && id != killer) {
		new h_Name[32];
		get_user_name(killer, h_Name, charsmax(h_Name));

		set_dhudmessage(250, 250, 250, 0.10, 0.50, 0, 3.0, 2.4, 0.1, 0.5, false)
		show_dhudmessage(id, "%s", h_Name)
		
		set_dhudmessage(250, 0, 0, 0.10, 0.54, 0, 3.0, 2.4, 0.1, 0.5, false)
		show_dhudmessage(id, "Killed you")
	}
}