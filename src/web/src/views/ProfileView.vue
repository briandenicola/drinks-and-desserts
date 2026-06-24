<script setup lang="ts">
import { ref, inject, onMounted, onBeforeUnmount } from 'vue'
import { useAuthStore } from '../stores/auth'
import { authApi, type OIDCLinkedIdentity, type OIDCPublicProvider } from '../services/auth'
import { usersApi } from '../services/users'
import type { ApiKeyResponse, CreateApiKeyResponse } from '../services/users'
import { RefreshKey } from '../composables/refreshKey'
import { getErrorMessage } from '../services/errors'

const auth = useAuthStore()
const feedbackTimers: ReturnType<typeof setTimeout>[] = []
const displayName = ref('')
const collectionSort = ref('rating')
const collectionSortDirection = ref<'asc' | 'desc'>('desc')
const collectionFilter = ref<string | undefined>(undefined)
const venueSort = ref('rating')
const venueSortDirection = ref<'asc' | 'desc'>('desc')
const venueFilter = ref<string | undefined>(undefined)
const isSaving = ref(false)
const saveMessage = ref('')

function getDefaultSortDirection(sort: string): 'asc' | 'desc' {
  return sort === 'type' ? 'asc' : 'desc'
}

const sortOptions = [
  { label: 'Rating', value: 'rating' },
  { label: 'Type', value: 'type' },
  { label: 'Date Added', value: 'createdAt' },
  { label: 'Date Updated', value: 'updatedAt' },
]

const sortDirectionOptions = [
  { label: 'Ascending', value: 'asc' as const },
  { label: 'Descending', value: 'desc' as const },
]

const filterOptions = [
  { label: 'All', value: undefined as string | undefined },
  { label: 'Whiskey', value: 'whiskey' as string | undefined },
  { label: 'Wine', value: 'wine' as string | undefined },
  { label: 'Cocktail', value: 'cocktail' as string | undefined },
  { label: 'Vodka', value: 'vodka' as string | undefined },
  { label: 'Gin', value: 'gin' as string | undefined },
  { label: 'Espresso', value: 'espresso' as string | undefined },
  { label: 'Latte', value: 'latte' as string | undefined },
  { label: 'Cappuccino', value: 'cappuccino' as string | undefined },
  { label: 'Cold Brew', value: 'cold-brew' as string | undefined },
  { label: 'Pour Over', value: 'pour-over' as string | undefined },
  { label: 'Coffee', value: 'coffee' as string | undefined },
  { label: 'Cigar', value: 'cigar' as string | undefined },
  { label: 'Dessert', value: 'dessert' as string | undefined },
  { label: 'Custom', value: 'custom' as string | undefined },
]

const venueSortOptions = [
  { label: 'Rating', value: 'rating' },
  { label: 'Type', value: 'type' },
  { label: 'Date Added', value: 'createdAt' },
  { label: 'Date Updated', value: 'updatedAt' },
]

const venueFilterOptions = [
  { label: 'All', value: undefined as string | undefined },
  { label: 'Bar', value: 'bar' as string | undefined },
  { label: 'Lounge', value: 'lounge' as string | undefined },
  { label: 'Restaurant', value: 'restaurant' as string | undefined },
  { label: 'Cafe', value: 'cafe' as string | undefined },
  { label: 'Other', value: 'other' as string | undefined },
]

// Password change
const currentPassword = ref('')
const newPassword = ref('')
const confirmPassword = ref('')
const isChangingPassword = ref(false)
const passwordMessage = ref('')
const passwordError = ref(false)

// API Keys
const apiKeys = ref<ApiKeyResponse[]>([])
const newKeyName = ref('')
const isCreatingKey = ref(false)
const newlyCreatedKey = ref<CreateApiKeyResponse | null>(null)
const keyCopied = ref(false)
const keyMessage = ref('')

// External sign-in providers
const oidcIdentities = ref<OIDCLinkedIdentity[]>([])
const oidcProviders = ref<OIDCPublicProvider[]>([])
const oidcLoading = ref(true)
const oidcMessage = ref('')
const oidcError = ref(false)
const linkingProviderId = ref<string | null>(null)
const unlinkingIdentityId = ref<string | null>(null)

