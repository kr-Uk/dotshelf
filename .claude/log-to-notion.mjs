#!/usr/bin/env node
// DotShelf Stop hook -> Notion DB row(s).
// Hook mode: read stdin, persist payload to tmp, spawn detached worker, exit immediately.
// Worker mode: parse transcript, mask secrets, post to Notion (workspace-routed, deduped),
// retry-queue any failed posts. Flushes pending queue at the start of each worker run.
// Failures never block the agent.

import fs from "node:fs";
import path from "node:path";
import os from "node:os";
import crypto from "node:crypto";
import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";

const SCRIPT_FILE = fileURLToPath(import.meta.url);
const SCRIPT_DIR = path.dirname(SCRIPT_FILE);
const CONFIG_FILE = path.join(SCRIPT_DIR, "notion-config.json");
const LOG_FILE = path.join(os.tmpdir(), "dotshelf-notion-hook.log");
const QUEUE_FILE = path.join(os.tmpdir(), "dotshelf-notion-queue.jsonl");
const DEDUP_WINDOW_MS = 30_000;
const QUEUE_MAX = 100;
const WORKER_FLAG = "--worker";

function logLocal(level, msg, extra = {}) {
  try {
    fs.mkdirSync(path.dirname(LOG_FILE), { recursive: true });
    const line = JSON.stringify({ ts: new Date().toISOString(), level, msg, ...extra }) + "\n";
    fs.appendFileSync(LOG_FILE, line);
  } catch {}
}

function loadConfig() {
  if (fs.existsSync(CONFIG_FILE)) {
    try { return JSON.parse(fs.readFileSync(CONFIG_FILE, "utf-8")); }
    catch (e) { logLocal("warn", "config parse failed", { error: String(e) }); }
  }
  return {
    token: process.env.NOTION_TOKEN,
    databases: {
      backend: process.env.NOTION_DB_BACKEND,
      apple: process.env.NOTION_DB_APPLE,
      front: process.env.NOTION_DB_FRONT,
      unity: process.env.NOTION_DB_UNITY,
    },
  };
}

function stripInjectedTags(s) {
  if (!s) return s;
  return s
    .replace(/<ide_opened_file>[\s\S]*?<\/ide_opened_file>/g, "")
    .replace(/<ide_selection>[\s\S]*?<\/ide_selection>/g, "")
    .replace(/<system-reminder>[\s\S]*?<\/system-reminder>/g, "")
    .replace(/<command-name>[\s\S]*?<\/command-name>/g, "")
    .replace(/<command-message>[\s\S]*?<\/command-message>/g, "")
    .replace(/<command-args>[\s\S]*?<\/command-args>/g, "")
    .replace(/<local-command-stdout>[\s\S]*?<\/local-command-stdout>/g, "")
    .replace(/<user-prompt-submit-hook>[\s\S]*?<\/user-prompt-submit-hook>/g, "")
    .replace(/^[\s\n]+/, "")
    .replace(/[\s\n]+$/, "");
}

