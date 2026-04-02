import type { InjectionKey } from 'vue'

export const RefreshKey: InjectionKey<(fn: () => Promise<void>) => void> = Symbol('pull-to-refresh')
