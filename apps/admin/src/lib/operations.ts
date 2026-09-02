import { createServerFn } from "@tanstack/react-start";

const databaseName = "sea-local";
const dashboardTables = [
	"player_ownership",
	"ship",
	"npc_ai",
	"player_progression",
	"world_object",
	"combat_event",
] as const;

type SqlResult = {
	schema?: { elements?: { name?: { some?: string } }[] };
	rows?: unknown[];
};
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
	for (const table of dashboardTables) {
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

// SpacetimeDB returns positional rows plus the column schema for the statement.
// Decoding from that schema keeps the dashboard correct as the module evolves.
function decode(result: SqlResult | undefined): DashboardRow[] {
	const columns = result?.schema?.elements?.map(
		(element) => element.name?.some,
	);
	return (result?.rows ?? []).map((row): DashboardRow => {
		if (Array.isArray(row))
			return Object.fromEntries(
				(columns ?? []).map((column, index) => [
					column ?? `column_${index}`,
					row[index],
				]),
			);
		if (row !== null && typeof row === "object") return row as DashboardRow;
		return { value: row };
	});
}

export const getOperationsSnapshot = createServerFn({ method: "GET" }).handler(
	async (): Promise<OperationsSnapshot> => {
		try {
			const results = await fetchSql();
			const tables = Object.fromEntries(
				dashboardTables.map((table, index) => [table, decode(results[index])]),
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