function maskSecrets(s) {
  if (!s) return s;
  return s
    .replace(/sk-ant-[A-Za-z0-9_-]{20,}/g, "[REDACTED:anthropic-key]")
    .replace(/sk-(?:proj-)?[A-Za-z0-9_-]{20,}/g, "[REDACTED:openai-key]")
    .replace(/ntn_[A-Za-z0-9]{40,}/g, "[REDACTED:notion-token]")
    .replace(/xox[baprs]-[A-Za-z0-9-]{10,}/g, "[REDACTED:slack-token]")
    .replace(/gh[psour]_[A-Za-z0-9]{30,}/g, "[REDACTED:github-token]")
    .replace(/glpat-[A-Za-z0-9_-]{20,}/g, "[REDACTED:gitlab-token]")
    .replace(/AKIA[0-9A-Z]{16}/g, "[REDACTED:aws-key]")
    .replace(/eyJ[A-Za-z0-9_-]{10,}\.eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}/g, "[REDACTED:jwt]")
    .replace(/-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?-----END [A-Z ]*PRIVATE KEY-----/g, "[REDACTED:private-key-block]")
    .replace(
      /((?:^|[\s\n,;])[A-Z][A-Z0-9_]*(?:SECRET|TOKEN|API_?KEY|PASSWORD|PASSWD|PRIVATE_KEY)[A-Z0-9_]*\s*[=:]\s*)(['"]?)([^\s'"`,;]{8,})\2/g,
      "$1$2[REDACTED]$2"
    );
}

function truncate(s, max = 1800) {
  if (!s) return "";
  if (s.length <= max) return s;
  const headLen = Math.floor((max - 30) * 2 / 3);
  const tailLen = (max - 30) - headLen;
  const omitted = s.length - headLen - tailLen;
  return s.slice(0, headLen) + `\n\n...[중간 ${omitted}자 생략]...\n\n` + s.slice(-tailLen);
}

function detectAgent(model) {
  if (!model) return "Claude Code";
  return /(^|[^a-z])(gpt|o\d|codex)/i.test(model) ? "Codex" : "Claude Code";
}

function pathWorkspace(p) {
  const n = (p || "").toLowerCase().replace(/\\/g, "/");
  if (/(^|\/)apple(\/|$)/.test(n)) return "apple";
  if (/(^|\/)frontend(\/|$)/.test(n)) return "front";
  if (/(^|\/)backend(\/|$)/.test(n)) return "backend";
  return null;
}

function isInfrastructurePath(p) {
  const n = (p || "").toLowerCase().replace(/\\/g, "/");
  return /(^|\/)\.(claude|codex|git)(\/|$)/.test(n);
}

async function decideWorkspaces(cwd, editedFiles, userText, llmToken) {
  if (process.env.WORKSPACE) return [process.env.WORKSPACE.toLowerCase()];
  const result = new Set();
  const cwdWs = pathWorkspace(cwd);
  if (cwdWs) result.add(cwdWs);
  for (const f of editedFiles) {
    if (isInfrastructurePath(f)) continue;
    const ws = pathWorkspace(f);
    if (ws) result.add(ws);
  }
  if (result.size === 0 && userText && llmToken) {
    const classified = await classifyWorkspace(llmToken, userText);
    if (classified) {
      result.add(classified);
      logLocal("info", "llm classified workspace", { workspace: classified });
    }
  }
  if (result.size === 0) result.add("backend");
  return [...result];
}

function resolveUser() {
  let v = (process.env.LOG_USER || "").trim();
  if (!v) {
    try {
      const fpath = path.join(os.homedir(), ".dotshelf", "log-user");
      if (fs.existsSync(fpath)) v = fs.readFileSync(fpath, "utf-8").trim();
    } catch {}
  }
  return v.slice(0, 80);
}

function dedupCheck(session_id, contentSig, workspace) {
  const key = crypto.createHash("sha256")
    .update(`${session_id}|${contentSig}|${workspace}`)
    .digest("hex").slice(0, 24);
  const lockFile = path.join(os.tmpdir(), `dotshelf-notion-${key}.lock`);
  try {
    if (fs.existsSync(lockFile)) {
      const age = Date.now() - fs.statSync(lockFile).mtimeMs;
      if (age < DEDUP_WINDOW_MS) return true;
    }
    fs.writeFileSync(lockFile, "");
  } catch {}
  return false;
}

function readStdin() {
  try {
    const s = fs.readFileSync(0, "utf-8");
    return s.charCodeAt(0) === 0xFEFF ? s.slice(1) : s;
  } catch (e) { logLocal("error", "stdin read failed", { error: String(e) }); return ""; }
}

function parseTranscript(transcriptPath) {
  const out = { lastUser: "", lastAsst: "", editedFiles: [] };
  if (!transcriptPath || !fs.existsSync(transcriptPath)) return out;
  try {
    const lines = fs.readFileSync(transcriptPath, "utf-8").split("\n").filter(Boolean);
    for (const line of lines) {
      let entry;
      try { entry = JSON.parse(line); } catch { continue; }
      if (entry.type === "user") {
        const c = entry.message?.content ?? entry.message;
        if (typeof c === "string") out.lastUser = c;
        else if (Array.isArray(c)) {
          const txt = c.map(x => typeof x === "string" ? x : (x.text || "")).join("").trim();
          if (txt) out.lastUser = txt;
        }
      } else if (entry.type === "assistant") {
        const content = entry.message?.content;
        if (Array.isArray(content)) {
          for (const block of content) {
            if (block.type === "text" && block.text) out.lastAsst = block.text;
            if (block.type === "tool_use") {
              const editTools = ["Edit", "Write", "MultiEdit", "NotebookEdit", "apply_patch"];
              if (editTools.includes(block.name)) {
                const fp = block.input?.file_path || block.input?.notebook_path || block.input?.path;
                if (fp) out.editedFiles.push(fp);
              }
            }
          }
        }
      }
    }
  } catch (e) {
    logLocal("warn", "transcript parse failed", { error: String(e) });
  }
  return out;
}

async function callAnthropic(token, model, system, userMsg, maxTokens) {
  const res = await fetch("https://api.anthropic.com/v1/messages", {
    method: "POST",
    headers: {
      "x-api-key": token,
      "anthropic-version": "2023-06-01",
      "content-type": "application/json",
    },
    body: JSON.stringify({
      model,
      max_tokens: maxTokens,
      system,
      messages: [{ role: "user", content: userMsg }],
    }),
  });
  if (!res.ok) {
    const txt = await res.text();
    throw new Error(`HTTP ${res.status}: ${txt.slice(0, 400)}`);
  }
  const json = await res.json();
  return (json.content?.[0]?.text || "").trim();
}

async function generateBrief(token, userText, asstText) {
  if (!token) return "";
  if (!userText && !asstText) return "";
  try {
    const body = `사용자 프롬프트:\n${userText.slice(0, 2000)}\n\n어시스턴트 응답:\n${asstText.slice(0, 2000)}`;
    return await callAnthropic(
      token,
      "claude-haiku-4-5-20251001",
      "이 Claude Code 세션 한 턴을 1~2 문장 한국어로 요약하라. 사용자가 무엇을 요청했고 어시스턴트가 무엇을 했는지에 집중하라. 불릿 사용 금지. 군더더기 금지.",
      body,
      200
    );
  } catch (e) {
    logLocal("warn", "brief generation failed", { error: String(e) });
    return "";
  }
}

async function classifyWorkspace(token, userText) {
  if (!token || !userText) return null;
  try {
    const result = await callAnthropic(
      token,
      "claude-haiku-4-5-20251001",
      "Classify which workspace this user message relates to in a monorepo with three options: apple (iOS/SwiftUI/Swift), backend (Spring Boot/Java/Postgres/Redis), frontend (web/Vite/React/Unity). Answer with EXACTLY one word: apple, backend, or frontend. If unclear, answer backend.",
      userText.slice(0, 1500),
      10
    );
    const ans = result.toLowerCase().replace(/[^a-z]/g, "");
    if (ans === "apple") return "apple";
    if (ans === "backend") return "backend";
    if (ans === "frontend") return "front";
    return null;
  } catch (e) {
    logLocal("warn", "workspace classify failed", { error: String(e) });
    return null;
  }
}

async function postToNotion(token, dbId, props) {
  const res = await fetch("https://api.notion.com/v1/pages", {
    method: "POST",
    headers: {
      Authorization: `Bearer ${token}`,
      "Notion-Version": "2022-06-28",
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ parent: { database_id: dbId }, properties: props }),
  });
  if (!res.ok) {
    const txt = await res.text();
    throw new Error(`HTTP ${res.status}: ${txt.slice(0, 400)}`);
  }
  return res.json();
}

