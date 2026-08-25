import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { getDealApprovalPreview, isGuid, submitDealApproval, type DealPreviewItem } from '../services/requestDealApprovalData'

type SortColumn = 'title' | 'territory' | 'businessWrittenYear' | 'sale' | 'budget' | 'latestForecast' | 'varianceToForecast' | null
type SortDirection = 'asc' | 'desc'

const moneyFormatter = new Intl.NumberFormat('en-US', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
})

function formatMoney(value: number | null | undefined): string {
  if (value === null || value === undefined || Number.isNaN(value)) return '-'
  return moneyFormatter.format(value)
}

function formatCurrency(value: number | null | undefined): string {
  return value === null || value === undefined || Number.isNaN(value) ? '-' : `$${moneyFormatter.format(value)}`
}

function compareValues(left: number | string | null | undefined, right: number | string | null | undefined): number {
  const leftValue = left == null ? '' : left
  const rightValue = right == null ? '' : right
  if (typeof leftValue === 'number' && typeof rightValue === 'number') return leftValue - rightValue
  return String(leftValue).localeCompare(String(rightValue), undefined, { numeric: true, sensitivity: 'base' })
}

function getDisplayValue(item: DealPreviewItem, field: 'budget' | 'latestForecast' | 'varianceToForecast' | 'latestForecastType'): string {
  if (item.includeInVariance === false) return 'N/A'

  switch (field) {
    case 'budget': {
      return item.budget === null || item.budget === undefined ? '-' : formatMoney(item.budget)
    }
    case 'latestForecast': {
      return item.latestForecast === null || item.latestForecast === undefined ? '-' : formatMoney(item.latestForecast)
    }
    case 'varianceToForecast': {
      return item.varianceToForecast === null || item.varianceToForecast === undefined ? '-' : formatMoney(item.varianceToForecast)
    }
    case 'latestForecastType': {
      return item.latestForecastType ? item.latestForecastType : '-'
    }
    default: {
      return '-'
    }
  }
}

