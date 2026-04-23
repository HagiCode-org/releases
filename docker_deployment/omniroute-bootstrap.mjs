import fs from "node:fs/promises";
import path from "node:path";

const fetchJson = async (url, options = {}) => {
  const response = await fetch(url, options);
  const text = await response.text();
  let json = null;

  if (text) {
    try {
      json = JSON.parse(text);
    } catch {
      json = null;
    }
  }

  return { response, json, text };
};

const normalizeBaseUrl = (value) => {
  if (!value || typeof value !== "string") {
    return "";
  }

  return value.trim().replace(/\/+$/, "");
};

const buildOpenAiNodePayload = (provider, baseUrl, apiType = "chat") => ({
  type: "openai-compatible",
  name: provider === "codex" ? "Codex Local Node" : "OpenCode Local Node",
  prefix: provider,
  apiType,
  baseUrl,
  ...(apiType === "responses" ? { chatPath: "/responses" } : {}),
});

const buildAnthropicNodePayload = (provider, baseUrl) => ({
  type: "anthropic-compatible",
  name: "Claude Local Node",
  prefix: provider,
  baseUrl,
});

const ensureDirectory = async (directoryPath) => {
  await fs.mkdir(directoryPath, { recursive: true });
};

const readJsonFile = async (filePath, fallbackValue) => {
  try {
    const content = await fs.readFile(filePath, "utf8");
    return JSON.parse(content);
  } catch {
    return fallbackValue;
  }
};

const writeJsonFile = async (filePath, value) => {
  await fs.writeFile(filePath, `${JSON.stringify(value, null, 2)}\n`, "utf8");
};

const getCookieHeader = async (baseUrl, password) => {
  const { response, json, text } = await fetchJson(`${baseUrl}/api/auth/login`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Accept: "application/json",
    },
    body: JSON.stringify({ password }),
  });

  if (!response.ok) {
    throw new Error(`Omniroute login failed (${response.status}): ${text || JSON.stringify(json)}`);
  }

  const setCookie = response.headers.get("set-cookie");

  if (!setCookie) {
    throw new Error("Omniroute login did not return an auth cookie");
  }

  return setCookie.split(";")[0];
};

