import { Navigate, createBrowserRouter } from 'react-router-dom'
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

function getBootOpportunityId(): string | null {
  const boot = (window as Window & { __boot?: { opportunityId?: string | null } }).__boot
  return boot?.opportunityId ?? null
}

function getRuntimeOpportunityId(): string | null {
  const context = (window as Window & { __dacContext?: { opportunityId?: string | null } }).__dacContext
  return context?.opportunityId ?? null
}

function RootEntryPage() {
  const opportunityId = getOpportunityIdFromLocation() ?? getRuntimeOpportunityId() ?? getBootOpportunityId()

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

export const appRouter = createBrowserRouter([
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
], { basename: window.location.pathname })
