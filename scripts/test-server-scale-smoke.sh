#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
database_name="sea-scale-smoke-$$"
state_relative=".cache/spacetime-scale-smoke-$$"
state_directory="$project_root/$state_relative"
runtime_relative=".cache/scale-runtime-$$"
runtime_directory="$project_root/$runtime_relative"
load_artifact_relative=".cache/load-smoke-$$"
load_artifact_directory="$project_root/$load_artifact_relative"
load_log="$runtime_directory/load.log"
load_evidence_relative="$load_artifact_relative/load-evidence.json"
load_evidence="$project_root/$load_evidence_relative"
resource_samples="$runtime_directory/resource-samples.txt"
resource_pid=""
load_pid=""
clients="${SEA_LOAD_CLIENTS:-100}"
active_clients="${SEA_LOAD_ACTIVE_CLIENTS:-$clients}"
duration="${SEA_LOAD_SECONDS:-15}"
sample_iterations="${SEA_TICK_SAMPLE_COUNT:-80}"
ready_timeout="${SEA_LOAD_READY_TIMEOUT:-120}"
ramp_seconds="${SEA_LOAD_RAMP_SECONDS:-2}"
setup_seconds="${SEA_LOAD_SETUP_SECONDS:-0}"
sample_warmup="${SEA_TICK_SAMPLE_WARMUP_SECONDS:-$setup_seconds}"
sample_interval="${SEA_TICK_SAMPLE_INTERVAL_SECONDS:-0.1}"
load_evidence_output="${SEA_LOAD_EVIDENCE_OUTPUT:-}"
server_evidence_output="${SEA_SERVER_EVIDENCE_OUTPUT:-}"
spacetime_container="${SEA_SPACETIME_CONTAINER:-}"
load_server="http://host.docker.internal:3000"
if [ -n "${SEA_DOCKER_NETWORK:-}" ]; then
  load_server="http://$spacetime_container:3000"
fi
mkdir -p "$runtime_directory"

cleanup() {
  if [ -n "$load_pid" ] && kill -0 "$load_pid" 2>/dev/null; then
    kill -TERM "$load_pid" 2>/dev/null || true
    wait "$load_pid" 2>/dev/null || true
  fi
  if [ -n "$resource_pid" ] && kill -0 "$resource_pid" 2>/dev/null; then
    kill -TERM "$resource_pid" 2>/dev/null || true
    wait "$resource_pid" 2>/dev/null || true
  fi
  SPACETIME_STATE_RELATIVE="$state_relative" \
    "$project_root/scripts/spacetime.sh" delete "$database_name" \
      --server http://host.docker.internal:3000 --yes >/dev/null 2>&1 || true
  rm -rf "$state_directory"
  if [ "${SEA_KEEP_PERF_ARTIFACTS:-0}" = "1" ]; then
    mkdir -p "$project_root/Build/performance"
    [ ! -f "$load_log" ] || cp "$load_log" \
      "$project_root/Build/performance/load-scale-last.log"
    [ ! -f "$load_evidence" ] || cp "$load_evidence" \
      "$project_root/Build/performance/load-scale-last.json"
    echo "Scale-smoke artifacts kept at $runtime_directory and $load_artifact_directory" >&2
  else
    rm -rf "$runtime_directory" "$load_artifact_directory"
  fi
}
trap cleanup EXIT

if [ -z "$spacetime_container" ]; then
  spacetime_container="$(docker compose -f "$project_root/infra/docker-compose.yml" \
    ps -q spacetimedb)"
fi
processor_count="$(docker exec "$spacetime_container" getconf _NPROCESSORS_ONLN)"

metric_value() {
  local metric_name="$1"
  local reducer_name="$2"
  awk \
    -v metric_name="$metric_name" -v database_identity="$database_identity" \
    -v reducer_name="$reducer_name" '
      index($1, metric_name "{") == 1 &&
      index($1, "db=\"" database_identity "\"") > 0 &&
      index($1, "reducer=\"" reducer_name "\"") > 0 &&
      (metric_name != "spacetime_num_txns_total" || index($1, "txn_type=\"Reducer\"") > 0) &&
      index($1, "committed=\"false\"") == 0 && value == "" { value = $2 }
      END { print value }
    '
}

metrics_snapshot() {
  curl --fail --silent --max-time 3 http://127.0.0.1:3000/v1/metrics
}

