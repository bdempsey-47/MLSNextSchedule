import { useState, useEffect } from 'react'
import { X } from 'lucide-react'
import { announcements } from '../announcements'
import './AnnouncementBar.css'

export default function AnnouncementBar() {
  const active = announcements.find(a =>
    a.enabled &&
    new Date() < new Date(a.expiresAt) &&
    !sessionStorage.getItem(`announcement-dismissed-${a.id}`)
  )

  const [visible, setVisible] = useState(false)
  const [mounted, setMounted] = useState(!!active)

  useEffect(() => {
    if (active) {
      // Small delay so the slide-down animation plays after mount
      const t = setTimeout(() => setVisible(true), 16)
      return () => clearTimeout(t)
    }
    return undefined
  }, [active?.id])

  if (!active || !mounted) return null

  const dismiss = () => {
    sessionStorage.setItem(`announcement-dismissed-${active.id}`, '1')
    setVisible(false)
  }

  const handleTransitionEnd = () => {
    if (!visible) setMounted(false)
  }

  return (
    <div
      className={`announcement-bar${visible ? ' announcement-bar--visible' : ''}`}
      onTransitionEnd={handleTransitionEnd}
    >
      <a
        href={active.url}
        className="announcement-link"
        target="_blank"
        rel="noopener noreferrer"
      >
        {active.message}
      </a>
      <button
        className="announcement-close"
        onClick={dismiss}
        aria-label="Dismiss announcement"
      >
        <X size={14} />
      </button>
    </div>
  )
}
