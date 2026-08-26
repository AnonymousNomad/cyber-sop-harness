import http from 'node:http';
import path from 'node:path';
import fs from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { WebSocketServer } from 'ws';

import { captureDeviceProfile } from './lib/device-profile.mjs';
import { createFileBoundary, PathEscapeError } from './lib/file-boundary.mjs';
import { createCipherBus } from './model/cipher-state.mjs';
import { createModelProvider, createRemoteModelProvider, ModelProviderError } from './model/provider.mjs';
import { createContextManager } from './model/context-manager.mjs';
import { createVoiceAssistant, WAKE_WORDS } from './model/voice-assistant.mjs';
import { MessageTypes, validateMessage, createConnectionManager, sendTo } from './lib/ws-protocol.mjs';
import { createPolicyEngine } from './governance/policy-engine.mjs';
import { createPermitIssuer } from './governance/permit-issuer.mjs';
import { createEvidenceChain } from './governance/evidence-chain.mjs';
import { createSecretVault } from './governance/secret-vault.mjs';
import { defineAdapter, createAdapterRegistry } from './tools/registry.mjs';
import { createDnsReverseAdapter } from './tools/adapters/dns-reverse.mjs';
import { createHttpHeadersAdapter } from './tools/adapters/http-headers.mjs';
import { sanitizeObject } from './tools/sanitizer.mjs';
import { createFileWatcher } from './autodebug/watcher.mjs';
import { checkFile } from './autodebug/detector.mjs';
import { createAutoFixer } from './autodebug/fixer.mjs';
import { createNotifier } from './autodebug/notifier.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const HOST = '127.0.0.1';
const PORT = parseInt(process.env.PORT || '7420', 10);

const state = {
  profile: null,
  boundary: null,
  cipher: null,
  modelProvider: null,
  remoteProvider: null,
  contextManager: null,
  voiceAssistant: null,
  policyEngine: null,
  permitIssuer: null,
  evidenceChain: null,
  secretVault: null,
  toolRegistry: null,
  engagementManifest: null,
  fileWatcher: null,
  autoFixer: null,
  notifier: null,
  wsClients: new Set(),
  startedAt: Date.now(),
  ready: false,
};

const VERSION = '0.1.0';

