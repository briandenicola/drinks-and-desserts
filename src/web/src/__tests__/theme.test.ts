import { describe, it, expect, beforeEach, vi } from 'vitest'
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { createPinia, setActivePinia } from 'pinia'
import { applyTheme, isThemeId, THEME_OPTIONS } from '../services/theme'
import type { User } from '../services/users'

vi.mock('../router', () => ({ default: { push: vi.fn() } }))
vi.mock('../services/msal', () => ({
  loginWithEntra: vi.fn(),
  logoutEntra: vi.fn(),
  isEntraConfigured: () => false,
}))
vi.mock('../services/users', () => ({
  usersApi: { getMe: vi.fn(), updateMe: vi.fn() },
}))

import { usersApi } from '../services/users'
import { useAuthStore } from '../stores/auth'
import { useThemeSelection } from '../composables/useThemeSelection'

const getMe = vi.mocked(usersApi.getMe)
const updateMe = vi.mocked(usersApi.updateMe)

function installDom() {
  const attrs = new Map<string, string>()
  const meta = { content: '#0c0a09', setAttribute: (_: string, v: string) => { meta.content = v } }
  const store = new Map<string, string>()
  Object.assign(globalThis, {
    document: {
      documentElement: {
        setAttribute: (k: string, v: string) => attrs.set(k, v),
        removeAttribute: (k: string) => attrs.delete(k),
        getAttribute: (k: string) => attrs.get(k) ?? null,
      },
      querySelector: () => meta,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      visibilityState: 'visible',
    },
    window: { addEventListener: vi.fn(), removeEventListener: vi.fn() },
    localStorage: {
      getItem: (k: string) => store.get(k) ?? null,
      setItem: (k: string, v: string) => void store.set(k, v),
      removeItem: (k: string) => void store.delete(k),
    },
  })
  return { attrs, meta }
}

function makeUser(id: string, theme?: string | null): User {
  return {
    id,
    displayName: id,
    email: `${id}@example.com`,
    role: 'user',
    authProvider: 'local',
    preferences: {
      favoriteTypes: [],
      defaultView: 'grid',
      collectionSort: 'rating',
      venueSort: 'rating',
      pushoverEnabled: true,
      ...(theme === undefined ? {} : { theme }),
    },
    createdAt: '',
    updatedAt: '',
  }
}

const auth = (user: User) => ({
  token: `token-${user.id}`,
  refreshToken: `refresh-${user.id}`,
  expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
  user,
})

describe('theme application', () => {
  let dom: ReturnType<typeof installDom>
  beforeEach(() => { dom = installDom() })

  it('exposes the seven labelled themes', () => {
    expect(THEME_OPTIONS.map(t => t.label)).toEqual([
      'Dark', 'Light', 'British Museum', 'Louvre', 'Capitoline', 'Byzantine', 'Modern Greek',
    ])
    expect(THEME_OPTIONS.every(t => isThemeId(t.id))).toBe(true)
  })

  it('sets a theme and removes it for missing or unknown ids', () => {
    applyTheme('louvre')
    expect(dom.attrs.get('data-theme')).toBe('louvre')
    expect(dom.meta.content).toBe('#1a1510')

    applyTheme('not-a-theme')
    expect(dom.attrs.has('data-theme')).toBe(false)
    expect(dom.meta.content).toBe('#0c0a09')

    applyTheme('light')
    applyTheme(null)
    expect(dom.attrs.has('data-theme')).toBe(false)
  })
})

