import assert from "node:assert/strict";
import test from "node:test";
import { sqlRowCount } from "./sql-result.mjs";

test("counts rows in a SpacetimeDB SQL response", () => {
  assert.equal(sqlRowCount([{ rows: [[1], [2], [3]] }]), 3);
});

test("rejects malformed SQL responses", () => {
  assert.throws(() => sqlRowCount({ rows: [] }), TypeError);
  assert.throws(() => sqlRowCount([]), TypeError);
});
