/*
	Zombie OutStanding v3.3
	SemiClip Plugin (E)
	(C) LondoN eXtream
*/

#include < amxmodx >
#include < amxmisc >
#include < fakemeta >
#include < engine >

new SemiClip [ 33 ] [ 33 ];
new bool: HasSemi [ 33 ];
new TaskID;
new MaxPlayers;

public plugin_init ( ) {
	register_plugin ( "SemiClip", "1.0", "LondoN eXtream" );
	
	register_forward ( FM_PlayerPreThink, "PreThink" );
	register_forward ( FM_PlayerPostThink, "PostThink" );
	register_forward ( FM_Think, "Think" );
	
	MaxPlayers = get_maxplayers ( );
	
	new ent = engfunc ( EngFunc_CreateNamedEntity, engfunc ( EngFunc_AllocString, "info_target" ) );
	set_pev ( ent, pev_classname, "task_semiclip" );
	set_pev ( ent, pev_nextthink, get_gametime ( ) + 1.01 );
	TaskID = ent;
}

public PreThink ( id ) {
	if ( is_user_alive ( id ) ) {
		for ( new i = 1; i <= MaxPlayers; i++ ) {
			if ( pev ( i, pev_solid ) == SOLID_SLIDEBOX && SemiClip [ id ] [ i ] && id != i && get_user_button ( id ) & IN_USE ) {
				set_pev ( i, pev_solid, SOLID_NOT );
				HasSemi [ i ] = true;
			}
		} 
	}
	
	return FMRES_IGNORED;
}

public PostThink ( id ) {
	if ( is_user_alive ( id ) ) {
		for ( new i = 1; i <= MaxPlayers; i++ ) {
			if ( HasSemi [ i ] ) {
				set_pev ( i, pev_solid, SOLID_SLIDEBOX );
				HasSemi [ i ] = false;
			}
		}
	}
	
	return FMRES_IGNORED;
}

public Think ( ent ) {
	static i, j;
	static Team [ 33 ];
	static Float: Origin [ 33 ] [ 3 ];
	
	if ( ent == TaskID ) {
		for ( i = 1; i <= MaxPlayers; i++ ) {
			if ( is_user_alive ( i ) ) {
				pev ( i, pev_origin, Origin [ i ] )
				Team [ i ] = get_user_team ( i );
				
				for ( j = 1; j <= MaxPlayers; j++ ) {
					if ( Team [ i ] != Team [ j ] ) {
						SemiClip [ i ] [ j ] = false;
						SemiClip [ j ] [ i ] = false;
					}
					
					else if ( floatabs ( Origin [ i ] [ 0 ] - Origin [ j ] [ 0 ] ) < 120.0 && floatabs ( Origin [ i ] [ 1 ] - Origin [ j ] [ 1 ] ) < 120.0 && floatabs ( Origin [ i ] [ 2 ] - Origin [ j ] [ 2 ] ) < ( 120.0 * 2 ) ) {
						SemiClip [ i ] [ j ] = true;
						SemiClip [ j ] [ i ] = true;
					}
					
					else {
						SemiClip [ i ] [ j ] = false;
						SemiClip [ j ] [ i ] = false;
					}
					
				}
			}
		}
		
		set_pev ( ent, pev_nextthink, get_gametime ( ) + 0.2 );
	}
}