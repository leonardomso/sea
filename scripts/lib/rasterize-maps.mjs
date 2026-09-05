// Turns the island and reef shapes a person authored into the one-square land mask the
// simulation reads, and into the terrain grid the same simulation reads for its water / shoal /
// land lookups. Shapes stay authored because that is how a person draws a coastline and how the
// client renders one back; the grid and the mask exist because the server needs a lookup instead
// of a shape to ask a question of. Both are produced here, in the same pass over the same
// objects, so they are the same land expressed twice and cannot drift apart the way a
// hand-authored terrain grid could.

const WATER = 0;
const LAND = 1;
const SHOAL = 2;

const TERRAIN_SYMBOL = { [WATER]: ".", [LAND]: "#", [SHOAL]: "~" };

/**
 * @param {{width: number, height: number, code?: string}} map
 * @returns {number} the mask word count for a map of this size
 */
export function wordCount(map) {
  return Math.ceil((map.width * map.height) / 64);
}

/**
 * Rasterises one map's authored objects into a land mask and a terrain grid.
 *
 * A square counts as inside a shape when the square's own centre is inside it: half a square of
 * slop at a coastline is invisible at 32 px a square, and it keeps a circle from swallowing the
 * water beside it. A shape too small to cover any cell centre still has to land somewhere, or a
 * later small rock could be authored and simply vanish from both the mask and the grid, so the
 * cell containing the shape's own centre is always marked regardless of radius.
 *
 * @param {{
 *   width: number,
 *   height: number,
 *   code?: string,
 *   objects: Array<{kind: string, x: number, y: number, radius: number, blocksMovement: boolean}>,
 * }} map
 * @returns {{ words: BigUint64Array, terrainRows: string[] }} `words` is one bit per square,
 *   row-major (index = cellY * width + cellX), bit set means land. `terrainRows` is the same
 *   land expressed as one character per square, `.` water, `~` shoal, `#` land.
 */
export function rasterizeMap(map) {
  const { width, height } = map;
  if (width !== height) {
    throw new Error(`${map.code ?? "map"}: the mask assumes a square map, got ${width}x${height}`);
  }

  const size = width;
  const words = new BigUint64Array(wordCount(map));
  const grid = new Uint8Array(size * size);

  const markLand = (cellX, cellY) => {
    if (cellX < 0 || cellY < 0 || cellX >= size || cellY >= size) return;
    const index = (cellY * size) + cellX;
    words[index >> 6] |= 1n << BigInt(index & 63);
    grid[index] = LAND;
  };

  // Shoals slow a hull; they do not block it, so they never touch the mask -- only shapes that
  // carry blocksMovement do. A shoal also never overwrites land: it can only claim water.
  const markShoal = (cellX, cellY) => {
    if (cellX < 0 || cellY < 0 || cellX >= size || cellY >= size) return;
    const index = (cellY * size) + cellX;
    if (grid[index] !== LAND) {
      grid[index] = SHOAL;
    }
  };

  const stamp = (shape, mark) => {
    const minX = Math.max(0, Math.floor(shape.x - shape.radius));
    const maxX = Math.min(size - 1, Math.ceil(shape.x + shape.radius));
    const minY = Math.max(0, Math.floor(shape.y - shape.radius));
    const maxY = Math.min(size - 1, Math.ceil(shape.y + shape.radius));
    for (let cellY = minY; cellY <= maxY; cellY++) {
      for (let cellX = minX; cellX <= maxX; cellX++) {
        const deltaX = cellX + 0.5 - shape.x;
        const deltaY = cellY + 0.5 - shape.y;
        if ((deltaX * deltaX) + (deltaY * deltaY) <= shape.radius * shape.radius) {
          mark(cellX, cellY);
        }
      }
    }
    mark(Math.floor(shape.x), Math.floor(shape.y));
  };

  const objects = map.objects ?? [];
  for (const shape of objects) {
    if (shape.blocksMovement) {
      stamp(shape, markLand);
    }
  }
  for (const shape of objects) {
    if (shape.kind === "shoal") {
      stamp(shape, markShoal);
    }
  }

  const terrainRows = [];
  for (let y = 0; y < size; y++) {
    let row = "";
    for (let x = 0; x < size; x++) {
      row += TERRAIN_SYMBOL[grid[(y * size) + x]];
    }
    terrainRows.push(row);
  }

  return { words, terrainRows };
}
