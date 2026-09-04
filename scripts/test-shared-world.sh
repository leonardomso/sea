#!/usr/bin/env bash
set -euo pipefail

# The two scenarios that only four connections in one world can show: four captains settling one
# conserved reward from the same hostile, and four captains breaking Havenmere's named captain to
# the hull she calls her escorts at.
project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SEA_TEST_FILTER='FullyQualifiedName~SharedRewardIntegrationTests|FullyQualifiedName~RedMaryIntegrationTests' \
  "$project_root/scripts/test-server-integration.sh"