const registerRefresh = inject(RefreshKey)
registerRefresh?.(async () => {
  await auth.loadUser()
  if (auth.user) displayName.value = auth.user.displayName
  await loadApiKeys()
})

const isExporting = ref(false)
const exportMessage = ref('')

// Pushover
const pushoverEnabled = ref(false)
const pushoverAppToken = ref('')
const pushoverUserKey = ref('')
const pushoverSound = ref(true)
const pushoverMessage = ref('')
const isSavingPushover = ref(false)

async function loadApiKeys() {
  try {
    const res = await usersApi.listApiKeys()
    apiKeys.value = res.data
  } catch { /* ignore */ }
}

async function loadOidcAccounts() {
  oidcLoading.value = true
  try {
    const [identitiesResponse, providersResponse] = await Promise.all([
      authApi.getOidcIdentities(),
      authApi.getOidcPublicProviders(),
    ])
    oidcIdentities.value = identitiesResponse.data.identities ?? []
    oidcProviders.value = providersResponse.data.providers ?? []
    oidcError.value = false
  } catch (e: unknown) {
    oidcIdentities.value = []
    oidcProviders.value = []
    oidcMessage.value = getErrorMessage(e, 'Failed to load linked sign-in providers.')
    oidcError.value = true
  } finally {
    oidcLoading.value = false
  }
}

function linkableOidcProviders() {
  const linkedProviderIds = new Set(oidcIdentities.value.map(identity => identity.providerId))
  return oidcProviders.value.filter(provider => !linkedProviderIds.has(provider.id))
}

async function linkOidcProvider(provider: OIDCPublicProvider) {
  oidcMessage.value = ''
  oidcError.value = false
  linkingProviderId.value = provider.id
  try {
    const response = await authApi.startOidcLink(provider.id, {
      redirectPath: '/profile',
      callbackPath: `/profile/oidc/link/callback/${provider.id}`,
    })
    if (!response.data.authorizationUrl) {
      oidcMessage.value = `${provider.displayName} did not return an authorization URL. Ask an administrator to test the provider.`
      oidcError.value = true
      return
    }
    window.location.assign(response.data.authorizationUrl)
  } catch (e: unknown) {
    oidcMessage.value = mapOidcAccountError(e, 'link')
    oidcError.value = true
  } finally {
    linkingProviderId.value = null
  }
}

async function unlinkOidcIdentity(identity: OIDCLinkedIdentity) {
  const confirmed = window.confirm(`Unlink ${identity.providerDisplayName} from your account?`)
  if (!confirmed) return

  oidcMessage.value = ''
  oidcError.value = false
  unlinkingIdentityId.value = identity.id
  try {
    await authApi.deleteOidcIdentity(identity.id)
    await loadOidcAccounts()
    oidcMessage.value = `${identity.providerDisplayName} unlinked.`
  } catch (e: unknown) {
    oidcMessage.value = mapOidcAccountError(e, 'unlink')
    oidcError.value = true
  } finally {
    unlinkingIdentityId.value = null
  }
}

function mapOidcAccountError(error: unknown, action: 'link' | 'unlink') {
  const response = getErrorResponse(error)
  const message = getErrorMessage(error, '')
  const normalized = message.toLowerCase()

  if (response?.status === 409 && action === 'link') {
    if (normalized.includes('another user') || normalized.includes('already linked')) {
      return 'This provider account is already linked to another user. Sign in with a different provider account or ask an administrator for help.'
    }
    return 'This provider account cannot be linked automatically. Sign in locally with the intended account, then try linking again.'
  }

  if (response?.status === 409 && action === 'unlink') {
    return 'This identity cannot be unlinked because your account would have no usable sign-in method. Add a password or another sign-in method first.'
  }

  if (response?.status === 404) {
    return 'That linked identity was not found for your account. Refresh settings and try again.'
  }

  if (normalized.includes('state') || normalized.includes('claims') || response?.status === 400) {
    return 'The provider response could not be validated. Start the linking flow again from Profile.'
  }

  if (normalized.includes('configuration') || normalized.includes('discovery') || response?.status === 500) {
    if (normalized.includes('public origin')) {
      return 'OIDC public web origin is not configured. Ask an administrator to set it in Admin Panel > Settings.'
    }
    return 'The sign-in provider is not configured correctly. Ask an administrator to test the provider settings.'
  }

  return message || `Failed to ${action} OIDC identity.`
}

