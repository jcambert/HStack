# HermesStack

HermesStack is a local secure control plane for AI-agent development workspaces.

**M3 Sessions is complete.** Herdr 0.9.3 is pinned inside the managed workspace and owns persistent project sessions for Claude Code, Codex, Hermes Agent and OpenCode. tmux remains available as a simple fallback multiplexer.

```text
hstack init
hstack project add mascara C:\\Dev\\Mascara

hstack session init mascara
hstack session status mascara
hstack session run codex mascara --name reviewer -- -m gpt-5.4
hstack herdr mascara
```

Detach from Herdr and return later: its server and agent panes keep running while the workspace container is alive. After a container recreation, Herdr restores the saved project layout and current official agent integrations provide native conversation restore when the upstream agent has reported a resumable session id.

Direct M2 launch remains available as an explicit fallback:

```text
hstack claude mascara
hstack codex mascara
hstack hermes mascara
hstack opencode mascara
hstack tmux mascara
```

## Project-scoped state

Authentication, agent state and Herdr session state remain isolated per project under `~/.hstack/data/projects/<project>/`. HermesStack never implicitly mounts the host user's agent, SSH, Docker or cloud credentials into the workspace.

Herdr uses the project HOME (`/home/hstack`) and the deterministic session name `hstack-<project>`. Its official Claude, Codex, Hermes and OpenCode integrations are installed into those same project-scoped agent homes.

See `docs/architecture.md`, `docs/security.md`, `docs/agile/ROADMAP.md` and `docs/agile/M3-REPORT.md`.

## Development workflow

Development changes are batched before they are pushed to GitHub. Normal pushes to `main` do not run CI. A consolidated pull request is opened only when a substantial batch is ready for validation; that PR runs one Linux gate. Optional Windows validation stays manual. See `docs/agile/CI-POLICY.md`.