async function boot() {
  console.log(`Edge AIDE Cybersecurity Workbench v${VERSION}`);
  console.log('Boot sequence starting...');

  state.profile = captureDeviceProfile();
  console.log(`  device: ${state.profile.arch} / ${state.profile.cpuCount} cpus / ${Math.round(state.profile.totalMemBytes / 1024 / 1024)} MiB`);

  state.boundary = createFileBoundary(path.join(state.profile.hostname === 'localhost' ? '/root' : '/home', '.edge-cyber'));
  await state.boundary.mkdir('');
  await state.boundary.mkdir('data');
  await state.boundary.mkdir('evidence');
  console.log(`  workspace jail: ${state.boundary.root}`);

  state.cipher = createCipherBus(state.boundary);
  await state.cipher.init();
  const recentEvents = await state.cipher.query({ limit: 5 });
  console.log(`  cipher bus: ${recentEvents.length > 0 ? `resumed (${recentEvents.length} recent events)` : 'initialized'}`);

  state.modelProvider = createModelProvider({
    host: process.env.LLAMA_HOST || 'http://127.0.0.1:8081',
    maxTokens: 512,
  });
  const modelHealthy = await state.modelProvider.checkHealth();
  console.log(`  model runtime: ${modelHealthy ? 'connected' : 'not running (start llama-server)'}`);

  if (process.env.REMOTE_MODEL_HOST) {
    state.remoteProvider = createRemoteModelProvider({
      host: process.env.REMOTE_MODEL_HOST,
      modelName: process.env.REMOTE_MODEL_NAME || 'north-mini-code',
      apiKey: process.env.REMOTE_MODEL_KEY || null,
    });
    const remoteHealthy = await state.remoteProvider.checkHealth();
    console.log(`  remote model: ${remoteHealthy ? `connected (${state.remoteProvider.name})` : 'not reachable'}`);
  } else {
    console.log('  remote model: not configured (set REMOTE_MODEL_HOST)');
  }

  state.contextManager = createContextManager({
    maxTokens: 4096,
    systemPrompt: [
      'You are Cipher, a cybersecurity operations assistant for authorized defensive testing.',
      'You propose actions through structured JSON. You never execute tools directly.',
      'The policy engine and permit system control all execution authority.',
      'When uncertain, say UNKNOWN rather than guessing.',
      'Always reference evidence IDs when citing findings.',
    ].join(' '),
  });
  console.log(`  context manager: ${state.contextManager.stats().budget} token budget`);

  state.voiceAssistant = createVoiceAssistant({
    modelProvider: state.modelProvider,
    contextManager: state.contextManager,
    policyEngine: null,
    evidenceChain: { append: async (e) => state.cipher.append(e) },
  });
  await state.voiceAssistant.detectCapabilities();
  const caps = state.voiceAssistant.capabilities;
  console.log(`  voice assistant: stt=${caps.stt ? '✓' : '✗'} tts=${caps.tts ? '✓' : '✗'} wake=${caps.wakeWord ? '✓' : '✗'}`);

  state.evidenceChain = createEvidenceChain(state.boundary);
  await state.evidenceChain.init();
  console.log(`  evidence chain: ${state.evidenceChain.length} entries`);

  let manifest = null;
  try {
    const raw = await state.boundary.readFile('engagement.json');
    manifest = JSON.parse(raw);
    state.policyEngine = createPolicyEngine(manifest);
    state.engagementManifest = manifest;
    console.log(`  engagement: ${manifest.id}`);
  } catch {
    state.policyEngine = createPolicyEngine({
      id: 'no-engagement',
      operatorId: 'system',
      expiresAt: new Date(0).toISOString(),
      scope: [],
      allowedCapabilities: [],
      authorizedRiskLevels: [],
    });
    console.log('  engagement: none loaded (all actions denied)');
  }

  state.permitIssuer = createPermitIssuer({ ttlMs: 30000 });
  console.log('  permits: issuer ready (30s TTL)');

  if (process.env.VAULT_PASSPHRASE) {
    try {
      state.secretVault = createSecretVault(state.boundary, process.env.VAULT_PASSPHRASE);
      await state.secretVault.init();
      const secretCount = (await state.secretVault.listSecrets()).length;
      console.log(`  secrets: ${secretCount} stored`);
    } catch {
      console.log('  secrets: vault error');
    }
  } else {
  console.log('  secrets: not configured (set VAULT_PASSPHRASE)');

  const adapters = [
    createDnsReverseAdapter(),
    createHttpHeadersAdapter(),
  ];
  state.toolRegistry = createAdapterRegistry(adapters);
  console.log(`  tools: ${state.toolRegistry.size} adapters registered`);

  state.sopCompiler = new SOPCompiler(state.toolRegistry);
  state.coverageLedger = new CoverageLedger(state.cipher);
  console.log('  sop engine: ready');

  try {
    const sopDir = path.join(state.boundary.root, 'sops');
    const { readdir } = await import('node:fs/promises');
    const sopFiles = await readdir(sopDir).catch(() => []);
    for (const file of sopFiles) {
      if (file.endsWith('.sop.json')) {
        try {
          const raw = await state.boundary.readFile(path.join('sops', file));
          const sop = JSON.parse(raw);
          state.sopCompiler.load(sop);
          console.log(`  sop: loaded ${sop.id}`);
        } catch (err) {
          console.log(`  sop: failed to load ${file}: ${err.message}`);
        }
      }
    }
  } catch {}

  state.fileWatcher = createFileWatcher(state.boundary, { pollInterval: 3000 });
  state.autoFixer = createAutoFixer({
    modelProvider: state.modelProvider,
    fileBoundary: state.boundary,
    checkFile,
    evidenceChain: state.evidenceChain,
  });
  state.notifier = createNotifier();

  state.fileWatcher.start(async (change) => {
    if (change.path.includes('.edge-cyber') || change.path.includes('evidence/')) return;
    const issues = await state.autoFixer.analyze([change]);
    if (issues.length > 0) {
      state.notifier.notifyErrorsDetected(state.wsClients, issues);
      for (const issue of issues) {
        state.autoFixer.registerPending(issue.path, issue.errors);
      }
    }
  });
  console.log('  autodebug: watching for file changes');
  }

  await new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(PORT, HOST, () => {
      server.removeListener('error', reject);
      resolve();
    });
  });

  state.ready = true;
  console.log('  governance: ready (fail-closed)');
  console.log(`  listening on http://${HOST}:${PORT}`);
}

