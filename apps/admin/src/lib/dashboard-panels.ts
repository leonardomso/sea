// Every SpacetimeDB table and column the dashboard reads. scripts/lib/world-contract.test.mjs
// checks this file against the generated module bindings, so a renamed table or column fails
// `pnpm check` instead of rendering an empty panel.
export const dashboardTables = [
	"player_ownership",
	"ship",
	"combat_event",
] as const;

export type DashboardTable = (typeof dashboardTables)[number];

export const dashboardPanels = {
	players: {
		table: "player_ownership",
		columns: ["owner", "ship_entity_id", "is_connected"],
	},
	playerShips: {
		table: "ship",
		columns: [
			"entity_id",
			"faction_code",
			"position_x",
			"position_y",
			"hull",
			"is_engaged",
		],
	},
	enemyShips: {
		table: "ship",
		columns: ["entity_id", "archetype_code", "hull", "max_hull", "is_active"],
	},
	events: {
		table: "combat_event",
		columns: ["owner_entity_id", "event_type", "details", "tick"],
	},
} as const satisfies Record<
	string,
	{ table: DashboardTable; columns: readonly string[] }
>;
