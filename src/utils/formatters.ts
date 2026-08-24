const usdCurrency = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  maximumFractionDigits: 0,
})

export function formatUsd(value: number | undefined | null): string {
  return value === undefined || value === null ? '-' : usdCurrency.format(value)
}

export function formatDateTime(value?: string): string {
  if (!value) return '-'
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? '-' : new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short' }).format(parsed)
}

export function formatDateOnly(value?: string): string {
  if (!value) return '-'
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? '-' : new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium' }).format(parsed)
}
