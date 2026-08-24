import { useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'

export interface RowAction {
  key: string
  label: string
  variant?: 'default' | 'danger' | 'restore'
  disabled?: boolean
  onClick: () => void
  confirm?: {
    message: string
    confirmLabel?: string
    cancelLabel?: string
  }
}

interface RowActionMenuProps {
  actions: RowAction[]
  label?: string
}

export function RowActionMenu({ actions, label = 'Row actions' }: RowActionMenuProps) {
  const [isOpen, setIsOpen] = useState(false)
  const [menuStyle, setMenuStyle] = useState<React.CSSProperties | null>(null)
  const [pendingActionKey, setPendingActionKey] = useState<string | null>(null)
  const triggerRef = useRef<HTMLButtonElement | null>(null)
  const menuRef = useRef<HTMLDivElement | null>(null)

  const openMenu = () => {
    const trigger = triggerRef.current
    if (!trigger) {
      return
    }

    const rect = trigger.getBoundingClientRect()
    const estimatedMenuWidth = 160
    const estimatedMenuHeight = actions.length * 38 + 8
    const spaceBelow = window.innerHeight - rect.bottom - 6
    const openUpward = spaceBelow < estimatedMenuHeight && rect.top > estimatedMenuHeight

    let left = rect.right - estimatedMenuWidth
    if (left < 8) {
      left = rect.left
    }

    const top = openUpward
      ? rect.top - estimatedMenuHeight - 4
      : rect.bottom + 4

    setMenuStyle({
      position: 'fixed',
      top,
      left,
      minWidth: estimatedMenuWidth,
      zIndex: 1200,
    })

    setIsOpen(true)
  }

  const closeMenu = () => {
    setIsOpen(false)
    setMenuStyle(null)
    setPendingActionKey(null)
  }

  const pendingAction = pendingActionKey
    ? actions.find((action) => action.key === pendingActionKey) ?? null
    : null

  const handleTriggerClick = (event: React.MouseEvent) => {
    event.stopPropagation()
    if (isOpen) {
      closeMenu()
    } else {
      openMenu()
    }
  }

  const handleTriggerKeyDown = (event: React.KeyboardEvent) => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault()
      event.stopPropagation()
      if (isOpen) {
        closeMenu()
      } else {
        openMenu()
      }
    }

    if (event.key === 'Escape') {
      closeMenu()
      triggerRef.current?.focus()
    }
  }

  const handleMenuKeyDown = (event: React.KeyboardEvent) => {
    if (event.key === 'Escape') {
      if (pendingAction) {
        setPendingActionKey(null)
        return
      }

      closeMenu()
      triggerRef.current?.focus()
    }
  }

  useEffect(() => {
    if (!isOpen) {
      return
    }

    const handleOutsideClick = (event: MouseEvent) => {
      const target = event.target
      if (!(target instanceof Node)) {
        return
      }

      const clickedTrigger = triggerRef.current?.contains(target)
      const clickedMenu = menuRef.current?.contains(target)

      if (!clickedTrigger && !clickedMenu) {
        closeMenu()
      }
    }

    const handleScroll = () => {
      closeMenu()
    }

    document.addEventListener('mousedown', handleOutsideClick, true)
    window.addEventListener('scroll', handleScroll, { passive: true, capture: true })

    return () => {
      document.removeEventListener('mousedown', handleOutsideClick, true)
      window.removeEventListener('scroll', handleScroll, true)
    }
  }, [isOpen])

  return (
    <div className="row-action-menu-wrap">
      <button
        ref={triggerRef}
        type="button"
        className={`row-action-trigger${isOpen ? ' row-action-trigger-open' : ''}`}
        onClick={handleTriggerClick}
        onKeyDown={handleTriggerKeyDown}
        aria-haspopup="menu"
        aria-expanded={isOpen}
        aria-label={label}
      >
        <span aria-hidden="true">…</span>
      </button>

      {isOpen && menuStyle
        ? createPortal(
          <div
            ref={menuRef}
            className="row-action-dropdown"
            style={menuStyle}
            role="menu"
            aria-label={label}
            onKeyDown={handleMenuKeyDown}
          >
            {pendingAction ? (
              <div className="row-action-confirm" role="group" aria-label={`Confirm ${pendingAction.label}`}>
                <p className="row-action-confirm-message">{pendingAction.confirm?.message ?? `Confirm ${pendingAction.label}?`}</p>
                <div className="row-action-confirm-actions">
                  <button
                    type="button"
                    className="row-action-confirm-cancel"
                    onClick={(event) => {
                      event.stopPropagation()
                      setPendingActionKey(null)
                    }}
                  >
                    {pendingAction.confirm?.cancelLabel ?? 'Cancel'}
                  </button>
                  <button
                    type="button"
                    className={`row-action-confirm-approve${pendingAction.variant === 'danger' ? ' row-action-confirm-approve-danger' : ''}${pendingAction.variant === 'restore' ? ' row-action-confirm-approve-restore' : ''}`}
                    onClick={(event) => {
                      event.stopPropagation()
                      closeMenu()
                      pendingAction.onClick()
                    }}
                  >
                    {pendingAction.confirm?.confirmLabel ?? pendingAction.label}
                  </button>
                </div>
              </div>
            ) : actions.map((action) => (
              <button
                key={action.key}
                type="button"
                role="menuitem"
                className={`row-action-item${action.variant === 'danger' ? ' row-action-item-danger' : ''}${action.variant === 'restore' ? ' row-action-item-restore' : ''}`}
                disabled={action.disabled}
                onClick={(event) => {
                  event.stopPropagation()
                  if (action.confirm) {
                    setPendingActionKey(action.key)
                    return
                  }

                  closeMenu()
                  action.onClick()
                }}
              >
                {action.label}
              </button>
            ))}
          </div>,
          document.body,
        )
        : null}
    </div>
  )
}
