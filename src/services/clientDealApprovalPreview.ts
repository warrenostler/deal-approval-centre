import type { DealApprovalPreview, DealPreviewItem } from './requestDealApprovalData'

type DataverseRecord = Record<string, unknown>

interface XrmWebApi {
  retrieveRecord: (entityLogicalName: string, id: string, options?: string) => Promise<DataverseRecord>
  retrieveMultipleRecords: (entityLogicalName: string, options?: string, maxPageSize?: number) => Promise<{ entities: DataverseRecord[] }>
}

type XrmContainer = { Xrm?: { WebApi?: XrmWebApi } }

interface OpportunityItemRow {
  id: string
  name: string
  contentId: string
  contentName: string
  targetTerritoryId: string
  targetTerritoryName: string
  businessWrittenYearId: string
  businessWrittenYearName: string
  sale: number
}

interface BusinessWrittenGroupRow {
  id: string
  name: string
  includeInVariance: boolean
}

interface BusinessWrittenTerritoryRow {
  id: string
  name: string
}

interface BudgetRow {
  id: string
  businessWrittenGroupId: string
  businessWrittenTerritoryId: string
  businessWrittenYearId: string
  currentYearBudget: number
  fc1: number | null
  fc2: number | null
  fc3: number | null
}

interface ResolvedItem {
  row: OpportunityItemRow
  businessWrittenGroup: BusinessWrittenGroupRow | null
  businessWrittenTerritory: BusinessWrittenTerritoryRow | null
}

interface FinancialResult {
  sale: number
  budget: number | null
  fc1: number | null
  fc2: number | null
  fc3: number | null
  latestForecast: number | null
  latestForecastType: string | null
  varianceToForecast: number | null
  varianceToBudget: number | null
  belowForecast: boolean
  financialComparisonAvailable: boolean
  financialWarning: string | null
}

function asRecord(value: unknown): DataverseRecord | null {
  return value && typeof value === 'object' ? value as DataverseRecord : null
}

function asString(value: unknown): string {
  return typeof value === 'string' ? value : ''
}

function asNumber(value: unknown, fallback = 0): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback
}

function asNullableNumber(value: unknown): number | null {
  return typeof value === 'number' && Number.isFinite(value) ? value : null
}

function asBoolean(value: unknown, fallback = true): boolean {
  return typeof value === 'boolean' ? value : fallback
}

function normalizeGuid(value: unknown): string {
  return asString(value).replace(/[{}]/g, '').toLowerCase()
}

function formattedValue(record: DataverseRecord, field: string): string {
  return asString(record[`${field}@OData.Community.Display.V1.FormattedValue`])
}

function aliasValue(record: DataverseRecord, alias: string): unknown {
  const direct = record[alias]
  const aliased = asRecord(direct)
  return aliased && 'value' in aliased ? aliased.value : direct
}

function aliasFormattedValue(record: DataverseRecord, alias: string): string {
  return asString(record[`${alias}@OData.Community.Display.V1.FormattedValue`])
}

function getXrmWebApi(): XrmWebApi {
  const current = window as Window & XrmContainer
  if (current.Xrm?.WebApi) return current.Xrm.WebApi

  try {
    const parentWindow = window.parent as Window & XrmContainer
    if (parentWindow?.Xrm?.WebApi) return parentWindow.Xrm.WebApi
  } catch {
    // Cross-origin parents are expected in some hosts.
  }

  throw new Error('Dataverse Web API is not available in this host. Open this page from the model-driven app.')
}

function inCondition(attributeName: string, ids: string[]): string {
  const values = [...new Set(ids.filter(Boolean))].map((id) => `<value>${id}</value>`).join('')
  return `<condition attribute='${attributeName}' operator='in'>${values}</condition>`
}

function budgetKey(businessWrittenGroupId: string, businessWrittenTerritoryId: string, businessWrittenYearId: string): string {
  return `${businessWrittenGroupId}|${businessWrittenTerritoryId}|${businessWrittenYearId}`
}

async function fetchRecords(entityLogicalName: string, fetchXml: string): Promise<DataverseRecord[]> {
  const result = await getXrmWebApi().retrieveMultipleRecords(entityLogicalName, `?fetchXml=${encodeURIComponent(fetchXml)}`)
  return result.entities ?? []
}