function requestHandler(req, res) {
  if (req.url === '/api/health') {
    res.setHeader('Content-Type', 'application/json');
    res.end(JSON.stringify({
      status: state.ready ? 'ready' : 'booting',
      version: VERSION,
      device: state.profile,
      uptimeSeconds: Math.floor((Date.now() - state.startedAt) / 1000),
    }));
    return;
  }

  if (req.url === '/' || req.url === '/index.html') {
    fs.readFile(path.join(__dirname, '..', 'public', 'index.html'), 'utf8')
      .then(html => {
        res.setHeader('Content-Type', 'text/html');
        res.setHeader('Content-Security-Policy', "default-src 'self'; script-src 'unsafe-inline'; style-src 'unsafe-inline'");
        res.setHeader('X-Frame-Options', 'DENY');
        res.setHeader('X-Content-Type-Options', 'nosniff');
        res.end(html);
      })
      .catch(() => {
        res.statusCode = 500;
        res.setHeader('Content-Type', 'application/json');
        res.end(JSON.stringify({ error: 'UI not found' }));
      });
    return;
  }

  res.statusCode = 404;
  res.setHeader('Content-Type', 'application/json');
  res.end(JSON.stringify({ error: 'not found' }));
}

const server = http.createServer(requestHandler);

const wss = new WebSocketServer({
  server,
  maxPayload: 1024 * 1024,
  perMessageDeflate: false,
});

const connections = createConnectionManager(wss);

wss.on('connection', (ws, req) => {
  if (req.socket.remoteAddress !== '127.0.0.1') {
    ws.close(4003, 'loopback only');
    return;
  }
  if (!connections.canAccept()) {
    ws.close(4029, 'max connections reached');
    return;
  }
  connections.onConnect(ws);
  state.wsClients.add(ws);
  ws.on('close', () => state.wsClients.delete(ws));

  sendTo(ws, MessageTypes.READY, {
    version: VERSION,
    device: state.profile,
    workspaceRoot: state.boundary?.root || null,
  });

  ws.on('message', (raw) => {
    const validation = validateMessage(raw);
    if (!validation.ok) {
      sendTo(ws, MessageTypes.ERROR, validation);
      return;
    }
    const msg = validation.message;
    if (msg.type === 'autodebug.fix') {
      handleAutoFix(ws, msg);
    } else {
      handleCommand(ws, msg);
    }
  });

  ws.on('error', () => {});
});

const commandHistory = [];
const MAX_HISTORY = 200;

async function handleAutoFix(ws, msg) {
  const filePath = msg.payload?.path;
  if (!filePath) {
    sendTo(ws, MessageTypes.ERROR, { code: 'MISSING_PATH', message: 'provide {path} to fix' });
    return;
  }
  const pending = state.autoFixer.pending.find(p => p.filePath === filePath);
  if (!pending) {
    sendTo(ws, MessageTypes.OUTPUT, { text: `no pending fixes for ${filePath}` });
    return;
  }
  sendTo(ws, MessageTypes.OUTPUT, { text: `fixing ${filePath}...`, color: 'color:#B26818' });
  const result = await state.autoFixer.attemptFix(filePath, pending.errors);
  state.autoFixer.resolvePending(filePath);
  state.notifier.notifyFixResult(state.wsClients, result);
}

function handleCommand(ws, msg) {
  switch (msg.type) {
    case MessageTypes.PING:
      sendTo(ws, 'pong', { at: new Date().toISOString() });
      break;

    case MessageTypes.COMMAND: {
      const text = String(msg.payload?.text || '').trim();
      if (!text) return;

      commandHistory.push(text);
      while (commandHistory.length > MAX_HISTORY) commandHistory.shift();

      state.cipher.append({ type: 'command', text }).catch(() => {});

      if (text.startsWith('/')) {
        handleSlashCommand(ws, text);
      } else {
        sendTo(ws, MessageTypes.OUTPUT, { text: `unknown command: ${text}. Type /help for commands.` });
      }
      break;
    }

    case MessageTypes.STATUS:
      sendTo(ws, MessageTypes.STATUS, getStatus());
      break;

    case MessageTypes.VIEW_DATA: {
      const viewName = msg.payload?.view;
      const data = getViewData(viewName);
      sendTo(ws, MessageTypes.VIEW_DATA, { view: viewName, data });
      break;
    }

    default:
      sendTo(ws, MessageTypes.ERROR, { code: 'NOT_IMPLEMENTED', message: `${msg.type} handler pending` });
  }
}

