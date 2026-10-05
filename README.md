# HermesStack

HermesStack is a local secure control plane for AI-agent development workspaces.

**M2 Agent Runtime is complete.** Claude Code, Codex, Hermes Agent and OpenCode are pinned in the workspace image and launched only inside the selected project's Docker workspace.

```text
hstack init
hstack project add mascara C:\\Dev\\Mascara
hstack up mascara

hstack agent list --project mascara
hstack claude mascara
hstack codex mascara
hstack hermes mascara
hstack opencode mascara

hstack auth claude --project mascara
hstack auth codex --project mascara
hstack auth hermes --project mascara
hstack auth opencode --project mascara
```

The ergonomic agent commands are aliases of the same harness/registry path used by `hstack agent run`; they do not duplicate launch policy.

## Project-scoped state

Agent authentication and state are isolated per project:

```text
~/.hstack/data/projects/<project>/
├── home/
├── claude/          -> /home/hstack/.claude
├── codex/           -> /home/hstack/.codex
├── hermes/          -> /home/hstack/.hermes
└── opencode/
    ├── config/      -> /home/hstack/.config/opencode
    └── data/        -> /home/hstack/.local/share/opencode
```

HermesStack never automatically mounts the host user's Claude, Codex, Hermes, OpenCode, SSH or Docker credentials into a managed workspace.

See `docs/architecture.md`, `docs/security.md`, `docs/agile/ROADMAP.md` and `docs/agile/M2-REPORT.md`.

## Development workflow

Development changes are batched before they are pushed to GitHub. Normal pushes to `main` do not run CI. A consolidated pull request is opened only when a substantial batch is ready for validation; that PR runs one Linux gate. Optional Windows validation stays manual. See `docs/agile/CI-POLICY.md`.