function getErrorResponse(error: unknown): { status?: number } | null {
  if (typeof error !== 'object' || error === null || !('response' in error)) return null
  const response = (error as { response?: unknown }).response
  if (typeof response !== 'object' || response === null) return null
  return response as { status?: number }
}

function formatDateTime(value: string) {
  return new Date(value).toLocaleString()
}

async function createApiKey() {
  if (!newKeyName.value.trim()) return
  isCreatingKey.value = true
  keyMessage.value = ''
  try {
    const res = await usersApi.createApiKey(newKeyName.value.trim())
    newlyCreatedKey.value = res.data
    keyCopied.value = false
    newKeyName.value = ''
    await loadApiKeys()
  } catch (e: unknown) {
    keyMessage.value = getErrorMessage(e, 'Failed to create key')
  } finally {
    isCreatingKey.value = false
  }
}

async function copyKey() {
  if (!newlyCreatedKey.value) return
  try {
    await navigator.clipboard.writeText(newlyCreatedKey.value.key)
    keyCopied.value = true
  } catch {
    keyMessage.value = 'Failed to copy'
  }
}

async function revokeKey(keyId: string) {
  try {
    await usersApi.revokeApiKey(keyId)
    await loadApiKeys()
  } catch {
    keyMessage.value = 'Failed to revoke key'
  }
}

