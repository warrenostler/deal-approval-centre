import { Fmi_dealapprovalitemsService } from '../generated/services/Fmi_dealapprovalitemsService'
import { Fmi_dealapprovalsService } from '../generated/services/Fmi_dealapprovalsService'
import { Fmi_GetPendingDealApprovalsService } from '../generated/services/Fmi_GetPendingDealApprovalsService'
import { AccountsService } from '../generated/services/AccountsService'
import { OpportunitiesService } from '../generated/services/OpportunitiesService'
import { SystemusersService } from '../generated/services/SystemusersService'
import { Fmi_contentsService } from '../generated/services/Fmi_contentsService'
import { Fmi_targetterritoriesService } from '../generated/services/Fmi_targetterritoriesService'
import { Fmi_businesswrittenyearsService } from '../generated/services/Fmi_businesswrittenyearsService'
import { Fmi_businesswrittengroupsService } from '../generated/services/Fmi_businesswrittengroupsService'
import { Fmi_ProcessDealApprovalDecisionService } from '../generated/services/Fmi_ProcessDealApprovalDecisionService'
import { Fmi_opportunityitemsService } from '../generated/services/Fmi_opportunityitemsService'
import type { Fmi_dealapprovalitems } from '../generated/models/Fmi_dealapprovalitemsModel'
import type { Fmi_dealapprovals } from '../generated/models/Fmi_dealapprovalsModel'
import type { Opportunities } from '../generated/models/OpportunitiesModel'

export interface ApprovalSummary {
  dealApprovalId: string
  opportunityId: string
  opportunityName: string
  companyId: string
  companyName: string
  approverId: string
  approverName: string
  requestedById: string
  requestedByName: string
  requestedOn: string
  submittedDealValue: number | null
  itemCount: number
  belowForecastCount: number
  approvalStatus: number
  salesExecutiveName: string
}

export interface ApprovalDetail extends Fmi_dealapprovals {
  items: DealContentItem[]
  salesExecutiveName: string
}

export interface BudgetHistoryEntry {
  businessWrittenYearId: string
  businessWrittenYearName: string
  currentYearBudget: number | null
  fc1: number | null
  fc2: number | null
  fc3: number | null
}

export type DealContentItem = Fmi_dealapprovalitems & { fmi_licensestartdate?: string; fmi_licenseenddate?: string; fmi_includeinvariances?: boolean; fmi_nobudgetrecordfound?: boolean; fmi_varianceexcluded?: boolean; budgetHistory?: BudgetHistoryEntry[] }

export type ApprovalDecision = 'approve' | 'reject'

export interface DecisionResult {
  approvalId: string
  status: number | null
}

export interface OpportunityApprovalHistory {
  opportunity: Opportunities
  approvals: Fmi_dealapprovals[]
}

const approvalSelect = ['fmi_dealapprovalid', '_fmi_opportunity_value', '_fmi_submittedcompany_value', '_fmi_approver_value', '_fmi_requestedby_value', 'createdon', 'fmi_submitteddealvalue', 'fmi_approvalstatus', 'fmi_requestorcomment']
const itemSelect = ['fmi_dealapprovalitemid', '_fmi_dealapproval_value', '_fmi_content_value', '_fmi_targetterritory_value', '_fmi_businesswrittenyear_value', '_fmi_businesswrittengroup_value', '_fmi_opportunityitem_value', 'fmi_submittedsalevalue', 'fmi_submittedbudgetvalue', 'fmi_submittedlatestforecast', 'fmi_latestforecasttype', 'fmi_variancetoforecast', 'fmi_variancetobudget', 'fmi_belowforecast']
const opportunityHistorySelect = ['opportunityid', 'name', 'fmi_dpssalescontractid', 'fmi_currentapprovalstatus']
const historySelect = ['fmi_dealapprovalid', 'fmi_name', '_fmi_opportunity_value', 'fmi_approvalstatus', 'fmi_approvaltype', 'fmi_approvalversion', 'fmi_approvalsenton', 'fmi_decisionon', 'fmi_decisioncomments', 'fmi_requestorcomment', 'fmi_iscurrentapproval', 'fmi_reapprovalrequired', 'fmi_cancelledon', 'fmi_cancellationreason', 'createdon', '_fmi_approver_value', '_fmi_requestedby_value', '_fmi_decisionby_value', 'fmi_submitteddealvalue']

