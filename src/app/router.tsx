import { Navigate, createHashRouter } from 'react-router-dom'
import { ApprovalCentrePage } from '../pages/ApprovalCentrePage'
import { ApprovalDetailPage } from '../pages/ApprovalDetailPage'
import { OpportunityApprovalHistoryPage } from '../pages/OpportunityApprovalHistoryPage'
import { RequestDealApprovalPage } from '../pages/RequestDealApprovalPage'

function getOpportunityIdFromLocation(): string | null {
  const fromSearch = new URLSearchParams(window.location.search).get('opportunityId')
  if (fromSearch && fromSearch.trim()) {
    return fromSearch.trim()
  }

  const hashQueryIndex = window.location.hash.indexOf('?')
  if (hashQueryIndex === -1) {
    return null
  }

  const hashQuery = window.location.hash.slice(hashQueryIndex + 1)
  const fromHash = new URLSearchParams(hashQuery).get('opportunityId')
  return fromHash && fromHash.trim() ? fromHash.trim() : null
}

function RootEntryPage() {
  const opportunityId = getOpportunityIdFromLocation()

  if (opportunityId) {
    return (
      <Navigate
        replace
        to={`/request-deal-approval?opportunityId=${encodeURIComponent(opportunityId)}`}
      />
    )
  }

  return <ApprovalCentrePage />
}

export const appRouter = createHashRouter([
  {
    path: '/',
    element: <RootEntryPage />,
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
])