async function handleSlashCommand(ws, input) {
  const [cmd, ...args] = input.split(/\s+/);

  switch (cmd) {
    case '/help':
      sendTo(ws, MessageTypes.OUTPUT, {
        text: [
          'Commands:',
          '  /help          show this help',
          '  /status        system status',
          '  /device        device profile',
          '  /history       recent commands',
          '  /clear         clear terminal',
          '',
          'Model commands:',
          '  /model status  check llama.cpp connection',
          '  /model pin <path> <sha256>  pin model file',
          '  /ask <query>   ask the model a question',
          '',
          'Voice assistant:',
          '  /voice         show voice capabilities',
          '  Wake words: ' + WAKE_WORDS.join(', '),
          '',
          'Tool dispatch:',
          '  /tools         list available adapters',
          '  /tool <name> <target>  execute governed tool',
        ].join('\n'),
      });
      break;

    case '/model': {
      const sub = args[0];
      if (sub === 'status') {
        const healthy = await state.modelProvider.checkHealth();
        let remoteStatus = '';
        if (state.remoteProvider) {
          const remoteHealthy = await state.remoteProvider.checkHealth();
          remoteStatus = `\nremote: ${remoteHealthy ? state.remoteProvider.name + ' at ' + state.remoteProvider.host : 'not reachable'}`;
        }
        sendTo(ws, MessageTypes.OUTPUT, {
          text: [
            `llama-server: ${healthy ? 'connected' : 'not reachable'}`,
            `host: ${state.modelProvider.host}`,
            `pinned: ${state.modelProvider.isPinned ? 'yes' : 'no'}`,
            state.modelProvider.isPinned ? `hash: ${state.modelProvider.modelHash?.slice(0, 16)}...` : '',
            remoteStatus,
          ].filter(Boolean).join('\n'),
        });
      } else if (sub === 'pin') {
        const filePath = args[1];
        const sha256 = args[2];
        if (!filePath || !sha256) {
          sendTo(ws, MessageTypes.ERROR, { code: 'USAGE', message: '/model pin <filepath> <sha256>' });
        } else {
          try {
            await state.modelProvider.pinModel(filePath, sha256);
            sendTo(ws, MessageTypes.OUTPUT, { text: `model pinned: ${state.modelProvider.modelHash.slice(0, 16)}...` });
          } catch (err) {
            sendTo(ws, MessageTypes.ERROR, { code: err.code || 'PIN_ERROR', message: err.message });
          }
        }
      } else {
        sendTo(ws, MessageTypes.OUTPUT, { text: 'usage: /model status | /model pin <path> <sha256>' });
      }
      break;
    }

    case '/ask': {
      const query = args.join(' ').trim();
      if (!query) {
        sendTo(ws, MessageTypes.OUTPUT, { text: 'usage: /ask <your question>' });
        break;
      }
      if (!state.modelProvider.isReady) {
        sendTo(ws, MessageTypes.OUTPUT, { text: 'model not connected. Start llama-server first.' });
        break;
      }

      state.contextManager.addTurn('user', query);
      sendTo(ws, MessageTypes.OUTPUT, { text: '[thinking...]', color: 'color:#3a5a3a' });

      try {
        const messages = state.contextManager.getMessages();
        let response = '';
        for await (const token of state.modelProvider.streamCompletion(messages, { maxTokens: 256 })) {
          response += token;
          sendTo(ws, MessageTypes.MODEL_TOKEN, { token });
        }
        state.contextManager.addTurn('assistant', response);
        sendTo(ws, MessageTypes.OUTPUT, { text: `\n${response}` });
      } catch (err) {
        sendTo(ws, MessageTypes.ERROR, { code: err.code || 'MODEL_ERROR', message: err.message });
      }
      break;
    }

    case '/voice': {
      const caps = state.voiceAssistant.capabilities;
      sendTo(ws, MessageTypes.OUTPUT, {
        text: [
          'Voice Assistant Capabilities:',
          `  Speech-to-text: ${caps.stt ? 'available' : 'not installed (install whisper-cli)'}`,
          `  Text-to-speech: ${caps.tts ? 'available' : 'not installed (install termux-api)'}`,
          `  Wake word:      ${caps.wakeWord ? 'available' : 'not installed (install porcupine)'}`,
          '',
          `Wake words: ${WAKE_WORDS.join(', ')}`,
          'Voice queries go through the same governance pipeline as text.',
        ].join('\n'),
      });
      break;
    }

    case '/tools': {
      const tools = state.toolRegistry.list();
      sendTo(ws, MessageTypes.OUTPUT, {
        text: [
          'Available tool adapters:',
          ...tools.map(t => `  ${t.name} [${t.riskLevel}] ${t.capability}`),
          '',
          'Usage: /tool <name> <target>',
        ].join('\n'),
      });
      break;
    }

    case '/autodebug': {
      const sub = args[0];
      if (sub === 'status') {
        sendTo(ws, MessageTypes.OUTPUT, {
          text: [
            `watching: ${state.fileWatcher?.fileCount || 0} files tracked`,
            `pending fixes: ${state.autoFixer?.pendingCount || 0}`,
            `auto mode: ${state.autoFixer?.autoMode ? 'on' : 'off'}`,
            '',
            'Commands:',
            '  /autodebug status   show debugger status',
            '  /autodebug list     show pending fixes',
            '  /autodebug auto on  enable auto-fix',
            '  /autodebug auto off disable auto-fix',
          ].join('\n'),
        });
      } else if (sub === 'list') {
        const pending = state.autoFixer?.pending || [];
        if (pending.length === 0) {
          sendTo(ws, MessageTypes.OUTPUT, { text: 'no pending fixes' });
        } else {
          sendTo(ws, MessageTypes.OUTPUT, {
            text: pending.map(p =>
              `  ${p.path}: ${p.errors.length} error(s) — ${p.errors.map(e => e.message).join(', ')}`
            ).join('\n'),
          });
        }
      } else if (sub === 'auto') {
        const mode = args[1];
        if (mode === 'on') {
          state.autoFixer.autoMode = true;
          sendTo(ws, MessageTypes.OUTPUT, { text: 'autodebug auto-fix: enabled', color: 'color:#54FF54' });
        } else if (mode === 'off') {
          state.autoFixer.autoMode = false;
          sendTo(ws, MessageTypes.OUTPUT, { text: 'autodebug auto-fix: disabled' });
        } else {
          sendTo(ws, MessageTypes.OUTPUT, { text: 'usage: /autodebug auto on|off' });
        }
      } else {
        sendTo(ws, MessageTypes.OUTPUT, { text: 'usage: /autodebug status|list|auto on|off' });
      }
      break;
    }

    case '/tool': {
      const toolName = args[0];
      const target = args.slice(1).join(' ').trim();

      if (!toolName || !target) {
        sendTo(ws, MessageTypes.OUTPUT, { text: 'usage: /tool <adapter-name> <target>' });
        break;
      }

      if (!state.toolRegistry.has(toolName)) {
        sendTo(ws, MessageTypes.ERROR, { code: 'UNKNOWN_TOOL', message: `"${toolName}" not registered. Use /tools to list.` });
        break;
      }

      const adapter = state.toolRegistry.get(toolName);
      const actionRequest = {
        target,
        tool: toolName,
        riskLevel: adapter.riskLevel,
        operatorId: state.engagementManifest?.operatorId || 'operator',
      };

      const policyResult = state.policyEngine.evaluate(actionRequest);

      await state.evidenceChain.append('action.proposed', {
        tool: toolName,
        target,
        policyDecision: policyResult.decision,
      });

      if (policyResult.decision === 'DENY') {
        sendTo(ws, MessageTypes.OUTPUT, {
          color: 'color:#FA4B4B',
          text: `[POLICY DENIED] ${policyResult.reason}\n${policyResult.detail || ''}`,
        });
        break;
      }

      if (policyResult.decision === 'APPROVAL_REQUIRED') {
        sendTo(ws, MessageTypes.OUTPUT, {
          color: 'color:#B26818',
          text: `[APPROVAL REQUIRED] ${policyResult.reason}\nType /approve to proceed or /deny to cancel.`,
        });
        state._pendingApproval = { ws, actionRequest, adapter };
        break;
      }

      // ALLOW — proceed through permit → execute → evidence
      try {
        const permit = state.permitIssuer.issue(actionRequest, policyResult);
        const consumed = state.permitIssuer.consume(permit.id, toolName, target, actionRequest.operatorId);

        if (!consumed.ok) {
          sendTo(ws, MessageTypes.ERROR, { code: 'PERMIT_ERROR', message: consumed.reason });
          break;
        }

        const result = await adapter.execute({ target });
        const sanitizedData = sanitizeObject(result.data || {});

        await state.evidenceChain.append('action.executed', {
          tool: toolName,
          target,
          permitId: permit.id.slice(0, 8),
          ok: result.ok,
          data: sanitizedData,
        });

        if (result.ok) {
          sendTo(ws, MessageTypes.OUTPUT, {
            color: 'color:#54FF54',
            text: JSON.stringify(sanitizedData, null, 2),
          });
        } else {
          sendTo(ws, MessageTypes.OUTPUT, {
            color: 'color:#FA4B4B',
            text: `[TOOL ERROR] ${result.error}${result.detail ? ': ' + result.detail : ''}`,
          });
        }
      } catch (err) {
        sendTo(ws, MessageTypes.ERROR, { code: 'EXECUTION_ERROR', message: err.message });
      }
      break;
    }

    case '/status':
      sendTo(ws, MessageTypes.STATUS, getStatus());
      break;

    case '/device':
      sendTo(ws, MessageTypes.OUTPUT, {
        text: JSON.stringify(state.profile, null, 2),
      });
      break;

    case '/history':
      sendTo(ws, MessageTypes.OUTPUT, {
        text: commandHistory.length > 0
          ? commandHistory.slice(-20).map((c, i) => `  ${i + 1}. ${c}`).join('\n')
          : 'no history',
      });
      break;

    case '/clear':
      sendTo(ws, MessageTypes.OUTPUT, { clear: true });
      break;

    case '/sop': {
      const sub = args[0];
      if (sub === 'list') {
        const sops = state.sopCompiler ? state.sopCompiler.list() : [];
        if (sops.length === 0) {
          sendTo(ws, MessageTypes.OUTPUT, { text: 'No SOPs loaded. Place .sop.json files in workspace or use /sop load <id>.' });
        } else {
          sendTo(ws, MessageTypes.OUTPUT, {
            text: 'Available SOPs:\n' + sops.map(s => `  ${s.id} — ${s.name} (${s.steps.length} steps)`).join('\n'),
          });
        }
      } else if (sub === 'load') {
        const sopId = args[1];
        if (!sopId) {
          sendTo(ws, MessageTypes.OUTPUT, { text: 'usage: /sop load <sop-id>' });
          break;
        }
        if (!state.sopCompiler) {
          sendTo(ws, MessageTypes.ERROR, { code: 'NO_SOP_ENGINE', message: 'SOP compiler not initialized' });
          break;
        }
        const sop = state.sopCompiler.get(sopId);
        if (!sop) {
          sendTo(ws, MessageTypes.ERROR, { code: 'SOP_NOT_FOUND', message: `SOP "${sopId}" not found` });
          break;
        }
        state.activeSOP = state.coverageLedger.startSOP(sop);
        sendTo(ws, MessageTypes.OUTPUT, { text: `SOP loaded: ${sop.name} (${sop.steps.length} steps)` });
        broadcastViewUpdate('sop', getSOPViewData());
      } else if (sub === 'run') {
        if (!state.activeSOP) {
          sendTo(ws, MessageTypes.OUTPUT, { text: 'No SOP loaded. Use /sop load <id> first.' });
          break;
        }
        sendTo(ws, MessageTypes.OUTPUT, { text: 'Running SOP... (use /sop status to check progress)' });
        runSOPStep(ws);
      } else if (sub === 'approve') {
        if (!state.activeSOP || !state._sopPendingApproval) {
          sendTo(ws, MessageTypes.OUTPUT, { text: 'No SOP step pending approval.' });
          break;
        }
        state._sopPendingApproval.approved = true;
        state._sopPendingApproval = null;
        runSOPStep(ws);
      } else if (sub === 'reject') {
        if (!state._sopPendingApproval) {
          sendTo(ws, MessageTypes.OUTPUT, { text: 'No SOP step pending approval.' });
          break;
        }
        state.coverageLedger.recordStep(state.activeSOP.sop.id, state._sopPendingApproval.stepId, 'skipped');
        state._sopPendingApproval = null;
        runSOPStep(ws);
      } else if (sub === 'status') {
        if (!state.activeSOP) {
          sendTo(ws, MessageTypes.OUTPUT, { text: 'No SOP active.' });
          break;
        }
        const ledger = state.coverageLedger.getStatus(state.activeSOP.sop.id);
        sendTo(ws, MessageTypes.OUTPUT, {
          text: [
            `SOP: ${state.activeSOP.sop.name}`,
            `Progress: ${ledger.completed}/${ledger.total} (${ledger.percentage}%)`,
            `Current step: ${state.activeSOP.currentStep || 'none'}`,
          ].join('\n'),
        });
      } else {
        sendTo(ws, MessageTypes.OUTPUT, {
          text: 'SOP commands:\n  /sop list      list available SOPs\n  /sop load <id> load an SOP\n  /sop run       execute next step\n  /sop approve   approve pending step\n  /sop reject    reject pending step\n  /sop status    show progress',
        });
      }
      break;
    }

    case '/engage': {
      const sub = args[0];
      if (sub === 'load') {
        const engagePath = args[1] || 'engagement.json';
        try {
          const { readFile } = await import('node:fs/promises');
          const raw = await readFile(engagePath, 'utf8');
          const manifest = JSON.parse(raw);
          state.engagementManifest = manifest;
          state.policyEngine.loadManifest(manifest);
          sendTo(ws, MessageTypes.OUTPUT, { text: `engagement loaded: ${manifest.name || manifest.target}` });
        } catch (err) {
          sendTo(ws, MessageTypes.ERROR, { code: 'ENGAGE_ERROR', message: `failed to load engagement: ${err.message}` });
        }
      } else if (sub === 'status') {
        const m = state.engagementManifest;
        if (!m) {
          sendTo(ws, MessageTypes.OUTPUT, { text: 'No engagement loaded.' });
        } else {
          sendTo(ws, MessageTypes.OUTPUT, {
            text: [
              `Name: ${m.name || 'unnamed'}`,
              `Target: ${m.target || 'unknown'}`,
              `Scope: ${JSON.stringify(m.scope || [])}`,
              `Operator: ${m.operatorId || 'unknown'}`,
            ].join('\n'),
          });
        }
      } else {
        sendTo(ws, MessageTypes.OUTPUT, {
          text: 'Engagement commands:\n  /engage load [path]   load engagement.json\n  /engage status        show current engagement',
        });
      }
      break;
    }

    case '/finding': {
      const sub = args[0];
      if (sub === 'add') {
        const findingText = args.slice(1).join(' ');
        if (!findingText) {
          sendTo(ws, MessageTypes.OUTPUT, { text: 'usage: /finding add <title> [--severity critical|high|medium|low|info]' });
          break;
        }
        const severityMatch = findingText.match(/--severity\s+(\w+)/);
        const severity = severityMatch ? severityMatch[1] : 'info';
        const title = findingText.replace(/--severity\s+\w+/, '').trim();
        state.findings.push({
          id: `f-${Date.now().toString(36)}`,
          title,
          severity,
          target: state.engagementManifest?.target || 'unknown',
          timestamp: new Date().toISOString(),
          description: '',
        });
        state.evidenceChain.append('finding.recorded', { title, severity });
        sendTo(ws, MessageTypes.OUTPUT, { text: `finding recorded: [${severity}] ${title}` });
      } else if (sub === 'list') {
        if (state.findings.length === 0) {
          sendTo(ws, MessageTypes.OUTPUT, { text: 'No findings recorded.' });
        } else {
          sendTo(ws, MessageTypes.OUTPUT, {
            text: state.findings.map(f => `  [${f.severity}] ${f.title} — ${f.timestamp}`).join('\n'),
          });
        }
      } else {
        sendTo(ws, MessageTypes.OUTPUT, {
          text: 'Finding commands:\n  /finding add <title> [--severity level]\n  /finding list',
        });
      }
      break;
    }

    default:
      sendTo(ws, MessageTypes.ERROR, { code: 'UNKNOWN_COMMAND', message: `unknown command: ${cmd}` });
  }
}

