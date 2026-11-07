namespace ParadoxModTool.FlagTracker;

/// <summary>
/// Eingebaute (dynamische) Variablen und Arrays von HOI IV: Das Spiel berechnet sie, kein Skript setzt sie. Eine gelesene Variable oder ein
/// gelesenes Array mit diesem Namen (bei <c>x@key</c> der Teil vor dem <c>@</c>) ergibt keine Fundstelle, sonst stünde in jeder Mod „nie gesetzt“.
/// </summary>
/// <remarks>
/// Erzeugt aus <c>documentation/dynamic_variables_documentation.md</c> von HOI IV 1.19.4.0 „Operation Postern“ (Dokumentation vom
/// 2026-10-15, gelesen 2026-10-28): jede Überschrift <c>### name</c> in den Abschnitten „Dynamic variables for scope …“ (global, country,
/// state, unit_leader, military_industrial_organization, special_project; 374 Namen, darunter Arrays wie <c>majors</c>, <c>countries</c>,
/// <c>owned_states</c>). Von Hand ergänzt, nicht in der Dokumentation: <c>id</c> (Kennung jedes Bereichs, <c>THIS.id</c>).
/// </remarks>
internal static class Hoi4BuiltInVariables
{
    public static readonly IReadOnlySet<string> Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Von Hand, nicht in der Dokumentation.
        "id",

