#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
compose_file="$project_root/infra/docker-compose.yml"
container_name="sea-performance-spacetimedb-$$"
network_name="sea-performance-network-$$"
spacetime_was_running=false
admin_was_running=false

service_is_running() {
  local service="$1"
  local container_id
  container_id="$(docker compose -f "$compose_file" ps -q "$service")"
  [ -n "$container_id" ] &&
    [ "$(docker inspect --format '{{.State.Running}}' "$container_id")" = "true" ]
}

restore_stack() {
  docker stop "$container_name" >/dev/null 2>&1 || true
  docker network rm "$network_name" >/dev/null 2>&1 || true
  if [ "$spacetime_was_running" = true ]; then
    docker compose -f "$compose_file" start spacetimedb >/dev/null
  fi
  if [ "$admin_was_running" = true ]; then
    docker compose -f "$compose_file" start admin >/dev/null
  fi
}
trap restore_stack EXIT INT TERM

if service_is_running spacetimedb; then
  spacetime_was_running=true
fi
if service_is_running admin; then
  admin_was_running=true
fi

if [ "$admin_was_running" = true ]; then
  docker compose -f "$compose_file" stop admin >/dev/null
fi
if [ "$spacetime_was_running" = true ]; then
  docker compose -f "$compose_file" stop spacetimedb >/dev/null
fi

docker stop "$container_name" >/dev/null 2>&1 || true
docker network rm "$network_name" >/dev/null 2>&1 || true
docker network create "$network_name" >/dev/null
spacetime_image="$(docker compose -f "$compose_file" config --images | \
  awk '/clockworklabs\/spacetime/ { print; exit }')"
if [ -z "$spacetime_image" ]; then
  echo "The pinned SpacetimeDB image could not be resolved." >&2
  exit 1
fi

docker run --detach --rm \
  --name "$container_name" \
  --network "$network_name" \
  --publish 3000:3000 \
  "$spacetime_image" \
  --root-dir=/tmp/spacetimedb \
  start \
  --listen-addr=0.0.0.0:3000 >/dev/null

for _ in {1..60}; do
  if curl --fail --silent --max-time 2 \
    http://127.0.0.1:3000/v1/ping >/dev/null 2>&1; then
    if SEA_DOCKER_NETWORK="$network_name" \
      SEA_SPACETIME_CONTAINER="$container_name" \
      "$@"; then
      exit
    else
      result=$?
      mkdir -p "$project_root/Build/performance"
      docker logs "$container_name" \
        >"$project_root/Build/performance/spacetimedb-scale-failure.log" 2>&1 || true
      exit "$result"
    fi
  fi
  sleep 1
done

echo "The isolated SpacetimeDB server did not become healthy." >&2
exit 1
