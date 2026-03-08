#include <amxmodx>
#include <zombieplague>

new const Name[] = "Classic Zombie";
new const Description[] = "\r(Balanced)";
new const Model[] = "z_out_classic";
new const ClawModel[] = "z_out_classic_claws.mdl";
const Health = 6000;
const Speed = 290;
const Float: Gravity = 0.6;
const Float: Knockback = 1.0;

public plugin_precache()
{
	register_plugin("[ZP] Classic Zombie", "1.0", "LondoN eXtream");
	
	zp_register_zombie_class ( Name, Description, Model, ClawModel, Health, Speed, Gravity, Knockback );
}
