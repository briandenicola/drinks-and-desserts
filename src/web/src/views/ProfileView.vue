<script setup lang="ts">
import { ref, inject, onMounted } from 'vue'
import { useAuthStore } from '../stores/auth'
import { usersApi } from '../services/users'
import type { ApiKeyResponse, CreateApiKeyResponse } from '../services/users'
import { RefreshKey } from '../composables/refreshKey'

const auth = useAuthStore()
const displayName = ref('')
const collectionSort = ref('rating')
const collectionFilter = ref<string | undefined>(undefined)
const isSaving = ref(false)
const saveMessage = ref('')

const sortOptions = [
  { label: 'Rating', value: 'rating' },
  { label: 'Date Added', value: 'createdAt' },
  { label: 'Date Updated', value: 'updatedAt' },
]

const filterOptions = [
  { label: 'All', value: undefined as string | undefined },
  { label: 'Whiskey', value: 'whiskey' as string | undefined },
  { label: 'Wine', value: 'wine' as string | undefined },
  { label: 'Cocktail', value: 'cocktail' as string | undefined },
  { label: 'Vodka', value: 'vodka' as string | undefined },
  { label: 'Gin', value: 'gin' as string | undefined },
  { label: 'Cigar', value: 'cigar' as string | undefined },
  { label: 'Venue', value: 'venue' as string | undefined },
  { label: 'Custom', value: 'custom' as string | undefined },
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

const registerRefresh = inject(RefreshKey)
registerRefresh?.(async () => {
  await auth.loadUser()
  if (auth.user) displayName.value = auth.user.displayName
  await loadApiKeys()
})

const isExporting = ref(false)
const exportMessage = ref('')

async function loadApiKeys() {
  try {
    const res = await usersApi.listApiKeys()
    apiKeys.value = res.data
  } catch { /* ignore */ }
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
  } catch (e: any) {
    keyMessage.value = e.response?.data?.message ?? 'Failed to create key'
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
    link.download = `whiskey-and-smokes-export-${new Date().toISOString().slice(0, 10)}.zip`
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    URL.revokeObjectURL(url)
    exportMessage.value = 'Export downloaded'
    setTimeout(() => { exportMessage.value = '' }, 3000)
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
    collectionFilter.value = auth.user.preferences?.collectionFilter || undefined
  }
  loadApiKeys()
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
        collectionFilter: collectionFilter.value,
      },
    })
    await auth.loadUser()
    saveMessage.value = 'Profile updated!'
    setTimeout(() => { saveMessage.value = '' }, 3000)
  } catch {
    saveMessage.value = 'Failed to save'
  } finally {
    isSaving.value = false
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
    setTimeout(() => { passwordMessage.value = '' }, 3000)
  } catch (e: any) {
    passwordMessage.value = e.response?.data?.message ?? 'Failed to change password'
    passwordError.value = true
  } finally {
    isChangingPassword.value = false
  }
}
</script>