function asRecord(value: unknown): Record<string, unknown> | null {
  return value && typeof value === 'object' ? value as Record<string, unknown> : null
}

function asString(value: unknown): string { return typeof value === 'string' ? value : '' }
function asNumber(value: unknown, fallback = 0): number { return typeof value === 'number' && Number.isFinite(value) ? value : fallback }
function asNullableNumber(value: unknown): number | null { return typeof value === 'number' && Number.isFinite(value) ? value : null }
function normalizeGuid(value: unknown): string { return asString(value).replace(/[{}]/g, '').toLowerCase() }
function isGuid(value: string): boolean { return value.length === 36 && /^[0-9a-f-]+$/.test(value) }
function formattedValue(record: Record<string, unknown>, field: string): string | undefined {
  const value = record[`${field}@OData.Community.Display.V1.FormattedValue`]
  return typeof value === 'string' ? value : undefined
}

function mapApprovalLabels(record: Fmi_dealapprovals): Fmi_dealapprovals {
  const raw = record as unknown as Record<string, unknown>
  return {
    ...record,
    fmi_opportunityname: formattedValue(raw, '_fmi_opportunity_value') ?? record.fmi_opportunityname,
    fmi_submittedcompanyname: formattedValue(raw, '_fmi_submittedcompany_value') ?? record.fmi_submittedcompanyname,
    fmi_approvername: formattedValue(raw, '_fmi_approver_value') ?? record.fmi_approvername,
    fmi_requestedbyname: formattedValue(raw, '_fmi_requestedby_value') ?? record.fmi_requestedbyname,
    fmi_approvalstatusname: formattedValue(raw, 'fmi_approvalstatus') ?? record.fmi_approvalstatusname,
  }
}

function mapItemLabels(record: Fmi_dealapprovalitems): Fmi_dealapprovalitems {
  const raw = record as unknown as Record<string, unknown>
  return {
    ...record,
    fmi_businesswrittengroupname: formattedValue(raw, '_fmi_businesswrittengroup_value') ?? record.fmi_businesswrittengroupname,
    fmi_contentname: formattedValue(raw, '_fmi_content_value') ?? record.fmi_contentname,
    fmi_targetterritoryname: formattedValue(raw, '_fmi_targetterritory_value') ?? record.fmi_targetterritoryname,
    fmi_businesswrittenyearname: formattedValue(raw, '_fmi_businesswrittenyear_value') ?? record.fmi_businesswrittenyearname,
    fmi_latestforecasttypename: formattedValue(raw, 'fmi_latestforecasttype') ?? record.fmi_latestforecasttypename,
    fmi_belowforecastname: formattedValue(raw, 'fmi_belowforecast') ?? record.fmi_belowforecastname,
  }
}

type LookupRow = Record<string, unknown>
type LookupService = (options: { select: string[]; filter: string }) => Promise<{ success: boolean; data: LookupRow[] }>
type XrmWebApi = { retrieveMultipleRecords: (entityLogicalName: string, options?: string, maxPageSize?: number) => Promise<{ entities: Record<string, unknown>[] }> }

function getXrmWebApi(): XrmWebApi | null {
  const current = window as Window & { Xrm?: { WebApi?: XrmWebApi } }
  if (current.Xrm?.WebApi) return current.Xrm.WebApi
  try {
    const parentWindow = window.parent as Window & { Xrm?: { WebApi?: XrmWebApi } }
    return parentWindow.Xrm?.WebApi ?? null
  } catch {
    return null
  }
}

function yearNumber(value: string | undefined): number | null {
  const match = value?.match(/(?:FY)?(\d{4})/i)
  if (!match) return null
  const parsed = Number(match[1])
  return Number.isFinite(parsed) ? parsed : null
}

