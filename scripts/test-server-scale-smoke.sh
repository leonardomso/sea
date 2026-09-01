#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
database_name="sea-scale-smoke-$$"
state_relative=".cache/spacetime-scale-smoke-$$"
state_directory="$project_root/$state_relative"
runtime_directory="$(mktemp -d)"
load_log="$runtime_directory/load.log"
tick_samples="$runtime_directory/tick-samples.txt"
movement_samples="$runtime_directory/movement-samples.txt"
clients="${SEA_LOAD_CLIENTS:-100}"
duration="${SEA_LOAD_SECONDS:-15}"

cleanup() {
  SPACETIME_STATE_RELATIVE="$state_relative" \
    "$project_root/scripts/spacetime.sh" delete "$database_name" \
      --server http://host.docker.internal:3000 --yes >/dev/null 2>&1 || true
  rm -rf "$state_directory" "$runtime_directory"
}
trap cleanup EXIT

metric_value() {
  local metric_name="$1"
  local reducer_name="$2"
  curl --fail --silent --max-time 3 http://127.0.0.1:3000/v1/metrics | awk \
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

active_ship_count() {
  curl --fail --silent --max-time 3 \
    --request POST \
    --header "content-type: text/plain" \
    --data "SELECT * FROM ship WHERE is_moving = true" \
    "http://127.0.0.1:3000/v1/database/$database_name/sql" | node -e '
      let body = "";
      process.stdin.on("data", chunk => body += chunk);
      process.stdin.on("end", () => {
        const result = JSON.parse(body)[0];
        process.stdout.write(String(result?.rows?.length ?? 0));
      });
    '
}

curl --fail --silent --max-time 2 http://127.0.0.1:3000/v1/ping >/dev/null
SPACETIME_STATE_RELATIVE="$state_relative" \
  "$project_root/scripts/spacetime.sh" publish "$database_name" \
    --server http://host.docker.internal:3000 \
    --yes \
    --module-path server/spacetimedb/spacetimedb >/dev/null

database_identity="$(curl --fail --silent --max-time 3 \
  "http://127.0.0.1:3000/v1/database/$database_name/identity")"
SEA_LOAD_DATABASE="$database_name" \
SEA_LOAD_SERVER="http://host.docker.internal:3000" \
SEA_LOAD_CLIENTS="$clients" \
SEA_LOAD_SECONDS="$duration" \
  "$project_root/scripts/dotnet10.sh" run \
    --project tests/load/Sea.LoadTests/Sea.LoadTests.csproj \
    --no-build >"$load_log" 2>&1 &
load_pid=$!

deadline=$((SECONDS + 30))
moving=0
while [ "$SECONDS" -lt "$deadline" ]; do
  moving="$(active_ship_count)"
  if [ "$moving" -ge "$clients" ]; then
    break
  fi
  sleep 1
done
if [ "$moving" -lt "$clients" ]; then
  cat "$load_log"
  echo "Only $moving of $clients load-test ships began sailing." >&2
  exit 1
fi

sleep 3
moving="$(active_ship_count)"
if [ "$moving" -lt "$clients" ]; then
  echo "Only $moving of $clients ships remained active after load-test warm-up." >&2
  exit 1
fi

previous_time="$(metric_value reducer_wasm_time_usec run_simulation_tick)"
previous_ticks="$(metric_value spacetime_num_txns_total run_simulation_tick)"
previous_movement_time="$(metric_value reducer_wasm_time_usec run_movement_shard)"
previous_movement_ticks="$(metric_value spacetime_num_txns_total run_movement_shard)"
for _ in {1..80}; do
  sleep 0.1
  current_time="$(metric_value reducer_wasm_time_usec run_simulation_tick)"
  current_ticks="$(metric_value spacetime_num_txns_total run_simulation_tick)"
  current_movement_time="$(metric_value reducer_wasm_time_usec run_movement_shard)"
  current_movement_ticks="$(metric_value spacetime_num_txns_total run_movement_shard)"
  tick_delta=$((current_ticks - previous_ticks))
  time_delta=$((current_time - previous_time))
  if [ "$tick_delta" -gt 0 ]; then
    awk -v elapsed="$time_delta" -v count="$tick_delta" \
      'BEGIN { printf "%.2f\n", elapsed / count }' >>"$tick_samples"
  fi
  movement_tick_delta=$((current_movement_ticks - previous_movement_ticks))
  movement_time_delta=$((current_movement_time - previous_movement_time))
  if [ "$movement_tick_delta" -gt 0 ]; then
    awk -v elapsed="$movement_time_delta" -v count="$movement_tick_delta" \
      'BEGIN { printf "%.2f\n", elapsed / count }' >>"$movement_samples"
  fi
  previous_time="$current_time"
  previous_ticks="$current_ticks"
  previous_movement_time="$current_movement_time"
  previous_movement_ticks="$current_movement_ticks"
done

wait "$load_pid"
if rg -q "fail count: [1-9]|fail count: [1-9][0-9]" "$load_log"; then
  cat "$load_log"
  echo "The real SDK load run reported failed client operations." >&2
  exit 1
fi

sample_count="$(wc -l <"$tick_samples" | tr -d ' ')"
movement_sample_count="$(wc -l <"$movement_samples" | tr -d ' ')"
if [ "$sample_count" -lt 20 ] || [ "$movement_sample_count" -lt 20 ]; then
  echo "Only $sample_count server tick samples were collected." >&2
  exit 1
fi
rank=$(( (sample_count * 95 + 99) / 100 ))
p95="$(sort -n "$tick_samples" | sed -n "${rank}p")"
movement_rank=$(( (movement_sample_count * 95 + 99) / 100 ))
movement_p95="$(sort -n "$movement_samples" | sed -n "${movement_rank}p")"
measured_p95="$(awk -v global="$p95" -v movement="$movement_p95" \
  'BEGIN { print (global > movement ? global : movement) }')"
if ! awk -v duration_us="$measured_p95" 'BEGIN { exit !(duration_us <= 10000) }'; then
  median_rank=$(( (sample_count + 1) / 2 ))
  p90_rank=$(( (sample_count * 90 + 99) / 100 ))
  median="$(sort -n "$tick_samples" | sed -n "${median_rank}p")"
  p90="$(sort -n "$tick_samples" | sed -n "${p90_rank}p")"
  maximum="$(sort -n "$tick_samples" | tail -1)"
  echo "Tick sample summary: global median=${median}us p90=${p90}us p95=${p95}us max=${maximum}us; movement p95=${movement_p95}us." >&2
  echo "Server tick p95 ${measured_p95}us exceeds the 10000us smoke budget." >&2
  exit 1
fi

echo "$clients real clients sailed concurrently; global tick p95 was ${p95}us and movement-shard p95 was ${movement_p95}us."
