import { execFile } from 'node:child_process';
import { promisify } from 'node:util';

const execFileAsync = promisify(execFile);

const WAKE_WORDS = Object.freeze(['hey cipher', 'hey cyber', 'ok cipher']);

export function createVoiceAssistant({ modelProvider, contextManager, policyEngine, evidenceChain }) {
  let listening = false;
  let active = false;

  const capabilities = {
    stt: null,
    tts: null,
    wakeWord: null,
  };

  async function detectCapabilities() {
    const checks = [
      { name: 'stt', binary: 'whisper-cli', args: ['--help'] },
      { name: 'tts', binary: 'termux-tts-speak', args: ['--help'] },
      { name: 'wakeWord', binary: 'porcupine', args: ['--version'] },
    ];

    const results = await Promise.allSettled(
      checks.map(check =>
        execFileAsync(check.binary, check.args, { timeout: 2000 })
          .then(() => ({ name: check.name, ok: true }))
          .catch(() => ({ name: check.name, ok: false }))
      )
    );

    for (const result of results) {
      if (result.status === 'fulfilled') {
        capabilities[result.value.name] = result.value.ok;
      }
    }

    return { ...capabilities };
  }

  async function processVoiceQuery(transcript) {
    if (!transcript?.trim()) return { ok: false, error: 'empty transcript' };

    const query = transcript.trim();

    await evidenceChain.append({
      type: 'voice.query',
      data: { transcript: query, source: 'voice_assistant' },
    });

    const isCommand = query.startsWith('/');
    if (isCommand) {
      return { ok: true, type: 'command', command: query };
    }

    contextManager.addTurn('user', query);
    const messages = contextManager.getMessages();

    try {
      const response = await modelProvider.complete(messages, { maxTokens: 256 });
      contextManager.addTurn('assistant', response);

      await evidenceChain.append({
        type: 'voice.response',
        data: { query, responseLength: response.length },
      });

      return {
        ok: true,
        type: 'response',
        query,
        response,
        shouldSpeak: capabilities.tts !== false,
      };
    } catch (err) {
      return { ok: false, error: err.message, code: err.code || 'INFERENCE_ERROR' };
    }
  }

  async function speak(text) {
    if (capabilities.tts === false) return { spoken: false, reason: 'no tts available' };
    try {
      await execFileAsync('termux-tts-speak', [text.slice(0, 500)], { timeout: 10000 });
      return { spoken: true };
    } catch {
      return { spoken: false, reason: 'tts execution failed' };
    }
  }

  async function listen() {
    if (capabilities.stt === false) return { ok: false, error: 'no stt engine available' };
    try {
      const { stdout } = await execFileAsync(
        'whisper-cli',
        ['-m', '/data/data/com.termux/files/home/.edge-cyber/models/whisper-tiny.bin', '-nt', '-f', '/dev/stdin'],
        { timeout: 30000, input: 'pipe' }
      );
      return { ok: true, transcript: stdout.trim() };
    } catch {
      return { ok: false, error: 'stt failed' };
    }
  }

  return {
    detectCapabilities,
    processVoiceQuery,
    speak,
    listen,
    get isListening() { return listening; },
    get isActive() { return active; },
    get capabilities() { return { ...capabilities }; },
    set listening(val) { listening = Boolean(val); },
    set active(val) { active = Boolean(val); },
  };
}

export { WAKE_WORDS };
