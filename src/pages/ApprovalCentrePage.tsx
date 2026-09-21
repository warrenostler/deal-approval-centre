import { useEffect, useMemo, useState } from 'react'
import { Opportunitiesfmi_salestype } from '../generated/models/OpportunitiesModel'
import { Link } from 'react-router-dom'
import { getApprovalQueue, type ApprovalSummary, type ApprovalQueue } from '../services/approvalData'
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
  const [access, setAccess] = useState<Omit<ApprovalQueue, 'approvals'> | null>(null)
  const [scope, setScope] = useState<'mine' | 'all'>('mine')
  const [search, setSearch] = useState('')
  const [dealType, setDealType] = useState('all')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    getApprovalQueue().then((queue) => {
      if (!active) return
      setApprovals(queue.approvals)
      setAccess(queue)
      const hasAssigned = queue.approvals.some((approval) => approval.approverId === queue.callerId)
      if (queue.isReadOnlyTeamMember && !queue.isSuperApprover && !queue.isApproverTeamMember && !hasAssigned) setScope('all')
    }).catch((reason: unknown) => {
      if (active) setError(reason instanceof Error ? reason.message : 'The approval queue could not be loaded.')
    }).finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [])

  const visibleApprovals = useMemo(() => {
    const term = search.trim().toLowerCase()
    return approvals
      .filter((approval) => scope === 'all' || !access?.callerId || approval.approverId === access.callerId)
      .filter((approval) => dealType === 'all' || String(approval.salesType) === dealType)
      .filter((approval) => !term || `${approval.companyName} ${approval.opportunityName} ${approval.salesExecutiveName}`.toLowerCase().includes(term))
      .sort((left, right) => new Date(left.requestedOn).getTime() - new Date(right.requestedOn).getTime())
  }, [approvals, search, dealType, scope, access])
  const hasFilters = search.trim() !== '' || dealType !== 'all'
  const canViewAll = access?.isSuperApprover || access?.isReadOnlyTeamMember

  return (
    <main className="approval-app queue-page">
      <section className="queue-panel" aria-labelledby="queue-heading">
        <div className="queue-toolbar">
          <div><h2 id="queue-heading">Pending approvals</h2><span className="toolbar-note">Oldest requests first</span></div>
          <label className="deal-type-field"><span>Deal type</span>
            <select value={dealType} onChange={(event) => setDealType(event.target.value)}>
              <option value="all">All deal types</option>
              {Object.entries(Opportunitiesfmi_salestype).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
              {approvals.some((approval) => approval.salesType === null) && <option value="null">Type unavailable</option>}
            </select>
          </label>
          <label className="search-field"><span>Search company, deal or Sales Executive</span>
            <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search..." />
          </label>
        </div>
        <div className="queue-scope-row">
          {access?.callerId && <div className="queue-scope" role="group" aria-label="Approval scope">
            <button type="button" aria-pressed={scope === 'mine'} onClick={() => setScope('mine')}>Assigned to Me</button>
            {canViewAll && <button type="button" aria-pressed={scope === 'all'} onClick={() => setScope('all')}>All</button>}
          </div>}
          <span className="toolbar-note" role="status">{!loading && !error ? `${visibleApprovals.length} of ${approvals.length} pending approvals` : ''}</span>
        </div>
        {loading && <div className="state-panel"><span className="spinner" />Loading approvals...</div>}
        {!loading && error && <div className="state-panel state-error"><strong>Could not load approvals</strong><span>{error}</span></div>}
        {!loading && !error && visibleApprovals.length === 0 && <div className="state-panel">
          <strong>{hasFilters ? 'No matching approvals' : scope === 'mine' && access?.callerId ? 'No approvals assigned to you' : 'Nothing is waiting for approval'}</strong>
          <span>{hasFilters ? 'Try a different deal type or search.' : scope === 'mine' && canViewAll ? 'Select All to view the other pending approvals available to you.' : 'The pending queue is currently clear.'}</span>
        </div>}
        {!loading && !error && visibleApprovals.length > 0 && <div className="approval-list">{visibleApprovals.map((approval) => {
          const pendingDays = getPendingDays(approval.requestedOn)
          const canDecide = access?.isSuperApprover || Boolean(access?.callerId && approval.approverId === access.callerId)
          return <Link className="approval-card" to={`/approvals/${approval.dealApprovalId}`} key={approval.dealApprovalId}>
            <div className="approval-card-main">
              <span className="card-kicker">{approval.companyName || 'Company unavailable'}</span>
              <span className="deal-type-badge">{Opportunitiesfmi_salestype[approval.salesType as keyof typeof Opportunitiesfmi_salestype] ?? 'Type unavailable'}</span>
              <h3>{approval.opportunityName || 'Deal name unavailable'}</h3>
              <div className="card-meta">
                <span><UserIcon width={18} height={18} />{approval.salesExecutiveName || 'Unassigned'}</span>
                <span className={`pending-age ${getPendingClass(pendingDays)}`}><CalendarIcon width={18} height={18} />{pendingDays} {pendingDays === 1 ? 'day' : 'days'} pending</span>
              </div>
              {scope === 'all' && <p className="queue-assignee">Assigned to {approval.approverName || 'Unassigned'}{access?.callerId && !canDecide ? ' · View only' : ''}</p>}
            </div>
            <div className="approval-card-metrics">
              <span><small>Value</small>{formatUsd(approval.submittedDealValue)}</span>
              {approval.salesType !== 797300008 && <span><small>Items</small>{approval.itemCount}</span>}
              {approval.salesType !== null && ![797300006, 797300007, 797300008].includes(approval.salesType) && <span className={approval.belowForecastCount > 0 ? 'metric-warning' : ''}><small>Below forecast</small>{approval.belowForecastCount}</span>}
            </div>
            <span className="card-arrow" aria-hidden="true"><span>View details</span><ChevronDownIcon width={17} height={17} /></span>
          </Link>
        })}</div>}
      </section>
    </main>
  )
}
