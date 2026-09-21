import { useEffect, useRef, useState } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { getApprovalDetail, submitApprovalDecision, type ApprovalDecision, type ApprovalDetail } from '../services/approvalData'
import { formatDateOnly, formatDateTime, formatUsd } from '../utils/formatters'
import { ArrowLeftIcon, CalendarIcon, CheckIcon, ChevronDownIcon, CommentIcon, DocumentIcon, DollarIcon, GavelIcon, LayersIcon, UserIcon, XIcon } from '../components/icons'

export function ApprovalDetailPage() {
  const { approvalId } = useParams()
  const location = useLocation()
  const navigate = useNavigate()
  const [approval, setApproval] = useState<ApprovalDetail | null>(null)
  const [comment, setComment] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [decisionError, setDecisionError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState<ApprovalDecision | null>(null)
  const [commentExpanded, setCommentExpanded] = useState(false)
  const [itemsExpanded, setItemsExpanded] = useState(false)
  const [commentSectionOpen, setCommentSectionOpen] = useState(false)
  const [itemsSectionOpen, setItemsSectionOpen] = useState(false)
  const [historyItemId, setHistoryItemId] = useState<string | null>(null)
  const commentRef = useRef<HTMLTextAreaElement>(null)

  useEffect(() => {
    if (!approvalId) { setError('This approval link does not contain an approval ID.'); return }
    let active = true
    getApprovalDetail(approvalId).then((value) => {
      if (!active) return
      setApproval(value)
      setCommentSectionOpen(Boolean(value.fmi_requestorcomment))
      setItemsSectionOpen(value.items.length > 0 && value.items.length <= 5)
    }).catch((reason: unknown) => {
      if (active) setError(reason instanceof Error ? reason.message : 'The approval could not be loaded.')
    })
    return () => { active = false }
  }, [approvalId])

  async function handleDecision(decision: ApprovalDecision) {
    if (!approvalId || submitting || !approval?.canDecide) return
    const trimmedComment = comment.trim()
    if (decision === 'reject' && !trimmedComment) {
      setDecisionError('A rejection comment is required.')
      window.setTimeout(() => commentRef.current?.focus(), 0)
      return
    }
    setDecisionError(null)
    setSubmitting(decision)
    try {
      await submitApprovalDecision(approvalId, decision, trimmedComment)
      setSuccess(decision === 'approve' ? 'Approval submitted.' : 'Rejection submitted.')
      window.setTimeout(() => navigate('/'), 700)
    } catch (reason: unknown) {
      setDecisionError(reason instanceof Error ? reason.message : 'The decision could not be submitted.')
      setSubmitting(null)
    }
  }

  const backLink = (
    <Link to="/" className="back-link">
      <ArrowLeftIcon width={16} height={16} />
      <span>Back to approvals</span>
    </Link>
  )

  if (error) {
    return (
      <main className="approval-app detail-page">
        <div className="detail-back-row">{backLink}</div>
        <div className="state-panel state-error"><strong>Unable to open approval</strong><span>{error}</span></div>
      </main>
    )
  }

  if (!approval) {
    return (
      <main className="approval-app detail-page">
        <div className="detail-back-row">{backLink}</div>
        <div className="state-panel"><span className="spinner" />Loading approval detail...</div>
      </main>
    )
  }

  const commentText = approval.fmi_requestorcomment
  const commentPreview = commentText ? 'Comment provided' : 'No coordinator comment provided.'
  const itemCount = approval.items.length
  const visibleItems = approval.items.slice(0, itemsExpanded ? itemCount : 5)
  const historyItem = approval.items.find((item) => item.fmi_dealapprovalitemid === historyItemId)
  const returnPath = typeof location.state === 'object' && location.state !== null && 'from' in location.state && typeof location.state.from === 'string' ? location.state.from : '/'

  return (
    <main className="approval-app detail-page">
      <div className="detail-back-row"><Link to={returnPath} className="back-link"><ArrowLeftIcon width={16} height={16} /><span>{returnPath.startsWith('/opportunity-history') ? 'Back to deal history' : 'Back to main menu'}</span></Link></div>

      <div className="deal-heading-row">
        <span className="deal-heading-icon"><DocumentIcon width={20} height={20} /></span>
        <div className="deal-heading-text">
          <h1>{approval.fmi_opportunityname || 'Deal approval'}</h1>
          <p className="header-copy">{approval.fmi_submittedcompanyname || 'Company unavailable'}</p>
        </div>
      </div>

      <section className="summary-card">
        <div className="summary-item">
          <span className="summary-icon"><UserIcon width={16} height={16} /></span>
          <div><small>Requested by</small><strong>{approval.fmi_requestedbyname || '-'}</strong></div>
        </div>
        <div className="summary-item">
          <span className="summary-icon"><UserIcon width={16} height={16} /></span>
          <div><small>Assigned approver</small><strong>{approval.fmi_approvername || '-'}</strong></div>
        </div>
        <div className="summary-item">
          <span className="summary-icon"><UserIcon width={16} height={16} /></span>
          <div><small>Sales Executive</small><strong>{approval.salesExecutiveName || 'Unassigned'}</strong></div>
        </div>
        <div className="summary-item">
          <span className="summary-icon"><CalendarIcon width={16} height={16} /></span>
          <div><small>Requested</small><strong>{formatDateTime(approval.createdon)}</strong></div>
        </div>
        <div className="summary-item">
          <span className="summary-icon"><DollarIcon width={16} height={16} /></span>
          <div><small>Submitted deal value</small><strong>{formatUsd(approval.fmi_submitteddealvalue)}</strong></div>
        </div>
      </section>

      {commentText && (
        <section className={`info-card accordion-card ${commentSectionOpen ? 'is-open' : ''}`}>
          <button type="button" className="accordion-trigger" onClick={() => setCommentSectionOpen((open) => !open)}>
            <span className="info-card-icon"><CommentIcon width={16} height={16} /></span>
            <strong>Requestor Comment</strong>
            <span className="accordion-summary">{commentPreview}</span>
            <ChevronDownIcon width={16} height={16} className="accordion-chevron" />
          </button>
          {commentSectionOpen && (
            <div className="accordion-content">
              <p className={`comment-box ${commentExpanded ? 'comment-expanded' : ''}`}>{commentText}</p>
              {commentText.length > 180 && (
                <button type="button" className="comment-toggle" onClick={() => setCommentExpanded((expanded) => !expanded)}>
                  {commentExpanded ? 'Show less' : 'Show more'}
                </button>
              )}
            </div>
          )}
        </section>
      )}

      <section className={`info-card accordion-card ${itemsSectionOpen ? 'is-open' : ''}`}>
        <button type="button" className="accordion-trigger" onClick={() => setItemsSectionOpen((open) => !open)}>
          <span className="info-card-icon"><LayersIcon width={16} height={16} /></span>
          <strong>Deal Content</strong>
          <span className="accordion-summary">{itemCount} approval items</span>
          <ChevronDownIcon width={16} height={16} className="accordion-chevron" />
        </button>
        {itemsSectionOpen && (
          <div className="accordion-content">
            <div className="item-section-heading">
              <span className="item-count-note">{itemCount > 5 && !itemsExpanded ? `Showing 5 of ${itemCount}` : `Showing ${itemCount} of ${itemCount}`}</span>
              {itemCount > 5 && (
                <button type="button" className="item-toggle" onClick={() => setItemsExpanded((expanded) => !expanded)}>
                  {itemsExpanded ? 'Show fewer' : `Show all ${itemCount} items`}
                </button>
              )}
            </div>
            <div className="item-cards item-cards-desktop">
              {visibleItems.map((item) => (
                <article className={item.fmi_belowforecast ? 'item-card below-forecast' : 'item-card'} key={item.fmi_dealapprovalitemid}>
                  <div className="item-card-heading">
                    <div>
                      <div className="item-card-title"><strong>{item.fmi_contentname || 'Content unavailable'}</strong><span className="item-bwg">BWG {item.fmi_businesswrittengroupname || '-'}</span></div>
                      <p>{item.fmi_targetterritoryname || '-'} · BWY {item.fmi_businesswrittenyearname || '-'} · Licence {formatDateOnly(item.fmi_licensestartdate)} – {formatDateOnly(item.fmi_licenseenddate)}</p>
                    </div>
                    <div className="item-card-actions">
                      <span>{item.fmi_belowforecast ? 'Below forecast' : 'On track'}</span>
                      {(item.budgetHistory?.length ?? 0) > 0 && (
                        <button type="button" className="budget-history-toggle" onClick={() => setHistoryItemId(item.fmi_dealapprovalitemid)}>
                          View historic budgets
                        </button>
                      )}
                    </div>
                  </div>
                  <div className="item-card-grid">
                    <div><dt>Sale Value</dt><dd>{formatUsd(item.fmi_submittedsalevalue)}</dd></div>
                    <div><dt>Submitted budget</dt><dd>{item.fmi_nobudgetrecordfound ? 'N/A' : formatUsd(item.fmi_submittedbudgetvalue)}</dd></div>
                    <div><dt>Latest forecast</dt><dd>{item.fmi_nobudgetrecordfound ? 'N/A' : formatUsd(item.fmi_submittedlatestforecast)}</dd></div>
                    <div><dt>Forecast type</dt><dd>{item.fmi_nobudgetrecordfound ? 'N/A' : item.fmi_latestforecasttypename || '-'}</dd></div>
                    <div><dt>Variance to forecast</dt><dd>{item.fmi_nobudgetrecordfound || item.fmi_varianceexcluded ? 'N/A' : formatUsd(item.fmi_variancetoforecast)}</dd></div>
                    <div><dt>Variance to budget</dt><dd>{item.fmi_nobudgetrecordfound || item.fmi_varianceexcluded ? 'N/A' : formatUsd(item.fmi_variancetobudget)}</dd></div>
                  </div>
                  {(item.fmi_nobudgetrecordfound || item.fmi_varianceexcluded) && (
                    <p className="item-financial-note">
                      {item.fmi_nobudgetrecordfound ? 'No budget record found for this BWG, territory and BWY combination.' : 'Variance excluded for this Business Written Group.'}
                    </p>
                  )}
                </article>
              ))}
            </div>
          </div>
        )}
      </section>

      {historyItem && (historyItem.budgetHistory?.length ?? 0) > 0 && (
        <div className="budget-history-modal-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) setHistoryItemId(null) }}>
          <section className="budget-history-modal" role="dialog" aria-modal="true" aria-labelledby="budget-history-title">
            <div className="budget-history-modal-heading">
              <div>
                <p className="eyebrow">Historic budgets</p>
                <h2 id="budget-history-title">{historyItem.fmi_contentname || 'Content unavailable'}</h2>
                <span>{historyItem.fmi_businesswrittengroupname || '-'} · {historyItem.fmi_targetterritoryname || '-'}</span>
              </div>
              <button type="button" className="budget-history-close" aria-label="Close historic budgets" onClick={() => setHistoryItemId(null)}><XIcon width={18} height={18} /></button>
            </div>
            <p className="budget-history-modal-note">Previous Business Written Years with at least one budget or forecast value.</p>
            <div className="budget-history-table">
              <div className="budget-history-row budget-history-header"><span>BWY</span><span>Budget</span><span>FC1</span><span>FC2</span><span>FC3</span></div>
              {historyItem.budgetHistory?.map((entry) => (
                <div className="budget-history-row" key={`${historyItem.fmi_dealapprovalitemid}-${entry.businessWrittenYearId}`}>
                  <span>{entry.businessWrittenYearName || '-'}</span>
                  <span>{formatUsd(entry.currentYearBudget)}</span>
                  <span>{formatUsd(entry.fc1)}</span>
                  <span>{formatUsd(entry.fc2)}</span>
                  <span>{formatUsd(entry.fc3)}</span>
                </div>
              ))}
            </div>
          </section>
        </div>
      )}

      {approval.canDecide ? <section className={`decision-card ${itemsSectionOpen || commentSectionOpen ? '' : 'sticky-decision-panel'}`} aria-labelledby="decision-heading">
        <span className="decision-card-icon"><GavelIcon width={18} height={18} /></span>
        <div className="decision-card-body">
          <p className="eyebrow">Decision</p>
          <h2 id="decision-heading">Process this approval</h2>
          <label className="decision-comment">
            <span>Comment</span>
            <textarea ref={commentRef} value={comment} onChange={(event) => setComment(event.target.value)} disabled={submitting !== null} placeholder="Add a comment if needed..." rows={2} />
          </label>
          {decisionError && <p className="decision-error" role="alert">{decisionError}</p>}
          {success && <p className="decision-success" role="status">{success}</p>}
          <div className="decision-actions">
            <button type="button" className="decision-button decision-approve" onClick={() => void handleDecision('approve')} disabled={submitting !== null}>
              <CheckIcon width={16} height={16} />
              {submitting === 'approve' ? 'Approving...' : 'Approve'}
            </button>
            <button type="button" className="decision-button decision-reject" onClick={() => void handleDecision('reject')} disabled={submitting !== null}>
              <XIcon width={16} height={16} />
              {submitting === 'reject' ? 'Rejecting...' : 'Reject'}
            </button>
          </div>
        </div>
      </section> : <section className="read-only-notice" role="status">View only. You can review this approval, but you cannot record a decision on it.</section>}
    </main>
  )
}
