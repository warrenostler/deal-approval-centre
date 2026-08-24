export class DataverseError extends Error {
  public readonly status?: number
  public readonly path?: string

  constructor(message: string, status?: number, path?: string) {
    super(message)
    this.name = 'DataverseError'
    this.status = status
    this.path = path
  }
}
