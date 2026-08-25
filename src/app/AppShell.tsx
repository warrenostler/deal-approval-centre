import { RouterProvider, createHashRouter, Outlet, useLocation } from 'react-router-dom'
import { appRouter } from './router'
import { AppHeader } from './AppHeader'

function ApprovalCentreLayout() {
  const location = useLocation()
  const showHeader = location.pathname === '/' || location.pathname === '/opportunity-history'

  return (
    <>
      {showHeader && <AppHeader />}
      <Outlet />
    </>
  )
}

export function AppShell() {
  const router = createHashRouter([
    {
      element: <ApprovalCentreLayout />,
      children: appRouter.routes,
    },
  ])
  return <RouterProvider router={router} />
}
