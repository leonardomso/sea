import { fileURLToPath } from "node:url";

export function sqlRowCount(document) {
  if (!Array.isArray(document) || !document[0] || !Array.isArray(document[0].rows)) {
    throw new TypeError("SpacetimeDB SQL response does not contain a row array.");
  }

  return document[0].rows.length;
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  let body = "";
  process.stdin.setEncoding("utf8");
  for await (const chunk of process.stdin) {
    body += chunk;
  }

  try {
    process.stdout.write(String(sqlRowCount(JSON.parse(body))));
  } catch {
    process.stdout.write("0");
  }
}