export function RequestDealApprovalPage() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const [items, setItems] = useState<DealPreviewItem[]>([])
  const [opportunityId, setOpportunityId] = useState<string>('')
  const [opportunityName, setOpportunityName] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [comment, setComment] = useState('')
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [sortColumn, setSortColumn] = useState<SortColumn>(null)
  const [sortDirection, setSortDirection] = useState<SortDirection>('asc')

  useEffect(() => {
    let active = true

    async function loadPreview() {
      const runtimeWindow = window as Window & { __dacContext?: { opportunityId?: string | null }; __dacOpportunityIdPromise?: Promise<string | null> }
      const runtimeContext = runtimeWindow.__dacContext
      const suppliedId = searchParams.get('opportunityId')?.trim() || runtimeContext?.opportunityId?.trim() || (await runtimeWindow.__dacOpportunityIdPromise)?.trim() || ''
      if (!active) return
      if (!suppliedId || !isGuid(suppliedId)) {
        setError('This page must be opened from an Opportunity record. No valid Opportunity was supplied.')
        setLoading(false)
        return
      }

      setOpportunityId(suppliedId)
      getDealApprovalPreview(suppliedId)
      .then((preview) => {
        if (!active) return
        setOpportunityName(preview.opportunityName || 'Opportunity')
        setItems(preview.items)
      })
      .catch((reason: unknown) => {
        if (!active) return
        const message = reason instanceof Error ? reason.message : 'The Opportunity could not be found, or you do not have permission to view it.'
        setError(message.includes('No valid Opportunity was supplied') ? 'This page must be opened from an Opportunity record. No valid Opportunity was supplied.' : message)
      })
      .finally(() => {
        if (active) setLoading(false)
      })
    }

    void loadPreview()

    return () => {
      active = false
    }
  }, [searchParams])

  const hasBelowForecast = items.some((item) => item.belowForecast === true)

  const sortedItems = useMemo(() => {
    if (!sortColumn) return items
    const sorted = [...items]
    sorted.sort((left, right) => {
      const direction = sortDirection === 'asc' ? 1 : -1
      const leftValue = left[sortColumn as keyof DealPreviewItem]
      const rightValue = right[sortColumn as keyof DealPreviewItem]
      return compareValues(leftValue as number | string | null | undefined, rightValue as number | string | null | undefined) * direction
    })
    return sorted
  }, [items, sortColumn, sortDirection])

  function toggleSort(column: Exclude<SortColumn, null>) {
    if (sortColumn !== column) {
      setSortColumn(column)
      setSortDirection('asc')
      return
    }

    setSortDirection((current) => (current === 'asc' ? 'desc' : 'asc'))
  }

  async function handleSubmit() {
    if (!opportunityId) return

    if (hasBelowForecast && !comment.trim()) {
      setSubmitError('A comment is required when any item is below forecast.')
      return
    }

    setSubmitError(null)
    setSubmitting(true)

    try {
      await submitDealApproval(opportunityId, comment)
      window.setTimeout(() => {
        const targets: Window[] = []
        let ancestor: Window = window
        while (ancestor !== ancestor.parent && targets.length < 10) {
          ancestor = ancestor.parent
          targets.push(ancestor)
        }
        for (const target of targets) {
          try {
            target.postMessage({ type: 'DAC_CLOSE_REQUEST' }, '*')
          } catch {
            // Ignore inaccessible frame targets.
          }
        }
      }, 500)
    } catch (reason: unknown) {
      setSubmitError(reason instanceof Error ? reason.message : 'The deal approval could not be submitted.')
      setSubmitting(false)
    }
  }

  if (error) {
    return (
      <main className="approval-app detail-page">
        <div className="state-panel state-error">
          <strong>Unable to open request</strong>
          <span>{error}</span>
        </div>
      </main>
    )
  }

  if (loading) {
    return (
      <main className="approval-app detail-page">
        <div className="state-panel">
          <span className="spinner" />
          Loading deal approval preview...
        </div>
      </main>
    )
  }

  return (
    <main className="approval-app request-deal-page">
      <header className="request-deal-header">
        <div>
          <h1>Request Deal Approval</h1>
          <p className="header-copy">{opportunityName || 'Opportunity'}</p>
        </div>
      </header>

      <p className="request-intro">Review the items included in this approval request.</p>

      <section className="request-table-panel">
        <div className="request-table-wrapper">
          <table className="request-table">
            <thead>
              <tr>
                <th className="sortable" onClick={() => toggleSort('title')}>
                  <button type="button">Title {sortColumn === 'title' ? (sortDirection === 'asc' ? '↑' : '↓') : ''}</button>
                </th>
                <th>Business Written Group</th>
                <th className="sortable" onClick={() => toggleSort('territory')}>
                  <button type="button">Territory {sortColumn === 'territory' ? (sortDirection === 'asc' ? '↑' : '↓') : ''}</button>
                </th>
                <th className="sortable" onClick={() => toggleSort('businessWrittenYear')}>
                  <button type="button">BWY {sortColumn === 'businessWrittenYear' ? (sortDirection === 'asc' ? '↑' : '↓') : ''}</button>
                </th>
                <th className="sortable numeric" onClick={() => toggleSort('sale')}>
                  <button type="button">Sale {sortColumn === 'sale' ? (sortDirection === 'asc' ? '↑' : '↓') : ''}</button>
                </th>
                <th className="sortable numeric" onClick={() => toggleSort('budget')}>
                  <button type="button">Budget {sortColumn === 'budget' ? (sortDirection === 'asc' ? '↑' : '↓') : ''}</button>
                </th>
                <th className="sortable numeric" onClick={() => toggleSort('latestForecast')}>
                  <button type="button">Latest Forecast {sortColumn === 'latestForecast' ? (sortDirection === 'asc' ? '↑' : '↓') : ''}</button>
                </th>
                <th>FC</th>
                <th className="sortable numeric" onClick={() => toggleSort('varianceToForecast')}>
                  <button type="button">Var. to Forecast {sortColumn === 'varianceToForecast' ? (sortDirection === 'asc' ? '↑' : '↓') : ''}</button>
                </th>
              </tr>
            </thead>
            <tbody>
              {sortedItems.map((item) => {
                const hasWarning = item.financialComparisonAvailable === false
                const warningText = item.financialWarning || 'Financial comparison unavailable.'
                const showNa = item.includeInVariance === false
                return (
                  <tr className={hasWarning ? 'request-warning-row' : undefined} key={item.opportunityItemId || `${item.title}-${item.territory}`}>
                    <td>
                      <div className="request-title-cell">
                        <span>{item.title || 'Untitled item'}</span>
                        {hasWarning && <span className="warning-badge" title={warningText}>⚠</span>}
                      </div>
                    </td>
                    <td>{item.businessWrittenGroup || '-'}</td>
                    <td>{item.territory || '-'}</td>
                    <td>{item.businessWrittenYear || '-'}</td>
                    <td className="numeric">{formatCurrency(item.sale ?? null)}</td>
                    <td className="numeric">{showNa ? 'N/A' : getDisplayValue(item, 'budget')}</td>
                    <td className="numeric">{showNa ? 'N/A' : getDisplayValue(item, 'latestForecast')}</td>
                    <td className="numeric">{showNa ? 'N/A' : getDisplayValue(item, 'latestForecastType')}</td>
                    <td className="numeric">{showNa ? 'N/A' : getDisplayValue(item, 'varianceToForecast')}</td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>

        <div className="request-footer">
          <span>{items.length} item{items.length === 1 ? '' : 's'}</span>
        </div>
      </section>

      <section className="request-comment-box">
        <label htmlFor="coordinator-comment">Coordinator comment</label>
        <textarea
          id="coordinator-comment"
          value={comment}
          onChange={(event) => setComment(event.target.value)}
          placeholder={hasBelowForecast ? 'A comment is required because one or more items are below forecast.' : 'Optional comment'}
          rows={4}
        />
        {hasBelowForecast && <small>Comment required because one or more items are below forecast.</small>}
        {submitError && <p className="request-error" role="alert">{submitError}</p>}
      </section>

      <div className="request-actions">
        <button type="button" className="secondary-button" onClick={() => (window.history.length > 1 ? navigate(-1) : navigate('/'))}>
          Cancel
        </button>
        <button type="button" className="primary-button" onClick={() => void handleSubmit()} disabled={submitting}>
          {submitting ? 'Submitting…' : 'Submit'}
        </button>
      </div>
    </main>
  )
}