<template>
  <div class="p-4 max-w-lg mx-auto">
    <h2 class="text-xl font-semibold mb-6">Profile</h2>

    <div v-if="auth.user" class="space-y-6">
      <!-- Profile Info -->
      <section class="bg-stone-900 border border-stone-800 rounded-xl p-4 space-y-4">
        <h3 class="text-sm font-medium text-stone-400 uppercase tracking-wide">Account</h3>

        <div>
          <label class="block text-sm text-stone-400 mb-1">Email</label>
          <p class="text-stone-300">{{ auth.user.email }}</p>
        </div>

        <div>
          <label class="block text-sm text-stone-400 mb-1">Display Name</label>
          <input
            v-model="displayName"
            class="w-full bg-stone-800 border border-stone-700 rounded-xl px-4 py-3 text-stone-100 focus:outline-none focus:border-amber-700"
          />
        </div>

        <div>
          <label class="block text-sm text-stone-400 mb-1">Role</label>
          <span class="inline-block px-2 py-0.5 rounded-full text-xs border"
            :class="auth.user.role === 'admin' ? 'border-amber-600 text-amber-500' : 'border-stone-700 text-stone-400'">
            {{ auth.user.role }}
          </span>
        </div>

        <div>
          <label class="block text-sm text-stone-400 mb-2">Default Collection Sort</label>
          <div class="flex gap-2">
            <button
              v-for="opt in sortOptions"
              :key="opt.value"
              @click="collectionSort = opt.value"
              class="px-4 py-2.5 min-h-[44px] rounded-full text-sm border transition-colors"
              :class="collectionSort === opt.value
                ? 'bg-amber-700 border-amber-600 text-white'
                : 'bg-stone-800 border-stone-700 text-stone-400 hover:border-stone-600'"
            >
              {{ opt.label }}
            </button>
          </div>
        </div>

        <div>
          <label class="block text-sm text-stone-400 mb-2">Default Collection Filter</label>
          <div class="flex flex-wrap gap-2">
            <button
              v-for="opt in filterOptions"
              :key="opt.label"
              @click="collectionFilter = opt.value"
              class="px-4 py-2.5 min-h-[44px] rounded-full text-sm border transition-colors"
              :class="collectionFilter === opt.value
                ? 'bg-amber-700 border-amber-600 text-white'
                : 'bg-stone-800 border-stone-700 text-stone-400 hover:border-stone-600'"
            >
              {{ opt.label }}
            </button>
          </div>
        </div>

        <div v-if="saveMessage" class="text-sm" :class="saveMessage.includes('Failed') ? 'text-red-400' : 'text-green-400'">
          {{ saveMessage }}
        </div>

        <button
          @click="saveProfile"
          :disabled="isSaving"
          class="w-full bg-amber-700 hover:bg-amber-600 disabled:bg-stone-700 text-white py-3 rounded-xl font-medium"
        >
          {{ isSaving ? 'Saving...' : 'Save Profile' }}
        </button>
      </section>

      <!-- Password Change -->
      <section class="bg-stone-900 border border-stone-800 rounded-xl p-4 space-y-4">
        <h3 class="text-sm font-medium text-stone-400 uppercase tracking-wide">Change Password</h3>

        <div>
          <label class="block text-sm text-stone-400 mb-1">Current Password</label>
          <input v-model="currentPassword" type="password" autocomplete="current-password"
            class="w-full bg-stone-800 border border-stone-700 rounded-xl px-4 py-3 text-stone-100 focus:outline-none focus:border-amber-700" />
        </div>

        <div>
          <label class="block text-sm text-stone-400 mb-1">New Password</label>
          <input v-model="newPassword" type="password" autocomplete="new-password" placeholder="Min 8 characters"
            class="w-full bg-stone-800 border border-stone-700 rounded-xl px-4 py-3 text-stone-100 placeholder-stone-600 focus:outline-none focus:border-amber-700" />
        </div>

        <div>
          <label class="block text-sm text-stone-400 mb-1">Confirm New Password</label>
          <input v-model="confirmPassword" type="password" autocomplete="new-password"
            class="w-full bg-stone-800 border border-stone-700 rounded-xl px-4 py-3 text-stone-100 focus:outline-none focus:border-amber-700" />
        </div>

        <div v-if="passwordMessage" class="text-sm" :class="passwordError ? 'text-red-400' : 'text-green-400'">
          {{ passwordMessage }}
        </div>

        <button
          @click="changePassword"
          :disabled="isChangingPassword || !currentPassword || !newPassword"
          class="w-full bg-stone-700 hover:bg-stone-600 disabled:bg-stone-800 disabled:text-stone-600 text-white py-3 rounded-xl font-medium"
        >
          {{ isChangingPassword ? 'Changing...' : 'Change Password' }}
        </button>
      </section>

      <!-- API Keys -->
      <section class="bg-stone-900 border border-stone-800 rounded-xl p-4 space-y-4">
        <h3 class="text-sm font-medium text-stone-400 uppercase tracking-wide">API Keys</h3>
        <p class="text-sm text-stone-400">
          Create API keys to integrate with iOS Shortcuts or other tools.
        </p>

        <!-- Newly created key banner -->
        <div v-if="newlyCreatedKey" class="bg-amber-900/30 border border-amber-700 rounded-lg p-3 space-y-2">
          <p class="text-sm text-amber-400 font-medium">Key created - copy it now, it will not be shown again</p>
          <div class="flex items-center gap-2">
            <code class="flex-1 bg-stone-800 px-3 py-2 rounded text-xs text-stone-200 break-all font-mono">
              {{ newlyCreatedKey.key }}
            </code>
            <button
              @click="copyKey"
              class="shrink-0 px-3 py-2 rounded bg-amber-700 hover:bg-amber-600 text-white text-xs font-medium"
            >
              {{ keyCopied ? 'Copied' : 'Copy' }}
            </button>
          </div>
          <button @click="newlyCreatedKey = null" class="text-xs text-stone-500 hover:text-stone-400">
            Dismiss
          </button>
        </div>

        <!-- Create new key -->
        <div class="flex gap-2">
          <input
            v-model="newKeyName"
            placeholder="Key name (e.g. iPhone Shortcut)"
            class="flex-1 bg-stone-800 border border-stone-700 rounded-xl px-4 py-2.5 text-sm text-stone-100 placeholder-stone-600 focus:outline-none focus:border-amber-700"
            @keyup.enter="createApiKey"
          />
          <button
            @click="createApiKey"
            :disabled="isCreatingKey || !newKeyName.trim()"
            class="shrink-0 px-4 py-2.5 bg-amber-700 hover:bg-amber-600 disabled:bg-stone-700 disabled:text-stone-500 text-white rounded-xl text-sm font-medium"
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
            class="flex items-center justify-between bg-stone-800 border border-stone-700 rounded-lg px-3 py-2.5"
            :class="key.isRevoked ? 'opacity-50' : ''"
          >
            <div class="min-w-0 flex-1">
              <div class="flex items-center gap-2">
                <span class="text-sm text-stone-200 truncate">{{ key.name }}</span>
                <span v-if="key.isRevoked" class="text-[10px] px-1.5 py-0.5 rounded bg-red-900/50 text-red-400 border border-red-800">
                  Revoked
                </span>
              </div>
              <div class="text-xs text-stone-500 mt-0.5">
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

      <!-- Export Data -->
      <section class="bg-stone-900 border border-stone-800 rounded-xl p-4 space-y-4">
        <h3 class="text-sm font-medium text-stone-400 uppercase tracking-wide">Data Export</h3>
        <p class="text-sm text-stone-400">
          Download all your data including items, captures, and images as a ZIP file.
        </p>

        <div v-if="exportMessage" class="text-sm" :class="exportMessage.includes('failed') ? 'text-red-400' : 'text-green-400'">
          {{ exportMessage }}
        </div>

        <button
          @click="exportData"
          :disabled="isExporting"
          class="w-full bg-stone-700 hover:bg-stone-600 disabled:bg-stone-800 disabled:text-stone-600 text-white py-3 rounded-xl font-medium"
        >
          {{ isExporting ? 'Exporting...' : 'Export All Data' }}
        </button>
      </section>

      <!-- Admin link -->
      <router-link
        v-if="auth.isAdmin"
        to="/admin"
        class="block bg-stone-900 border border-stone-800 rounded-xl p-4 text-center text-amber-500 hover:text-amber-400 hover:border-stone-700 transition-colors"
      >
        Admin Panel
      </router-link>

      <!-- Logout -->
      <button
        @click="auth.logout()"
        class="w-full bg-stone-900 border border-red-900/50 hover:border-red-700 text-red-400 py-3 rounded-xl font-medium transition-colors"
      >
        Sign Out
      </button>
    </div>
  </div>
</template>
