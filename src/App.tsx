import { useEffect } from 'react'
import { AppShell } from './app/AppShell'
import { AppProviders } from './app/providers'

function App() {
  useEffect(() => {
    const stamp = () => new Date().toISOString()

    const logLocation = (eventName: string) => {
      console.info('[App] Location event', {
        eventName,
        at: stamp(),
        href: window.location.href,
        pathname: window.location.pathname,
        search: window.location.search,
        hash: window.location.hash,
      })
    }

    const onHashChange = () => logLocation('hashchange')
    const onPopState = () => logLocation('popstate')

    logLocation('mount')
    window.addEventListener('hashchange', onHashChange)
    window.addEventListener('popstate', onPopState)

    return () => {
      logLocation('unmount')
      window.removeEventListener('hashchange', onHashChange)
      window.removeEventListener('popstate', onPopState)
    }
  }, [])

  return (
    <AppProviders>
      <AppShell />
    </AppProviders>
  )
}

export default App
