namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// What the SSL compiler itself knows without any header: its keywords and the names of its built-in functions, procedures and
	/// constants (<c>display_msg</c>, <c>dude_obj</c>, <c>true</c>, <c>bwand</c>...).
	/// </summary>
	/// <remarks>
	/// The list of names was produced by asking the real compiler (<c>compile.exe</c>, sfall 5.1.3 edition): every word of its string
	/// table that sslc accepts as a name instead of reporting it as unknown. It is a superset of <c>opcodes.txt</c> and
	/// <c>functions.ini</c> (which miss, for example, <c>false</c>, <c>bwand</c> or <c>get_config_setting</c>), so a script that compiles
	/// is never reported as using an unknown name. The names of the description files are added on top of this list at run time
	/// (<see cref="FunctionSignatureCatalog"/>), so a newer sfall function that a user documents in <c>functions.ini</c> is known too.
	/// </remarks>
	public static class ScriptBuiltins
	{
		private static readonly HashSet<String> _keywords = new(StringComparer.OrdinalIgnoreCase)
		{
			"begin", "end", "if", "then", "else", "while", "do", "for", "foreach", "in", "call", "procedure", "variable", "return", "break", "continue",
			"switch", "case", "default", "import", "export", "inline", "pure", "critical", "timed", "conditional", "when", "startcritical", "endcritical",
			"and", "or", "not", "andalso", "orelse", "bwand", "bwor", "bwxor", "bwnot",
		};

		private static readonly HashSet<String> _builtins = CreateBuiltins();

		private static readonly HashSet<String>.AlternateLookup<ReadOnlySpan<Char>> _keywordLookup = _keywords.GetAlternateLookup<ReadOnlySpan<Char>>();
		private static readonly HashSet<String>.AlternateLookup<ReadOnlySpan<Char>> _builtinLookup = _builtins.GetAlternateLookup<ReadOnlySpan<Char>>();

		/// <summary>Gets the number of built-in names.</summary>
		public static Int32 Count => _builtins.Count;

		/// <summary>Gets the built-in names (lower case).</summary>
		public static IEnumerable<String> Names => _builtins;

		/// <summary>Determines whether a word is a keyword of the language or an operator written as a word (<c>and</c>, <c>bwand</c>).</summary>
		/// <param name="word">The word; the case is ignored.</param>
		/// <returns><see langword="true"/> for a keyword.</returns>
		public static Boolean IsKeyword(ReadOnlySpan<Char> word)
		{
			return _keywordLookup.Contains(word);
		}

		/// <summary>Determines whether the compiler knows a name by itself.</summary>
		/// <param name="name">The name; the case is ignored.</param>
		/// <returns><see langword="true"/> for a keyword or a built-in function, procedure or constant.</returns>
		public static Boolean IsBuiltin(ReadOnlySpan<Char> name)
		{
			return _builtinLookup.Contains(name) || _keywordLookup.Contains(name);
		}

		private static HashSet<String> CreateBuiltins()
		{
			HashSet<String> names = new(NameList.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);

			return names;
		}

		private const String NameList =
			"__date__ __file__ __line__ __mcpp __stdc__ __stdc_hosted__ __stdc_version__ __time__ abs action_being_used activate_shader " +
			"activateregion active_hand add_mult_objs_to_inven add_obj_to_inven add_timer_event addbutton addbuttonflag addbuttongfx " +
			"addbuttonproc addbuttonrightproc addbuttontext addkey addnamedevent addnamedhandler addregion addregionflag addregionproc " +
			"addregionrightproc and andalso anim anim_action_frame anim_busy animate_move_obj_to_tile animate_stand_obj " +
			"animate_stand_reverse_obj apply_heaveho_fix arctan array_key array_key_exists arrayexpr art_anim art_exists atof atoi attack " +
			"attack_complex attack_setup available_global_script_types begin block_combat break buttontab bwand bwnot bwor bwxor call " +
			"call_offset_r0 call_offset_r1 call_offset_r2 call_offset_r3 call_offset_r4 call_offset_v0 call_offset_v1 call_offset_v2 " +
			"call_offset_v3 call_offset_v4 callstart cancel cancelall case ceil ceilf charcode checkregion clear_selectable_perks clearnamed " +
			"combat_difficulty combat_is_initialized continue cos create_array create_message_window create_object_sid create_spatial createwin " +
			"critical critter_add_trait critter_attempt_placement critter_dmg critter_heal critter_injure critter_inven_obj critter_is_fleeing " +
			"critter_mod_skill critter_rm_trait critter_set_flee_state critter_state critter_stop_attacking cur_map_index days_since_visited " +
			"deactivate_shader debug_msg default deletebutton deletekey deleteregion deletewin destroy_mult_objs destroy_object detach " +
			"dialogue_reaction dialogue_system_enter difficulty_level disable_aimed_shots display display_msg displaygfx displayraw div do " +
			"do_check drop_obj dude_obj elevation else end end_dialogue endcritical endgame_movie endgame_slideshow exec exit explosion " +
			"exponent export fadein fadeout false fillrect fillwin fillwin3x3 fix_array fixed_param float_msg floor for force_aimed_shots " +
			"force_encounter force_encounter_with_flags force_graphics_refresh foreach fork format free_array free_shader fs_copy fs_create " +
			"fs_delete fs_find fs_pos fs_read_byte fs_read_float fs_read_int fs_read_short fs_resize fs_seek fs_size fs_write_bstring " +
			"fs_write_byte fs_write_float fs_write_int fs_write_short fs_write_string game_loaded game_ticks game_time game_time_advance " +
			"game_time_hour game_time_in_seconds game_ui_disable game_ui_enable game_ui_is_disabled gdialog_get_barter_mod gdialog_mod_barter " +
			"gdialog_set_barter_mod get_array get_attack_type get_available_skill_points get_bodypart_hit_modifier get_config_setting " +
			"get_critical_table get_critter_base_stat get_critter_current_ap get_critter_extra_stat get_critter_skill_points get_critter_stat " +
			"get_day get_game_mode get_ini_setting get_ini_string get_kill_counter get_last_attacker get_last_target get_light_level get_month " +
			"get_mouse_buttons get_mouse_x get_mouse_y get_npc_level get_pc_base_stat get_pc_extra_stat get_pc_stat get_perk_available " +
			"get_perk_freq get_perk_owed get_poison get_proto_data get_screen_height get_screen_width get_script get_sfall_arg get_sfall_args " +
			"get_sfall_global_float get_sfall_global_int get_shader_texture get_shader_version get_tile_fid get_unspent_ap_bonus " +
			"get_unspent_ap_perk_bonus get_uptime get_viewport_x get_viewport_y get_weapon_ammo_count get_weapon_ammo_pid " +
			"get_window_under_mouse get_world_map_x_pos get_world_map_y_pos get_year gfade_in gfade_out giq_option give_exp_points global_var " +
			"gotoxy graphics_funcs_available gsay_end gsay_message gsay_option gsay_reply gsay_start has_fake_perk has_fake_trait has_skill " +
			"has_trait hero_select_win hide_iface_tag hide_real_perks hidemouse how_much if import in in_world_map inc_npc_level include " +
			"init_hook inline input_funcs_available inven_cmds inven_unwield is_critical is_iface_tag_active is_success item_caps_adjust " +
			"item_caps_total jam_lock justifycenter justifyleft justifyright key_pressed kill_critter kill_critter_type len_array list_as_array " +
			"list_begin list_end list_next load_array load_map load_shader loadpalettetable local_var log make_daytime map_var mark_area_known " +
			"mark_movie_played message_str message_str_game metarule metarule2_explosions metarule3 mod_kill_counter mod_skill_points_per_level " +
			"modified_ini mouseheld mouseoff mouseon mousepress mouserelease mouseright mouseshape move_obj_inven_to_obj move_to moviecenter " +
			"moviedirect movieflags moviescale moviesubtitle nb_create_char noop not obj_art_fid obj_being_used_with obj_blocking_line " +
			"obj_blocking_tile obj_can_hear_obj obj_can_see_obj obj_carrying_pid_obj obj_close obj_is_carrying_obj obj_is_carrying_obj_pid " +
			"obj_is_locked obj_is_open obj_item_subtype obj_lock obj_name obj_on_screen obj_open obj_pid obj_set_light_level obj_type " +
			"obj_unlock or orelse override_map_start party_add party_member_list party_member_obj party_remove path_find_to perk_add_mode " +
			"pickup_obj play_gmovie play_sfall_sound play_sfx playmovie playmoviealpha playmoviealpharect playmovierect poison print printrect " +
			"procedure proto_data pure radiation_dec radiation_inc random reaction_influence read_byte read_int read_short read_string " +
			"refresh_pc_art refreshmouse reg_anim_animate reg_anim_animate_and_hide reg_anim_animate_forever reg_anim_animate_reverse " +
			"reg_anim_callback reg_anim_change_fid reg_anim_combat_check reg_anim_destroy reg_anim_func reg_anim_light reg_anim_obj_move_to_obj " +
			"reg_anim_obj_move_to_tile reg_anim_obj_run_to_obj reg_anim_obj_run_to_tile reg_anim_play_sfx reg_anim_take_out " +
			"reg_anim_turn_towards regiontab register_hook register_hook_proc register_hook_proc_spec remove_attacker_knockback remove_script " +
			"remove_target_knockback remove_trait remove_weapon_knockback reset_critical_table resize_array resizewin resume_game return " +
			"rm_mult_objs_from_inven rm_obj_from_inven rm_timer_event roll_dice roll_vs_skill rotation_to_tile round roundf running_burning_guy " +
			"save_array sayborder sayend saygetlastpos saygotoreply saymessage saymessagetimeout sayoption sayoptioncolor sayoptionflags " +
			"sayoptionwindow sayquit sayreply sayreplycolor sayreplyflags sayreplytitle sayreplywindow sayrestart sayscrolldown sayscrollup " +
			"saysetspacing saystart saystartpos scalewin scan_array scr_return script_action script_overrides selectfilelist selectwin self_obj " +
			"set_array set_attacker_knockback set_available_skill_points set_base_hit_chance_mod set_base_pickpocket_mod set_base_skill_mod " +
			"set_bodypart_hit_modifier set_car_current_town set_config_setting set_critical_table set_critter_base_stat " +
			"set_critter_burst_disable set_critter_current_ap set_critter_extra_stat set_critter_hit_chance_mod set_critter_pickpocket_mod " +
			"set_critter_skill_mod set_critter_skill_points set_critter_stat set_df_model set_dm_model set_eax_environment set_exit_grids " +
			"set_fake_perk set_fake_trait set_global_script_repeat set_global_script_type set_global_var set_hero_race set_hero_style " +
			"set_hit_chance_max set_hp_per_level_mod set_inven_ap_cost set_light_level set_local_var set_map_music set_map_start " +
			"set_map_time_multi set_map_var set_movie_path set_npc_stat_max set_npc_stat_min set_obj_visibility set_palette set_pc_base_stat " +
			"set_pc_extra_stat set_pc_stat_max set_pc_stat_min set_perk_agl set_perk_chr set_perk_desc set_perk_end set_perk_freq " +
			"set_perk_image set_perk_int set_perk_lck set_perk_level set_perk_level_mod set_perk_name set_perk_owed set_perk_per set_perk_ranks " +
			"set_perk_skill1 set_perk_skill1_mag set_perk_skill2 set_perk_skill2_mag set_perk_stat set_perk_stat_mag set_perk_str set_perk_type " +
			"set_perkbox_title set_pickpocket_max set_pipboy_available set_proto_data set_pyromaniac_mod set_script set_selectable_perk " +
			"set_self set_sfall_arg set_sfall_global set_sfall_return set_shader_float set_shader_int set_shader_mode set_shader_texture " +
			"set_shader_type set_shader_vector set_skill_max set_stat_max set_stat_min set_swiftlearner_mod set_target_knockback " +
			"set_unspent_ap_bonus set_unspent_ap_perk_bonus set_viewport_x set_viewport_y set_weapon_ammo_count set_weapon_ammo_pid " +
			"set_weapon_knockback set_world_map_pos set_xp_mod setfont setglobalmousefunc sethighlightcolor setoneoptpause settextcolor " +
			"settextflags sfall_func0 sfall_func1 sfall_func2 sfall_func3 sfall_func4 sfall_func5 sfall_func6 sfall_func7 sfall_func8 " +
			"sfall_ver_build sfall_ver_major sfall_ver_minor sfx_build_ambient_name sfx_build_char_name sfx_build_interface_name " +
			"sfx_build_item_name sfx_build_open_name sfx_build_scenery_name sfx_build_weapon_name show_iface_tag show_real_perks showmouse " +
			"showwin signalnamed sin skill_contest sneak_success sound16bit sound8bit sounddelete soundloop soundmemory soundmono soundnormal " +
			"soundpause soundplay soundresume soundrewind soundstereo soundstop soundstreamed source_obj spawn sprintf sqrt start start_gdialog " +
			"startcritical stop_game stop_sfall_sound stopmovie string_split strlen substr switch tan tap_key target_obj temp_array " +
			"terminate_combat textdirect textmonospace textnofill textshadow texttrunc textunderline then tile_contains_obj_pid " +
			"tile_contains_pid_obj tile_distance tile_distance_objs tile_get_objs tile_in_tile_rect tile_is_visible tile_light tile_num " +
			"tile_num_in_direction tile_under_cursor toggle_active_hand tokenize true typeof unsafe_script use_obj use_obj_on_obj using_skill " +
			"variable wait when while wield_obj_critter wm_area_set_pos world_map write_byte write_int write_short write_string";
	}
}