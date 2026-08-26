# Edge Context Budget Manager

## What To Do
Track token usage across system prompt, conversation history, tool outputs, and SOP context. Enforce a hard budget. When exceeded, intelligently truncate oldest turns first while preserving pinned memory blocks.

## Why
LFM2.5 has 32K context but effective quality degrades well before the limit. On edge devices with limited RAM, oversized contexts also slow inference. A budget manager ensures consistent performance and prevents silent truncation of critical instructions.

## Code Guidance
```javascript
export function createContextManager(maxTokens = 4096) {
  const systemPromptTokens = 0; // set at init
  const pinnedBlocks = [];
  let history = [];

  function estimateTokens(text) {
    // Rough approximation: ~4 chars per token for English/code mix
    // For production, use llama.cpp's tokenize endpoint
    return Math.ceil(Buffer.byteLength(text, 'utf8') / 4);
  }

  function totalUsed() {
    return systemPromptTokens
      + pinnedBlocks.reduce((sum, b) => sum + estimateTokens(b), 0)
      + history.reduce((sum, h) => sum + estimateTokens(h.content), 0);
  }

  return {
    setSystemPrompt(text) { systemPromptTokens = estimateTokens(text); },
    pinBlock(id, text) {
      const idx = pinnedBlocks.findIndex(b => b.id === id);
      if (idx >= 0) pinnedBlocks[idx] = { id, text };
      else pinnedBlocks.push({ id, text });
    },
    addTurn(role, content) {
      history.push({ role, content });
      while (totalUsed() > maxTokens && history.length > 2) {
        history.shift(); // drop oldest turn
      }
      if (totalUsed() > maxTokens) {
        throw new Error('context budget exceeded even after truncation');
      }
    },
    getMessages() {
      const msgs = [{ role: 'system', content: pinnedBlocks.map(b => b.text).join('\n\n') }];
      return [...msgs, ...history];
    },
    stats() {
      return {
        used: totalUsed(), budget: maxTokens,
        historyTurns: history.length,
        pinnedBlocks: pinnedBlocks.length,
      };
    },
  };
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Silent context truncation drops safety instructions | Unsafe model behavior | System prompt is always included; only history is truncated |
| Token estimation inaccurate | Actual context exceeds limit | Use llama.cpp tokenize endpoint for precise counting |
| Large tool output consumes entire budget | No room for model response | Cap individual tool output contribution |
| Memory blocks accumulate without bound | Budget exhaustion | Limit pinned blocks to configurable maximum |

## Dependencies
- llama.cpp `/tokenize` endpoint (optional, for precise counting)

## Pitfalls & Bugs
- Character-per-token ratios vary wildly between English prose, JSON, and base64. Use real tokenizer for accuracy.
- Dropping the first turn after system prompt can lose task framing; keep at least one user message.
- Streaming responses consume tokens too; account for generation length in the budget.
- Pinned blocks should be sorted by priority; lowest-priority blocks get evicted first if space runs out.