async function exportData() {
  isExporting.value = true
  exportMessage.value = ''
  try {
    const response = await usersApi.exportData()
    const blob = new Blob([response.data], { type: 'application/zip' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `drinks-and-desserts-export-${new Date().toISOString().slice(0, 10)}.zip`
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    URL.revokeObjectURL(url)
    exportMessage.value = 'Export downloaded'
    feedbackTimers.push(setTimeout(() => { exportMessage.value = '' }, 3000))
  } catch {
    exportMessage.value = 'Export failed'
  } finally {
    isExporting.value = false
  }
}

onMounted(() => {
  if (auth.user) {
    displayName.value = auth.user.displayName
    collectionSort.value = auth.user.preferences?.collectionSort || 'rating'
    collectionSortDirection.value = auth.user.preferences?.collectionSortDirection || getDefaultSortDirection(collectionSort.value)
    collectionFilter.value = auth.user.preferences?.collectionFilter || undefined
    venueSort.value = auth.user.preferences?.venueSort || 'rating'
    venueSortDirection.value = auth.user.preferences?.venueSortDirection || getDefaultSortDirection(venueSort.value)
    venueFilter.value = auth.user.preferences?.venueFilter || undefined
    pushoverEnabled.value = auth.user.preferences?.pushoverEnabled ?? false
    pushoverAppToken.value = auth.user.preferences?.pushoverAppToken || ''
    pushoverUserKey.value = auth.user.preferences?.pushoverUserKey || ''
    pushoverSound.value = auth.user.preferences?.pushoverSound ?? true
  }
  loadApiKeys()
  loadOidcAccounts()
})

onBeforeUnmount(() => {
  feedbackTimers.forEach(clearTimeout)
})

async function saveProfile() {
  isSaving.value = true
  saveMessage.value = ''
  try {
    await usersApi.updateMe({
      displayName: displayName.value,
      preferences: {
        ...auth.user!.preferences,
        collectionSort: collectionSort.value,
        collectionSortDirection: collectionSortDirection.value,
        collectionFilter: collectionFilter.value,
        venueSort: venueSort.value,
        venueSortDirection: venueSortDirection.value,
        venueFilter: venueFilter.value,
      },
    })
    await auth.loadUser()
    saveMessage.value = 'Profile updated!'
    feedbackTimers.push(setTimeout(() => { saveMessage.value = '' }, 3000))
  } catch {
    saveMessage.value = 'Failed to save'
  } finally {
    isSaving.value = false
  }
}

async function savePushover() {
  isSavingPushover.value = true
  pushoverMessage.value = ''
  try {
    await usersApi.updateMe({
      preferences: {
        ...auth.user!.preferences,
        pushoverEnabled: pushoverEnabled.value,
        pushoverAppToken: pushoverAppToken.value || undefined,
        pushoverUserKey: pushoverUserKey.value || undefined,
        pushoverSound: pushoverSound.value,
      },
    })
    await auth.loadUser()
    pushoverMessage.value = 'Pushover settings saved!'
    feedbackTimers.push(setTimeout(() => { pushoverMessage.value = '' }, 3000))
  } catch {
    pushoverMessage.value = 'Failed to save'
  } finally {
    isSavingPushover.value = false
  }
}

async function changePassword() {
  if (newPassword.value !== confirmPassword.value) {
    passwordMessage.value = 'Passwords do not match'
    passwordError.value = true
    return
  }
  if (newPassword.value.length < 8) {
    passwordMessage.value = 'Password must be at least 8 characters'
    passwordError.value = true
    return
  }

  isChangingPassword.value = true
  passwordMessage.value = ''
  passwordError.value = false

  try {
    await usersApi.changePassword({
      currentPassword: currentPassword.value,
      newPassword: newPassword.value,
    })
    passwordMessage.value = 'Password changed!'
    passwordError.value = false
    currentPassword.value = ''
    newPassword.value = ''
    confirmPassword.value = ''
    feedbackTimers.push(setTimeout(() => { passwordMessage.value = '' }, 3000))
  } catch (e: unknown) {
    passwordMessage.value = getErrorMessage(e, 'Failed to change password')
    passwordError.value = true
  } finally {
    isChangingPassword.value = false
  }
}
</script>

<template>
  <div class="p-4 max-w-lg mx-auto">
    <div class="flex items-center justify-between mb-6">
      <h2 class="text-xl font-semibold">Profile</h2>
      <router-link
        v-if="auth.isAdmin"
        to="/admin"
        class="text-[#96BEE6] hover:text-white text-sm transition-colors"
      >
        Admin
      </router-link>
    </div>

    <div v-if="auth.user" class="space-y-6">
      <!-- Profile Info -->
      <section class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 space-y-4">
        <h3 class="text-sm font-medium text-[#96BEE6] uppercase tracking-wide">Account</h3>

        <div>
          <label class="block text-sm text-[#96BEE6] mb-1">Email</label>
          <p class="text-white/80">{{ auth.user.email }}</p>
        </div>

        <div>
          <label class="block text-sm text-[#96BEE6] mb-1">Display Name</label>
          <input
            v-model="displayName"
            class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-3 text-white focus:outline-none focus:border-[#1e407c]"
          />
        </div>

        <div>
          <label class="block text-sm text-[#96BEE6] mb-1">Role</label>
          <span class="inline-block px-2 py-0.5 rounded-full text-xs border"
            :class="auth.user.role === 'admin' ? 'border-[#1e407c] text-[#96BEE6]' : 'border-[#1e407c]/50 text-[#96BEE6]'">
            {{ auth.user.role }}
          </span>
        </div>

        <div class="rounded-xl border border-[#0a2a52] overflow-hidden divide-y divide-[#0a2a52]">
          <div class="bg-[#0a2a52]/30 p-3 space-y-2">
            <div>
              <p class="text-sm text-white">Collection Sort</p>
              <p class="text-xs text-[#4a7aa5]">Default order for your collection list.</p>
            </div>
            <select
              v-model="collectionSort"
              class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-lg px-3 py-2.5 text-sm text-white focus:outline-none focus:border-[#1e407c]"
            >
              <option v-for="opt in sortOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
            </select>
          </div>

          <div class="bg-[#0a2a52]/30 p-3 space-y-2">
            <div>
              <p class="text-sm text-white">Collection Sort Direction</p>
              <p class="text-xs text-[#4a7aa5]">Ascending or descending for collection sorting.</p>
            </div>
            <select
              v-model="collectionSortDirection"
              class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-lg px-3 py-2.5 text-sm text-white focus:outline-none focus:border-[#1e407c]"
            >
              <option v-for="opt in sortDirectionOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
            </select>
          </div>

          <div class="bg-[#0a2a52]/30 p-3 space-y-2">
            <div>
              <p class="text-sm text-white">Collection Filter</p>
              <p class="text-xs text-[#4a7aa5]">Initial item type when opening collection.</p>
            </div>
            <select
              v-model="collectionFilter"
              class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-lg px-3 py-2.5 text-sm text-white focus:outline-none focus:border-[#1e407c]"
            >
              <option v-for="opt in filterOptions" :key="opt.label" :value="opt.value">{{ opt.label }}</option>
            </select>
          </div>

          <div class="bg-[#0a2a52]/30 p-3 space-y-2">
            <div>
              <p class="text-sm text-white">Venue Sort</p>
              <p class="text-xs text-[#4a7aa5]">Default order for your venues list.</p>
            </div>
            <select
              v-model="venueSort"
              class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-lg px-3 py-2.5 text-sm text-white focus:outline-none focus:border-[#1e407c]"
            >
              <option v-for="opt in venueSortOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
            </select>
          </div>

          <div class="bg-[#0a2a52]/30 p-3 space-y-2">
            <div>
              <p class="text-sm text-white">Venue Sort Direction</p>
              <p class="text-xs text-[#4a7aa5]">Ascending or descending for venue sorting.</p>
            </div>
            <select
              v-model="venueSortDirection"
              class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-lg px-3 py-2.5 text-sm text-white focus:outline-none focus:border-[#1e407c]"
            >
              <option v-for="opt in sortDirectionOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
            </select>
          </div>

          <div class="bg-[#0a2a52]/30 p-3 space-y-2">
            <div>
              <p class="text-sm text-white">Venue Filter</p>
              <p class="text-xs text-[#4a7aa5]">Initial venue type when opening venues.</p>
            </div>
            <select
              v-model="venueFilter"
              class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-lg px-3 py-2.5 text-sm text-white focus:outline-none focus:border-[#1e407c]"
            >
              <option v-for="opt in venueFilterOptions" :key="opt.label" :value="opt.value">{{ opt.label }}</option>
            </select>
          </div>
        </div>

        <div v-if="saveMessage" class="text-sm" :class="saveMessage.includes('Failed') ? 'text-red-400' : 'text-green-400'">
          {{ saveMessage }}
        </div>

        <button
          @click="saveProfile"
          :disabled="isSaving"
          class="w-full bg-[#1e407c] hover:bg-[#2a5299] disabled:bg-[#1e407c] text-white py-3 rounded-xl font-medium"
        >
          {{ isSaving ? 'Saving...' : 'Save Profile' }}
        </button>
      </section>

      <!-- Password Change -->
      <section class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 space-y-4">
        <h3 class="text-sm font-medium text-[#96BEE6] uppercase tracking-wide">Change Password</h3>

        <div>
          <label class="block text-sm text-[#96BEE6] mb-1">Current Password</label>
          <input v-model="currentPassword" type="password" autocomplete="current-password"
            class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-3 text-white focus:outline-none focus:border-[#1e407c]" />
        </div>

        <div>
          <label class="block text-sm text-[#96BEE6] mb-1">New Password</label>
          <input v-model="newPassword" type="password" autocomplete="new-password" placeholder="Min 8 characters"
            class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-3 text-white placeholder-[#4a7aa5] focus:outline-none focus:border-[#1e407c]" />
        </div>

        <div>
          <label class="block text-sm text-[#96BEE6] mb-1">Confirm New Password</label>
          <input v-model="confirmPassword" type="password" autocomplete="new-password"
            class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-3 text-white focus:outline-none focus:border-[#1e407c]" />
        </div>

        <div v-if="passwordMessage" class="text-sm" :class="passwordError ? 'text-red-400' : 'text-green-400'">
          {{ passwordMessage }}
        </div>

        <button
          @click="changePassword"
          :disabled="isChangingPassword || !currentPassword || !newPassword"
          class="w-full bg-[#1e407c] hover:bg-[#1e407c] disabled:bg-[#0a2a52] disabled:text-[#4a7aa5]/60 text-white py-3 rounded-xl font-medium"
        >
          {{ isChangingPassword ? 'Changing...' : 'Change Password' }}
        </button>
      </section>

      <!-- Connected Sign-in Providers -->
      <section class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 space-y-4">
        <div>
          <h3 class="text-sm font-medium text-[#96BEE6] uppercase tracking-wide">Connected Sign-in Providers</h3>
          <p class="text-sm text-[#96BEE6]/80 mt-1">
            Link Entra ID, Pocket ID, or another external provider after signing in locally. This avoids unsafe automatic account merges.
          </p>
        </div>

        <div v-if="oidcMessage" class="text-sm" :class="oidcError ? 'text-red-400' : 'text-green-400'">
          {{ oidcMessage }}
        </div>

        <div v-if="oidcLoading" class="text-sm text-[#96BEE6]/70">Loading linked providers...</div>
        <template v-else>
          <div v-if="oidcIdentities.length" class="space-y-3">
            <div
              v-for="identity in oidcIdentities"
              :key="identity.id"
              class="rounded-xl border border-[#1e407c]/50 bg-[#0a2a52]/60 p-3"
            >
              <div class="flex items-start justify-between gap-3">
                <div class="min-w-0 space-y-1">
                  <div class="flex flex-wrap items-center gap-2">
                    <p class="font-medium text-white">{{ identity.providerDisplayName }}</p>
                    <span
                      class="rounded-full border px-2 py-0.5 text-[10px]"
                      :class="identity.emailVerified ? 'border-green-700 text-green-400' : 'border-red-800 text-red-400'"
                    >
                      {{ identity.emailVerified ? 'Email verified' : 'Email unverified' }}
                    </span>
                  </div>
                  <p class="break-all text-xs text-[#96BEE6]/70">Issuer: {{ identity.issuer }}</p>
                  <p class="break-all text-xs text-[#96BEE6]/70">Subject: {{ identity.subjectPreview }}</p>
                  <p class="break-all text-xs text-[#96BEE6]/70">Email: {{ identity.email }}</p>
                  <p class="text-xs text-[#4a7aa5]">
                    Linked {{ formatDateTime(identity.createdAt) }}
                    <template v-if="identity.lastLoginAt"> · Last login {{ formatDateTime(identity.lastLoginAt) }}</template>
                  </p>
                </div>
                <button
                  type="button"
                  @click="unlinkOidcIdentity(identity)"
                  :disabled="unlinkingIdentityId === identity.id"
                  class="shrink-0 text-xs text-red-400 hover:text-red-300 disabled:text-red-400/50"
                >
                  {{ unlinkingIdentityId === identity.id ? 'Unlinking...' : 'Unlink' }}
                </button>
              </div>
            </div>
          </div>
          <p v-else class="text-sm text-[#96BEE6]/70">No external sign-in providers linked.</p>

          <div v-if="linkableOidcProviders().length" class="flex flex-wrap gap-2">
            <button
              v-for="provider in linkableOidcProviders()"
              :key="provider.id"
              type="button"
              @click="linkOidcProvider(provider)"
              :disabled="linkingProviderId === provider.id"
              class="rounded-xl border border-[#1e407c]/50 bg-[#0a2a52] px-3 py-2 text-sm font-medium text-[#96BEE6] hover:bg-[#1e407c] disabled:text-[#4a7aa5]/60"
            >
              {{ linkingProviderId === provider.id ? 'Starting...' : `Link ${provider.displayName}` }}
            </button>
          </div>
          <p v-else-if="!oidcProviders.length" class="text-sm text-[#96BEE6]/70">
            No enabled OIDC providers are available for linking.
          </p>
        </template>
      </section>

      <!-- API Keys -->
      <section class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 space-y-4">
        <h3 class="text-sm font-medium text-[#96BEE6] uppercase tracking-wide">API Keys</h3>
        <p class="text-sm text-[#96BEE6]">
          Create API keys to integrate with iOS Shortcuts or other tools.
        </p>

        <!-- Newly created key banner -->
        <div v-if="newlyCreatedKey" class="bg-[#1e407c]/30 border border-[#1e407c] rounded-lg p-3 space-y-2">
          <p class="text-sm text-[#96BEE6] font-medium">Key created - copy it now, it will not be shown again</p>
          <div class="flex items-center gap-2">
            <code class="flex-1 bg-[#0a2a52] px-3 py-2 rounded text-xs text-white break-all font-mono">
              {{ newlyCreatedKey.key }}
            </code>
            <button
              @click="copyKey"
              class="shrink-0 px-3 py-2 rounded bg-[#1e407c] hover:bg-[#2a5299] text-white text-xs font-medium"
            >
              {{ keyCopied ? 'Copied' : 'Copy' }}
            </button>
          </div>
          <button @click="newlyCreatedKey = null" class="text-xs text-[#96BEE6]/70 hover:text-[#96BEE6]">
            Dismiss
          </button>
        </div>

        <!-- Create new key -->
        <div class="flex gap-2">
          <input
            v-model="newKeyName"
            placeholder="Key name (e.g. iPhone Shortcut)"
            class="flex-1 bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-2.5 text-sm text-white placeholder-[#4a7aa5] focus:outline-none focus:border-[#1e407c]"
            @keyup.enter="createApiKey"
          />
          <button
            @click="createApiKey"
            :disabled="isCreatingKey || !newKeyName.trim()"
            class="shrink-0 px-4 py-2.5 bg-[#1e407c] hover:bg-[#2a5299] disabled:bg-[#1e407c] disabled:text-[#96BEE6]/70 text-white rounded-xl text-sm font-medium"
          >
            {{ isCreatingKey ? '...' : 'Create' }}
          </button>
        </div>

        <div v-if="keyMessage" class="text-sm text-red-400">{{ keyMessage }}</div>

        <!-- Existing keys -->
        <div v-if="apiKeys.length" class="space-y-2">
          <div
            v-for="key in apiKeys"
            :key="key.id"
            class="flex items-center justify-between bg-[#0a2a52] border border-[#1e407c]/50 rounded-lg px-3 py-2.5"
            :class="key.isRevoked ? 'opacity-50' : ''"
          >
            <div class="min-w-0 flex-1">
              <div class="flex items-center gap-2">
                <span class="text-sm text-white truncate">{{ key.name }}</span>
                <span v-if="key.isRevoked" class="text-[10px] px-1.5 py-0.5 rounded bg-red-900/50 text-red-400 border border-red-800">
                  Revoked
                </span>
              </div>
              <div class="text-xs text-[#96BEE6]/70 mt-0.5">
                <span class="font-mono">{{ key.prefix }}</span>
                <span v-if="key.lastUsedAt" class="ml-2">
                  Last used {{ new Date(key.lastUsedAt).toLocaleDateString() }}
                </span>
                <span v-else class="ml-2">Never used</span>
              </div>
            </div>
            <button
              v-if="!key.isRevoked"
              @click="revokeKey(key.id)"
              class="shrink-0 ml-3 text-xs text-red-400 hover:text-red-300"
            >
              Revoke
            </button>
          </div>
        </div>
      </section>

      <!-- Push Notifications (Pushover) -->
      <section class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 space-y-4">
        <h3 class="text-sm font-medium text-[#96BEE6] uppercase tracking-wide">Push Notifications</h3>
        <p class="text-sm text-[#96BEE6]">
          Get real-time push notifications via <a href="https://pushover.net" target="_blank" rel="noopener" class="underline hover:text-white">Pushover</a> when workflows complete.
        </p>

        <div class="flex items-center justify-between">
          <label class="text-sm text-[#96BEE6]">Enable Pushover</label>
          <button
            @click="pushoverEnabled = !pushoverEnabled"
            class="relative w-11 h-6 rounded-full transition-colors"
            :class="pushoverEnabled ? 'bg-[#1e407c]' : 'bg-[#0a2a52]'"
          >
            <span
              class="absolute top-0.5 left-0.5 w-5 h-5 bg-white rounded-full transition-transform"
              :class="pushoverEnabled ? 'translate-x-5' : ''"
            />
          </button>
        </div>

        <template v-if="pushoverEnabled">
          <div>
            <label class="block text-sm text-[#96BEE6] mb-1">Application Token</label>
            <input
              v-model="pushoverAppToken"
              type="password"
              placeholder="Your Pushover application API token"
              class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-3 text-white placeholder-[#4a7aa5] focus:outline-none focus:border-[#1e407c] font-mono text-sm"
            />
            <p class="text-xs text-[#4a7aa5] mt-1">Create an application at <a href="https://pushover.net/apps/build" target="_blank" rel="noopener" class="underline hover:text-white">pushover.net/apps/build</a></p>
          </div>

          <div>
            <label class="block text-sm text-[#96BEE6] mb-1">User Key</label>
            <input
              v-model="pushoverUserKey"
              type="text"
              placeholder="Your Pushover user key"
              class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-3 text-white placeholder-[#4a7aa5] focus:outline-none focus:border-[#1e407c] font-mono text-sm"
            />
            <p class="text-xs text-[#4a7aa5] mt-1">Find this in your Pushover app or at pushover.net/dashboard</p>
          </div>

          <div class="flex items-center justify-between">
            <label class="text-sm text-[#96BEE6]">Play sound</label>
            <button
              @click="pushoverSound = !pushoverSound"
              class="relative w-11 h-6 rounded-full transition-colors"
              :class="pushoverSound ? 'bg-[#1e407c]' : 'bg-[#0a2a52]'"
            >
              <span
                class="absolute top-0.5 left-0.5 w-5 h-5 bg-white rounded-full transition-transform"
                :class="pushoverSound ? 'translate-x-5' : ''"
              />
            </button>
          </div>
        </template>

        <div v-if="pushoverMessage" class="text-sm" :class="pushoverMessage.includes('Failed') ? 'text-red-400' : 'text-green-400'">
          {{ pushoverMessage }}
        </div>

        <button
          @click="savePushover"
          :disabled="isSavingPushover || (pushoverEnabled && (!pushoverAppToken.trim() || !pushoverUserKey.trim()))"
          class="w-full bg-[#1e407c] hover:bg-[#2a5299] disabled:bg-[#0a2a52] disabled:text-[#4a7aa5]/60 text-white py-3 rounded-xl font-medium"
        >
          {{ isSavingPushover ? 'Saving...' : 'Save Pushover Settings' }}
        </button>
      </section>

      <!-- Export Data -->
      <section class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 space-y-4">
        <h3 class="text-sm font-medium text-[#96BEE6] uppercase tracking-wide">Data Export</h3>
        <p class="text-sm text-[#96BEE6]">
          Download all your data including items, captures, and images as a ZIP file.
        </p>

        <div v-if="exportMessage" class="text-sm" :class="exportMessage.includes('failed') ? 'text-red-400' : 'text-green-400'">
          {{ exportMessage }}
        </div>

        <button
          @click="exportData"
          :disabled="isExporting"
          class="w-full bg-[#1e407c] hover:bg-[#1e407c] disabled:bg-[#0a2a52] disabled:text-[#4a7aa5]/60 text-white py-3 rounded-xl font-medium"
        >
          {{ isExporting ? 'Exporting...' : 'Export All Data' }}
        </button>
      </section>

      <!-- Logout -->
      <button
        @click="auth.logout()"
        class="w-full bg-[#041e3e] border border-red-900/50 hover:border-red-700 text-red-400 py-3 rounded-xl font-medium transition-colors"
      >
        Sign Out
      </button>
    </div>
  </div>
</template>
