import type { SVGProps } from 'react'

type IconProps = SVGProps<SVGSVGElement>

const base = {
  viewBox: '0 0 24 24',
  fill: 'none' as const,
  stroke: 'currentColor',
  strokeWidth: 1.8,
  strokeLinecap: 'round' as const,
  strokeLinejoin: 'round' as const,
}

export function ArrowLeftIcon(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <path d="M19 12H5" />
      <path d="M11 18l-6-6 6-6" />
    </svg>
  )
}

export function DocumentIcon(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <path d="M7 3h7l4 4v14H7z" />
      <path d="M14 3v4h4" />
      <path d="M9.5 13h5M9.5 16.5h5" />
    </svg>
  )
}

export function ClockIcon(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <circle cx="12" cy="12" r="8.5" />
      <path d="M12 8v4.2l3 1.8" />
    </svg>
  )
}

export function UserIcon(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <circle cx="12" cy="8" r="3.4" />
      <path d="M5 20c1.2-3.6 4-5.4 7-5.4s5.8 1.8 7 5.4" />
    </svg>
  )
}

export function CalendarIcon(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <rect x="4" y="5.5" width="16" height="14.5" rx="2" />
      <path d="M4 10h16M8 3v4M16 3v4" />
    </svg>
  )
}

export function DollarIcon(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <path d="M12 3v18" />
      <path d="M16.5 7.2c-.7-1-2.1-1.7-4-1.7-2.3 0-4.2 1.1-4.2 3s1.9 2.7 4.2 3.1c2.3.4 4.2 1.2 4.2 3.1s-1.9 3-4.2 3c-1.9 0-3.3-.6-4-1.7" />
    </svg>
  )
}

export function CommentIcon(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <path d="M4 5.5h16v10.5H9.5L5 20v-4H4z" />
    </svg>
  )
}

export function LayersIcon(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <path d="M12 3.5l8 4.4-8 4.4-8-4.4z" />
      <path d="M4 13.4l8 4.4 8-4.4" />
      <path d="M4 17.5l8 4.4 8-4.4" />
    </svg>
  )
}

export function GavelIcon(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <path d="M9 4.5l6 6" />
      <path d="M4.5 9l6 6" />
      <path d="M6.7 6.7l6 6" />
      <path d="M13 15l6 6" />
      <path d="M3 21h9" />
    </svg>
  )
}

export function CheckIcon(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <path d="M5 12.5l4.5 4.5L19 7.5" />
    </svg>
  )
}

export function XIcon(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <path d="M6 6l12 12M18 6L6 18" />
    </svg>
  )
}

export function ChevronDownIcon(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <path d="M6 9l6 6 6-6" />
    </svg>
  )
}

export function ShieldCheckIcon(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <path d="M12 3l7 3v6c0 4.6-3 7.6-7 9-4-1.4-7-4.4-7-9V6z" />
      <path d="M9 12l2 2 4-4" />
    </svg>
  )
}

export function HelpCircleIcon(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <circle cx="12" cy="12" r="8.5" />
      <path d="M9.5 9.3a2.5 2.5 0 1 1 3.5 2.3c-.9.5-1.3 1-1.3 1.9" />
      <path d="M12 16.6h.01" />
    </svg>
  )
}