        // dynamic_variables_documentation.md (HOI IV 1.19.4.0).
        "agency_upgrade_number", "ai_attitude_allied_weight", "ai_attitude_friendly_weight", "ai_attitude_hostile_weight",
        "ai_attitude_is_threatened", "ai_attitude_neutral_weight", "ai_attitude_outraged_weight", "ai_attitude_protective_weight",
        "ai_attitude_threatened_weight", "ai_attitude_wants_ally", "ai_attitude_wants_antagonize", "ai_attitude_wants_ignore",
        "ai_attitude_wants_protect", "ai_attitude_wants_weaken", "ai_irrationality", "ai_strategy_activate_crypto", "ai_strategy_alliance",
        "ai_strategy_antagonize", "ai_strategy_befriend", "ai_strategy_conquer", "ai_strategy_consider_weak", "ai_strategy_contain",
        "ai_strategy_declare_war", "ai_strategy_decrypt_target", "ai_strategy_dont_defend_ally_borders", "ai_strategy_force_defend_ally_borders",
        "ai_strategy_ignore", "ai_strategy_ignore_claim", "ai_strategy_influence", "ai_strategy_invade", "ai_strategy_occupation_policy",
        "ai_strategy_prepare_for_war", "ai_strategy_protect", "ai_strategy_send_volunteers_desire", "ai_strategy_support", "ai_wants_divisions",
        "air_chief", "air_experience", "air_intel", "alliance_naval_strength_ratio", "alliance_strength_ratio", "allies",
        "amount_manpower_in_deployment_queue", "amount_research_slots", "any_war_score", "arms_factory_level", "army_attack_level", "army_chief",
        "army_defense_level", "army_experience", "army_intel", "army_leaders", "attack_level", "attack_skill_level", "autonomy_ratio",
        "average_stats", "avg_combat_status", "avg_defensive_combat_status", "avg_offensive_combat_status", "avg_unit_entrenchment_ratio",
        "avg_unit_planning_ratio", "avg_units_acclimation", "building_level", "capital", "casualties", "casualties_k", "civilian_intel",
        "command_power", "command_power_daily", "compare_autonomy_progress_ratio", "compliance", "compliance_speed", "controlled_states",
        "controller", "convoy_threat", "convoys_destroyed", "coordination_level", "core_compliance", "core_countries", "core_resistance",
        "core_states", "countries", "country_leader", "cryptology_defense_level", "current_party_ideology_group", "damaged_building_level", "date",
        "days_decision_timeout", "days_mission_timeout", "days_since_capitulated", "days_since_last_strategic_bombing", "days_until_next_election",
        "decryption_progress", "decryption_speed", "defense_level", "defense_skill_level", "deployed_airforce_manpower_k", "deployed_army_manpower_k",
        "deployed_navy_manpower_k", "deployed_total_manpower_k", "difficulty", "distance_to", "encryption_strength", "enemies",
        "enemies_naval_strength_ratio", "enemies_of_allies", "enemies_strength_ratio", "energy_ratio", "exiles", "facility_province_id",
        "facility_state", "faction_influence_rank", "faction_influence_ratio", "faction_influence_score", "faction_leader",
        "faction_manifest_fulfillment", "faction_members", "faction_power_projection", "faction_resource_extracted",
        "faction_resource_extracted_total", "foreign_manpower", "fuel_k", "fuel_ratio", "funds", "garrison_manpower_need",
        "global_resource_extracted", "global_resource_extracted_total", "has_added_tension_amount", "has_bombing_war_support",
        "has_casualties_war_support", "has_collaboration", "has_convoys_war_support", "has_legitimacy", "has_manpower", "has_orders_group",
        "has_political_power", "has_stability", "has_war_support", "high_command", "highest_party_ideology", "highest_party_popularity", "host",
        "ideology_groups", "industrial_complex_level", "infrastructure_level", "intel_yield_factor_on_capture", "land_doctrine_level",
        "leader_modifier", "legitimacy", "logistics_level", "logistics_skill_level", "longest_war_length", "majors", "maneuvering_level", "manpower",
        "manpower_k", "manpower_per_military_factory", "max_available_manpower", "max_available_manpower_k", "max_fuel_k", "max_manpower",
        "max_manpower_k", "max_task_capacity", "mine_threat", "modifier", "navy_chief", "navy_experience", "navy_intel", "navy_leaders", "neighbors",
        "neighbors_owned", "network_national_coverage", "non_damaged_building_level", "num_armies", "num_armies_in_state", "num_armies_with_type",
        "num_armored", "num_artillery", "num_assigned_traits", "num_basic_traits", "num_battalions", "num_battalions_with_type", "num_battle_plans",
        "num_cavalry", "num_controlled_states", "num_core_states", "num_days", "num_deployed_planes", "num_deployed_planes_with_type",
        "num_divisions", "num_equipment", "num_equipment_in_armies", "num_equipment_in_armies_k", "num_faction_members", "num_fake_intel_divisions",
        "num_free_operative_slots", "num_infantry", "num_max_traits", "num_mechanized", "num_motorized", "num_nukes_being_dropped",
        "num_nukes_left_to_drop", "num_occupied_states", "num_of_available_civilian_factories", "num_of_available_military_factories",
        "num_of_available_naval_factories", "num_of_career_profile_points", "num_of_civilian_factories",
        "num_of_civilian_factories_available_for_projects", "num_of_civilian_factories_in_cores", "num_of_controlled_factories",
        "num_of_controlled_states", "num_of_factories", "num_of_military_factories", "num_of_military_factories_in_cores", "num_of_naval_factories",
        "num_of_naval_factories_in_cores", "num_of_nukes", "num_of_operatives", "num_of_owned_factories", "num_of_supply_nodes",
        "num_operative_slots", "num_orders_groups", "num_owned_controlled_states", "num_owned_states", "num_personality_traits",
        "num_researched_technologies", "num_rocket", "num_ships", "num_ships_with_type", "num_special", "num_status_traits", "num_subjects",
        "num_target_equipment", "num_target_equipment_in_armies", "num_target_equipment_in_armies_k", "num_tech_sharing_groups", "num_terrain_traits",
        "num_traits", "num_units", "num_units_crossing_river", "num_units_defensive_combats", "num_units_defensive_combats_on", "num_units_in_combat",
        "num_units_in_state", "num_units_offensive_combats", "num_units_offensive_combats_against", "num_units_on_climate", "num_units_with_type",
        "number_of_currently_assigned_tasks", "number_of_unlocked_traits", "number_of_unused_trait_points", "occupied_countries", "operation_country",
        "operation_state", "operation_type", "operations", "operative_captor", "operatives", "opinion", "original_research_slots", "original_tag",
        "overlord", "own_capture_chance_factor", "own_forced_into_hiding_time_factor", "own_harmed_time_factor", "owned_controlled_states",
        "owned_states", "owner", "party_popularity", "party_popularity_100", "pc_current_score", "pc_total_score", "pc_turn", "planning_level",
        "planning_skill_level", "political_advisor", "political_power", "political_power_daily", "political_power_growth",
        "potential_and_current_enemies", "power_balance_daily", "power_balance_daily_change", "power_balance_value", "power_balance_weekly",
        "power_balance_weekly_change", "province_controllers", "research_bonus", "researched_techs", "resistance", "resistance_speed",
        "resistance_target", "resource", "resource_consumed", "resource_exported", "resource_imported", "resource_produced", "scientist", "size",
        "skill", "skill_level", "stability", "state_and_terrain_strategic_value", "state_population", "state_population_k", "state_strategic_value",
        "states", "subjects", "sum_unit_terrain_modifier", "surrender_progress", "technology", "theorist", "threat", "total_constructed_air_base",
        "total_constructed_anti_air", "total_constructed_civilian_factory", "total_constructed_dockyard", "total_constructed_fuel_silo",
        "total_constructed_gun_emplacement", "total_constructed_infrastructure", "total_constructed_land_fort", "total_constructed_military_factory",
        "total_constructed_naval_fort", "total_constructed_naval_headquarter", "total_constructed_naval_supply_hub",
        "total_constructed_nuclear_reactor", "total_constructed_other", "total_constructed_port", "total_constructed_radar",
        "total_constructed_refinery", "total_constructed_rocket_site", "total_constructed_supply_node", "total_equipment_produced_",
        "total_equipment_produced_air_transport", "total_equipment_produced_amphibious", "total_equipment_produced_anti_air",
        "total_equipment_produced_anti_tank", "total_equipment_produced_armor", "total_equipment_produced_artillery",
        "total_equipment_produced_ballistic_missile", "total_equipment_produced_capital_ship", "total_equipment_produced_carrier",
        "total_equipment_produced_cas", "total_equipment_produced_convoy", "total_equipment_produced_emplacement_gun_ammo",
        "total_equipment_produced_fighter", "total_equipment_produced_flame", "total_equipment_produced_floating_harbor",
        "total_equipment_produced_heavy_fighter", "total_equipment_produced_infantry", "total_equipment_produced_interceptor",
        "total_equipment_produced_land_cruiser", "total_equipment_produced_maritime_patrol_plane", "total_equipment_produced_mechanized",
        "total_equipment_produced_missile", "total_equipment_produced_missile_launcher", "total_equipment_produced_motorized",
        "total_equipment_produced_naval_bomber", "total_equipment_produced_nuclear_missile", "total_equipment_produced_railway_gun",
        "total_equipment_produced_rocket", "total_equipment_produced_sam_missile", "total_equipment_produced_scout_plane",
        "total_equipment_produced_screen_ship", "total_equipment_produced_strategic_bomber", "total_equipment_produced_submarine",
        "total_equipment_produced_suicide", "total_equipment_produced_support", "total_equipment_produced_support_ship",
        "total_equipment_produced_tactical_bomber", "total_equipment_produced_train", "unit_modifier", "unit_ratio_ready_for_plan", "year",
    };
}
