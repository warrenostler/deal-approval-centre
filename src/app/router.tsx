import { createHashRouter } from 'react-router-dom'
import { ApprovalCentrePage } from '../pages/ApprovalCentrePage'
import { ApprovalDetailPage } from '../pages/ApprovalDetailPage'
import { RequestDealApprovalPage } from '../pages/RequestDealApprovalPage'

export const appRouter = createHashRouter([
  {
    path: '/',
    element: <ApprovalCentrePage />,
  },
  {
    path: '/approvals/:approvalId',
    element: <ApprovalDetailPage />,
  },
  {
    path: '/request-deal-approval',
    element: <RequestDealApprovalPage />,
  },
])
