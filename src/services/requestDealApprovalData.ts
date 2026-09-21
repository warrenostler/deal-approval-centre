import { Fmi_GetDealApprovalPreviewService } from '../generated/services/Fmi_GetDealApprovalPreviewService'
import { Fmi_SubmitDealApprovalService } from '../generated/services/Fmi_SubmitDealApprovalService'

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
  salesType?: number | null
  salesTypeLabel?: string | null
  items: DealPreviewItem[]
}

/**
 * Sales Types with no budget/forecast comparison and their own review-screen layouts.
 */
export const SALES_TYPE_HOME_ENTERTAINMENT = 797300007
export const SALES_TYPE_INFLIGHT = 797300006
export const SALES_TYPE_ANCILLARY = 797300008

export interface DealApprovalSubmissionResult {
  dealApprovalId: string
  itemCount: number
}

function asString(value: unknown): string {
  return typeof value === 'string' ? value : ''
}

function asBoolean(value: unknown): boolean | undefined {
  if (typeof value === 'boolean') return value
  if (typeof value === 'string' && value.toLowerCase() === 'true') return true
  if (typeof value === 'string' && value.toLowerCase() === 'false') return false
  return undefined
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

function mapItem(record: Record<string, unknown>): DealPreviewItem {
  return {
    opportunityItemId: normalizeGuid(parseProperty(record, ['opportunityItemId', 'opportunityitemid', 'opportunity_item_id'], asString)) || undefined,
    title: parseProperty(record, ['title', 'Title'], asString) || undefined,
    territory: parseProperty(record, ['territory', 'Territory'], asString) || undefined,
    businessWrittenGroup: parseProperty(record, ['businessWrittenGroup', 'businesswrittenGroup', 'businessWrittenGroupName', 'businesswrittengroup'], asString) || null,
    businessWrittenYear: parseProperty(record, ['businessWrittenYear', 'businesswrittenYear', 'businessWrittenYearName', 'businesswrittenyear'], asString) || undefined,
    sale: parseProperty(record, ['sale', 'Sale'], (value) => typeof value === 'number' && Number.isFinite(value) ? value : null) ?? null,
    budget: parseProperty(record, ['budget', 'Budget'], (value) => typeof value === 'number' && Number.isFinite(value) ? value : null) ?? null,
    fc1: parseProperty(record, ['fc1', 'FC1'], (value) => typeof value === 'number' && Number.isFinite(value) ? value : null) ?? null,
    fc2: parseProperty(record, ['fc2', 'FC2'], (value) => typeof value === 'number' && Number.isFinite(value) ? value : null) ?? null,
    fc3: parseProperty(record, ['fc3', 'FC3'], (value) => typeof value === 'number' && Number.isFinite(value) ? value : null) ?? null,
    latestForecast: parseProperty(record, ['latestForecast', 'latestforecast'], (value) => typeof value === 'number' && Number.isFinite(value) ? value : null) ?? null,
    latestForecastType: parseProperty(record, ['latestForecastType', 'latestforecasttype'], asString) || null,
    varianceToForecast: parseProperty(record, ['varianceToForecast', 'variancetoforecast'], (value) => typeof value === 'number' && Number.isFinite(value) ? value : null) ?? null,
    varianceToBudget: parseProperty(record, ['varianceToBudget', 'variancetobudget'], (value) => typeof value === 'number' && Number.isFinite(value) ? value : null) ?? null,
    belowForecast: asBoolean(parseProperty(record, ['belowForecast', 'belowforecast'], (value) => value)),
    financialComparisonAvailable: asBoolean(parseProperty(record, ['financialComparisonAvailable', 'financialcomparisonavailable'], (value) => value)),
    financialWarning: parseProperty(record, ['financialWarning', 'financialwarning'], asString) || null,
    includeInVariance: asBoolean(parseProperty(record, ['includeInVariance', 'includeinvariance'], (value) => value)),
  }
}

function parsePreviewJson(value: unknown): DealApprovalPreview {
  if (!value || typeof value !== 'object') throw new Error('The preview payload could not be parsed.')
  const root = value as Record<string, unknown>
  const rawText = parseProperty(root, ['PreviewJson', 'previewJson', 'previewjson'], asString) ?? ''
  if (!rawText.trim()) throw new Error('The preview payload was empty.')
  const parsed = JSON.parse(rawText) as Record<string, unknown>
  const items = Array.isArray(parsed.items) ? parsed.items.map((entry) => mapItem((entry as Record<string, unknown>) ?? {})) : []
  return {
    opportunityId: normalizeGuid(parseProperty(parsed, ['opportunityId', 'opportunityid'], asString)) || undefined,
    opportunityName: parseProperty(parsed, ['opportunityName', 'opportunityname'], asString) || undefined,
    salesType: parseProperty(parsed, ['salesType', 'salestype'], (value) => (typeof value === 'number' && Number.isFinite(value) ? value : null)) ?? null,
    salesTypeLabel: parseProperty(parsed, ['salesTypeLabel', 'salestypelabel'], asString) || null,
    items,
  }
}

export async function getDealApprovalPreview(opportunityId: string): Promise<DealApprovalPreview> {
  const normalizedId = opportunityId.trim()
  if (!isGuid(normalizedId)) {
    throw new Error('This page must be opened from an Opportunity record. No valid Opportunity was supplied.')
  }

  const result = await Fmi_GetDealApprovalPreviewService.fmi_GetDealApprovalPreview(normalizedId)
  if (!result.success) {
    throw new Error(extractApiMessage(result.error) || 'The Opportunity could not be loaded.')
  }

  const preview = parsePreviewJson(result.data)
  if (preview.items.length === 0 && preview.salesType !== SALES_TYPE_ANCILLARY) {
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
