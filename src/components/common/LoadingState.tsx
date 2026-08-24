interface LoadingStateProps {
  label?: string
}

export function LoadingState({ label = 'Loading...' }: LoadingStateProps) {
  return <p className="state state-loading">{label}</p>
}
