import { createHashRouter } from 'react-router-dom'
import { ApprovalCentrePage } from '../pages/ApprovalCentrePage'
import { ApprovalDetailPage } from '../pages/ApprovalDetailPage'

export const appRouter = createHashRouter([
  {
    path: '/',
    element: <ApprovalCentrePage />,
  },
  {
    path: '/approvals/:approvalId',
    element: <ApprovalDetailPage />,
  },
])
