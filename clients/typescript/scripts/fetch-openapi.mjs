import { writeFile } from "node:fs/promises";

const url = process.env.OPENAPI_URL ?? "http://localhost:5000/openapi/v1.json";

const res = await fetch(url);
if (!res.ok) {
  console.error(`Could not fetch ${url}: ${res.status}. Is the API running in Development?`);
  process.exit(1);
}

await writeFile("openapi.json", JSON.stringify(await res.json(), null, 2));
console.log(`Saved openapi.json from ${url}`);