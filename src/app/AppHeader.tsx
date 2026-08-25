import { useEffect, useState } from 'react'
import { NavLink } from 'react-router-dom'
import { getPendingApprovals } from '../services/approvalData'

export function AppHeader() {
  const [pendingCount, setPendingCount] = useState<number | null>(null)

  useEffect(() => {
    let active = true
    getPendingApprovals().then((approvals) => {
      if (active) setPendingCount(approvals.length)
    }).catch(() => {
      if (active) setPendingCount(null)
    })
    return () => { active = false }
  }, [])

  return (
    <header className="approval-centre-header">
      <div className="approval-centre-heading">
        <h1>Deal Approval Centre</h1>
        <div className="approval-centre-tabs" role="tablist" aria-label="Approval views">
          <NavLink to="/" end className={({ isActive }) => `approval-centre-tab${isActive ? ' is-active' : ''}`} role="tab">Pending approvals</NavLink>
          <NavLink to="/opportunity-history" className={({ isActive }) => `approval-centre-tab${isActive ? ' is-active' : ''}`} role="tab">Approval history</NavLink>
        </div>
      </div>
      <div className="pending-count" aria-label={`${pendingCount ?? 0} pending approvals`}>
        <strong>{pendingCount ?? '-'}</strong>
        <span>Pending</span>
      </div>
    </header>
  )
}
