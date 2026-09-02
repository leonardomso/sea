import { createServerFn } from "@tanstack/react-start";

const databaseName = "sea-local";
const tableColumns = {
	player_ownership: ["owner", "ship_entity_id", "is_connected"],
	ship: [
		"entity_id",
		"archetype_id",
		"faction",
		"position_x",
		"position_y",
		"hull",
		"max_hull",
		"sails",
		"cannons",
		"is_engaged",
		"is_active",
	],
	npc_ai: ["ship_entity_id", "archetype_id", "is_active", "next_decision_tick"],
	player_progression: ["owner", "level", "experience", "gold"],
	world_object: [
		"entity_id",
		"kind",
		"position_x",
		"position_y",
		"is_active",
		"blocks_movement",
	],
	combat_event: [
		"event_id",
		"owner_entity_id",
		"event_type",
		"details",
		"tick",
	],
} as const;

type SqlResult = { rows?: unknown[] };
type DashboardRow = Record<string, unknown>;

export type OperationsSnapshot = {
	generatedAt: string;
	service: { connected: boolean; message: string };
	tables: Record<string, DashboardRow[]>;
};

function spacetimeUrl() {
	return process.env.SEA_SPACETIMEDB_URL ?? "http://127.0.0.1:43000";
}

async function fetchSql() {
	const results: SqlResult[] = [];
	for (const table of Object.keys(tableColumns)) {
		const response = await fetch(
			`${spacetimeUrl()}/v1/database/${databaseName}/sql`,
			{
				method: "POST",
				headers: { "content-type": "text/plain" },
				body: `SELECT * FROM ${table}`,
			},
		);
		if (!response.ok)
			throw new Error(
				`SpacetimeDB SQL request for ${table} failed with ${response.status}`,
			);
		const statementResults = (await response.json()) as SqlResult[];
		results.push(statementResults[0] ?? {});
	}
	return results;
}

function rowsForResult(
	result: SqlResult | undefined,
	columns: readonly string[],
) {
	return (result?.rows ?? []).map((row): DashboardRow => {
		if (Array.isArray(row))
			return Object.fromEntries(
				columns.map((column, index) => [column, row[index]]),
			);
		if (row !== null && typeof row === "object") return row as DashboardRow;
		return { value: row };
	});
}

export const getOperationsSnapshot = createServerFn({ method: "GET" }).handler(
	async (): Promise<OperationsSnapshot> => {
		try {
			const results = await fetchSql();
			const tableNames = Object.keys(tableColumns) as Array<
				keyof typeof tableColumns
			>;
			const tables = Object.fromEntries(
				tableNames.map((table, index) => [
					table,
					rowsForResult(results[index], tableColumns[table]),
				]),
			);
			return {
				generatedAt: new Date().toISOString(),
				service: { connected: true, message: "SpacetimeDB is reachable" },
				tables,
			};
		} catch (error) {
			return {
				generatedAt: new Date().toISOString(),
				service: {
					connected: false,
					message:
						error instanceof Error ? error.message : "Unknown service error",
				},
				tables: {},
			};
		}
	},
);
