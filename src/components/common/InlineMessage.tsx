interface InlineMessageProps {
  tone: 'success' | 'error' | 'info'
  message: string
  className?: string
  dismissLabel?: string
  onDismiss?: () => void
}

export function InlineMessage({ tone, message, className, dismissLabel = 'Dismiss message', onDismiss }: InlineMessageProps) {
  return (
    <div className={`inline-message inline-message-${tone}${className ? ` ${className}` : ''}`} role="status" aria-live="polite">
      <span>{message}</span>
      {onDismiss ? (
        <button
          type="button"
          className="inline-message-dismiss"
          onClick={onDismiss}
          aria-label={dismissLabel}
        >
          ×
        </button>
      ) : null}
    </div>
  )
}
