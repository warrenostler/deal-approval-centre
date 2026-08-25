import { RouterProvider } from 'react-router-dom'
import { appRouter } from './router'

export function AppShell() {
  return <RouterProvider router={appRouter} />
}