function getViewData(viewName) {
  switch (viewName) {
    case 'engagement': return state.engagementManifest || null;
    case 'permits': return state.permitIssuer ? Array.from(state.permitIssuer.active.values()) : [];
    case 'evidence': return state.evidenceChain ? state.evidenceChain.recent(50) : [];
    case 'findings': return state.findings || [];
    case 'sop': return getSOPViewData();
    default: return null;
  }
}

function getSOPViewData() {
  if (!state.activeSOP) return null;
  const ledger = state.coverageLedger.getStatus(state.activeSOP.sop.id);
  return {
    id: state.activeSOP.sop.id,
    name: state.activeSOP.sop.name,
    steps: state.activeSOP.sop.steps.map(s => ({
      id: s.id,
      name: s.name,
      tool: s.tool,
      status: ledger.stepStatus[s.id] || 'pending',
    })),
    percentage: ledger.percentage,
  };
}

function broadcastViewUpdate(viewName, data) {
  for (const client of state.wsClients) {
    sendTo(client, MessageTypes.SOP_UPDATE, { view: viewName, data });
  }
}

async function runSOPStep(ws) {
  if (!state.activeSOP) return;
  const idx = state.activeSOP.currentStepIndex ?? 0;
  const steps = state.activeSOP.sop.steps;
  if (idx >= steps.length) {
    sendTo(ws, MessageTypes.OUTPUT, { text: 'SOP complete!', color: 'color:#54FF54' });
    state.activeSOP = null;
    return;
  }
  const step = steps[idx];
  state.activeSOP.currentStepIndex = idx;
  state.activeSOP.currentStep = step.id;

  if (step.approvalRequired && !state._sopPendingApproval) {
    state._sopPendingApproval = { stepId: step.id, approved: false };
    sendTo(ws, MessageTypes.OUTPUT, {
      color: 'color:#B26818',
      text: `[SOP APPROVAL REQUIRED] Step: ${step.name}\nTool: ${step.tool}\nType /sop approve or /sop reject.`,
    });
    return;
  }

  if (step.approvalRequired && !state._sopPendingApproval?.approved) {
    state.coverageLedger.recordStep(state.activeSOP.sop.id, step.id, 'skipped');
    state.activeSOP.currentStepIndex = idx + 1;
    runSOPStep(ws);
    return;
  }

  sendTo(ws, MessageTypes.OUTPUT, { text: `[SOP] Running: ${step.name}...`, color: 'color:#18B2B2' });
  state.coverageLedger.recordStep(state.activeSOP.sop.id, step.id, 'running');

  if (!step.tool || !state.toolRegistry.has(step.tool)) {
    state.coverageLedger.recordStep(state.activeSOP.sop.id, step.id, 'failed');
    sendTo(ws, MessageTypes.OUTPUT, { text: `[SOP] Tool "${step.tool}" not available`, color: 'color:#FA4B4B' });
    state.activeSOP.currentStepIndex = idx + 1;
    broadcastViewUpdate('sop', getSOPViewData());
    runSOPStep(ws);
    return;
  }

  try {
    const adapter = state.toolRegistry.get(step.tool);
    const target = resolveTemplate(step.params?.target || state.engagementManifest?.target || '');
    const result = await adapter.execute({ target });
    const sanitized = sanitizeObject(result.data || {});
    state.coverageLedger.recordStep(state.activeSOP.sop.id, step.id, result.ok ? 'completed' : 'failed');
    await state.evidenceChain.append('sop.step.executed', { sop: state.activeSOP.sop.id, step: step.id, ok: result.ok, data: sanitized });
    sendTo(ws, MessageTypes.OUTPUT, { text: result.ok ? JSON.stringify(sanitized, null, 2) : `[SOP ERROR] ${result.error}`, color: result.ok ? 'color:#54FF54' : 'color:#FA4B4B' });
  } catch (err) {
    state.coverageLedger.recordStep(state.activeSOP.sop.id, step.id, 'failed');
    sendTo(ws, MessageTypes.ERROR, { code: 'SOP_EXEC_ERROR', message: err.message });
  }

  state.activeSOP.currentStepIndex = idx + 1;
  broadcastViewUpdate('sop', getSOPViewData());
  runSOPStep(ws);
}