async function retrieveOpportunity(opportunityId: string): Promise<{ id: string; name: string }> {
  try {
    const opportunity = await getXrmWebApi().retrieveRecord('opportunity', opportunityId, '?$select=name')
    return { id: opportunityId, name: asString(opportunity.name) }
  } catch {
    throw new Error('The Opportunity could not be found, or you do not have permission to view it.')
  }
}

async function retrieveOpportunityItems(opportunityId: string): Promise<OpportunityItemRow[]> {
  const fetchXml = `
    <fetch>
      <entity name='fmi_opportunityitem'>
        <attribute name='fmi_opportunityitemid' />
        <attribute name='fmi_name' />
        <attribute name='fmi_content' />
        <attribute name='fmi_targetterritory' />
        <attribute name='fmi_businesswrittenyear' />
        <attribute name='fmi_actualrevenue_base' />
        <filter>
          <condition attribute='fmi_opportunityid' operator='eq' value='${opportunityId}' />
        </filter>
      </entity>
    </fetch>`

  return (await fetchRecords('fmi_opportunityitem', fetchXml)).map((record) => ({
    id: normalizeGuid(record.fmi_opportunityitemid),
    name: asString(record.fmi_name),
    contentId: normalizeGuid(record._fmi_content_value),
    contentName: formattedValue(record, '_fmi_content_value'),
    targetTerritoryId: normalizeGuid(record._fmi_targetterritory_value),
    targetTerritoryName: formattedValue(record, '_fmi_targetterritory_value'),
    businessWrittenYearId: normalizeGuid(record._fmi_businesswrittenyear_value),
    businessWrittenYearName: formattedValue(record, '_fmi_businesswrittenyear_value'),
    sale: asNumber(record.fmi_actualrevenue_base),
  }))
}

function validateRequiredLookups(rows: OpportunityItemRow[]): void {
  for (const row of rows) {
    const identity = row.name ? `Opportunity Item '${row.name}'` : 'An Opportunity Item'
    if (!row.contentId) throw new Error(`${identity} does not have Content.`)
    if (!row.targetTerritoryId) throw new Error(`${identity} does not have a Target Territory.`)
    if (!row.businessWrittenYearId) throw new Error(`${identity} does not have a Business Written Year.`)
  }
}

async function resolveContentToBusinessWrittenGroup(contentIds: string[]): Promise<Map<string, BusinessWrittenGroupRow>> {
  const uniqueIds = [...new Set(contentIds.filter(Boolean))]
  const fetchXml = `
    <fetch>
      <entity name='fmi_content'>
        <attribute name='fmi_contentid' />
        <attribute name='fmi_name' />
        <filter>${inCondition('fmi_contentid', uniqueIds)}</filter>
        <link-entity name='fmi_fmi_businesswrittengroup_fmi_content' from='fmi_contentid' to='fmi_contentid' intersect='true' visible='false' link-type='outer'>
          <link-entity name='fmi_businesswrittengroup' from='fmi_businesswrittengroupid' to='fmi_businesswrittengroupid' alias='bwg' link-type='outer'>
            <attribute name='fmi_businesswrittengroupid' />
            <attribute name='fmi_name' />
            <attribute name='fmi_includeinvariances' />
          </link-entity>
        </link-entity>
      </entity>
    </fetch>`

  const matchesByContentId = new Map<string, BusinessWrittenGroupRow[]>()
  const contentNameById = new Map<string, string>()

  for (const record of await fetchRecords('fmi_content', fetchXml)) {
    const contentId = normalizeGuid(record.fmi_contentid)
    contentNameById.set(contentId, asString(record.fmi_name) || contentId)

    const bwgId = normalizeGuid(aliasValue(record, 'bwg.fmi_businesswrittengroupid'))
    if (!bwgId) continue

    const existing = matchesByContentId.get(contentId) ?? []
    existing.push({
      id: bwgId,
      name: asString(aliasValue(record, 'bwg.fmi_name')) || aliasFormattedValue(record, 'bwg.fmi_name'),
      includeInVariance: asBoolean(aliasValue(record, 'bwg.fmi_includeinvariances'), true),
    })
    matchesByContentId.set(contentId, existing)
  }

  const result = new Map<string, BusinessWrittenGroupRow>()

  for (const contentId of uniqueIds) {
    const contentName = contentNameById.get(contentId)
    if (!contentName) throw new Error('A Content record referenced by an Opportunity Item could not be found.')

    const matches = matchesByContentId.get(contentId) ?? []
    if (matches.length === 0) continue
    if (matches.length > 1) {
      throw new Error(`Content '${contentName}' is assigned to more than one Business Written Group. Approval cannot be submitted until the mapping is corrected.`)
    }
    result.set(contentId, matches[0])
  }

  return result
}

