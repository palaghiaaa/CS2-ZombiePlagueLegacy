#include <amxmodx>
#include <engine>

native zp_get_user_zombie(id);

public client_PreThink(id) {
	if(!is_user_alive(id) || zp_get_user_zombie(id))
		return;

	new Float: a = 100.0 * -1.0;
	
	if(get_user_button(id) & IN_USE) {
		new Float: b[3];
		entity_get_vector(id, EV_VEC_velocity, b);
		
		if(b[2] < 0.0) {
			entity_set_int(id, EV_INT_sequence, 3);
			entity_set_int(id, EV_INT_gaitsequence, 1);
			entity_set_float(id, EV_FL_frame, 1.0);
			entity_set_float(id, EV_FL_framerate, 1.0);

			b[2] = (b[2] + 40.0 < a) ? b[2] + 40.0 : a;
			entity_set_vector(id, EV_VEC_velocity, b);
		}

	}
}