function hasMeaningfulBudgetValue(entry: BudgetHistoryEntry): boolean {
  return [entry.currentYearBudget, entry.fc1, entry.fc2, entry.fc3].some((value) => value !== null && value !== 0)
}

function formattedAlias(record: Record<string, unknown>, field: string): string {
  return asString(record[`${field}@OData.Community.Display.V1.FormattedValue`])
}

async function getBusinessWrittenTerritories(ids: string[]): Promise<Map<string, string>> {
  const uniqueIds = [...new Set(ids.map(normalizeGuid).filter(Boolean))]
  if (uniqueIds.length === 0) return new Map()
  const filter = uniqueIds.map((id) => `fmi_targetterritoryid eq ${id}`).join(' or ')
  const result = await Fmi_targetterritoriesService.getAll({ select: ['fmi_targetterritoryid', '_fmi_businesswrittenterritory_value'], filter })
  if (!result.success) throw new Error('Business Written Territory mappings could not be loaded.')
  return new Map((result.data ?? []).map((row) => [normalizeGuid(row.fmi_targetterritoryid), normalizeGuid(row._fmi_businesswrittenterritory_value)]))
}

async function getBudgetHistory(items: Fmi_dealapprovalitems[]): Promise<Map<string, BudgetHistoryEntry[]>> {
  const webApi = getXrmWebApi()
  if (!webApi) {
    console.info('[DealApprovalCentre] Budget history skipped because Xrm.WebApi is unavailable')
    return new Map()
  }

  const itemRows = items.map((item) => {
    const raw = item as unknown as Record<string, unknown>
    return {
      itemId: item.fmi_dealapprovalitemid,
      businessWrittenGroupId: normalizeGuid(raw._fmi_businesswrittengroup_value),
      targetTerritoryId: normalizeGuid(raw._fmi_targetterritory_value),
      businessWrittenYearId: normalizeGuid(raw._fmi_businesswrittenyear_value),
      businessWrittenYearName: item.fmi_businesswrittenyearname ?? formattedValue(raw, '_fmi_businesswrittenyear_value'),
    }
  })
  const businessWrittenTerritories = await getBusinessWrittenTerritories(itemRows.map((item) => item.targetTerritoryId))
  const combinations = new Map<string, { businessWrittenGroupId: string; businessWrittenTerritoryId: string }>()

  for (const item of itemRows) {
    const businessWrittenTerritoryId = businessWrittenTerritories.get(item.targetTerritoryId)
    if (!item.businessWrittenGroupId || !businessWrittenTerritoryId) continue
    combinations.set(`${item.businessWrittenGroupId}|${businessWrittenTerritoryId}`, { businessWrittenGroupId: item.businessWrittenGroupId, businessWrittenTerritoryId })
  }

  const combinationRows = [...combinations.values()]
  if (combinationRows.length === 0) return new Map()

  const groupValues = [...new Set(combinationRows.map((row) => row.businessWrittenGroupId))].map((id) => `<value>${id}</value>`).join('')
  const territoryValues = [...new Set(combinationRows.map((row) => row.businessWrittenTerritoryId))].map((id) => `<value>${id}</value>`).join('')
  const fetchXml = `
    <fetch>
      <entity name='goal'>
        <attribute name='goalid' />
        <attribute name='fmi_businesswrittengroup' />
        <attribute name='fmi_bwterritory' />
        <attribute name='fmi_businesswrittenyear' />
        <attribute name='fmi_currentyearbudget' />
        <attribute name='fmi_fc1' />
        <attribute name='fmi_fc2' />
        <attribute name='fmi_fc3' />
        <filter type='and'>
          <condition attribute='fmi_businesswrittengroup' operator='in'>${groupValues}</condition>
          <condition attribute='fmi_bwterritory' operator='in'>${territoryValues}</condition>
        </filter>
      </entity>
    </fetch>`
  const result = await webApi.retrieveMultipleRecords('goal', `?fetchXml=${encodeURIComponent(fetchXml)}`)
  const goalsByCombination = new Map<string, BudgetHistoryEntry[]>()

  for (const goal of result.entities ?? []) {
    const key = `${normalizeGuid(goal._fmi_businesswrittengroup_value)}|${normalizeGuid(goal._fmi_bwterritory_value)}`
    const entry: BudgetHistoryEntry = {
      businessWrittenYearId: normalizeGuid(goal._fmi_businesswrittenyear_value),
      businessWrittenYearName: formattedAlias(goal, '_fmi_businesswrittenyear_value'),
      currentYearBudget: asNullableNumber(goal.fmi_currentyearbudget),
      fc1: asNullableNumber(goal.fmi_fc1),
      fc2: asNullableNumber(goal.fmi_fc2),
      fc3: asNullableNumber(goal.fmi_fc3),
    }
    if (!hasMeaningfulBudgetValue(entry)) continue
    const existing = goalsByCombination.get(key) ?? []
    existing.push(entry)
    goalsByCombination.set(key, existing)
  }

  const historyByItemId = new Map<string, BudgetHistoryEntry[]>()
  for (const item of itemRows) {
    const businessWrittenTerritoryId = businessWrittenTerritories.get(item.targetTerritoryId)
    if (!businessWrittenTerritoryId) continue
    const currentYear = yearNumber(item.businessWrittenYearName)
    const history = (goalsByCombination.get(`${item.businessWrittenGroupId}|${businessWrittenTerritoryId}`) ?? [])
      .filter((entry) => entry.businessWrittenYearId !== item.businessWrittenYearId)
      .filter((entry) => {
        const entryYear = yearNumber(entry.businessWrittenYearName)
        return currentYear === null || entryYear === null ? true : entryYear < currentYear
      })
      .sort((left, right) => (yearNumber(right.businessWrittenYearName) ?? 0) - (yearNumber(left.businessWrittenYearName) ?? 0))
    if (history.length > 0) historyByItemId.set(item.itemId, history)
  }

  console.info('[DealApprovalCentre] Budget history loaded', { itemCount: historyByItemId.size })

  return historyByItemId
}

