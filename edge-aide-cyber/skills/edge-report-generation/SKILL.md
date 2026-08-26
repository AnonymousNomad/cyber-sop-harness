# Edge Bug Bounty Report Generation

## What To Do
Build a report generator that produces professional bug bounty submission reports from engagement data, findings, and evidence chain. Output: Markdown (HackerOne/Bugcrowd), JSON, and PDF-ready HTML.

## Why
Professional bug bounty reports require structured evidence, clear reproduction steps, and impact analysis. Auto-generating from the evidence chain ensures nothing is missed.

## Code Guidance
```javascript
// src/report/generator.mjs
export class ReportGenerator {
  generateMarkdown(engagement, findings, sopId) {
    // Executive summary with severity counts
    // Detailed findings with reproduction steps
    // Evidence chain hash verification
    // Methodology coverage percentage
  }
  generateJSON(engagement, findings, sopId) { ... }
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Report contains real credentials | Leak on submission | Sanitize all evidence |
| Evidence chain broken | Report integrity questioned | Verify chain hash |
| Report sent to wrong program | Legal exposure | Confirm target matches scope |

## Dependencies
- Evidence chain module, Coverage ledger module

## Pitfalls
- Never include real API keys or tokens in reports
- HackerOne has specific markdown formatting
- Large evidence chains need pagination