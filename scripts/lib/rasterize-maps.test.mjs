import assert from "node:assert/strict";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

import { loadContent } from "./content-catalog.mjs";
import { rasterizeMap, wordCount } from "./rasterize-maps.mjs";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const dataDir = path.join(repoRoot, "server/spacetimedb/spacetimedb/Content/Data");

/** Reads the mask bit for one cell exactly the way LandMask.cs does: word index >> 6, bit index & 63. */
function isLandCell(words, size, cellX, cellY) {
  const index = (cellY * size) + cellX;
  const word = words[index >> 6];
  return (word & (1n << BigInt(index & 63))) !== 0n;
}

test("a blocking circle sets the cells whose centre it covers, and nothing else", () => {
  const map = {
    width: 10,
    height: 10,
    objects: [{ kind: "island", x: 5, y: 5, radius: 2, blocksMovement: true }],
  };

  const { words, terrainRows } = rasterizeMap(map);

  // Distance from (5,5) to the centre of (3,3) is sqrt(8) ~= 2.83, outside radius 2: water.
  assert.equal(isLandCell(words, 10, 3, 3), false);
  assert.equal(terrainRows[3][3], ".");

  // (5,5) itself, cell (5,5) has centre (5.5, 5.5), distance sqrt(0.5) ~= 0.7, inside radius 2: land.
  assert.equal(isLandCell(words, 10, 5, 5), true);
  assert.equal(terrainRows[5][5], "#");

  // Bit layout matches LandMask.cs: index = cellY * size + cellX, word index >> 6, bit index & 63.
  const index = (5 * 10) + 5;
  assert.equal((words[index >> 6] & (1n << BigInt(index & 63))) !== 0n, true);
});

test("a shoal marks the terrain grid but never the mask, and never overwrites land", () => {
  const map = {
    width: 10,
    height: 10,
    objects: [
      { kind: "island", x: 2, y: 2, radius: 1.4, blocksMovement: true },
      // Overlaps the island above at its near edge, and reaches clear water beyond it.
      { kind: "shoal", x: 4, y: 2, radius: 3, blocksMovement: false },
    ],
  };

  const { words, terrainRows } = rasterizeMap(map);

  // Clear water under the shoal, away from the island: marked shoal, mask bit stays clear.
  assert.equal(terrainRows[2][5], "~");
  assert.equal(isLandCell(words, 10, 5, 2), false);

  // Where the shoal's circle overlaps the island, land wins in the grid and the mask agrees.
  assert.equal(terrainRows[2][2], "#");
  assert.equal(isLandCell(words, 10, 2, 2), true);
});

test("a shape smaller than a square still claims its own centre cell", () => {
  const map = {
    width: 10,
    height: 10,
    objects: [{ kind: "reef", x: 7.2, y: 3.4, radius: 0.1, blocksMovement: true }],
  };

  const { words, terrainRows } = rasterizeMap(map);

  assert.equal(isLandCell(words, 10, 7, 3), true);
  assert.equal(terrainRows[3][7], "#");

  // Nothing else on a 10x10 map is land.
  let landCells = 0;
  for (let y = 0; y < 10; y++) {
    for (let x = 0; x < 10; x++) {
      if (isLandCell(words, 10, x, y)) landCells++;
    }
  }
  assert.equal(landCells, 1);
});

test("a non-blocking, non-shoal object (a harbor or a storm) touches neither grid", () => {
  const map = {
    width: 6,
    height: 6,
    objects: [
      { kind: "harbor", x: 3, y: 3, radius: 2, blocksMovement: false },
      { kind: "storm", x: 1, y: 1, radius: 1, blocksMovement: false },
    ],
  };

  const { words, terrainRows } = rasterizeMap(map);

  assert.ok(words.every((word) => word === 0n));
  assert.ok(terrainRows.every((row) => [...row].every((symbol) => symbol === ".")));
});

test("the word array is exactly ceil(width*height/64) words long", () => {
  assert.equal(wordCount({ width: 10, height: 10 }), 2); // 100 bits -> 2 words
  assert.equal(wordCount({ width: 400, height: 400 }), 2500); // matches LandMask.WordCount(400)

  const { words } = rasterizeMap({ width: 10, height: 10, objects: [] });
  assert.equal(words.length, 2);
});

test("a non-square map is refused rather than silently misread", () => {
  assert.throws(() => rasterizeMap({ width: 10, height: 5, objects: [] }), /square/);
});

test("Havenmere's authored islands and reefs rasterise to land, and the port stays water", () => {
  const content = loadContent(dataDir);
  const map = content.maps.find((candidate) => candidate.code === "1/1");
  assert.ok(map, "Havenmere (map 1/1) is missing from the committed content");

  const { words, terrainRows } = rasterizeMap(map);

  for (const shape of map.objects) {
    if (shape.kind !== "island" && shape.kind !== "reef") continue;
    const cellX = Math.floor(shape.x);
    const cellY = Math.floor(shape.y);
    assert.equal(
      isLandCell(words, map.width, cellX, cellY),
      true,
      `${shape.kind} at (${shape.x}, ${shape.y}) did not rasterise to land`,
    );
    assert.equal(terrainRows[cellY][cellX], "#");
  }

  const portCellX = Math.floor(map.portX);
  const portCellY = Math.floor(map.portY);
  assert.equal(isLandCell(words, map.width, portCellX, portCellY), false);
  assert.equal(terrainRows[portCellY][portCellX], ".");
});
