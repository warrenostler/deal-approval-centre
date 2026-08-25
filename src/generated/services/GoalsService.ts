import type { IOperationResult } from '@microsoft/power-apps/data'
import { dataSourcesInfo } from '../../../.power/schemas/appschemas/dataSourcesInfo'
import { getClient } from '@microsoft/power-apps/data'

export type GoalRecord = Record<string, unknown>

export class GoalsService {
  private static readonly dataSourceName = 'goals'
  private static readonly client = getClient(dataSourcesInfo)

  public static async getAll(options?: { select?: string[]; filter?: string; top?: number }): Promise<IOperationResult<GoalRecord[]>> {
    return GoalsService.client.retrieveMultipleRecordsAsync<GoalRecord>(GoalsService.dataSourceName, options)
  }
}
