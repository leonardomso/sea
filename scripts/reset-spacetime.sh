#!/usr/bin/env sh

set -eu

compose_file=infra/docker-compose.yml
. ./scripts/lib/local-ports.sh

docker compose -f "$compose_file" rm --stop --force spacetimedb
docker volume rm sea_spacetimedb-data 2>/dev/null || true
docker compose -f "$compose_file" up -d --wait spacetimedb
# A server-issued local token is signed by the deleted server state. Clear it
# after recreation so publish obtains a fresh identity from the new server.
./scripts/spacetime.sh logout >/dev/null 2>&1 || true
./scripts/spacetime.sh publish sea-local \
  --server "$SEA_SPACETIME_DOCKER_URL" \
  --yes \
  --module-path server/spacetimedb/spacetimedb
