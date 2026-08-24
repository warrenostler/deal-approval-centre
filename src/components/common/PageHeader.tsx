import { Link } from 'react-router-dom'

interface PageHeaderProps {
  title: string
  subtitle?: string
  actionLabel?: string
  actionTo?: string
}

export function PageHeader({ title, subtitle, actionLabel, actionTo }: PageHeaderProps) {
  return (
    <header className="page-header">
      <div>
        <h1>{title}</h1>
        {subtitle ? <p>{subtitle}</p> : null}
      </div>
      {actionLabel && actionTo ? <Link to={actionTo}>{actionLabel}</Link> : null}
    </header>
  )
}
