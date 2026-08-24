import { HelpCircleIcon, ShieldCheckIcon, ChevronDownIcon } from '../components/icons'

export function AppHeader() {
  return (
    <header className="app-header">
      <div className="app-header-brand">
        <span className="app-header-mark"><ShieldCheckIcon width={18} height={18} /></span>
        <span className="app-header-title">Deal Approval Centre</span>
      </div>
      <div className="app-header-actions">
        <button type="button" className="app-header-icon-button" aria-label="Help">
          <HelpCircleIcon width={19} height={19} />
        </button>
        <span className="app-header-avatar" aria-hidden="true">WO</span>
        <ChevronDownIcon width={16} height={16} className="app-header-chevron" />
      </div>
    </header>
  )
}
