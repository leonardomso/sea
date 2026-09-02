# shellcheck shell=sh
# Host ports for the local development stack. The block sits far above the
# defaults other projects use (3000, 3001, 5432, 6379, 9000) so two checkouts
# on one machine never fight over a port. Every value can be overridden from
# the environment; infra/docker-compose.yml reads the same variable names.
#
# POSIX sh: sourced by bash and sh scripts alike.
: "${SPACETIME_PORT:=43000}"
: "${ADMIN_PORT:=43001}"
: "${POSTGRES_PORT:=45432}"
: "${REDIS_PORT:=46379}"
: "${MINIO_API_PORT:=49000}"
: "${MINIO_CONSOLE_PORT:=49001}"
export SPACETIME_PORT ADMIN_PORT POSTGRES_PORT REDIS_PORT MINIO_API_PORT MINIO_CONSOLE_PORT

# The stack as seen from this machine.
SEA_SPACETIME_LOCAL_URL="http://127.0.0.1:$SPACETIME_PORT"
SEA_ADMIN_LOCAL_URL="http://127.0.0.1:$ADMIN_PORT"
# The stack as seen from a tool container (the SpacetimeDB CLI, dotnet test runs).
SEA_SPACETIME_DOCKER_URL="http://host.docker.internal:$SPACETIME_PORT"
export SEA_SPACETIME_LOCAL_URL SEA_ADMIN_LOCAL_URL SEA_SPACETIME_DOCKER_URL
