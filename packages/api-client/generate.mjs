#!/usr/bin/env node
/**
 * Generates the API client from the API owned OpenAPI document.
 *
 *   node packages/api-client/generate.mjs          writes the generated files
 *   node packages/api-client/generate.mjs --check  fails when the committed
 *                                                 files differ from a fresh run
 *
 * The check mode is the reproducible generation oracle of the contract
 * requirement: the same API must always produce the same bytes.
 */
import { execFileSync } from "node:child_process";
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { createRequire } from "node:module";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const packageRoot = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(packageRoot, "..", "..");
const openApiPath = join(packageRoot, "openapi.json");
const schemaPath = join(packageRoot, "src", "generated", "schema.ts");
const checkOnly = process.argv.includes("--check");

// openapi-typescript is a tool of the web application, so it is resolved from
// the web application instead of adding a second dependency tree here.
const requireFromWeb = createRequire(join(repoRoot, "apps", "web", "package.json"));
const openapiTypescriptManifest = requireFromWeb.resolve("openapi-typescript/package.json");
const openapiTypescriptPackage = JSON.parse(readFileSync(openapiTypescriptManifest, "utf8"));
const openapiTypescriptEntry = resolve(
  dirname(openapiTypescriptManifest),
  openapiTypescriptPackage.exports["."].import,
);
const { default: openapiTS, astToString } = await import(pathToFileURL(openapiTypescriptEntry).href);

function generateOpenApiDocument(targetPath) {
  execFileSync(
    "dotnet",
    [
      "run",
      "--project",
      join(repoRoot, "src", "Blindmesh.Api"),
      "--no-launch-profile",
      "--",
      "openapi",
      "--output",
      targetPath,
    ],
    { cwd: repoRoot, stdio: ["ignore", "inherit", "inherit"] },
  );
}

function normalise(text) {
  return text.replace(/\r\n/g, "\n");
}

async function main() {
  const workDirectory = join(packageRoot, ".generated");

  rmSync(workDirectory, { recursive: true, force: true });
  mkdirSync(workDirectory, { recursive: true });

  const workOpenApi = join(workDirectory, "openapi.json");
  const workSchema = join(workDirectory, "schema.ts");

  generateOpenApiDocument(workOpenApi);

  const document = readFileSync(workOpenApi, "utf8");
  const schema = normalise(await astToString(await openapiTS(JSON.parse(document))));

  const artifacts = [
    { path: openApiPath, content: normalise(document) },
    { path: schemaPath, content: schema },
  ];

  if (checkOnly) {
    const differences = [];

    for (const artifact of artifacts) {
      if (!existsSync(artifact.path)) {
        differences.push(`missing: ${artifact.path}`);
        continue;
      }

      const current = normalise(readFileSync(artifact.path, "utf8"));

      if (current !== artifact.content) {
        differences.push(`changed: ${artifact.path}`);
      }
    }

    rmSync(workDirectory, { recursive: true, force: true });

    if (differences.length > 0) {
      console.error("The generated API client is out of date:");
      for (const difference of differences) {
        console.error(`  - ${difference}`);
      }

      console.error("Run `npm --prefix apps/web run api:generate` and commit the result.");
      process.exitCode = 1;
      return;
    }

    console.log("The generated API client matches a fresh generation.");
    return;
  }

  for (const artifact of artifacts) {
    mkdirSync(dirname(artifact.path), { recursive: true });
    writeFileSync(artifact.path, artifact.content, "utf8");
    console.log(`Wrote ${artifact.path}`);
  }

  rmSync(workDirectory, { recursive: true, force: true });
}

await main();
