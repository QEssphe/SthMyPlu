import { spawn } from "node:child_process";
import { createInterface } from "node:readline";
import { existsSync, readdirSync, renameSync, statSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const outPath = process.argv[2] || join(here, "quota-live.json");
const codexPath = resolveCodexPath();

let proc = null;
let nextId = 2;
let lastRequestId = 0;

function writeState(state) {
  const payload = JSON.stringify({
    receivedAt: new Date().toISOString(),
    ...state
  }, null, 2);
  const tempPath = `${outPath}.${process.pid}.tmp`;
  writeFileSync(tempPath, payload);
  renameSync(tempPath, outPath);
}

function resolveCodexPath() {
  if (process.env.CODEX_EXE && existsSync(process.env.CODEX_EXE)) {
    return process.env.CODEX_EXE;
  }

  const roots = [
    process.env.LOCALAPPDATA && join(process.env.LOCALAPPDATA, "OpenAI", "Codex", "bin"),
    process.env.ProgramFiles && join(process.env.ProgramFiles, "OpenAI", "Codex", "bin"),
  ].filter(Boolean);

  for (const root of roots) {
    try {
      if (!existsSync(root)) continue;
      const direct = join(root, "codex.exe");
      if (existsSync(direct)) return direct;

      const candidates = readdirSync(root)
        .map(name => join(root, name, "codex.exe"))
        .filter(path => existsSync(path))
        .map(path => ({ path, mtime: statSync(path).mtimeMs }))
        .sort((a, b) => b.mtime - a.mtime);

      if (candidates.length) return candidates[0].path;
    } catch {
      // Try the next known location.
    }
  }

  return "codex.exe";
}

function startServer() {
  writeState({ ok: false, error: "Starting Codex app-server transport." });
  proc = spawn(codexPath, ["app-server"], {
    stdio: ["pipe", "pipe", "pipe"],
    windowsHide: true
  });

  let stderr = "";
  const rl = createInterface({ input: proc.stdout });

  proc.stderr.on("data", chunk => {
    stderr += chunk.toString();
  });

  proc.on("error", error => {
    writeState({ ok: false, error: error.message });
    setTimeout(startServer, 10000);
  });

  proc.on("exit", code => {
    writeState({ ok: false, error: `codex app-server exited with ${code}`, stderr: stderr.trim() });
    setTimeout(startServer, 10000);
  });

  function send(message) {
    if (proc && !proc.killed) {
      proc.stdin.write(`${JSON.stringify(message)}\n`);
    }
  }

  function requestRateLimits() {
    lastRequestId = nextId++;
    send({ method: "account/rateLimits/read", id: lastRequestId, params: {} });
  }

  rl.on("line", line => {
    let msg;
    try {
      msg = JSON.parse(line);
    } catch {
      return;
    }

    if (msg.id === 1) {
      send({ method: "initialized", params: {} });
      requestRateLimits();
      return;
    }

    if (msg.id === lastRequestId) {
      if (msg.error) {
        writeState({ ok: false, error: msg.error, stderr: stderr.trim() });
      } else {
        writeState({ ok: true, result: msg.result });
      }
    }
  });

  send({
    method: "initialize",
    id: 1,
    params: {
      clientInfo: {
        name: "quota_liquid_orb",
        title: "Quota Liquid Orb",
        version: "0.2.0"
      },
      capabilities: {
        experimentalApi: true
      }
    }
  });

  setInterval(requestRateLimits, 60000);
}

writeState({ ok: false, error: "Starting Codex rate limit poller." });
startServer();
