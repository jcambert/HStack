# M2 Increment Report — Agent Runtime

## Scope completed

M2 delivers the agent abstraction plus Claude Code, Codex, Hermes Agent and OpenCode inside the managed workspace image.

Pinned toolchain:

| Tool | Version | Installation strategy |
|---|---:|---|
| Claude Code | 2.1.289 | exact official native release artifact with published SHA-256 |
| Codex | 0.160.0 | supported npm package, exact version |
| Hermes Agent | 0.21.5 / v2026.9.24 / f97608f | official release source + pinned uv.lock, exact stable tag + commit |
| OpenCode | 1.18.34 | supported npm package, exact version |

`toolchain.lock.yaml` is loaded by the control plane and supplies the Docker build arguments; `latest` and deferred values are rejected for M2 tools.

## Functional acceptance

- `IAgentHarness` and `IAgentHarnessRegistry` exist.
- Four concrete harnesses are registered without a central agent switch.
- `hstack agent run <agent> --project <project>` launches the agent in `/workspace`.
- `hstack claude|codex|hermes|opencode <project>` delegates to the same launch path.
- `hstack auth <agent> --project <project>` delegates to each upstream authentication/setup flow (including `claude auth login`).
- A stopped workspace is started before an agent is launched.
- `hstack agent list --project <project>` inspects installed versions inside the running workspace.

## State isolation

Per project:

```text
data/projects/<project>/
  home/
  claude/
  codex/
  hermes/
  opencode/config/
  opencode/data/
```

These directories are mounted only into their corresponding agent homes. The host user's agent credentials are not consumed.

## Security configuration

- Codex credentials are stored as files under project `CODEX_HOME`; update checks are disabled.
- Claude state is directed to project `CLAUDE_CONFIG_DIR`.
- Hermes state is directed to project `HERMES_HOME`; `TERMINAL_ENV=local` ensures it uses the existing workspace sandbox rather than Docker-on-Docker.
- OpenCode config/data are project-scoped and managed auto-update is disabled. XDG parent directories are pre-created in the project-owned HOME so nested binds never leave root-owned host directories.
- M1 container restrictions remain unchanged.

## Test strategy

The M2 gate runs:

1. Release build.
2. Unit tests for registry behavior, auth command delegation, config generation, toolchain pin validation and deployment-plan isolation.
3. Integration tests.
4. A real Docker end-to-end smoke test that builds the pinned image, launches ProjectA, verifies all four exact versions, exercises all four public aliases, checks agent registry inspection, verifies every state bind, writes markers through each agent home and proves no marker or mount leaks into ProjectB.

Interactive third-party account login is intentionally not automated in CI because doing so would require real external credentials. The command path and persistent storage targets are tested without placing secrets in CI.

## Next increment

M3 integrates Herdr for resumable/named sessions while preserving direct harness launch as a fallback.
