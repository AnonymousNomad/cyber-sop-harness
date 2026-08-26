# Edge Touch Optimization

## What To Do
Ensure all UI interactions work with touch input on tablets: large tap targets, swipe navigation between views, long-press context menus, and proper handling of the virtual keyboard.

## Why
This runs on a tablet, not a desktop. Mouse-hover states don't exist. Tap targets below 44px cause mis-taps. The virtual keyboard covers half the screen. These constraints drive fundamentally different UX decisions.

## Code Guidance
```css
/* Minimum touch target size */
button, .clickable, .tab-btn {
  min-height: 44px;
  min-width: 44px;
  padding: 10px 16px;
  touch-action: manipulation; /* Prevents double-tap zoom delay */
}

/* Swipe detection */
.view-container {
  touch-action: pan-y;
  overflow-x: hidden;
}

/* Long press for context menu */
.context-menu-trigger {
  -webkit-touch-callout: none;
  user-select: none;
}

/* Keyboard-safe layout */
#terminal-input-area {
  position: sticky;
  bottom: 0;
}
```

```javascript
// Swipe navigation between views
let touchStartX = 0;
let touchStartY = 0;

document.addEventListener('touchstart', (e) => {
  touchStartX = e.touches[0].clientX;
  touchStartY = e.touches[0].clientY;
}, { passive: true });

document.addEventListener('touchend', (e) => {
  const deltaX = e.changedTouches[0].clientX - touchStartX;
  const deltaY = e.changedTouches[0].clientY - touchStartY;

  // Horizontal swipe > 80px and vertical movement < 50px
  if (Math.abs(deltaX) > 80 && Math.abs(deltaY) < 50) {
    if (deltaX < 0) nextView();
    else prevView();
  }
}, { passive: true });
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Accidental tap triggers destructive action | Unintended execution | Confirmation dialog for riskLevel R2+ actions |
| Swipe gesture conflicts with browser back navigation | Unexpected page exit | Prevent default on horizontal swipes within the app |
| Virtual keyboard covers critical buttons | Cannot approve/reject | Position approval buttons above keyboard area |

## Dependencies
- None

## Pitfalls & Bugs
- iOS Safari fires `touchstart` before `mousedown`; handle both without duplicating actions.
- `touch-action: manipulation` removes the 300ms tap delay but also disables pinch-zoom on that element.
- Long-press duration varies between Android (~500ms) and iOS (~700ms). Use a consistent custom timer.
- The virtual keyboard's appearance triggers a window resize; debounce resize handlers.
- Some Android keyboards (Samsung Keyboard, Gboard) insert zero-width joiners in some languages; strip these from terminal input.
