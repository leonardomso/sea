import { createFileRoute, useRouter } from "@tanstack/react-router";
import { dashboardPanels } from "../lib/dashboard-panels";
import { getOperationsSnapshot } from "../lib/operations";

export const Route = createFileRoute("/")({
	loader: () => getOperationsSnapshot(),
	component: Home,
});

function Home() {
	const router = useRouter();
	const snapshot = Route.useLoaderData();
	const players = snapshot.tables.player_ownership ?? [];
	const allShips = snapshot.tables.ship ?? [];
	const ships = allShips.filter((ship) => ship.faction_code === 1);
	const enemies = allShips.filter((ship) => ship.faction_code === 2);
	const events = snapshot.tables.combat_event ?? [];

	return (
		<div className="app-shell">
			<header className="topbar">
				<div>
					<p className="eyebrow">SEA / LOCAL OPERATIONS</p>
					<h1>Starter Cove</h1>
				</div>
				<div className="topbar-actions">
					<span
						className={`status-pill ${snapshot.service.connected ? "is-online" : "is-offline"}`}
					>
						<span className="status-dot" />
						{snapshot.service.connected ? "Online" : "Offline"}
					</span>
					<button
						type="button"
						className="button button-quiet"
						onClick={() => router.invalidate()}
					>
						Refresh
					</button>
				</div>
			</header>

			<main className="dashboard">
				<section className="hero-card">
					<div>
						<p className="eyebrow">AUTHORITATIVE WORLD</p>
						<h2>One map. One ship. A living sea.</h2>
						<p className="muted">
							Read-only visibility into the local SpacetimeDB replica.
						</p>
					</div>
					<div className="hero-wave" aria-hidden="true">
						≈ ≈ ≈
					</div>
				</section>

				<section className="metrics-grid" aria-label="World metrics">
					<Metric
						label="Connected players"
						value={
							players.filter((player) => player.is_connected === true).length
						}
						detail={`${players.length} identities`}
					/>
					<Metric
						label="Player ships"
						value={ships.length}
						detail="Authoritative state"
					/>
					<Metric
						label="Active enemies"
						value={enemies.filter((enemy) => enemy.is_active !== false).length}
						detail={`${enemies.length} seeded`}
					/>
					<Metric
						label="Recent events"
						value={events.length}
						detail="Current local cache"
					/>
				</section>

				{!snapshot.service.connected && (
					<div className="notice notice-danger">
						{snapshot.service.message}. Start and publish the local server to
						populate the dashboard.
					</div>
				)}

				<section className="panel-grid">
					<DataPanel
						title="Connected players"
						rows={players}
						columns={dashboardPanels.players.columns}
						empty="No players connected"
					/>
					<DataPanel
						title="Player ships"
						rows={ships}
						columns={dashboardPanels.playerShips.columns}
						empty="No player ships"
					/>
					<DataPanel
						title="Enemy ships"
						rows={enemies}
						columns={dashboardPanels.enemyShips.columns}
						empty="No enemy ships"
					/>
					<DataPanel
						title="Recent events"
						rows={events.slice(-8).reverse()}
						columns={dashboardPanels.events.columns}
						empty="No events recorded"
					/>
				</section>
			</main>

			<footer className="footer">
				Last read {formatDate(snapshot.generatedAt)} ·{" "}
				{snapshot.service.message}
			</footer>
		</div>
	);
}

function Metric({
	label,
	value,
	detail,
}: {
	label: string;
	value: number;
	detail: string;
}) {
	return (
		<article className="metric-card">
			<p className="metric-label">{label}</p>
			<p className="metric-value">{value}</p>
			<p className="metric-detail">{detail}</p>
		</article>
	);
}

function DataPanel({
	title,
	rows,
	columns,
	empty,
}: {
	title: string;
	rows: Record<string, unknown>[];
	columns: string[];
	empty: string;
}) {
	return (
		<section className="data-panel">
			<div className="panel-heading">
				<h3>{title}</h3>
				<span>{rows.length}</span>
			</div>
			{rows.length === 0 ? (
				<p className="empty-state">{empty}</p>
			) : (
				<div className="table-scroll">
					<table>
						<thead>
							<tr>
								{columns.map((column) => (
									<th key={column}>{column.replaceAll("_", " ")}</th>
								))}
							</tr>
						</thead>
						<tbody>
							{rows.map((row) => (
								<tr key={`${title}-${JSON.stringify(row)}`}>
									{columns.map((column) => (
										<td key={column}>{formatValue(row[column])}</td>
									))}
								</tr>
							))}
						</tbody>
					</table>
				</div>
			)}
		</section>
	);
}

function formatValue(value: unknown) {
	if (typeof value === "boolean") return value ? "yes" : "no";
	if (value === null || value === undefined) return "—";
	if (typeof value === "object") return JSON.stringify(value);
	return String(value);
}

function formatDate(value: string) {
	return new Intl.DateTimeFormat("en", {
		dateStyle: "medium",
		timeStyle: "short",
	}).format(new Date(value));
}