function enqueueFailure(dbId, properties) {
  try {
    const entry = JSON.stringify({ dbId, properties, ts: new Date().toISOString() });
    fs.appendFileSync(QUEUE_FILE, entry + "\n");
  } catch (e) {
    logLocal("error", "queue write failed", { error: String(e) });
  }
}

async function flushQueue(token) {
  if (!fs.existsSync(QUEUE_FILE)) return;
  let lines;
  try { lines = fs.readFileSync(QUEUE_FILE, "utf-8").split("\n").filter(Boolean); }
  catch (e) { logLocal("warn", "queue read failed", { error: String(e) }); return; }
  if (lines.length === 0) {
    try { fs.unlinkSync(QUEUE_FILE); } catch {}
    return;
  }
  const remaining = [];
  let flushed = 0;
  for (const line of lines) {
    let entry;
    try { entry = JSON.parse(line); } catch { continue; }
    try {
      await postToNotion(token, entry.dbId, entry.properties);
      flushed++;
    } catch {
      remaining.push(line);
    }
  }
  if (flushed) logLocal("info", "queue flushed", { flushed, remaining: remaining.length });
  try {
    if (remaining.length === 0) fs.unlinkSync(QUEUE_FILE);
    else fs.writeFileSync(QUEUE_FILE, remaining.slice(-QUEUE_MAX).join("\n") + "\n");
  } catch {}
}