async function resolveTerritoryToBusinessWrittenTerritory(territoryIds: string[]): Promise<Map<string, BusinessWrittenTerritoryRow>> {
  const uniqueIds = [...new Set(territoryIds.filter(Boolean))]
  const fetchXml = `
    <fetch>
      <entity name='fmi_targetterritory'>
        <attribute name='fmi_targetterritoryid' />
        <attribute name='fmi_name' />
        <attribute name='fmi_businesswrittenterritory' />
        <filter>${inCondition('fmi_targetterritoryid', uniqueIds)}</filter>
      </entity>
    </fetch>`

  const result = new Map<string, BusinessWrittenTerritoryRow>()
  const seen = new Set<string>()

  for (const record of await fetchRecords('fmi_targetterritory', fetchXml)) {
    const territoryId = normalizeGuid(record.fmi_targetterritoryid)
    seen.add(territoryId)
    const businessWrittenTerritoryId = normalizeGuid(record._fmi_businesswrittenterritory_value)
    if (!businessWrittenTerritoryId) continue
    result.set(territoryId, {
      id: businessWrittenTerritoryId,
      name: formattedValue(record, '_fmi_businesswrittenterritory_value'),
    })
  }

  if (uniqueIds.some((id) => !seen.has(id))) {
    throw new Error('A Target Territory referenced by an Opportunity Item could not be retrieved.')
  }

  return result
}

