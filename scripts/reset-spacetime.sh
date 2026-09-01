#!/usr/bin/env sh

set -eu

compose_file=infra/docker-compose.yml

docker compose -f "$compose_file" rm --stop --force spacetimedb
docker volume rm sea_spacetimedb-data 2>/dev/null || true
docker compose -f "$compose_file" up -d --wait spacetimedb
./scripts/spacetime.sh publish sea-local \
  --server http://host.docker.internal:3000 \
  --yes \
  --module-path server/spacetimedb/spacetimedb
