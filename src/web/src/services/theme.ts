export const THEME_OPTIONS = [
  { id: 'dark', label: 'Dark' },
  { id: 'light', label: 'Light' },
  { id: 'british-museum', label: 'British Museum' },
  { id: 'louvre', label: 'Louvre' },
  { id: 'capitoline', label: 'Capitoline' },
  { id: 'byzantine', label: 'Byzantine' },
  { id: 'modern-greek', label: 'Modern Greek' },
] as const

export type ThemeId = (typeof THEME_OPTIONS)[number]['id']

const DEFAULT_THEME_COLOR = '#0c0a09'

// Mirrors --th-page for each palette so the browser chrome matches the page
const THEME_COLORS: Record<ThemeId, string> = {
  dark: '#0f0f1a',
  light: '#f5f0e8',
  'british-museum': '#0c0c0b',
  louvre: '#1a1510',
  capitoline: '#0a0808',
  byzantine: '#100a08',
  'modern-greek': '#060d18',
}

export function isThemeId(value: unknown): value is ThemeId {
  return typeof value === 'string' && THEME_OPTIONS.some(option => option.id === value)
}

// A missing or unrecognised theme leaves the site's original appearance in place
export function applyTheme(theme: string | null | undefined, doc: Document = document) {
  const root = doc.documentElement
  const valid = isThemeId(theme) ? theme : null

  if (valid) root.setAttribute('data-theme', valid)
  else root.removeAttribute('data-theme')

  doc
    .querySelector('meta[name="theme-color"]')
    ?.setAttribute('content', valid ? THEME_COLORS[valid] : DEFAULT_THEME_COLOR)
}
