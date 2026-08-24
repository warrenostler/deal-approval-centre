import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { getOpportunityApprovalHistory, type OpportunityApprovalHistory } from '../services/approvalData'
import { Fmi_dealapprovalsfmi_approvalstatus, Fmi_dealapprovalsfmi_approvaltype } from '../generated/models/Fmi_dealapprovalsModel'
import { Opportunitiesfmi_currentapprovalstatus } from '../generated/models/OpportunitiesModel'
import { ArrowLeftIcon, CalendarIcon, CheckIcon, ClockIcon, DocumentIcon, GavelIcon } from '../components/icons'
import { formatDateTime, formatUsd } from '../utils/formatters'

function optionLabel(options: Record<number, string>, value: number | undefined, fallback: string): string {
  return value === undefined ? fallback : options[value] ?? fallback
}

export function OpportunityApprovalHistoryPage() {
  const [contractId, setContractId] = useState('')
  const [result, setResult] = useState<OpportunityApprovalHistory | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setLoading(true)
    setError(null)
    try {
      setResult(await getOpportunityApprovalHistory(contractId))
    } catch (reason: unknown) {
      setResult(null)
      setError(reason instanceof Error ? reason.message : 'The opportunity could not be searched.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <main className="approval-app history-page">
      <div className="detail-back-row"><Link to="/" className="back-link"><ArrowLeftIcon width={16} height={16} /><span>Back to approvals</span></Link></div>
      <header className="history-header">
        <div className="deal-heading-row">
          <span className="deal-heading-icon"><GavelIcon width={20} height={20} /></span>
          <div className="deal-heading-text">
            <p className="eyebrow">Opportunity lookup</p>
            <h1>Approval history</h1>
            <p className="header-copy">Find the current approval status and previous approval decisions for a DPS sale contract.</p>
          </div>
        </div>
        <form className="history-search" onSubmit={handleSearch}>
          <label htmlFor="contract-id">DPS sale contract ID</label>
          <div className="history-search-row">
            <input id="contract-id" value={contractId} onChange={(event) => setContractId(event.target.value)} placeholder="Enter contract ID" autoComplete="off" />
            <button type="submit" className="history-search-button" disabled={loading}>{loading ? 'Searching...' : 'Search'}</button>
          </div>
        </form>
      </header>

      {error && <div className="state-panel state-error history-state"><strong>Could not find this contract</strong><span>{error}</span></div>}
      {loading && <div className="state-panel history-state"><span className="spinner" />Loading approval history...</div>}
      {!loading && !error && result && (
        <>
          <section className="history-opportunity summary-card" aria-labelledby="opportunity-heading">
            <div className="history-opportunity-title"><span className="summary-icon"><DocumentIcon width={16} height={16} /></span><div><small>Opportunity</small><h2 id="opportunity-heading">{result.opportunity.name || 'Unnamed opportunity'}</h2><span>{result.opportunity.fmi_dpssalescontractid || contractId.trim()}</span></div></div>
            <div className="summary-item"><span className="summary-icon"><CheckIcon width={16} height={16} /></span><div><small>Current approval status</small><strong>{optionLabel(Opportunitiesfmi_currentapprovalstatus, result.opportunity.fmi_currentapprovalstatus, 'Not available')}</strong></div></div>
            <div className="summary-item"><span className="summary-icon"><GavelIcon width={16} height={16} /></span><div><small>Approval records</small><strong>{result.approvals.length}</strong></div></div>
          </section>

          <section className="history-panel" aria-labelledby="history-heading">
            <div className="history-panel-heading"><div><p className="eyebrow">Audit trail</p><h2 id="history-heading">Approval history</h2></div><span>{result.approvals.length === 1 ? '1 record' : `${result.approvals.length} records`}</span></div>
            {result.approvals.length === 0 && <div className="state-panel history-state"><strong>No approval history</strong><span>This opportunity has no Deal Approval records.</span></div>}
            {result.approvals.length > 0 && <div className="history-list">{result.approvals.map((approval) => {
              const status = optionLabel(Fmi_dealapprovalsfmi_approvalstatus, approval.fmi_approvalstatus, 'Unknown')
              const isPending = approval.fmi_approvalstatus === 1
              return <article className="history-entry" key={approval.fmi_dealapprovalid}>
                <div className="history-entry-marker"><ClockIcon width={17} height={17} /></div>
                <div className="history-entry-body">
                  <div className="history-entry-heading"><div><strong>{status}</strong><span>{optionLabel(Fmi_dealapprovalsfmi_approvaltype, approval.fmi_approvaltype, 'Approval')} {approval.fmi_approvalversion ? `· Version ${approval.fmi_approvalversion}` : ''}</span></div>{isPending ? <Link className="history-detail-link" to={`/approvals/${approval.fmi_dealapprovalid}`}>Open approval</Link> : <span className="history-complete">Recorded</span>}</div>
                  <div className="history-entry-meta"><span><CalendarIcon width={15} height={15} />{formatDateTime(approval.fmi_decisionon || approval.fmi_approvalsenton || approval.createdon)}</span><span>Value {formatUsd(approval.fmi_submitteddealvalue)}</span></div>
                  {(approval.fmi_decisioncomments || approval.fmi_requestorcomment) && <p className="history-comment">{approval.fmi_decisioncomments || approval.fmi_requestorcomment}</p>}
                </div>
              </article>
            })}</div>}
          </section>
        </>
      )}
    </main>
  )
}