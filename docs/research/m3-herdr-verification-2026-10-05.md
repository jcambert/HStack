# M3 upstream verification — 2026-10-05

Herdr stable v0.9.3 was verified before M3 implementation.

- Official stable release assets exist for Linux x86_64 and arm64 and publish SHA-256 digests.
- `herdr server` is the documented headless/service mode.
- `workspace create` and `tab create` return JSON identifiers suitable for deterministic automation.
- `agent start <name> --kind <kind> --pane <id>` launches supported coding agents without shell command construction.
- Official integrations exist for Claude Code, Codex, OpenCode and Hermes Agent and report native session identity used for restore.
- Herdr keeps processes alive across client detach; after server restart it restores layout and can natively resume supported agent conversations when integration session references exist.

HermesStack therefore integrates Herdr's current session server directly and keeps tmux as a fallback rather than treating tmux as Herdr's persistence substrate.
