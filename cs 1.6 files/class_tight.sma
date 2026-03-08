/*
Multijump addon by twistedeuphoria
Plagued by Dabbi
Classed by B!gBud

CVARS:
    zp_tight_jump 2 (Default)

*/

#include <amxmodx>
#include <amxmisc>
#include <engine>
#include <fakemeta>
#include <zombieplague>

new jumpznum[33] = 0
new bool:dozjump[33] = false
new cvar_jumps
new g_zclass_tight

// Tight Zombie Atributes
new const zclass_name[] = { "Tight Zombie" } // name
new const zclass_info[] = { "\r(Double Jump)" } // description
new const zclass_model[] = { "z_out_tight" } // model
new const zclass_clawmodel[] = { "z_out_tight_claws.mdl" } // claw model
const zclass_health = 7500 // health
const zclass_speed = 220 // speed
const Float:zclass_gravity = 0.8 // gravity
const Float:zclass_knockback = 1.5 // knockback

public plugin_init()
{
    register_plugin("[ZP] Class Tight", "1.0c", "MultiJump by twistedeuphoria, Plagued by Dabbi, Classed by B!gBud")
    cvar_jumps = register_cvar("zp_tight_jump","2")    
}

public plugin_precache()
{
    g_zclass_tight = zp_register_zombie_class(zclass_name, zclass_info, zclass_model, zclass_clawmodel, zclass_health, zclass_speed, zclass_gravity, zclass_knockback)
}


public client_PreThink(id)
{
    if(!is_user_alive(id) || !zp_get_user_zombie(id)) return PLUGIN_CONTINUE
    if(zp_get_user_zombie_class(id) != g_zclass_tight) return PLUGIN_CONTINUE
    
    new nzbut = get_user_button(id)
    new ozbut = get_user_oldbutton(id)
    if((nzbut & IN_JUMP) && !(get_entity_flags(id) & FL_ONGROUND) && !(ozbut & IN_JUMP))
    {
        if (jumpznum[id] < get_pcvar_num(cvar_jumps))
        {
            dozjump[id] = true
            jumpznum[id]++
            return PLUGIN_CONTINUE
        }
    }
    if((nzbut & IN_JUMP) && (get_entity_flags(id) & FL_ONGROUND))
    {
        jumpznum[id] = 0
        return PLUGIN_CONTINUE
    }    
    return PLUGIN_CONTINUE
}

public client_PostThink(id)
{
    if(!is_user_alive(id) || !zp_get_user_zombie(id)) return PLUGIN_CONTINUE
    if(zp_get_user_zombie_class(id) != g_zclass_tight) return PLUGIN_CONTINUE
    
    if(dozjump[id] == true)
    {
        new Float:vezlocityz[3]    
        entity_get_vector(id,EV_VEC_velocity,vezlocityz)
        vezlocityz[2] = random_float(265.0,285.0)
        entity_set_vector(id,EV_VEC_velocity,vezlocityz)
        dozjump[id] = false
        return PLUGIN_CONTINUE
    }    
    return PLUGIN_CONTINUE
} 