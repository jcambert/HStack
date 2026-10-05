# HermesStack

HermesStack is a local secure control plane for AI-agent development workspaces.

Current milestone: **M1 Secure Foundation**.

```text
hstack init
hstack project add mascara C:\\Dev\\Mascara
hstack up mascara
hstack shell mascara
hstack status mascara
hstack down mascara
```

The control plane runs on the host. Managed coding agents will run inside one Docker workspace per project. Docker daemon sockets, privileged containers and broad host mounts are forbidden by policy.

See `docs/architecture.md`, `docs/security.md` and `docs/agile/ROADMAP.md`.

## Development workflow

Development changes are batched before they are pushed to GitHub. CI is manual by default (or runs on pull requests) to avoid consuming GitHub Actions capacity on small intermediate edits. See `docs/agile/CI-POLICY.md`.
