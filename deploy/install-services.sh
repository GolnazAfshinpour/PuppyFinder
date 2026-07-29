#!/bin/bash
# Installs PuppyFinder as macOS launchd user agents: both servers start at login,
# restart on crash, and keep the email-alert checker watching the feeds 24/7.
# Re-run after editing the plists. Uninstall: launchctl bootout gui/$(id -u)/com.puppyfinder.api
# (and .web), then delete the plists from ~/Library/LaunchAgents.
set -euo pipefail

DIR="$(cd "$(dirname "$0")/launchd" && pwd)"
AGENTS="$HOME/Library/LaunchAgents"
UID_NUM=$(id -u)

mkdir -p "$AGENTS"
for name in com.puppyfinder.api com.puppyfinder.web; do
  cp "$DIR/$name.plist" "$AGENTS/$name.plist"
  launchctl bootout "gui/$UID_NUM/$name" 2>/dev/null || true
  launchctl bootstrap "gui/$UID_NUM" "$AGENTS/$name.plist"
  echo "installed + started $name"
done

echo "logs: /tmp/puppyfinder-api.log, /tmp/puppyfinder-web.log"
