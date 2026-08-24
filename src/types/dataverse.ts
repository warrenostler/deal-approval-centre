export interface DataverseQueryOptions {
  select?: string[]
  filter?: string
  expand?: string[]
  orderBy?: string[]
  top?: number
}

declare global {
  interface Window {
    Xrm?: {
      Utility?: {
        getGlobalContext?: () => {
          getClientUrl: () => string
          userSettings?: {
            userId?: string
          }
        }
      }
    }
  }
}

export {}