async function getLookupNames(service: LookupService, idField: string, nameField: string, ids: string[]): Promise<Map<string, string>> {
  const uniqueIds = [...new Set(ids.map(normalizeGuid).filter(Boolean))]
  if (uniqueIds.length === 0) return new Map()
  const filter = uniqueIds.map((id) => `${idField} eq ${id}`).join(' or ')
  const result = await service({ select: [idField, nameField], filter })
  if (!result.success) throw new Error(`Lookup data could not be loaded for ${nameField}.`)
  return new Map(result.data.map((row) => [normalizeGuid(row[idField]), asString(row[nameField])]))
}

async function getOpportunityRows(ids: string[]): Promise<Map<string, { name: string; salesExecutiveId: string }>> {
  const uniqueIds = [...new Set(ids.map(normalizeGuid).filter(Boolean))]
  if (uniqueIds.length === 0) return new Map()
  const filter = uniqueIds.map((id) => `opportunityid eq ${id}`).join(' or ')
  const result = await (OpportunitiesService.getAll as unknown as LookupService)({ select: ['opportunityid', 'name', '_fmi_salesexecutive_value'], filter })
  if (!result.success) throw new Error('Opportunity ownership data could not be loaded.')
  return new Map(result.data.map((row) => [normalizeGuid(row.opportunityid), { name: asString(row.name), salesExecutiveId: normalizeGuid(row._fmi_salesexecutive_value) }]))
}

async function getLicenceDates(ids: string[]): Promise<Map<string, { start?: string; end?: string }>> {
  const uniqueIds = [...new Set(ids.map(normalizeGuid).filter(Boolean))]
  if (uniqueIds.length === 0) return new Map()
  const filter = uniqueIds.map((id) => `fmi_opportunityitemid eq ${id}`).join(' or ')
  const result = await (Fmi_opportunityitemsService.getAll as unknown as LookupService)({ select: ['fmi_opportunityitemid', 'fmi_licensestartdate', 'fmi_licenseenddate'], filter })
  if (!result.success) throw new Error('Licence dates could not be loaded.')
  return new Map(result.data.map((row) => [normalizeGuid(row.fmi_opportunityitemid), { start: asString(row.fmi_licensestartdate) || undefined, end: asString(row.fmi_licenseenddate) || undefined }]))
}

