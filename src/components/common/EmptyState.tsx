interface EmptyStateProps {
  title: string
  hint?: string
}

export function EmptyState({ title, hint }: EmptyStateProps) {
  return (
    <div className="state state-empty" role="status" aria-live="polite">
      <p>{title}</p>
      {hint ? <small>{hint}</small> : null}
    </div>
  )
}
