import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './styles/tokens.css'
import './styles/app.css'
import './styles/approvals.css'

const BUILD_ID = 'highlight-financial-warnings-2026-08-24-11'
const HOST_ORIGIN = 'https://orgf7602101.crm11.dynamics.com'

type RuntimeContext = {
  opportunityId: string | null
}

type RuntimeWindow = Window & {
  __boot?: Record<string, unknown>
  __dacContext?: RuntimeContext
  __dacOpportunityIdPromise?: Promise<string | null>
}

function isGuid(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value)
}

function readUrlOpportunityId(): string | null {
  const value = new URLSearchParams(window.location.search).get('opportunityId')?.trim() ?? ''
  return isGuid(value) ? value : null
}

export const opportunityIdPromise = new Promise<string | null>((resolve) => {
  const fromUrl = readUrlOpportunityId()
  if (fromUrl) {
    resolve(fromUrl)
    return
  }

  if (window.self === window.top) {
    resolve(null)
    return
  }

  let settled = false
  const finish = (opportunityId: string | null) => {
    if (settled) return
    settled = true
    window.removeEventListener('message', onMessage)
    resolve(opportunityId)
  }
  const onMessage = (event: MessageEvent<unknown>) => {
    console.info('[DealApprovalCentre] message received', { origin: event.origin, data: event.data })
    if (event.origin !== HOST_ORIGIN) return
    const data = event.data as { type?: unknown; opportunityId?: unknown } | null
    if (!data || data.type !== 'DAC_CONTEXT' || typeof data.opportunityId !== 'string') return
    const opportunityId = data.opportunityId.trim()
    if (!isGuid(opportunityId)) return
    if (event.source && event.source !== window) {
      ;(event.source as Window).postMessage({ type: 'DAC_ACK' }, HOST_ORIGIN)
    }
    finish(opportunityId)
  }

  window.addEventListener('message', onMessage)
  const targets: Window[] = []
  let ancestor: Window = window
  while (ancestor !== ancestor.parent && targets.length < 10) {
    ancestor = ancestor.parent
    targets.push(ancestor)
  }
  console.info('[DealApprovalCentre] broadcasting DAC_READY', { targetCount: targets.length, frameDepth: targets.length })
  for (const target of targets) {
    try {
      target.postMessage({ type: 'DAC_READY' }, '*')
    } catch (error) {
      console.warn('[DealApprovalCentre] DAC_READY target failed', error)
    }
  }
  window.setTimeout(() => finish(null), 15000)
})

;(window as RuntimeWindow).__dacOpportunityIdPromise = opportunityIdPromise

opportunityIdPromise.then((opportunityId) => {
  ;(window as RuntimeWindow).__dacContext = { opportunityId }
  const boot = {
    buildId: BUILD_ID,
    href: window.location.href,
    search: window.location.search,
    hash: window.location.hash,
    referrer: document.referrer,
    embedded: window.self !== window.top,
    windowName: window.name,
    opportunityId,
  }
  ;(window as RuntimeWindow).__boot = boot
  console.info('[DealApprovalCentre] boot', boot)
})

async function start() {
  const { default: App } = await import('./App.tsx')
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <App />
    </StrictMode>,
  )
}

opportunityIdPromise.then((opportunityId) => {
  if (opportunityId && !new URLSearchParams(window.location.search).get('opportunityId')) {
    const query = new URLSearchParams(window.location.search)
    query.set('opportunityId', opportunityId)
    window.history.replaceState(null, '', `${window.location.pathname}?${query.toString()}${window.location.hash}`)
  }
})

void start()