async function getVarianceFlags(ids: string[]): Promise<Map<string, boolean>> {
  const uniqueIds = [...new Set(ids.map(normalizeGuid).filter(Boolean))]
  if (uniqueIds.length === 0) return new Map()
  const filter = uniqueIds.map((id) => `fmi_businesswrittengroupid eq ${id}`).join(' or ')
  const result = await Fmi_businesswrittengroupsService.getAll({ select: ['fmi_businesswrittengroupid', 'fmi_includeinvariances'], filter })
  if (!result.success) throw new Error('Business Written Group variance settings could not be loaded.')
  return new Map((result.data ?? []).map((row) => [normalizeGuid(row.fmi_businesswrittengroupid), row.fmi_includeinvariances !== false]))
}

async function resolveQueueSalesExecutives(approvals: ApprovalSummary[]): Promise<ApprovalSummary[]> {
  const opportunities = await getOpportunityRows(approvals.map((approval) => approval.opportunityId))
  const salesExecutiveIds = [...new Set([...opportunities.values()].map((row) => row.salesExecutiveId).filter(Boolean))]
  const names = await getLookupNames(SystemusersService.getAll as unknown as LookupService, 'systemuserid', 'fullname', salesExecutiveIds)
  return approvals.map((approval) => ({ ...approval, salesExecutiveName: names.get(opportunities.get(approval.opportunityId)?.salesExecutiveId ?? '') ?? 'Unassigned' }))
}

async function resolveDetailLookups(approval: Fmi_dealapprovals, items: Fmi_dealapprovalitems[]): Promise<{ approval: Fmi_dealapprovals & { salesExecutiveName: string }; items: DealContentItem[] }> {
  const approvalRaw = approval as unknown as Record<string, unknown>
  const itemRaw = items.map((item) => item as unknown as Record<string, unknown>)
  const licenceDates = await getLicenceDates(itemRaw.map((item) => asString(item._fmi_opportunityitem_value)))
  const varianceFlags = await getVarianceFlags(itemRaw.map((item) => asString(item._fmi_businesswrittengroup_value)))
  const userIds = [approvalRaw._fmi_approver_value, approvalRaw._fmi_requestedby_value].map(asString)
  const [companies, opportunityRows, users, content, territories, years] = await Promise.all([
    getLookupNames(AccountsService.getAll as unknown as LookupService, 'accountid', 'name', [asString(approvalRaw._fmi_submittedcompany_value)]),
    getOpportunityRows([asString(approvalRaw._fmi_opportunity_value)]),
    getLookupNames(SystemusersService.getAll as unknown as LookupService, 'systemuserid', 'fullname', userIds),
    getLookupNames(Fmi_contentsService.getAll as unknown as LookupService, 'fmi_contentid', 'fmi_name', itemRaw.map((item) => asString(item._fmi_content_value))),
    getLookupNames(Fmi_targetterritoriesService.getAll as unknown as LookupService, 'fmi_targetterritoryid', 'fmi_name', itemRaw.map((item) => asString(item._fmi_targetterritory_value))),
    getLookupNames(Fmi_businesswrittenyearsService.getAll as unknown as LookupService, 'fmi_businesswrittenyearid', 'fmi_name', itemRaw.map((item) => asString(item._fmi_businesswrittenyear_value))),
  ])
  const companyId = normalizeGuid(approvalRaw._fmi_submittedcompany_value)
  const opportunityId = normalizeGuid(approvalRaw._fmi_opportunity_value)
  const approverId = normalizeGuid(approvalRaw._fmi_approver_value)
  const requestedById = normalizeGuid(approvalRaw._fmi_requestedby_value)
  const opportunityRow = opportunityRows.get(opportunityId)
  const salesExecutiveNames = await getLookupNames(SystemusersService.getAll as unknown as LookupService, 'systemuserid', 'fullname', opportunityRow?.salesExecutiveId ? [opportunityRow.salesExecutiveId] : [])
  const labelledItems = items.map((item) => {
    const raw = item as unknown as Record<string, unknown>
    return { ...item, fmi_contentname: content.get(normalizeGuid(raw._fmi_content_value)) ?? item.fmi_contentname, fmi_targetterritoryname: territories.get(normalizeGuid(raw._fmi_targetterritory_value)) ?? item.fmi_targetterritoryname, fmi_businesswrittenyearname: years.get(normalizeGuid(raw._fmi_businesswrittenyear_value)) ?? item.fmi_businesswrittenyearname }
  })
  const budgetHistory = await getBudgetHistory(labelledItems).catch((error: unknown) => {
    console.warn('[DealApprovalCentre] Budget history could not be loaded', error)
    return new Map<string, BudgetHistoryEntry[]>()
  })
  return {
    approval: { ...approval, fmi_submittedcompanyname: companies.get(companyId) ?? approval.fmi_submittedcompanyname, fmi_opportunityname: opportunityRow?.name ?? approval.fmi_opportunityname, fmi_approvername: users.get(approverId) ?? approval.fmi_approvername, fmi_requestedbyname: users.get(requestedById) ?? approval.fmi_requestedbyname, salesExecutiveName: salesExecutiveNames.get(opportunityRow?.salesExecutiveId ?? '') ?? 'Unassigned' },
    items: labelledItems.map((item) => {
      const raw = item as unknown as Record<string, unknown>
      const dates = licenceDates.get(normalizeGuid(raw._fmi_opportunityitem_value))
      const includeInVariances = varianceFlags.get(normalizeGuid(raw._fmi_businesswrittengroup_value))
      const noBudgetRecordFound = item.fmi_submittedbudgetvalue === null || item.fmi_submittedbudgetvalue === undefined
      return { ...item, fmi_licensestartdate: dates?.start, fmi_licenseenddate: dates?.end, fmi_includeinvariances: includeInVariances, fmi_nobudgetrecordfound: noBudgetRecordFound, fmi_varianceexcluded: includeInVariances === false, budgetHistory: budgetHistory.get(item.fmi_dealapprovalitemid) ?? [] }
    }),
  }
}

