/*	Copyright © 2008, ConnorMcLeod

	No Radio Spam is free software;
	you can redistribute it and/or modify it under the terms of the
	GNU General Public License as published by the Free Software Foundation.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU General Public License for more details.

	You should have received a copy of the GNU General Public License
	along with No Radio Spam; if not, write to the
	Free Software Foundation, Inc., 59 Temple Place - Suite 330,
	Boston, MA 02111-1307, USA.
*/

#include <amxmodx>

#define PLUGIN "No Radio Spam"
#define AUTHOR "ConnorMcLeod"
#define VERSION "0.0.2"

public plugin_init()
{
	register_plugin( PLUGIN, VERSION, AUTHOR )


	new szRadioCommands[][] = 
	{
		"radio1", 
		"coverme", "takepoint", "holdpos", "regroup", "followme", "takingfire",
		"radio2", 
		"go", "fallback", "sticktog", "getinpos", "stormfront", "report",
		"radio3", 
		"roger", "enemyspot", "needbackup", "sectorclear", "inposition", "reportingin", "getout", "negative", "enemydown"
	}

	for(new i; i<sizeof(szRadioCommands); i++)
		register_clcmd(szRadioCommands[i], "ClientCommand_Radio")
}

public ClientCommand_Radio(id)
{
	return PLUGIN_HANDLED;
}