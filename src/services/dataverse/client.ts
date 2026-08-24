import type { DataverseQueryOptions } from '../../types/dataverse'
import { getClient } from '@microsoft/power-apps/data'
import { DataverseError } from './errors'
import { dataSourcesInfo } from '../../../.power/schemas/appschemas/dataSourcesInfo'

const entitySetNameCache = new Map<string, string>()

const dataClient = getClient(dataSourcesInfo)
const availableDataSources = new Set(Object.keys(dataSourcesInfo).map((key) => key.toLowerCase()))

function getCandidateDataSourceNames(entitySetName: string): string[] {
  const normalized = entitySetName.trim().toLowerCase()
  const candidates = new Set<string>([normalized])

  if (normalized.endsWith('ies')) {
    candidates.add(`${normalized.slice(0, -3)}y`)
  }

  if (normalized.endsWith('es')) {
    candidates.add(normalized.slice(0, -2))
  }

  if (normalized.endsWith('s')) {
    candidates.add(normalized.slice(0, -1))
  }

  candidates.add(`${normalized}s`)
  candidates.add(`${normalized}es`)

  return Array.from(candidates)
}

function getDataSourceName(entitySetName: string): string {
  const candidates = getCandidateDataSourceNames(entitySetName)
  const matched = candidates.find((candidate) => availableDataSources.has(candidate))

  if (!matched) {
    throw new DataverseError(
      `Data source not configured for entity set: ${entitySetName}. Add it with power-apps add-data-source.`,
      undefined,
      entitySetName,
    )
  }

  return matched
}

function unwrapResult<T>(result: { success: boolean; data: T; error?: unknown }, operation: string): T {
  if (result.success) {
    return result.data
  }

  let message: string | null = null

  if (result.error instanceof Error) {
    message = result.error.message
  } else if (typeof result.error === 'string') {
    message = result.error
  } else if (result.error && typeof result.error === 'object') {
    const errorRecord = result.error as Record<string, unknown>
    const directMessage = errorRecord.message
    if (typeof directMessage === 'string' && directMessage.trim()) {
      message = directMessage
    } else {
      const nestedMessage =
        (errorRecord.error as { message?: unknown } | undefined)?.message ??
        (errorRecord.details as { message?: unknown } | undefined)?.message
      if (typeof nestedMessage === 'string' && nestedMessage.trim()) {
        message = nestedMessage
      }
    }

    if (!message) {
      try {
        message = JSON.stringify(result.error)
      } catch {
        message = null
      }
    }
  }

  if (!message) {
    message = `Dataverse runtime operation failed: ${operation}`
  }

  throw new DataverseError(`${message} | operation: ${operation}`)
}

export async function retrieveMultiple<T>(entitySetName: string, options?: DataverseQueryOptions): Promise<{ value: T[] }> {
  const dataSourceName = getDataSourceName(entitySetName)
  const result = await dataClient.retrieveMultipleRecordsAsync<unknown>(dataSourceName, options)
  const payload = unwrapResult(result, `retrieveMultipleRecordsAsync(${dataSourceName})`)

  if (Array.isArray(payload)) {
    return { value: payload as T[] }
  }

  if (payload && typeof payload === 'object' && Array.isArray((payload as { value?: unknown }).value)) {
    return { value: (payload as { value: T[] }).value }
  }

  throw new DataverseError(
    'Unexpected Dataverse response shape: expected an array or an object with a value array.',
    undefined,
    entitySetName,
  )
}

export async function retrieveOne<T>(entitySetName: string, id: string, options?: DataverseQueryOptions): Promise<T> {
  const dataSourceName = getDataSourceName(entitySetName)
  const result = await dataClient.retrieveRecordAsync<T>(dataSourceName, id, options)
  return unwrapResult(result, `retrieveRecordAsync(${dataSourceName}, ${id})`)
}

export async function resolveEntitySetName(logicalName: string, fallbackEntitySetName: string): Promise<string> {
  const cacheKey = logicalName.toLowerCase()
  const cached = entitySetNameCache.get(cacheKey)
  if (cached) {
    return cached
  }

  entitySetNameCache.set(cacheKey, fallbackEntitySetName)
  return fallbackEntitySetName
}

function toCanonicalKey(key: string): string {
  return key.replace(/[^a-zA-Z0-9]/g, '').toLowerCase()
}

function findGuidValue(value: unknown): string | undefined {
  return typeof value === 'string' && /^[0-9a-fA-F-]{36}$/.test(value) ? value : undefined
}

function extractCreatedId(data: unknown, preferredIdField?: string): string | undefined {
  const directId = findGuidValue(data)
  if (directId) {
    return directId
  }

  if (!data || typeof data !== 'object') {
    return undefined
  }

  const dataRecord = data as Record<string, unknown>

  if (preferredIdField) {
    const preferred = findGuidValue(dataRecord[preferredIdField])
    if (preferred) {
      return preferred
    }

    const preferredCanonical = toCanonicalKey(preferredIdField)
    for (const [key, value] of Object.entries(dataRecord)) {
      if (toCanonicalKey(key) === preferredCanonical) {
        const matched = findGuidValue(value)
        if (matched) {
          return matched
        }
      }
    }
  }

  for (const value of Object.values(dataRecord)) {
    const matched = findGuidValue(value)
    if (matched) {
      return matched
    }
  }

  return undefined
}

export async function createRecord(
  entitySetName: string,
  body: Record<string, unknown>,
  preferredIdField?: string,
): Promise<{ id?: string }> {
  const dataSourceName = getDataSourceName(entitySetName)
  const result = await dataClient.createRecordAsync<Record<string, unknown>, unknown>(dataSourceName, body)
  const data = unwrapResult(result, `createRecordAsync(${dataSourceName})`)

  return { id: extractCreatedId(data, preferredIdField) }
}

export async function updateRecord(entitySetName: string, id: string, body: Record<string, unknown>): Promise<void> {
  const dataSourceName = getDataSourceName(entitySetName)
  const result = await dataClient.updateRecordAsync<Record<string, unknown>, unknown>(dataSourceName, id, body)
  void unwrapResult(result, `updateRecordAsync(${dataSourceName}, ${id})`)
}

export async function deleteRecord(entitySetName: string, id: string): Promise<void> {
  const dataSourceName = getDataSourceName(entitySetName)
  const result = await dataClient.deleteRecordAsync(dataSourceName, id)
  void unwrapResult(result, `deleteRecordAsync(${dataSourceName}, ${id})`)
}
