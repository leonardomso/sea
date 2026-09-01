#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SEA_TEST_FILTER=FourClientsShareBoundedWorldInterestWithoutPlayerCombat \
  "$project_root/scripts/test-server-integration.sh"