async function resolveBudgets(resolvedItems: ResolvedItem[]): Promise<Map<string, BudgetRow>> {
  const combinations = new Map<string, ResolvedItem>()

  for (const item of resolvedItems) {
    if (!item.businessWrittenGroup || !item.businessWrittenTerritory) continue
    combinations.set(
      budgetKey(item.businessWrittenGroup.id, item.businessWrittenTerritory.id, item.row.businessWrittenYearId),
      item,
    )
  }

  const combinationItems = [...combinations.values()]
  if (combinationItems.length === 0) return new Map()

  const businessWrittenGroupIds = combinationItems.map((item) => item.businessWrittenGroup?.id ?? '')
  const businessWrittenTerritoryIds = combinationItems.map((item) => item.businessWrittenTerritory?.id ?? '')
  const businessWrittenYearIds = combinationItems.map((item) => item.row.businessWrittenYearId)
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
          ${inCondition('fmi_businesswrittengroup', businessWrittenGroupIds)}
          ${inCondition('fmi_bwterritory', businessWrittenTerritoryIds)}
          ${inCondition('fmi_businesswrittenyear', businessWrittenYearIds)}
        </filter>
      </entity>
    </fetch>`

  const matchesByCombination = new Map<string, BudgetRow[]>()

  for (const record of await fetchRecords('goal', fetchXml)) {
    const row: BudgetRow = {
      id: normalizeGuid(record.goalid),
      businessWrittenGroupId: normalizeGuid(record._fmi_businesswrittengroup_value),
      businessWrittenTerritoryId: normalizeGuid(record._fmi_bwterritory_value),
      businessWrittenYearId: normalizeGuid(record._fmi_businesswrittenyear_value),
      currentYearBudget: asNumber(record.fmi_currentyearbudget),
      fc1: asNullableNumber(record.fmi_fc1),
      fc2: asNullableNumber(record.fmi_fc2),
      fc3: asNullableNumber(record.fmi_fc3),
    }
    const key = budgetKey(row.businessWrittenGroupId, row.businessWrittenTerritoryId, row.businessWrittenYearId)
    const existing = matchesByCombination.get(key) ?? []
    existing.push(row)
    matchesByCombination.set(key, existing)
  }

  const result = new Map<string, BudgetRow>()
  for (const [key, item] of combinations) {
    const matches = matchesByCombination.get(key) ?? []
    if (matches.length === 0) continue
    if (matches.length > 1) {
      throw new Error(`More than one Budget exists for ${item.businessWrittenGroup?.name} / ${item.businessWrittenTerritory?.name} / ${item.row.businessWrittenYearName}. The Budget configuration must be corrected before approval can be submitted.`)
    }
    result.set(key, matches[0])
  }

  return result
}

function calculateFinancials(sale: number, budget: BudgetRow): FinancialResult {
  const latestForecastType = budget.fc3 !== null ? 'FC3' : budget.fc2 !== null ? 'FC2' : budget.fc1 !== null ? 'FC1' : null
  const latestForecast = budget.fc3 ?? budget.fc2 ?? budget.fc1 ?? 0

  return {
    sale,
    budget: budget.currentYearBudget,
    fc1: budget.fc1,
    fc2: budget.fc2,
    fc3: budget.fc3,
    latestForecast,
    latestForecastType,
    varianceToForecast: sale - latestForecast,
    varianceToBudget: sale - budget.currentYearBudget,
    belowForecast: latestForecastType !== null && sale < latestForecast,
    financialComparisonAvailable: true,
    financialWarning: null,
  }
}

function unavailableFinancials(sale: number, warning: string): FinancialResult {
  return {
    sale,
    budget: null,
    fc1: null,
    fc2: null,
    fc3: null,
    latestForecast: null,
    latestForecastType: null,
    varianceToForecast: null,
    varianceToBudget: null,
    belowForecast: false,
    financialComparisonAvailable: false,
    financialWarning: warning,
  }
}

function resolveFinancials(item: ResolvedItem, budgetsByCombination: Map<string, BudgetRow>): FinancialResult {
  if (!item.businessWrittenGroup) {
    return unavailableFinancials(item.row.sale, 'Content is not assigned to a Business Written Group.')
  }

  if (!item.businessWrittenTerritory) {
    return unavailableFinancials(item.row.sale, 'Target Territory is not mapped to a Business Written Territory.')
  }

  const key = budgetKey(item.businessWrittenGroup.id, item.businessWrittenTerritory.id, item.row.businessWrittenYearId)
  const budget = budgetsByCombination.get(key)
  if (!budget) {
    return unavailableFinancials(item.row.sale, `No Budget/Forecast record was found for ${item.businessWrittenGroup.name} / ${item.businessWrittenTerritory.name} / ${item.row.businessWrittenYearName}.`)
  }

  return calculateFinancials(item.row.sale, budget)
}

function buildPreviewItem(item: ResolvedItem, budgetsByCombination: Map<string, BudgetRow>): DealPreviewItem {
  const financials = resolveFinancials(item, budgetsByCombination)
  return {
    opportunityItemId: item.row.id,
    title: item.row.contentName || item.row.name,
    territory: item.row.targetTerritoryName,
    businessWrittenGroup: item.businessWrittenGroup?.name ?? null,
    businessWrittenYear: item.row.businessWrittenYearName,
    sale: financials.sale,
    budget: financials.budget,
    fc1: financials.fc1,
    fc2: financials.fc2,
    fc3: financials.fc3,
    latestForecast: financials.latestForecast,
    latestForecastType: financials.latestForecastType,
    varianceToForecast: financials.varianceToForecast,
    varianceToBudget: financials.varianceToBudget,
    belowForecast: financials.belowForecast,
    financialComparisonAvailable: financials.financialComparisonAvailable,
    financialWarning: financials.financialWarning,
    includeInVariance: item.businessWrittenGroup?.includeInVariance ?? true,
  }
}

export async function getClientDealApprovalPreview(opportunityId: string): Promise<DealApprovalPreview> {
  const [opportunity, items] = await Promise.all([
    retrieveOpportunity(opportunityId),
    retrieveOpportunityItems(opportunityId),
  ])

  if (items.length === 0) {
    throw new Error('This Opportunity has no Opportunity Items, so there is nothing to preview.')
  }

  validateRequiredLookups(items)

  const [businessWrittenGroupsByContentId, businessWrittenTerritoriesByTerritoryId] = await Promise.all([
    resolveContentToBusinessWrittenGroup(items.map((item) => item.contentId)),
    resolveTerritoryToBusinessWrittenTerritory(items.map((item) => item.targetTerritoryId)),
  ])

  const resolvedItems = items.map((row) => ({
    row,
    businessWrittenGroup: businessWrittenGroupsByContentId.get(row.contentId) ?? null,
    businessWrittenTerritory: businessWrittenTerritoriesByTerritoryId.get(row.targetTerritoryId) ?? null,
  }))
  const budgetsByCombination = await resolveBudgets(resolvedItems)

  return {
    opportunityId: opportunity.id,
    opportunityName: opportunity.name,
    items: resolvedItems.map((item) => buildPreviewItem(item, budgetsByCombination)),
  }
}