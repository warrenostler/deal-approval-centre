# Code App Template

Reusable Power Platform code app template built with React, TypeScript, and Vite.

## Configure power.config.json

1. Open power.config.json.
2. Replace YOUR_APP_ID with your Model-driven App ID.
3. Replace YOUR_ENV_ID with your Power Platform Environment ID.
4. Set appDisplayName to your new app name.

Where to find values:
- App ID: make.powerapps.com > Apps > Model-driven app details.
- Environment ID: make.powerapps.com > top-right environment selector > Environment details.

## Add Dataverse data sources

Add the Dataverse tables your app needs as data sources:

npx power-apps add-data-source --data-source <table-logical-name>

Examples:

npx power-apps add-data-source --data-source accounts
npx power-apps add-data-source --data-source contacts

## Folder structure

- src/pages: route-level pages.
- src/components/common: reusable shared UI pieces.
- src/services/dataverse: Dataverse client and generic service helpers.
- src/types: shared TypeScript interfaces.
- src/styles: design tokens and global styles.

Add new feature slices by creating:
- page components in src/pages
- data services in src/services/dataverse
- model interfaces in src/types

## Run, build, push

- Dev server: npm run dev
- Build: npm run build
- Push to Power Apps: npm run power:push

Recommended flow:
1. npm install
2. npm run build
3. npm run power:push
