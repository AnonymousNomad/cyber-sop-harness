const DEFAULT_BUDGET = 4096;
const MAX_TOOL_OUTPUT_TOKENS = 512;
const MIN_HISTORY_TURNS = 2;

export function createContextManager(options = {}) {
  const budget = options.maxTokens || DEFAULT_BUDGET;
  const systemPromptTokens = estimate(options.systemPrompt || '');
  const pinnedBlocks = [];
  let history = [];

  function estimate(text) {
    if (typeof text !== 'string') return 0;
    return Math.ceil(Buffer.byteLength(text, 'utf8') / 4);
  }

  function totalUsed() {
    return systemPromptTokens
      + pinnedBlocks.reduce((sum, b) => sum + estimate(b.text), 0)
      + history.reduce((sum, h) => sum + estimate(h.content), 0);
  }

  return {
    pinBlock(id, text) {
      const idx = pinnedBlocks.findIndex(b => b.id === id);
      if (idx >= 0) pinnedBlocks[idx] = { id, text };
      else pinnedBlocks.push({ id, text });
    },

    unpinBlock(id) {
      const idx = pinnedBlocks.findIndex(b => b.id === id);
      if (idx >= 0) pinnedBlocks.splice(idx, 1);
    },

    addTurn(role, content) {
      history.push({ role, content: String(content).slice(0, budget * 4) });

      while (totalUsed() > budget && history.length > MIN_HISTORY_TURNS) {
        history.shift();
      }

      if (totalUsed() > budget) {
        throw new Error(`context budget exceeded (${totalUsed()}/${budget}) even after truncation`);
      }
    },

    addToolOutput(toolName, output, permitId) {
      const truncated = String(output);
      const tokens = estimate(truncated);

      if (tokens > MAX_TOOL_OUTPUT_TOKENS) {
        const maxBytes = MAX_TOOL_OUTPUT_TOKENS * 4;
        history.push({
          role: 'tool',
          name: toolName,
          permitId,
          content: truncated.slice(0, maxBytes) + `\n[truncated at ${MAX_TOOL_OUTPUT_TOKENS} tokens]`,
        });
      } else {
        history.push({ role: 'tool', name: toolName, permitId, content: truncated });
      }

      while (totalUsed() > budget && history.length > MIN_HISTORY_TURNS) {
        history.shift();
      }
    },

    getMessages() {
      const pinnedText = pinnedBlocks.map(b => b.text).join('\n\n');
      const messages = [];

      if (pinnedText || systemPromptTokens > 0) {
        const sysContent = [options.systemPrompt, pinnedText].filter(Boolean).join('\n\n');
        if (sysContent) messages.push({ role: 'system', content: sysContent });
      }

      return [...messages, ...history];
    },

    clearHistory() {
      history = [];
    },

    stats() {
      const result = {
        used: totalUsed(),
        budget,
        utilizationPct: Math.round((totalUsed() / budget) * 100),
        historyTurns: history.length,
        pinnedBlocks: pinnedBlocks.map(b => b.id),
        remaining: budget - totalUsed(),
      };
      return Object.freeze(result);
    },
  };
}

function estimate(text) {
  if (typeof text !== 'string') return 0;
  return Math.ceil(Buffer.byteLength(text, 'utf8') / 4);
}
