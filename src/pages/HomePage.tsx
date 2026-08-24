import { PageHeader } from '../components/common/PageHeader'

export function HomePage() {
  return (
    <main className="page">
      <section className="page-content">
        <PageHeader
          title="Code App Template"
          subtitle="Start a new Power Platform app from scratch"
        />

        <div style={{ padding: '2rem', maxWidth: '800px' }}>
          <h2>Getting Started</h2>
          <p>
            This repository is a clean React + TypeScript + Vite starter for building code apps embedded in Dynamics 365 / Power Platform model-driven apps.
          </p>

          <h3>Next Steps</h3>
          <ol>
            <li>
              <strong>Customize your app:</strong> Update <code>package.json</code> with your app name and version.
            </li>
            <li>
              <strong>Configure Power Apps:</strong> Update <code>power.config.json</code> with your environment details (appId, environmentId, region).
            </li>
            <li>
              <strong>Add data sources:</strong> Use <code>power-apps add-data-source</code> to register the Dataverse tables your app needs.
            </li>
            <li>
              <strong>Create your first feature:</strong> Add a page in <code>src/pages</code> and wire it in <code>src/app/router.tsx</code>.
            </li>
            <li>
              <strong>Add Dataverse queries:</strong> Use <code>src/services/dataverse/client.ts</code> for retrieve/create/update/delete operations.
            </li>
            <li>
              <strong>Build and deploy:</strong> Run <code>npm run build</code> to compile, then <code>npm run power:push</code> to push to your configured environment.
            </li>
          </ol>

          <h3>Key Directories</h3>
          <ul>
            <li><code>src/pages</code> - Route-level pages</li>
            <li><code>src/components/common</code> - Reusable shared UI components</li>
            <li><code>src/services/dataverse</code> - Generic Dataverse client and service helpers</li>
            <li><code>src/types</code> - Shared TypeScript types</li>
            <li><code>src/styles</code> - Global CSS and design tokens</li>
          </ul>

          <h3>Key Files</h3>
          <ul>
            <li><code>package.json</code> - Scripts and dependencies</li>
            <li><code>power.config.json</code> - Power Apps environment settings</li>
            <li><code>src/services/dataverse/client.ts</code> - Dataverse runtime client wrapper</li>
            <li><code>src/app/router.tsx</code> - Application routes</li>
          </ul>
        </div>
      </section>
    </main>
  )
}