async function runWorker(payload) {
  const {
    session_id = "",
    transcript_path = "",
    cwd = "",
    model = "",
    last_assistant_message = "",
  } = payload;

  const cfg = loadConfig();
  if (!cfg.token) { logLocal("error", "no token configured"); return; }

  await flushQueue(cfg.token);

  const t = parseTranscript(transcript_path);
  const lastUser = truncate(maskSecrets(stripInjectedTags(t.lastUser)));
  const lastAsst = truncate(maskSecrets(stripInjectedTags(last_assistant_message || t.lastAsst)));

  if (!lastUser && !lastAsst && t.editedFiles.length === 0) {
    logLocal("warn", "nothing to log; skipping", { hadTranscript: !!transcript_path });
    return;
  }

  const briefPromise = generateBrief(cfg.llm_token, lastUser, lastAsst);
  const workspaces = await decideWorkspaces(cwd, t.editedFiles, lastUser, cfg.llm_token);
  const brief = await briefPromise;

  const title = (lastUser.split("\n").find(l => l.trim()) || "(empty)").trim().slice(0, 80);
  const project = path.basename(cwd || "unknown");
  const filesSummary = [...new Set(t.editedFiles)].slice(0, 10).join(",").slice(0, 1800);
  const agent = detectAgent(model);
  const contentSig = (lastAsst || lastUser || "no-content").slice(0, 200);
  const user = resolveUser();

  for (const workspace of workspaces) {
    if (dedupCheck(session_id, contentSig, workspace)) {
      logLocal("info", "dedup skip", { workspace, session: session_id.slice(0, 8) });
      continue;
    }
    const dbId = cfg.databases?.[workspace];
    if (!dbId) {
      logLocal("error", "no db for workspace", { workspace });
      continue;
    }
    const properties = {
      Name:      { title:     [{ text: { content: title || "(empty)" } }] },
      Agent:     { select:    { name: agent } },
      Project:   { rich_text: [{ text: { content: project } }] },
      Session:   { rich_text: [{ text: { content: session_id } }] },
      Command:   { rich_text: [{ text: { content: lastUser } }] },
      Summary:   { rich_text: [{ text: { content: lastAsst } }] },
      Files:     { rich_text: [{ text: { content: filesSummary } }] },
      Timestamp: { date:      { start: new Date().toISOString() } },
    };
    if (user) properties.User = { select: { name: user } };
    if (brief) properties.Brief = { rich_text: [{ text: { content: brief.slice(0, 1800) } }] };
    try {
      const result = await postToNotion(cfg.token, dbId, properties);
      logLocal("info", "logged", { agent, workspace, project, title, files: filesSummary.length, brief: brief.length, page: result.id });
    } catch (e) {
      logLocal("error", "notion post failed; enqueuing", { error: String(e), workspace });
      enqueueFailure(dbId, properties);
    }
  }
}

async function main() {
  if (process.argv[2] === WORKER_FLAG) {
    const payloadFile = process.argv[3];
    let payload = {};
    try { payload = JSON.parse(fs.readFileSync(payloadFile, "utf-8")); }
    catch (e) { logLocal("error", "worker payload read failed", { error: String(e) }); return; }
    try { await runWorker(payload); }
    finally { try { fs.unlinkSync(payloadFile); } catch {} }
    return;
  }

  const raw = readStdin();
  if (!raw) { logLocal("warn", "no stdin payload"); return; }
  let payload;
  try { payload = JSON.parse(raw); }
  catch (e) { logLocal("error", "stdin not json", { raw: raw.slice(0, 200) }); return; }

  const tmpPayload = path.join(os.tmpdir(), `dotshelf-payload-${Date.now()}-${process.pid}.json`);
  try { fs.writeFileSync(tmpPayload, JSON.stringify(payload)); }
  catch (e) {
    logLocal("error", "tmp payload write failed; running inline", { error: String(e) });
    await runWorker(payload);
    return;
  }
  try {
    const child = spawn(process.execPath, [SCRIPT_FILE, WORKER_FLAG, tmpPayload], {
      detached: true,
      stdio: "ignore",
      windowsHide: true,
    });
    child.unref();
  } catch (e) {
    logLocal("error", "spawn failed; running inline", { error: String(e) });
    try { fs.unlinkSync(tmpPayload); } catch {}
    await runWorker(payload);
  }
}

main().catch(e => logLocal("error", "unhandled", { error: String(e) }));