describe('account theme state', () => {
  let dom: ReturnType<typeof installDom>
  beforeEach(() => {
    dom = installDom()
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('applies the saved theme from the stored session on startup', () => {
    const user = makeUser('a', 'capitoline')
    localStorage.setItem('whiskey_and_smokes_token', 'token-a')
    localStorage.setItem('whiskey_and_smokes_user', JSON.stringify(user))
    getMe.mockResolvedValue({ data: user } as never)

    useAuthStore().initialize()

    expect(dom.attrs.get('data-theme')).toBe('capitoline')
  })

  it('keeps the default appearance when the account has no saved theme', () => {
    const store = useAuthStore()
    store.applyAuthResponse(auth(makeUser('a')))
    expect(dom.attrs.has('data-theme')).toBe(false)

    store.applyAuthResponse(auth(makeUser('a', null)))
    expect(dom.attrs.has('data-theme')).toBe(false)
  })

  it('does not carry one account\'s theme to the next account or past logout', async () => {
    const store = useAuthStore()
    store.applyAuthResponse(auth(makeUser('a', 'byzantine')))
    expect(dom.attrs.get('data-theme')).toBe('byzantine')

    store.applyAuthResponse(auth(makeUser('b')))
    expect(dom.attrs.has('data-theme')).toBe(false)

    store.applyAuthResponse(auth(makeUser('c', 'light')))
    await store.logout()
    expect(dom.attrs.has('data-theme')).toBe(false)
  })

  it('ignores a profile response that arrives after sign-out', async () => {
    const store = useAuthStore()
    store.applyAuthResponse(auth(makeUser('a', 'louvre')))
    let resolve!: (v: unknown) => void
    getMe.mockReturnValue(new Promise(r => { resolve = r }) as never)

    const pending = store.loadUser()
    await store.logout()
    resolve({ data: makeUser('a', 'louvre') })
    await pending

    expect(store.user).toBeNull()
    expect(dom.attrs.has('data-theme')).toBe(false)
  })
})

describe('theme selection', () => {
  let dom: ReturnType<typeof installDom>
  beforeEach(() => {
    dom = installDom()
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('saves the full preferences object with the new theme and applies it', async () => {
    const store = useAuthStore()
    store.applyAuthResponse(auth(makeUser('a')))
    updateMe.mockImplementation(async data => ({
      data: { ...makeUser('a'), preferences: data.preferences! },
    }) as never)

    const picker = useThemeSelection()
    await picker.selectTheme('modern-greek')

    expect(updateMe).toHaveBeenCalledWith({
      preferences: expect.objectContaining({
        collectionSort: 'rating',
        pushoverEnabled: true,
        theme: 'modern-greek',
      }),
    })
    expect(picker.selectedTheme.value).toBe('modern-greek')
    expect(dom.attrs.get('data-theme')).toBe('modern-greek')
    expect(picker.isError.value).toBe(false)
  })

  describe('delayed saves', () => {
    function deferredUpdate() {
      let resolve!: (user: User) => void
      let reject!: (e: unknown) => void
      updateMe.mockReturnValue(new Promise((res, rej) => {
        resolve = user => res({ data: user } as never)
        reject = rej
      }) as never)
      return { resolve, reject }
    }

    it('does not restore the old account when the save finishes after logout', async () => {
      const store = useAuthStore()
      store.applyAuthResponse(auth(makeUser('a')))
      const save = deferredUpdate()
      const picker = useThemeSelection()

      const pending = picker.selectTheme('louvre')
      await store.logout()
      save.resolve(makeUser('a', 'louvre'))
      await pending

      expect(store.user).toBeNull()
      expect(dom.attrs.has('data-theme')).toBe(false)
      expect(JSON.parse(localStorage.getItem('whiskey_and_smokes_user') ?? 'null')).toBeNull()
    })

    it('does not overwrite a different account that signed in during the save', async () => {
      const store = useAuthStore()
      store.applyAuthResponse(auth(makeUser('a')))
      const save = deferredUpdate()
      const picker = useThemeSelection()

      const pending = picker.selectTheme('louvre')
      store.applyAuthResponse(auth(makeUser('b', 'light')))
      save.resolve(makeUser('a', 'louvre'))
      await pending

      expect(store.user?.id).toBe('b')
      expect(dom.attrs.get('data-theme')).toBe('light')
      expect(picker.message.value).toBe('')
      expect(picker.isSaving.value).toBe(false)
    })

    it('drops a stale failure instead of showing it to the next account', async () => {
      const store = useAuthStore()
      store.applyAuthResponse(auth(makeUser('a')))
      const save = deferredUpdate()
      const picker = useThemeSelection()

      const pending = picker.selectTheme('louvre')
      store.applyAuthResponse(auth(makeUser('b')))
      save.reject(new Error('boom'))
      await pending

      expect(picker.isError.value).toBe(false)
      expect(picker.message.value).toBe('')
      expect(picker.displayedTheme.value).toBeNull()
    })
  })

  it('keeps the current theme and reports the error when saving fails', async () => {
    const store = useAuthStore()
    store.applyAuthResponse(auth(makeUser('a', 'dark')))
    updateMe.mockRejectedValue({ response: { data: { message: 'Invalid theme.' } } })

    const picker = useThemeSelection()
    await picker.selectTheme('light')

    expect(picker.selectedTheme.value).toBe('dark')
    expect(picker.displayedTheme.value).toBe('dark')
    expect(dom.attrs.get('data-theme')).toBe('dark')
    expect(picker.isError.value).toBe(true)
    expect(picker.message.value).toBe('Invalid theme.')
    expect(picker.isSaving.value).toBe(false)
  })
})

describe('stylesheet coverage', () => {
  const root = join(fileURLToPath(import.meta.url), '..', '..')
  const css = readFileSync(join(root, 'style.css'), 'utf8')

  function tokensOf(selector: string) {
    const escaped = selector.replace(/[[\]"]/g, '\\$&')
    const block = new RegExp(`${escaped}\\s*\\{([^}]*)\\}`).exec(css)
    return [...(block?.[1] ?? '').matchAll(/(--th-[\w-]+):/g)].map(m => m[1]).sort()
  }

  // Pale accent tokens whose :root value already suits the dark themes; only light must override them.
  const inheritedByDark = ['--th-warning', '--th-sky', '--th-blue', '--th-pink', '--th-emerald']

  it('defines every semantic token for every theme', () => {
    const base = tokensOf(':root')
    expect(base.length).toBeGreaterThan(10)
    for (const { id } of THEME_OPTIONS) {
      const expected = id === 'light' ? base : base.filter(t => !inheritedByDark.includes(t))
      expect(tokensOf(`[data-theme="${id}"]`), id).toEqual(expected)
    }
  })

  function vueSources() {
    const files: string[] = []
    const walk = (dir: string) => {
      for (const name of readdirSync(dir)) {
        const path = join(dir, name)
        if (statSync(path).isDirectory()) walk(path)
        else if (name.endsWith('.vue')) files.push(path)
      }
    }
    walk(root)
    return files.map(path => ({ path, text: readFileSync(path, 'utf8') }))
  }

  it('maps the status and category tokens to utilities', () => {
    for (const t of inheritedByDark) expect(css).toContain(`--color-th-${t.slice(5)}: var(${t})`)
  })

  it('keeps pale or fixed-hue status colours out of themed components', () => {
    const rawStatus = /(?:text|border)-(?:red|green|yellow|amber|emerald|sky|blue|pink)-\d00\b|bg-(?:red|green|yellow|amber)-(?:[89]00|950)\/\d+|disabled:text-th-accent\/70/
    const offenders = vueSources().filter(f => rawStatus.test(f.text)).map(f => f.path)
    expect(offenders).toEqual([])
  })

  function valueOf(selector: string, token: string) {
    const escaped = selector.replace(/[[\]"]/g, '\\$&')
    const block = new RegExp(`${escaped}\\s*\\{([^}]*)\\}`).exec(css)?.[1] ?? ''
    return new RegExp(`${token}:\\s*([^;]+);`).exec(block)?.[1]
  }

  it('only Capitoline separates cream labels from gold headings and lifts inputs above panels', () => {
    for (const { id } of THEME_OPTIONS) {
      if (id === 'capitoline') continue
      const sel = `[data-theme="${id}"]`
      expect(valueOf(sel, '--th-emphasis'), id).toBe(valueOf(sel, '--th-accent'))
      expect(valueOf(sel, '--th-chrome'), id).toBe(valueOf(sel, '--th-inset'))
      expect(valueOf(sel, '--th-heading'), id).toBe(valueOf(sel, '--th-text'))
    }
    const cap = '[data-theme="capitoline"]'
    expect(valueOf(cap, '--th-emphasis')).toBe('#f0bc42')
    expect(valueOf(cap, '--th-heading')).toBe('#f0bc42')
    expect(valueOf(cap, '--th-accent')).toBe('#f0e8d8')
    expect(valueOf(cap, '--th-card')).not.toBe(valueOf(cap, '--th-inset'))
    expect(valueOf(cap, '--th-chrome')).not.toBe(valueOf(cap, '--th-inset'))
    expect(css).toMatch(/\[data-theme="capitoline"\] body \{[^}]*Inter Capitoline/)
  })

  it('colours headings with a heading role, not the label or body text colour', () => {
    const offenders = vueSources()
      .filter(f => /<(?:h[1-4]|legend)\b[^>]*text-th-(?:accent|text)\b/.test(f.text))
      .map(f => f.path)
    expect(offenders).toEqual([])
  })

  it('leaves no hardcoded palette colours in components', () => {
    const offenders = vueSources().filter(f => /\[#[0-9a-f]{6}\]/i.test(f.text)).map(f => f.path)
    expect(offenders).toEqual([])
  })
})
