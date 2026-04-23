# Commit Policy

This document defines the contribution and commit conventions for the LifeSupportAlarms repository. It applies to all contributors — human and AI agents alike.

## Branch Naming

Each unit of work (feature, fix, phase) lives on its own branch before being merged to `main` via a pull request.

| Pattern | Use case |
|---|---|
| `feature/phase-N-<short-name>` | Planned implementation phases |
| `feature/<short-name>` | New features outside the phase plan |
| `fix/<short-name>` | Bug fixes |
| `chore/<short-name>` | Maintenance (deps, config, docs) |

## Commit Message Format

```
<type>(<scope>): <description> [<author-tag>]
```

- **type**: `feat`, `fix`, `docs`, `chore`, `refactor`, `test`
- **scope**: component being changed, e.g. `plugin`, `settings`, `build`
- **description**: short imperative sentence, no trailing period
- **author-tag**: identifies the author — use `[copilot]` for AI-assisted commits, or omit for purely human commits

**Examples**
```
feat(plugin): add core alarm logic for all 4 resources [copilot]
fix(settings): clamp lead time to valid range [copilot]
docs(readme): update installation instructions
chore(build): upgrade TargetFrameworkVersion to v4.8
```

## Pull Request Policy

- Every branch must be merged via a PR — no direct commits to `main`
- PR title should match the primary commit message (without the author-tag)
- PRs should be reviewed before merging, even when authored by an AI agent
- One phase per PR; do not mix phase work across PRs
- **Sole-developer exception**: if the only reviewer available is the PR creator, the approval requirement may be temporarily disabled in branch protection settings, the PR merged, then re-enabled

## Agent Identification

AI-assisted commits use a dedicated git identity set at the repo level (`.git/config`):

```
user.name  = GitHub Copilot
user.email = copilot-agent@users.noreply.github.com
```

This identity is configured with `git config --local` and applies to all commits made in this repository by an agent session. Human contributors who clone the repo should override this with their own identity via `git config --local user.name "..."` and `git config --local user.email "..."`.

In addition, AI-assisted commits must include `[copilot]` at the end of the commit message subject line, so the author tag is visible directly in the git log without needing to inspect commit metadata.

**PR creator identity**: `gh pr create` always attributes the PR to the authenticated GitHub account, regardless of the git commit identity. This is a GitHub platform constraint and cannot be worked around via the CLI or API. The commits themselves will correctly show `GitHub Copilot` as author; the PR creator being the human account owner is expected and acceptable.

## No Force Push

Do not force-push to `main` or any shared branch. Use `git revert` to undo merged changes.
