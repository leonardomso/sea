#!/usr/bin/env bash
set -euo pipefail

runtime_directory="$(mktemp -d)"
metrics_before="$runtime_directory/metrics-before.txt"
metrics_after="$runtime_directory/metrics-after.txt"
cpu_samples="$runtime_directory/cpu-samples.txt"
tick_interval_usec=10000
database_identity="$(curl --fail --silent --max-time 3 \
  http://127.0.0.1:3000/v1/database/sea-local/identity)"

cleanup() {
  rm -rf "$runtime_directory"
}
trap cleanup EXIT

metric_value() {
  local metric_file="$1"
  local metric_name="$2"

  awk -v metric_name="$metric_name" -v database_identity="$database_identity" '
    index($1, metric_name "{") == 1 &&
    index($1, "db=\"" database_identity "\"") > 0 &&
    index($1, "reducer=\"run_simulation_dispatch\"") > 0 &&
    (metric_name != "spacetime_num_txns_total" ||
      index($1, "txn_type=\"Reducer\"") > 0) &&
    index($1, "committed=\"false\"") == 0 {
      print $2
      exit
    }
  ' "$metric_file"
}

curl --fail --silent --max-time 3 http://127.0.0.1:3000/v1/metrics >"$metrics_before"
sleep 3
curl --fail --silent --max-time 3 http://127.0.0.1:3000/v1/metrics >"$metrics_after"

time_before="$(metric_value "$metrics_before" reducer_wasm_time_usec)"
time_after="$(metric_value "$metrics_after" reducer_wasm_time_usec)"
ticks_before="$(metric_value "$metrics_before" spacetime_num_txns_total)"
ticks_after="$(metric_value "$metrics_after" spacetime_num_txns_total)"

tick_count=$((ticks_after - ticks_before))
tick_time=$((time_after - time_before))
if [ "$tick_count" -le 0 ]; then
  echo "The scheduled simulation did not advance during the idle sample." >&2
  exit 1
fi

average_tick_usec="$(awk -v elapsed="$tick_time" -v count="$tick_count" \
  'BEGIN { printf "%.2f", elapsed / count }')"
if ! awk -v duration="$average_tick_usec" -v budget="$tick_interval_usec" \
  'BEGIN { exit !(duration < budget) }'; then
  echo "Idle tick average ${average_tick_usec}us exceeds ${tick_interval_usec}us interval." >&2
  exit 1
fi

# Let short-lived integration and build activity drain before measuring idle load.
sleep 5
for _ in {1..3}; do
  docker stats --no-stream --format '{{.CPUPerc}}' \
    sea-spacetimedb-1 sea-admin-1 sea-postgres-1 sea-redis-1 sea-minio-1 \
    | tr -d '%' \
    | awk '{ total += $1 } END { printf "%.2f\n", total }' \
    >>"$cpu_samples"
done

average_cpu="$(awk '{ total += $1 } END { printf "%.2f", total / NR }' "$cpu_samples")"
if ! awk -v cpu="$average_cpu" 'BEGIN { exit !(cpu < 25) }'; then
  echo "Idle local stack CPU average ${average_cpu}% exceeds 25%." >&2
  exit 1
fi

echo "Idle tick averaged ${average_tick_usec}us and local stack CPU averaged ${average_cpu}%."
