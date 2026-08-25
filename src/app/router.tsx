import { createHashRouter, Outlet, useLocation } from 'react-router-dom'
import { AppHeader } from './AppHeader'
import { ApprovalCentrePage } from '../pages/ApprovalCentrePage'
import { ApprovalDetailPage } from '../pages/ApprovalDetailPage'
import { OpportunityApprovalHistoryPage } from '../pages/OpportunityApprovalHistoryPage'
import { RequestDealApprovalPage } from '../pages/RequestDealApprovalPage'

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

export const appRouter = createHashRouter([
  {
    element: <ApprovalCentreLayout />,
    children: [
      {
        path: '/',
        element: <ApprovalCentrePage />,
      },
      {
        path: '/approvals/:approvalId',
        element: <ApprovalDetailPage />,
      },
      {
        path: '/opportunity-history',
        element: <OpportunityApprovalHistoryPage />,
      },
      {
        path: '/request-deal-approval',
        element: <RequestDealApprovalPage />,
      },
    ],
  },
])
