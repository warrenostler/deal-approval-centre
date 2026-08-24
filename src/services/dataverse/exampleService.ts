import { retrieveMultiple } from './client'

export interface BasicRecord {
  id: string
  name: string
}

export async function getAccounts(top = 25): Promise<BasicRecord[]> {
  const response = await retrieveMultiple<Record<string, unknown>>('accounts', {
    select: ['accountid', 'name'],
    top,
    orderBy: ['name asc'],
  })

  return response.value.map((record) => ({
    id: String(record.accountid ?? record.id ?? ''),
    name: String(record.name ?? ''),
  }))
}
