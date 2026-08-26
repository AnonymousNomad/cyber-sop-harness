#!/usr/bin/env bash
# Edge AIDE Cybersecurity Workbench — Termux Installer
# Run: bash install.sh
set -euo pipefail

INSTALL_DIR="$HOME/.edge-cyber"
REPO_URL="https://github.com/AnonymousNomad/edge-aide-cyber"

echo "╔══════════════════════════════════════════════╗"
echo "║  Edge AIDE Cybersecurity Workbench Installer ║"
echo "║  Authorized defensive testing only           ║"
echo "╚══════════════════════════════════════════════╝"
echo ""

# Check Node.js
if ! command -v node &>/dev/null; then
  echo "[!] Node.js not found. Installing..."
  pkg install -y nodejs
fi

NODE_VER=$(node -v | cut -d'v' -f2 | cut -d'.' -f1)
if [ "$NODE_VER" -lt 18 ]; then
  echo "[!] Node.js >= 18 required (found v$(node -v)). Updating..."
  pkg install -y nodejs
fi

echo "[✓] Node.js $(node -v)"

# Check git
if ! command -v git &>/dev/null; then
  echo "[!] Git not found. Installing..."
  pkg install -y git
fi

# Clone or update
if [ -d "$INSTALL_DIR" ]; then
  echo "[...] Updating existing installation..."
  cd "$INSTALL_DIR"
  git pull --quiet
else
  echo "[...] Cloning repository..."
  git clone --quiet "$REPO_URL" "$INSTALL_DIR"
  cd "$INSTALL_DIR"
fi

# Install dependencies
echo "[...] Installing dependencies..."
npm ci --omit=dev 2>/dev/null || npm install --omit=dev

# Create workspace directories
mkdir -p "$INSTALL_DIR/evidence"
mkdir -p "$INSTALL_DIR/sops"
mkdir -p "$INSTALL_DIR/.edge-cyber"

# Create default engagement if none exists
if [ ! -f "$INSTALL_DIR/engagement.json" ]; then
  cat > "$INSTALL_DIR/engagement.json" << 'ENGAGE'
{
  "id": "default",
  "name": "Default Engagement",
  "operatorId": "operator",
  "target": "localhost",
  "scope": ["127.0.0.1/32", "localhost"],
  "allowedCapabilities": ["dns.reverse", "http.headers"],
  "authorizedRiskLevels": ["R0", "R1"],
  "expiresAt": "2027-12-31T23:59:59Z"
}
ENGAGE
  echo "[✓] Default engagement.json created"
fi

echo ""
echo "╔══════════════════════════════════════════════╗"
echo "║  Installation complete!                      ║"
echo "║                                              ║"
echo "║  Start:  cd ~/.edge-cyber && npm start       ║"
echo "║  Open:   http://127.0.0.1:7420              ║"
echo "║                                              ║"
echo "║  Configure:                                  ║"
echo "║    REMOTE_MODEL_HOST=<laptop-ip>            ║"
echo "║    VAULT_PASSPHRASE=<your-passphrase>        ║"
echo "╚══════════════════════════════════════════════╝"