function resolveTemplate(str) {
  if (!state.engagementManifest || !str.includes('{{')) return str;
  return str.replace(/\{\{(\w+)\}\}/g, (_, key) => state.engagementManifest[key] || `{{${key}}}`);
}

function getStatus() {
  return {
    version: VERSION,
    uptimeSeconds: Math.floor((Date.now() - state.startedAt) / 1000),
    freeMemBytes: state.profile?.freeMemBytes,
    totalMemBytes: state.profile?.totalMemBytes,
    modelLoaded: state.modelProvider?.isReady || false,
    engagementActive: state.policyEngine ? !state.policyEngine.isExpired : false,
    activePermits: state.permitIssuer ? state.permitIssuer.active.size : 0,
    evidenceCount: state.evidenceChain ? state.evidenceChain.count : 0,
    connections: connections.count,
  };
}

async function writePid() {
  try {
    await fs.writeFile(path.join(state.boundary.root, 'daemon.pid'), String(process.pid));
  } catch {}
}

let shuttingDown = false;
async function shutdown(signal) {
  if (shuttingDown) return;
  shuttingDown = true;
  console.log(`\n${signal} received. Shutting down...`);
  wss.clients.forEach(client => client.close(1001, 'server shutting down'));
  server.close(() => process.exit(0));
  setTimeout(() => process.exit(0), 3000);
}

process.on('SIGTERM', () => shutdown('SIGTERM'));
process.on('SIGINT', () => shutdown('SIGINT'));
process.on('uncaughtException', (err) => {
  console.error('Uncaught exception:', err.message);
  shutdown('UNCAUGHT_EXCEPTION');
});

boot()
  .then(writePid)
  .catch(err => {
    console.error('Fatal boot error:', err.message);
    process.exit(1);
  });