active_ship_count() {
  local response
  if ! response="$(curl --fail --silent --max-time 5 \
    --request POST \
    --header "content-type: text/plain" \
    --data "SELECT * FROM ship WHERE is_moving = true" \
    "http://127.0.0.1:3000/v1/database/$database_name/sql")"; then
    echo 0
    return
  fi

  printf '%s' "$response" | node "$project_root/scripts/lib/sql-result.mjs"
}

simulation_telemetry() {
  curl --fail --silent --max-time 3 \
    --request POST \
    --header "content-type: text/plain" \
    --data "SELECT * FROM simulation_telemetry WHERE id = 1" \
    "http://127.0.0.1:3000/v1/database/$database_name/sql"
}

table_count() {
  local table_name="$1"
  curl --fail --silent --max-time 5 \
    --request POST \
    --header "content-type: text/plain" \
    --data "SELECT * FROM $table_name" \
    "http://127.0.0.1:3000/v1/database/$database_name/sql" |
    node "$project_root/scripts/lib/sql-result.mjs"
}

print_load_diagnostics() {
  grep -E 'SEA_LOAD_EVIDENCE|LoadPhaseTimeout|failedClients|failures|failureSamples|\[E\]|\[X\]|Unhandled' \
    "$load_log" | tail -200 || true
}

sample_container_resources() {
  local samples="$1"
  local seconds="$2"
  : >"$samples"
  for ((resource_second = 0; resource_second < seconds; resource_second++)); do
    docker stats --no-stream --format '{{.CPUPerc}}|{{.MemUsage}}' \
      "$spacetime_container" >>"$samples" 2>/dev/null || true
    sleep 1
  done
}

curl --fail --silent --max-time 2 http://127.0.0.1:3000/v1/ping >/dev/null
SPACETIME_STATE_RELATIVE="$state_relative" \
  "$project_root/scripts/spacetime.sh" publish "$database_name" \
    --server http://host.docker.internal:3000 \
    --yes \
    --module-path server/spacetimedb/spacetimedb >/dev/null

database_identity="$(curl --fail --silent --max-time 3 \
  "http://127.0.0.1:3000/v1/database/$database_name/identity")"
"$project_root/scripts/dotnet10.sh" build \
  tests/load/Sea.LoadTests/Sea.LoadTests.csproj >/dev/null
SEA_LOAD_DATABASE="$database_name" \
SEA_LOAD_SERVER="$load_server" \
SEA_LOAD_CLIENTS="$clients" \
SEA_LOAD_ACTIVE_CLIENTS="$active_clients" \
SEA_LOAD_RAMP_SECONDS="$ramp_seconds" \
SEA_LOAD_SETUP_SECONDS="$setup_seconds" \
SEA_LOAD_SECONDS="$duration" \
SEA_LOAD_EVIDENCE="$load_evidence_relative" \
SEA_LOAD_REPORT_DIRECTORY="$load_artifact_relative/nbomber" \
  "$project_root/scripts/dotnet10.sh" run \
    --project tests/load/Sea.LoadTests/Sea.LoadTests.csproj \
    --no-build >"$load_log" 2>&1 &
load_pid=$!

deadline=$((SECONDS + ready_timeout))
moving=0
loaded=0
while [ "$SECONDS" -lt "$deadline" ]; do
  moving="$(active_ship_count)"
  loaded="$(table_count player_ownership)"
  if [ "$moving" -ge "$active_clients" ] && [ "$loaded" -ge "$clients" ]; then
    break
  fi
  if ! kill -0 "$load_pid" 2>/dev/null; then
    break
  fi
  sleep 1
done
if [ "$moving" -lt "$active_clients" ] || [ "$loaded" -lt "$clients" ]; then
  print_load_diagnostics
  echo "Load readiness reached $loaded/$clients players and $moving/$active_clients moving ships." >&2
  exit 1
fi

if [ "$sample_warmup" -gt 0 ]; then
  sleep "$sample_warmup"
else
  sleep 3
fi
moving="$(active_ship_count)"
if ! kill -0 "$load_pid" 2>/dev/null; then
  print_load_diagnostics
  echo "The load generator exited during warm-up." >&2
  exit 1
fi
loaded="$(table_count player_ownership)"
if [ "$moving" -lt "$active_clients" ] || [ "$loaded" -lt "$clients" ]; then
  print_load_diagnostics
  echo "Warm-up retained $loaded/$clients players and $moving/$active_clients moving ships." >&2
  exit 1