function parseApprovalSummary(value: unknown): ApprovalSummary | null {
  const record = asRecord(value)
  if (!record) return null
  const dealApprovalId = normalizeGuid(record.dealApprovalId)
  if (!dealApprovalId) return null
  return {
    dealApprovalId, opportunityId: normalizeGuid(record.opportunityId), opportunityName: asString(record.opportunityName), companyId: normalizeGuid(record.companyId), companyName: asString(record.companyName), approverId: normalizeGuid(record.approverId), approverName: asString(record.approverName), requestedById: normalizeGuid(record.requestedById), requestedByName: asString(record.requestedByName), requestedOn: asString(record.requestedOn), submittedDealValue: asNullableNumber(record.submittedDealValue), itemCount: Math.max(0, asNumber(record.itemCount)), belowForecastCount: Math.max(0, asNumber(record.belowForecastCount)), approvalStatus: asNumber(record.approvalStatus), salesExecutiveName: 'Unassigned',
  }
}

export function parseApprovalsJson(value: unknown): ApprovalSummary[] {
  const root = asRecord(value)
  const rawJson = root?.fmi_ApprovalsJson ?? root?.ApprovalsJson ?? root?.approvalsJson
  if (typeof rawJson !== 'string' || !rawJson.trim()) return []
  try {
    const payload = asRecord(JSON.parse(rawJson))
    const approvals = Array.isArray(payload?.approvals) ? payload.approvals : []
    return approvals.map(parseApprovalSummary).filter((approval): approval is ApprovalSummary => approval !== null)
  } catch { throw new Error('The pending approvals response was not valid JSON.') }
}

export async function getPendingApprovals(): Promise<ApprovalSummary[]> {
  const result = await Fmi_GetPendingDealApprovalsService.fmi_GetPendingDealApprovals()
  if (!result.success) throw new Error('The pending approval queue could not be loaded.')
  return resolveQueueSalesExecutives(parseApprovalsJson(result.data))
}

