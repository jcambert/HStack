# HermesStack

HermesStack is a local secure control plane for AI-agent development workspaces.

**M1 Secure Foundation is complete.** The next product increment is M2 Agent Runtime.

```text
hstack init
hstack cert add company-root.crt
hstack project add mascara C:\\Dev\\Mascara
hstack up mascara
hstack shell mascara
hstack status mascara
hstack down mascara
```

The control plane runs on the host. Managed coding agents run inside one Docker workspace per project. Docker daemon sockets, privileged containers and broad host mounts are forbidden by policy.

See `docs/architecture.md`, `docs/security.md`, `docs/agile/ROADMAP.md` and `docs/agile/M1-REPORT.md`.

## Development workflow

Development changes are batched before they are pushed to GitHub. Normal pushes to `main` do not run CI. A consolidated pull request is opened only when a substantial batch is ready for validation; that PR runs one Linux gate. Optional Windows validation stays manual. See `docs/agile/CI-POLICY.md`.
