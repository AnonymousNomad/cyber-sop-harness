# Edge Workflow Views

## What To Do
Create tabbed views accessible from the terminal: Engagement overview, Active permits, Evidence timeline, Findings list, and SOP progress tracker.

## Why
Terminal-only interaction works for commands but is poor for reviewing structured data like evidence chains or permit states. Tabbed views provide at-a-glance awareness without leaving the terminal context.

## Code Guidance
```javascript
// View router — minimal, no framework
const views = new Map();

function registerView(name, renderFn) {
  views.set(name, renderFn);
}

function switchView(name) {
  const container = document.getElementById('view-container');
  container.innerHTML = '';
  const renderer = views.get(name);
  if (!renderer) return;

  const element = document.createElement('div');
  element.className = 'view-panel';
  renderer(element, wsSend);
  container.appendChild(element);
}

// Tab bar
function createTabBar() {
  const tabs = ['terminal', 'engagement', 'permits', 'evidence', 'findings', 'sop'];
  const bar = document.getElementById('tab-bar');
  for (const tab of tabs) {
    const btn = document.createElement('button');
    btn.textContent = tab;
    btn.className = 'tab-btn';
    btn.onclick = () => {
      document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      if (tab === 'terminal') {
        hideViewPanel();
      } else {
        showViewPanel();
        switchView(tab);
      }
    };
    bar.appendChild(btn);
  }
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Evidence viewer renders raw HTML from tool output | XSS | Use textContent for all data rendering |
| Permit view reveals permit IDs to shoulder surfers | Unauthorized use | Mask IDs by default; tap to reveal |
| Findings list exposes target URLs in notifications | Privacy leak in public | No push notifications; require active viewing |

## Dependencies
- None beyond base terminal UI

## Pitfalls & Bugs
- Switching views while WebSocket data streams can cause race conditions; queue updates per view.
- On small screens, tab labels may truncate; use icons or abbreviated text.
- View state should be preserved when switching back (don't re-fetch unless data changed).
- Large evidence lists need lazy loading / pagination to avoid freezing the browser.
