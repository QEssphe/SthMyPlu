import { spawn } from "node:child_process";
import { createInterface } from "node:readline";
import { existsSync, readdirSync, statSync, writeFileSync } from "node:fs";
import { join } from "node:path";

const codexPath = resolveCodexPath();
const outPath = process.argv[2] || "quota-live.json";
const proc = spawn(codexPath, ["app-server"], {
  stdio: ["pipe", "pipe", "pipe"],
  windowsHide: true
});

const rl = createInterface({ input: proc.stdout });
let stderr = "";
let done = false;

proc.stderr.on("data", chunk => {
  stderr += chunk.toString();
});

function send(message) {
  proc.stdin.write(`${JSON.stringify(message)}\n`);
}

function finish(payload, code = 0) {
  if (done) return;
  done = true;
  writeFileSync(outPath, JSON.stringify(payload, null, 2));
  proc.kill();
  process.exit(code);
}

const timeout = setTimeout(() => {
  finish({
    ok: false,
    error: "Timed out while reading Codex rate limits.",
    stderr: stderr.trim()
  }, 2);
}, 15000);

rl.on("line", line => {
  let msg;
  try {
    msg = JSON.parse(line);
  } catch {
    return;
  }

  if (msg.id === 1) {
    send({ method: "initialized", params: {} });
    send({ method: "account/rateLimits/read", id: 2, params: {} });
    return;
  }

  if (msg.id === 2) {
    clearTimeout(timeout);
    if (msg.error) {
      finish({ ok: false, error: msg.error, stderr: stderr.trim() }, 1);
    } else {
      finish({ ok: true, receivedAt: new Date().toISOString(), result: msg.result }, 0);
    }
  }
});

proc.on("error", error => {
  clearTimeout(timeout);
  finish({ ok: false, error: error.message, stderr: stderr.trim() }, 1);
});

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
