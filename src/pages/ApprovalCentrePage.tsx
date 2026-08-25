import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { getPendingApprovals, type ApprovalSummary } from '../services/approvalData'
import { formatUsd } from '../utils/formatters'
import { CalendarIcon, ChevronDownIcon, UserIcon } from '../components/icons'

function getPendingDays(requestedOn: string): number {
  const requestedTime = new Date(requestedOn).getTime()
  if (Number.isNaN(requestedTime)) return 0
  return Math.max(0, Math.floor((Date.now() - requestedTime) / 86400000))
}

function getPendingClass(days: number): string {
  if (days >= 6) return 'pending-critical'
  if (days >= 3) return 'pending-warning'
  return 'pending-fresh'
}

export function ApprovalCentrePage() {
  const [approvals, setApprovals] = useState<ApprovalSummary[]>([])
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => { let active = true; getPendingApprovals().then((items) => { if (active) setApprovals(items) }).catch((reason: unknown) => { if (active) setError(reason instanceof Error ? reason.message : 'The approval queue could not be loaded.') }).finally(() => { if (active) setLoading(false) }); return () => { active = false } }, [])
  const visibleApprovals = useMemo(() => { const term = search.trim().toLowerCase(); return approvals.filter((approval) => !term || `${approval.companyName} ${approval.opportunityName} ${approval.salesExecutiveName}`.toLowerCase().includes(term)).sort((left, right) => new Date(left.requestedOn).getTime() - new Date(right.requestedOn).getTime()) }, [approvals, search])
  return <main className="approval-app queue-page"><header className="approval-header"><div><h1>Deal Approval Centre</h1><Link className="history-nav-link" to="/opportunity-history">Find a deal by contract ID</Link></div><div className="pending-count" aria-label={`${approvals.length} pending approvals`}><strong>{approvals.length}</strong><span>Pending</span></div></header><section className="queue-panel" aria-labelledby="queue-heading"><div className="queue-toolbar"><div><h2 id="queue-heading">Pending approvals</h2><span className="toolbar-note">Oldest requests first</span></div><label className="search-field"><span>Search company, deal or Sales Executive</span><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search..." /></label></div>{loading && <div className="state-panel"><span className="spinner" />Loading approvals...</div>}{!loading && error && <div className="state-panel state-error"><strong>Could not load approvals</strong><span>{error}</span></div>}{!loading && !error && visibleApprovals.length === 0 && <div className="state-panel"><strong>{search ? 'No matching approvals' : 'Nothing is waiting for approval'}</strong><span>{search ? 'Try a different company, deal or Sales Executive.' : 'The pending queue is currently clear.'}</span></div>}{!loading && !error && visibleApprovals.length > 0 && <div className="approval-list">{visibleApprovals.map((approval) => { const pendingDays = getPendingDays(approval.requestedOn); return <Link className="approval-card" to={`/approvals/${approval.dealApprovalId}`} key={approval.dealApprovalId}><div className="approval-card-main"><span className="card-kicker">{approval.companyName || 'Company unavailable'}</span><h3>{approval.opportunityName || 'Deal name unavailable'}</h3><div className="card-meta"><span><UserIcon width={18} height={18} />{approval.salesExecutiveName || 'Unassigned'}</span><span className={`pending-age ${getPendingClass(pendingDays)}`}><CalendarIcon width={18} height={18} />{pendingDays} {pendingDays === 1 ? 'day' : 'days'} pending</span></div></div><div className="approval-card-metrics"><span><small>Value</small>{formatUsd(approval.submittedDealValue)}</span><span><small>Items</small>{approval.itemCount}</span><span className={approval.belowForecastCount > 0 ? 'metric-warning' : ''}><small>Below forecast</small>{approval.belowForecastCount}</span></div><span className="card-arrow" aria-hidden="true"><span>View details</span><ChevronDownIcon width={17} height={17} /></span></Link>})}</div>}</section></main>
}