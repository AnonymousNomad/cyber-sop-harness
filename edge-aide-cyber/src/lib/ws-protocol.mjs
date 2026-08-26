const MessageTypes = Object.freeze({
  COMMAND: 'command',
  PING: 'ping',
  STATUS: 'status',
  OUTPUT: 'output',
  ERROR: 'error',
  READY: 'ready',
  MODEL_TOKEN: 'model.token',
  EVIDENCE_ENTRY: 'evidence.entry',
  VIEW_DATA: 'view-data',
  SOP_UPDATE: 'sop-update',
});

const MAX_MESSAGE_SIZE = 1024 * 1024;
const MAX_CONNECTIONS = 3;

function validateMessage(raw) {
  if (typeof raw !== 'string' && !(raw instanceof Buffer)) {
    return { ok: false, code: 'INVALID_INPUT', message: 'message must be string or Buffer' };
  }
  const text = typeof raw === 'string' ? raw : raw.toString('utf8');
  if (text.length > MAX_MESSAGE_SIZE) {
    return { ok: false, code: 'MESSAGE_TOO_LARGE', message: `exceeds ${MAX_MESSAGE_SIZE} bytes` };
  }

  let parsed;
  try {
    parsed = JSON.parse(text);
  } catch {
    return { ok: false, code: 'PARSE_ERROR', message: 'invalid JSON' };
  }

  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
    return { ok: false, code: 'PARSE_ERROR', message: 'expected JSON object' };
  }
  if (!parsed.type || !Object.values(MessageTypes).includes(parsed.type)) {
    return { ok: false, code: 'UNKNOWN_TYPE', message: `unknown type: ${parsed.type}` };
  }

  return { ok: true, message: parsed };
}

function createConnectionManager(wss) {
  let connectionCount = 0;

  function canAccept() {
    return connectionCount < MAX_CONNECTIONS;
  }

  function onConnect(ws) {
    connectionCount += 1;
    ws.on('close', () => { connectionCount -= 1; });
  }

  return { canAccept, onConnect, get count() { return connectionCount; } };
}

function sendTo(ws, type, payload) {
  if (ws.readyState !== 1) return;
  try {
    ws.send(JSON.stringify({ type, payload, at: new Date().toISOString() }));
  } catch {}
}

export { MessageTypes, validateMessage, createConnectionManager, sendTo, MAX_MESSAGE_SIZE };
