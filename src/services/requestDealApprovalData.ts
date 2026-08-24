import { Fmi_SubmitDealApprovalService } from '../generated/services/Fmi_SubmitDealApprovalService'
import { getClientDealApprovalPreview } from './clientDealApprovalPreview'

export interface DealPreviewItem {
  opportunityItemId?: string
  title?: string
  territory?: string
  businessWrittenGroup?: string | null
  businessWrittenYear?: string
  sale?: number | null
  budget?: number | null
  fc1?: number | null
  fc2?: number | null
  fc3?: number | null
  latestForecast?: number | null
  latestForecastType?: string | null
  varianceToForecast?: number | null
  varianceToBudget?: number | null
  belowForecast?: boolean
  financialComparisonAvailable?: boolean
  financialWarning?: string | null
  includeInVariance?: boolean
}

export interface DealApprovalPreview {
  opportunityId?: string
  opportunityName?: string
  items: DealPreviewItem[]
}

export interface DealApprovalSubmissionResult {
  dealApprovalId: string
  itemCount: number
}

function asString(value: unknown): string {
  return typeof value === 'string' ? value : ''
}

function normalizeGuid(value: unknown): string {
  return asString(value).replace(/[{}]/g, '').toLowerCase()
}

export function isGuid(value: string | null | undefined): boolean {
  if (!value) return false
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value.trim())
}

function parseProperty<T>(root: Record<string, unknown>, keyCandidates: string[], transform: (value: unknown) => T): T | undefined {
  for (const key of keyCandidates) {
    if (key in root) return transform(root[key])
  }
  return undefined
}

function stringifyError(value: unknown): string {
  if (value instanceof Error) return value.message
  if (typeof value === 'string') return value
  if (value && typeof value === 'object') {
    const record = value as Record<string, unknown>
    const message = parseProperty(record, ['message', 'Message'], asString)
    if (message) return message
    const error = parseProperty(record as Record<string, unknown>, ['error', 'Error'], (inner) => {
      if (inner && typeof inner === 'object') return stringifyError(inner)
      return asString(inner)
    })
    if (error) return error
    try {
      return JSON.stringify(value)
    } catch {
      return 'An unexpected error occurred.'
    }
  }
  return value === undefined ? 'An unexpected error occurred.' : String(value)
}

export function extractApiMessage(value: unknown): string {
  const message = stringifyError(value)
  const normalized = message.trim()
  if (!normalized) return 'The request could not be completed.'
  return normalized
}

export async function getDealApprovalPreview(opportunityId: string): Promise<DealApprovalPreview> {
  const normalizedId = opportunityId.trim()
  if (!isGuid(normalizedId)) {
    throw new Error('This page must be opened from an Opportunity record. No valid Opportunity was supplied.')
  }

  const preview = await getClientDealApprovalPreview(normalizedId)
  if (preview.items.length === 0) {
    throw new Error('The Opportunity contains no items to preview.')
  }

  return preview
}

export async function submitDealApproval(opportunityId: string, coordinatorComment: string): Promise<DealApprovalSubmissionResult> {
  const normalizedId = opportunityId.trim()
  if (!isGuid(normalizedId)) {
    throw new Error('This page must be opened from an Opportunity record. No valid Opportunity was supplied.')
  }

  const result = await Fmi_SubmitDealApprovalService.fmi_SubmitDealApproval(normalizedId, coordinatorComment.trim())
  if (!result.success) {
    throw new Error(extractApiMessage(result.error) || 'The deal approval could not be submitted.')
  }

  const payload = (result.data ?? {}) as Record<string, unknown>
  const dealApprovalId = normalizeGuid(parseProperty(payload, ['DealApprovalId', 'dealApprovalId', 'fmi_DealApprovalId', 'fmi_dealapprovalid'], asString))
  const itemCountNumber = Number(parseProperty(payload, ['ItemCount', 'itemCount', 'fmi_ItemCount', 'fmi_itemcount'], (value) => value))

  if (!dealApprovalId) {
    return { dealApprovalId: normalizedId, itemCount: Number.isFinite(itemCountNumber) ? Math.max(0, itemCountNumber) : 0 }
  }

  return {
    dealApprovalId,
    itemCount: Number.isFinite(itemCountNumber) ? Math.max(0, itemCountNumber) : 0,
  }
}
