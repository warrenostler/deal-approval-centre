# Copilot Instructions

This is the Code App Template, a generic React/TypeScript/Vite starter for building code apps intended to be embedded in a Dynamics 365 / Power Platform model-driven app.

## Safety And Scope

- Preserve existing behaviour unless explicitly asked to change it.
- Keep changes small and scoped. Do not refactor unrelated code.
- Do not change Dataverse schema files, table names, field names, option-set values, auth logic, deployment config, or package versions unless explicitly requested.
- Do not make broad architectural changes unless explicitly requested.
- Do not delete files unless explicitly requested.

## Git Workflow (Mandatory)

- Treat `main` as the safe approved baseline.
- Never implement feature/fix work directly on `main`.
- For each task, branch from updated `main`.
- Use branch names:
	- `fix/short-description`
	- `feature/short-description`
	- `agent/short-description` for riskier Copilot/agent work
- Make and test changes only on that branch.
- Run `npm run build` before any deployment.
- Commit and push the branch.
- Deploy branch builds to DEV only.
- Do not merge to `main` until Warren has tested and approved in DEV.
- After approval: merge into `main`, push `main`, create a clear tag on `main`, deploy `main` to DEV, then delete completed branch.

## Deployment Rules

- Never deploy to UAT or Live unless explicitly instructed.

## Required Reporting

- Always confirm: branch, working tree status, build result, commit hash, deployment target, and warnings.