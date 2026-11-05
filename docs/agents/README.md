# Agent task guides

Start with [AGENTS.md](../../AGENTS.md). These focused playbooks give a new agent
enough context to finish a task without requiring a maintainer to reconstruct
the project in chat. They are repository instructions, not automatically running
agent processes or an MCP client configuration.

| Task | Guide | Expected handoff |
| --- | --- | --- |
| README, onboarding, manual, website | [Documentation](documentation.md) | Accurate instructions, generated pages where applicable, link/search checks |
| MCP adapter or Unity command endpoint | [MCP](mcp.md) | Shared-workspace behavior, protocol checks, clear async completion evidence |
| Experiments, GUI, tracking, recording, replay | [Runtime](runtime.md) | Production change, focused regressions, appropriate Unity/player evidence |
| Stable integration, versioning, packages, publication | [Release](release.md) | Reviewed revision, checks, immutable tag, truthful notes and asset inventory |

For cross-cutting work, one integrating agent owns the final diff and validation.
Delegate by files or subsystem, declare shared-file ownership before editing, and
give each agent an acceptance condition. Do independent reads/checks in parallel;
keep dependent edits, builds, and publication sequential. Do not let multiple
Unity processes use the same disposable project.

## Common evidence rules

Use the current code and the user's requirements as the source of truth.
Historical plans and dated JSON reports explain earlier decisions; they do not
prove that a new revision passed. Keep the source revision, exact command,
result, and limits of any substantial runtime validation. Save generated logs
and bulky temporary runs under ignored local output directories, and retain only
the intentional review evidence in Git.

When a check fails, determine whether the failure comes from the change, an
existing unrelated issue, or a missing dependency. Fix failures in scope, report
the rest precisely, and continue other independent work. Do not label an
unexecuted check as passed or use a source assertion as evidence of native UI
acceptance.