fi

sample_container_resources "$resource_samples" "$duration" &
resource_pid=$!

  reducer_labels=(dispatch snapshot hazard)
  reducer_names=(
    run_simulation_dispatch
    run_movement_snapshot_dispatch
    run_hazard_dispatch
  )
minimum_samples=$((sample_iterations < 20 ? sample_iterations : 20))
  reducer_minimums=("$minimum_samples" "$minimum_samples" "$minimum_samples")
previous_times=()
previous_ticks=()
sample_files=()
metrics="$(metrics_snapshot)"
for reducer_index in "${!reducer_names[@]}"; do
  reducer_name="${reducer_names[$reducer_index]}"
  sample_file="$runtime_directory/${reducer_labels[$reducer_index]}-samples.txt"
  sample_files[reducer_index]="$sample_file"
  : >"$sample_file"
  previous_times[reducer_index]="$(
    printf '%s\n' "$metrics" |
      metric_value reducer_wasm_time_usec "$reducer_name"
  )"
  previous_ticks[reducer_index]="$(
    printf '%s\n' "$metrics" |
      metric_value spacetime_num_txns_total "$reducer_name"
  )"
done

for ((sample_index = 0; sample_index < sample_iterations; sample_index++)); do
  sleep "$sample_interval"
  metrics="$(metrics_snapshot)"
  for reducer_index in "${!reducer_names[@]}"; do
    reducer_name="${reducer_names[$reducer_index]}"
    current_time="$(
      printf '%s\n' "$metrics" |
        metric_value reducer_wasm_time_usec "$reducer_name"
    )"
    current_ticks="$(
      printf '%s\n' "$metrics" |
        metric_value spacetime_num_txns_total "$reducer_name"
    )"
    previous_time="${previous_times[$reducer_index]}"
    previous_tick="${previous_ticks[$reducer_index]}"
    if [ -n "$current_time" ] && [ -n "$current_ticks" ] &&
      [ -n "$previous_time" ] && [ -n "$previous_tick" ]; then
      tick_delta=$((current_ticks - previous_tick))
      time_delta=$((current_time - previous_time))
      if [ "$tick_delta" -gt 0 ] && [ "$time_delta" -ge 0 ]; then
        awk -v elapsed="$time_delta" -v count="$tick_delta" \
          'BEGIN { printf "%.2f\n", elapsed / count }' \
          >>"${sample_files[$reducer_index]}"
      fi
    fi
    previous_times[reducer_index]="$current_time"
    previous_ticks[reducer_index]="$current_ticks"
  done
done

wait "$load_pid"
load_pid=""
wait "$resource_pid"
resource_pid=""
if [ ! -s "$load_evidence" ]; then
  print_load_diagnostics
  echo "The real SDK load run did not write structured evidence." >&2
  exit 1
fi
server_result_relative="$runtime_relative/server-evidence.json"
scale_arguments=()
for reducer_index in "${!reducer_names[@]}"; do
  reducer_label="${reducer_labels[reducer_index]}"
  reducer_minimum="${reducer_minimums[reducer_index]}"
  scale_arguments+=(
    "$reducer_label:$reducer_minimum:$runtime_relative/$reducer_label-samples.txt"
  )
done
if ! "$project_root/scripts/dotnet.sh" run \
  --project tests/performance/Sea.PerformanceEvidence.Cli/Sea.PerformanceEvidence.Cli.csproj \
  -- scale "$load_evidence_relative" "$clients" "$active_clients" \
  "$runtime_relative/resource-samples.txt" "$processor_count" \
  "$server_result_relative" "${scale_arguments[@]}"; then
  print_load_diagnostics
  echo "Server rows: ownership=$(table_count player_ownership) ships=$(table_count ship)." >&2
  echo "Simulation telemetry: $(simulation_telemetry)" >&2
  echo "The scale evidence failed its typed contract or performance budget." >&2
  exit 1
fi

if [ -n "$load_evidence_output" ]; then
  mkdir -p "$(dirname "$load_evidence_output")"
  cp "$load_evidence" "$load_evidence_output"
fi
if [ -n "$server_evidence_output" ]; then
  mkdir -p "$(dirname "$server_evidence_output")"
  cp "$project_root/$server_result_relative" "$server_evidence_output"
fi

echo "$clients real SDK clients retained; $active_clients ships remained active."
