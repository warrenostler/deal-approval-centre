# Development Workflow

This document defines the required Git and deployment workflow for Code App Template changes.

## Rules

- `main` is the safe approved baseline.
- Never make feature or fix changes directly on `main`.
- For each change, create a new branch from `main`.
- Keep changes scoped. Do not refactor unrelated code.
- Run `npm run build` before deployment.
- Commit the change to the branch.
- Push the branch to GitHub.
- Deploy the branch build to the DEV Power Platform environment for testing.
- Do not merge to `main` until Warren has tested and approved the change in DEV.
- Never deploy to UAT or Live unless explicitly instructed.
- Never change Dataverse schema files, table names, field names, option-set values, auth logic, deployment config, or package versions unless explicitly requested.

## Branch Naming

Use these branch prefixes:

- `fix/short-description`
- `feature/short-description`
- `agent/short-description` for riskier Copilot/agent work

## Standard Change Flow

1. Checkout `main`.
2. Pull latest from origin.
3. Create a new change branch from `main`.
4. Make only the requested change on that branch.
5. Run `npm run build`.
6. Commit the change to the branch.
7. Push the branch to GitHub.
8. Deploy that branch build to DEV.
9. Wait for Warren to test and approve in DEV.

## Promotion To Main (After Approval)

1. Merge the approved branch into `main`.
2. Push `main`.
3. Create a clear Git tag on `main` as a restore point.
4. Push the tag.
5. Deploy `main` to DEV so DEV reflects the approved baseline.
6. Delete completed feature/fix branches only after merge, tag, and DEV deployment are confirmed.

## Required Status Confirmation

For each change and deployment, always confirm:

- Branch name
- Working tree status
- Build result
- Commit hash
- Deployment target
- Any warnings
