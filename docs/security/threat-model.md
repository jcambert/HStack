# Threat model

## Assets

Host files outside the selected project, credentials, Docker daemon, corporate trust material, agent state and project source code.

## Primary threats

Malicious prompts or repositories may induce destructive commands, secret exfiltration, package compromise, excessive mounts, Docker daemon access, LAN exposure, TLS bypass or container privilege escalation.

## M1 mitigations

Exact project bind mounts, deny-by-default host path policy, non-root workspace user, dropped capabilities, no-new-privileges, localhost-only ports, no docker.sock, project-scoped runtime/data directories, and explicit CA injection.

## Residual risk

Docker reduces blast radius but is not equivalent to a strongly isolated VM. Future execution providers may add VM or remote sandbox isolation for higher-risk workloads.