export async function getApprovalDetail(id: string): Promise<ApprovalDetail> {
  const normalizedId = normalizeGuid(id)
  if (!isGuid(normalizedId)) throw new Error('The approval ID in this link is not valid.')
  const approvalResult = await Fmi_dealapprovalsService.get(normalizedId, { select: approvalSelect })
  if (!approvalResult.success || !approvalResult.data) throw new Error('The approval could not be loaded.')
  const itemResult = await Fmi_dealapprovalitemsService.getAll({ select: itemSelect, filter: `_fmi_dealapproval_value eq ${normalizedId}` })
  if (!itemResult.success) throw new Error('The approval items could not be loaded.')
  const mappedApproval = mapApprovalLabels(approvalResult.data)
  const mappedItems = (itemResult.data ?? []).map(mapItemLabels)
  const resolved = await resolveDetailLookups(mappedApproval, mappedItems)
  return { ...resolved.approval, items: resolved.items }
}

export async function getOpportunityApprovalHistory(contractId: string): Promise<OpportunityApprovalHistory> {
  const normalizedContractId = contractId.trim()
  if (!normalizedContractId) throw new Error('Enter a DPS sale contract ID.')
  const escapedValue = normalizedContractId.replace(/'/g, "''")
  const result = await OpportunitiesService.getAll({
    select: opportunityHistorySelect,
    filter: `fmi_dpssalescontractid eq '${escapedValue}'`,
  })
  if (!result.success) throw new Error('The opportunity could not be searched.')
  let opportunity = result.data?.[0]
  if (!opportunity) {
    const nameResult = await OpportunitiesService.getAll({
      select: opportunityHistorySelect,
      filter: `contains(name, '${escapedValue}') or contains(fmi_dpssalescontractid, '${escapedValue}')`,
    })
    if (!nameResult.success) throw new Error('The opportunity could not be searched.')
    opportunity = nameResult.data?.[0]
  }
  if (!opportunity) throw new Error(`No opportunity was found for contract ID ${normalizedContractId}.`)

  const opportunityId = normalizeGuid(opportunity.opportunityid)
  const historyResult = await Fmi_dealapprovalsService.getAll({
    select: historySelect,
    filter: `_fmi_opportunity_value eq ${opportunityId}`,
    orderBy: ['createdon desc'],
  })
  if (!historyResult.success) throw new Error('The approval history could not be loaded.')
  const approvals = historyResult.data ?? []
  return { opportunity, approvals }
}

function normalizeDecisionError(error: unknown): Error {
  const raw = error instanceof Error ? error.message : typeof error === 'string' ? error : ''
  const message = raw.toLowerCase()
  if (message.includes('permission') || message.includes('not authorized') || message.includes('access')) return new Error('You are not authorized to decide this approval.')
  if (message.includes('pending') || message.includes('already') || message.includes('processed')) return new Error('This approval is no longer pending. Refresh the queue and try again.')
  return new Error(raw || 'The approval decision could not be submitted.')
}

export async function submitApprovalDecision(approvalId: string, decision: ApprovalDecision, comment: string): Promise<DecisionResult> {
  const normalizedId = normalizeGuid(approvalId)
  const normalizedComment = comment.trim()
  if (!isGuid(normalizedId)) throw new Error('The approval ID is not valid.')
  if (decision === 'reject' && !normalizedComment) throw new Error('A rejection comment is required.')
  const result = await Fmi_ProcessDealApprovalDecisionService.fmi_ProcessDealApprovalDecision(normalizedId, decision === 'approve' ? 1 : 2, normalizedComment)
  if (!result.success) {
    console.error('[ApprovalDecision] Dataverse decision failed', result.error)
    throw normalizeDecisionError(result.error)
  }
  const response = result.data as Record<string, unknown>
  return { approvalId: normalizeGuid(asString(response.fmi_DealApprovalId ?? response.fmi_dealapprovalid ?? normalizedId)), status: typeof response.fmi_ApprovalStatus === 'number' ? response.fmi_ApprovalStatus : null }
}