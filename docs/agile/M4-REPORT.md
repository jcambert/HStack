# M4 Increment Report — Enterprise Network & Security

## Stories completed

- US-040 Corporate proxy propagation with mandatory NO_PROXY entries.
- US-041 Protected project/agent secret storage and process-scoped injection.
- US-042 Declared/live workspace security inspection and scoring.
- US-043 Host/project doctor diagnostics.

## Acceptance criteria

- Proxy supports HTTP_PROXY, HTTPS_PROXY, NO_PROXY and lowercase variants.
- Corporate CA behavior continues without TLS bypasses.
- Secret values are absent from hstack.yaml, projects.yaml and Compose.
- Secrets require matching project + agent + allow-list policy.
- security inspect reports the required runtime categories.
- docker.sock, privileged, host network and host-root mount are Critical.
- doctor supports project, network, certificate and security diagnostics.

## Security findings

M4 found no reason to widen the existing container boundary. Secrets remain host-side until a specific agent exec and are not added to the long-lived workspace environment.

## Validation

CI runs Windows restore/build/unit tests plus Linux restore/build/unit/integration, the M3 regression gate, and the M4 Docker end-to-end gate. Distribution artifacts are published only after both OS gates succeed on main.

## Known limitations

Interactive upstream agent authentication remains owned by each agent. M4's generic protected secret injection applies to direct HermesStack agent launches; Herdr-managed conversations continue to use the agents' project-scoped native authentication state.

## Next

M5 / EPIC-014 — Token Optimization.