const omnirouteRequest = async (baseUrl, cookieHeader, pathname, init = {}) => {
  const headers = new Headers(init.headers || {});
  headers.set("Cookie", cookieHeader);
  headers.set("Accept", "application/json");

  if (init.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const { response, json, text } = await fetchJson(`${baseUrl}${pathname}`, {
    ...init,
    headers,
  });

  if (!response.ok) {
    throw new Error(`Omniroute request failed for ${pathname} (${response.status}): ${text || JSON.stringify(json)}`);
  }

  return json;
};

const findNodeByPrefix = async (baseUrl, cookieHeader, prefix, type) => {
  const payload = await omnirouteRequest(baseUrl, cookieHeader, "/api/provider-nodes");
  const nodes = Array.isArray(payload?.nodes) ? payload.nodes : [];
  return nodes.find((node) => node?.prefix === prefix && (!type || node?.type === type)) || null;
};

const findConnection = async (baseUrl, cookieHeader, provider, name) => {
  const payload = await omnirouteRequest(baseUrl, cookieHeader, "/api/providers");
  const connections = Array.isArray(payload?.connections) ? payload.connections : [];
  return connections.find((connection) => connection?.provider === provider && connection?.name === name) || null;
};

const upsertNode = async (baseUrl, cookieHeader, provider, payload) => {
  const existing = await findNodeByPrefix(baseUrl, cookieHeader, provider, payload.type);

  if (existing) {
    await omnirouteRequest(baseUrl, cookieHeader, `/api/provider-nodes/${existing.id}`, {
      method: "PUT",
      body: JSON.stringify(payload),
    });

    return { action: "updated", id: existing.id };
  }

  const created = await omnirouteRequest(baseUrl, cookieHeader, "/api/provider-nodes", {
    method: "POST",
    body: JSON.stringify(payload),
  });

  return { action: "created", id: created?.node?.id ?? null };
};

const upsertConnection = async (baseUrl, cookieHeader, payload) => {
  const existing = await findConnection(baseUrl, cookieHeader, payload.provider, payload.name);

  if (existing) {
    await omnirouteRequest(baseUrl, cookieHeader, `/api/providers/${existing.id}`, {
      method: "PUT",
      body: JSON.stringify({
        apiKey: payload.apiKey,
        defaultModel: payload.defaultModel ?? null,
        priority: payload.priority,
        globalPriority: payload.globalPriority ?? null,
        isActive: true,
        providerSpecificData: payload.providerSpecificData ?? {},
      }),
    });

    return { action: "updated", id: existing.id };
  }

  const created = await omnirouteRequest(baseUrl, cookieHeader, "/api/providers", {
    method: "POST",
    body: JSON.stringify(payload),
  });

  return { action: "created", id: created?.connection?.id ?? null };
};

const collectBootstrapTargets = () => {
  const claudeBaseUrl = normalizeBaseUrl(process.env.OMNIROUTE_CLAUDE_UPSTREAM_BASE_URL || process.env.ANTHROPIC_URL);
  const claudeApiKey = process.env.OMNIROUTE_CLAUDE_UPSTREAM_AUTH_TOKEN || process.env.ANTHROPIC_AUTH_TOKEN;
  const codexBaseUrl = normalizeBaseUrl(
    process.env.OMNIROUTE_CODEX_UPSTREAM_BASE_URL ||
      process.env.CODEX_BASE_URL ||
      process.env.OPENAI_BASE_URL,
  );
  const codexApiKey =
    process.env.OMNIROUTE_CODEX_UPSTREAM_API_KEY || process.env.CODEX_API_KEY || process.env.OPENAI_API_KEY;
  const openCodeBaseUrl = normalizeBaseUrl(
    process.env.OMNIROUTE_OPENCODE_UPSTREAM_BASE_URL ||
      process.env.OPENCODE_BASE_URL ||
      process.env.OPENCODE_API_BASE_URL ||
      process.env.OPENCODE_BASE_URL_COMPAT,
  );
  const openCodeApiKey = process.env.OMNIROUTE_OPENCODE_UPSTREAM_API_KEY || process.env.OPENCODE_API_KEY;

  return [
    {
      provider: "claude",
      enabled: Boolean(claudeApiKey && claudeBaseUrl),
      skipReason: !claudeApiKey
        ? "missing ANTHROPIC_AUTH_TOKEN or OMNIROUTE_CLAUDE_UPSTREAM_AUTH_TOKEN"
        : !claudeBaseUrl
          ? "missing Anthropic upstream base URL"
          : null,
      nodePayload: claudeBaseUrl ? buildAnthropicNodePayload("claude", claudeBaseUrl) : null,
      connectionPayload: {
        provider: "anthropic",
        name: "Claude Bootstrap",
        apiKey: claudeApiKey || "",
        priority: 1,
        defaultModel: process.env.ANTHROPIC_SONNET_MODEL || process.env.ANTHROPIC_OPUS_MODEL || null,
      },
    },
    {
      provider: "codex",
      enabled: Boolean(codexApiKey && codexBaseUrl),
      skipReason: !codexApiKey
        ? "missing CODEX_API_KEY, OPENAI_API_KEY, or OMNIROUTE_CODEX_UPSTREAM_API_KEY"
        : !codexBaseUrl
          ? "missing Codex/OpenAI upstream base URL"
          : null,
      nodePayload: codexBaseUrl ? buildOpenAiNodePayload("codex", codexBaseUrl, "responses") : null,
      connectionPayload: {
        provider: "openai",
        name: "Codex Bootstrap",
        apiKey: codexApiKey || "",
        priority: 1,
        defaultModel: process.env.CODEX_MODEL || process.env.OPENAI_MODEL || null,
        providerSpecificData: {
          requestDefaults: {
            reasoningEffort: process.env.CODEX_REASONING_EFFORT || undefined,
          },
        },
      },
    },
    {
      provider: "opencode",
      enabled: Boolean(openCodeApiKey && openCodeBaseUrl),
      skipReason: !openCodeApiKey
        ? "missing OPENCODE_API_KEY or OMNIROUTE_OPENCODE_UPSTREAM_API_KEY"
        : !openCodeBaseUrl
          ? "missing OpenCode upstream base URL"
          : null,
      nodePayload: openCodeBaseUrl ? buildOpenAiNodePayload("opencode", openCodeBaseUrl, "chat") : null,
      connectionPayload: {
        provider: "openai",
        name: "OpenCode Bootstrap",
        apiKey: openCodeApiKey || "",
        priority: 1,
        defaultModel: process.env.OPENCODE_MODEL || null,
      },
    },
  ];
};

const main = async () => {
  const stateDir = process.env.OMNIROUTE_STATE_DIR;
  const bootstrapStateFile = process.env.OMNIROUTE_BOOTSTRAP_STATE_FILE;
  const readyFile = process.env.OMNIROUTE_READY_FILE;
  const baseUrl = normalizeBaseUrl(process.env.OMNIROUTE_BASE_URL);
  const managementPassword = process.env.OMNIROUTE_INITIAL_PASSWORD;

  if (!stateDir || !bootstrapStateFile || !readyFile || !baseUrl || !managementPassword) {
    throw new Error("Missing Omniroute bootstrap environment");
  }

  await ensureDirectory(stateDir);
  await ensureDirectory(path.dirname(bootstrapStateFile));
  await ensureDirectory(path.dirname(readyFile));

  const previousState = await readJsonFile(bootstrapStateFile, {
    syncedProviders: {},
    runs: [],
  });

  const cookieHeader = await getCookieHeader(baseUrl, managementPassword);
  const targets = collectBootstrapTargets();
  const syncedProviders = { ...previousState.syncedProviders };
  const runSummary = [];

  for (const target of targets) {
    if (!target.enabled) {
      runSummary.push({
        provider: target.provider,
        status: "skipped",
        reason: target.skipReason,
      });
      continue;
    }

    const nodeResult = await upsertNode(baseUrl, cookieHeader, target.provider, target.nodePayload);
    const connectionResult = await upsertConnection(baseUrl, cookieHeader, target.connectionPayload);

    syncedProviders[target.provider] = {
      lastSyncedAt: new Date().toISOString(),
      node: nodeResult,
      connection: connectionResult,
    };

    runSummary.push({
      provider: target.provider,
      status: "synced",
      node: nodeResult,
      connection: connectionResult,
    });
  }

  const nextState = {
    syncedProviders,
    runs: [
      ...(Array.isArray(previousState.runs) ? previousState.runs : []).slice(-9),
      {
        executedAt: new Date().toISOString(),
        summary: runSummary,
      },
    ],
  };

  await writeJsonFile(bootstrapStateFile, nextState);
  await fs.writeFile(readyFile, `${new Date().toISOString()}\n`, "utf8");
};

main().catch((error) => {
  console.error("[omniroute-bootstrap]", error instanceof Error ? error.message : String(error));
  process.exitCode = 1;
});
