import { describe, it, after } from 'node:test';
import assert from 'node:assert/strict';
import WebSocket from 'ws';

describe('websocket integration', { skip: !process.env.RUN_INTEGRATION }, () => {
  it('connects, pings, sends command, receives status', async () => {
    const ws = new WebSocket('ws://127.0.0.1:7420');

    const messages = [];
    const waitFor = (type) => new Promise((resolve, reject) => {
      const timer = setTimeout(() => reject(new Error(`timeout waiting for ${type}`)), 3000);
      const handler = (raw) => {
        try {
          const msg = JSON.parse(raw.toString());
          messages.push(msg);
          if (msg.type === type) {
            clearTimeout(timer);
            ws.removeListener('message', handler);
            resolve(msg);
          }
        } catch {}
      };
      ws.on('message', handler);
    });

    await new Promise((resolve) => {
      if (ws.readyState === WebSocket.OPEN) return resolve();
      ws.on('open', resolve);
    });

    const readyMsg = await waitFor('ready');
    assert.ok(readyMsg.payload.version);

    ws.send(JSON.stringify({ type: 'ping' }));
    const pong = await waitFor('pong');
    assert.ok(pong.at);

    ws.send(JSON.stringify({ type: 'command', payload: { text: '/status' } }));
    const statusMsg = await waitFor('status');
    assert.ok(statusMsg.payload.version);

    ws.send(JSON.stringify({ type: 'command', payload: { text: '/help' } }));
    const helpOutput = await waitFor('output');
    assert.ok(helpOutput.payload.text.includes('/help'));

    ws.close();
  });
});
