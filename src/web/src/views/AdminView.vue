<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { authApi, type OIDCAdminProvider, type OIDCAdminProviderInput, type OIDCProviderType } from '../services/auth'
import { getErrorMessage } from '../services/errors'
import { useAuthStore } from '../stores/auth'
import { usersApi, type AdminAuthSettingsResponse, type AdminAuthSettingsUpdate, type FoundryStatus, type LoggingSettings, type LoggingSettingsResponse, type Prompt, type User, type UserRole } from '../services/users'

const auth = useAuthStore()
const router = useRouter()
const feedbackTimers: ReturnType<typeof setTimeout>[] = []
const users = ref<User[]>([])
const prompts = ref<Prompt[]>([])
const loggingData = ref<LoggingSettingsResponse | null>(null)
const foundryStatus = ref<FoundryStatus | null>(null)
const isLoading = ref(true)
const searchQuery = ref('')
const activeTab = ref<'users' | 'prompts' | 'oidc' | 'settings' | 'foundry' | 'logging'>('users')
const showMenu = ref(false)

const roleUpdatingUserId = ref<string | null>(null)
const roleMessage = ref('')
const roleError = ref(false)
const resetPasswordUserId = ref<string | null>(null)
const newPassword = ref('')
const resetMessage = ref('')
const deleteConfirmUserId = ref<string | null>(null)
const expandedPromptId = ref<string | null>(null)

const oidcProviders = ref<OIDCAdminProvider[]>([])
const oidcLoading = ref(false)
const oidcSaving = ref(false)
const oidcTestingId = ref<string | null>(null)
const oidcMessage = ref('')
const oidcError = ref(false)
const editingOidcId = ref<string | null>(null)
const oidcForm = ref<OIDCAdminProviderInput>(defaultOidcForm())

const adminSettings = ref<AdminAuthSettingsResponse | null>(null)
const settingsForm = ref<AdminAuthSettingsUpdate>({ oidcPublicOrigin: '' })
const settingsLoading = ref(false)
const settingsSaving = ref(false)
const settingsMessage = ref('')
const settingsError = ref(false)

const editedLevels = ref<Record<string, string>>({})
const editedDefaultLevel = ref('Information')
const loggingMessage = ref('')
const loggingSaving = ref(false)
const foundryTesting = ref(false)

const filteredUsers = computed(() => {
  const q = searchQuery.value.toLowerCase()
  if (!q) return users.value
  return users.value.filter(u => [u.displayName, u.email, u.role, u.authProvider].some(v => v.toLowerCase().includes(q)))
})

onMounted(async () => {
  try {
    const [usersRes, promptsRes, loggingRes, foundryRes] = await Promise.all([
      usersApi.listUsers(), usersApi.listPrompts(), usersApi.getLoggingSettings(), usersApi.getFoundryStatus()
    ])
    users.value = usersRes.data
    prompts.value = promptsRes.data
    loggingData.value = loggingRes.data
    foundryStatus.value = foundryRes.data
    resetLoggingForm()
    await Promise.all([loadOidcProviders(), loadAdminSettings()])
  } finally {
    isLoading.value = false
  }
})

onBeforeUnmount(() => feedbackTimers.forEach(clearTimeout))
function selectTab(tab: typeof activeTab.value) { activeTab.value = tab; showMenu.value = false }
function goToActivity() { showMenu.value = false; router.push('/history') }
function flash(messageRef: typeof roleMessage, value: string) { messageRef.value = value; feedbackTimers.push(setTimeout(() => { messageRef.value = '' }, 3000)) }

async function toggleRole(user: User) {
  const newRole: UserRole = user.role === 'admin' ? 'user' : 'admin'
  if (user.id === auth.user?.id && newRole === 'user' && !window.confirm('Demote your own account? The backend should block last-admin/self-lockout attempts.')) return
  roleUpdatingUserId.value = user.id
  roleMessage.value = ''
  roleError.value = false
  try {
    const { data } = await usersApi.updateRole(user.id, newRole)
    const index = users.value.findIndex(u => u.id === user.id)
    if (index !== -1) users.value[index] = data
    flash(roleMessage, `${data.displayName} is now ${data.role}.`)
  } catch (e: unknown) {
    roleMessage.value = getErrorMessage(e, 'Failed to update user role. The backend may have blocked a last-admin or self-lockout change.')
    roleError.value = true
  } finally { roleUpdatingUserId.value = null }
}

function authProviderLabel(provider: string) {
  if (!provider || provider === 'local') return 'Local'
  if (provider === 'linked') return 'Local + external'
  if (provider === 'entra') return 'Entra ID'
  if (provider === 'oidc') return 'OIDC'
  return provider
}
function canResetPassword(user: User) { return !user.authProvider || ['local', 'password'].includes(user.authProvider.toLowerCase()) }
function startResetPassword(userId: string) { resetPasswordUserId.value = userId; newPassword.value = ''; resetMessage.value = '' }
async function confirmResetPassword() {
  if (!resetPasswordUserId.value || newPassword.value.length < 8) { resetMessage.value = 'Password must be at least 8 characters'; return }
  try { await usersApi.resetPassword(resetPasswordUserId.value, newPassword.value); resetMessage.value = 'Password reset successfully'; feedbackTimers.push(setTimeout(() => { resetPasswordUserId.value = null }, 1500)) }
  catch (e: unknown) { resetMessage.value = getErrorMessage(e, 'Failed to reset password') }
}
function startDeleteUser(userId: string) { deleteConfirmUserId.value = userId }
async function confirmDeleteUser() {
  if (!deleteConfirmUserId.value) return
  try { await usersApi.deleteUser(deleteConfirmUserId.value); users.value = users.value.filter(u => u.id !== deleteConfirmUserId.value); deleteConfirmUserId.value = null }
  catch (e: unknown) { roleMessage.value = getErrorMessage(e, 'Failed to delete user'); roleError.value = true }
}
function togglePromptExpand(promptId: string) { expandedPromptId.value = expandedPromptId.value === promptId ? null : promptId }

function defaultOidcForm(): OIDCAdminProviderInput { return { name: '', displayName: '', providerType: 'entra', enabled: true, issuerUrl: '', clientId: '', clientSecret: '', scopes: ['openid', 'profile', 'email'], callbackPath: '', requireVerifiedEmail: true } }
function scopesText(provider: OIDCAdminProviderInput | OIDCAdminProvider) { return provider.scopes.join(' ') }
function handleScopesInput(event: Event) { oidcForm.value.scopes = (event.target as HTMLInputElement).value.split(/[,\s]+/).map(s => s.trim()).filter(Boolean) }
async function loadOidcProviders() {
  oidcLoading.value = true
  try { oidcProviders.value = (await authApi.getAdminOidcProviders()).data.providers ?? []; oidcError.value = false }
  catch (e: unknown) { oidcProviders.value = []; oidcMessage.value = getErrorMessage(e, 'Failed to load OIDC providers.'); oidcError.value = true }
  finally { oidcLoading.value = false }
}
function editOidcProvider(provider: OIDCAdminProvider) { editingOidcId.value = provider.id; oidcForm.value = { name: provider.name, displayName: provider.displayName, providerType: provider.providerType, enabled: provider.enabled, issuerUrl: provider.issuerUrl, clientId: provider.clientId, clientSecret: '', scopes: [...provider.scopes], callbackPath: provider.callbackPath, requireVerifiedEmail: provider.requireVerifiedEmail ?? true }; oidcMessage.value = ''; oidcError.value = false }
function resetOidcForm() { editingOidcId.value = null; oidcForm.value = defaultOidcForm() }
async function saveOidcProvider() {
  oidcSaving.value = true; oidcMessage.value = ''; oidcError.value = false
  try {
    const payload: OIDCAdminProviderInput = { ...oidcForm.value }
    if (!payload.clientSecret?.trim()) delete payload.clientSecret
    if (editingOidcId.value) { await authApi.updateAdminOidcProvider(editingOidcId.value, payload); oidcMessage.value = 'OIDC provider updated.' }
    else { await authApi.createAdminOidcProvider(payload); oidcMessage.value = 'OIDC provider created.' }
    resetOidcForm(); await loadOidcProviders()
  } catch (e: unknown) { oidcMessage.value = getErrorMessage(e, 'Failed to save OIDC provider.'); oidcError.value = true }
  finally { oidcSaving.value = false }
}
async function deleteOidcProvider(provider: OIDCAdminProvider) {
  if (!window.confirm(`Delete ${provider.displayName}? Existing linked accounts may stop working.`)) return
  try { await authApi.deleteAdminOidcProvider(provider.id); oidcMessage.value = `${provider.displayName} deleted.`; oidcError.value = false; await loadOidcProviders() }
  catch (e: unknown) { oidcMessage.value = getErrorMessage(e, 'Failed to delete OIDC provider.'); oidcError.value = true }
}
async function testOidcProvider(provider: OIDCAdminProvider) {
  oidcTestingId.value = provider.id; oidcMessage.value = ''; oidcError.value = false
  try { const { data } = await authApi.testAdminOidcProvider(provider.id); oidcMessage.value = data.message || (data.available ? 'Provider discovery succeeded.' : 'Provider discovery failed.'); oidcError.value = !data.available; await loadOidcProviders() }
  catch (e: unknown) { oidcMessage.value = getErrorMessage(e, 'Failed to test OIDC provider.'); oidcError.value = true }
  finally { oidcTestingId.value = null }
}
function oidcProviderTypeLabel(type: OIDCProviderType) { return type === 'entra' ? 'Entra ID' : type === 'pocket_id' ? 'Pocket ID' : 'Generic OIDC' }

function resetSettingsForm() { settingsForm.value = { oidcPublicOrigin: adminSettings.value?.settings.oidcPublicOrigin ?? adminSettings.value?.fallbackOidcPublicOrigin ?? '' } }
async function loadAdminSettings() {
  settingsLoading.value = true
  try { adminSettings.value = (await usersApi.getAdminSettings()).data; resetSettingsForm(); settingsError.value = false; settingsMessage.value = '' }
  catch (e: unknown) { adminSettings.value = null; settingsMessage.value = getErrorMessage(e, 'Admin settings are not available yet. Backend route expected: GET /api/admin/auth-settings.'); settingsError.value = true }
  finally { settingsLoading.value = false }
}
function validatePublicOrigin(origin: string) {
  const trimmed = origin.trim()
  if (!trimmed) return 'OIDC public web origin is required.'
  try { const url = new URL(trimmed); const local = url.protocol === 'http:' && ['localhost', '127.0.0.1', '[::1]'].includes(url.hostname); if (url.protocol !== 'https:' && !local) return 'Use https:// for public origins, except localhost during development.'; if (url.pathname !== '/' || url.search || url.hash) return 'Enter only the origin, for example https://app.example.com.' }
  catch { return 'Enter a valid absolute origin, for example https://app.example.com.' }
  return null
}
async function saveAdminSettings() {
  const validationError = validatePublicOrigin(settingsForm.value.oidcPublicOrigin)
  if (validationError) { settingsMessage.value = validationError; settingsError.value = true; return }
  settingsSaving.value = true; settingsMessage.value = ''; settingsError.value = false
  try { const payload = { oidcPublicOrigin: settingsForm.value.oidcPublicOrigin.trim().replace(/\/$/, '') }; adminSettings.value = (await usersApi.updateAdminSettings(payload)).data; resetSettingsForm(); flash(settingsMessage, 'Admin settings saved.') }
  catch (e: unknown) { settingsMessage.value = getErrorMessage(e, 'Failed to save admin settings. Backend route expected: PUT /api/admin/auth-settings.'); settingsError.value = true }
  finally { settingsSaving.value = false }
}

function resetLoggingForm() {
  if (!loggingData.value) return
  editedDefaultLevel.value = loggingData.value.settings.defaultLevel
  editedLevels.value = { ...loggingData.value.availableCategories, ...loggingData.value.settings.categoryLevels }
}
function getLevelColor(level: string) { return level === 'Error' || level === 'Critical' ? 'text-red-400' : level === 'Warning' ? 'text-yellow-400' : level === 'Information' ? 'text-green-400' : 'text-[#96BEE6]' }
async function saveLoggingSettings() {
  loggingSaving.value = true
  try { const settings: LoggingSettings = { defaultLevel: editedDefaultLevel.value, categoryLevels: { ...editedLevels.value } }; if (loggingData.value) loggingData.value.settings = (await usersApi.updateLoggingSettings(settings)).data; flash(loggingMessage, 'Log levels updated — changes take effect immediately') }
  catch (e: unknown) { loggingMessage.value = getErrorMessage(e, 'Failed to save logging settings') }
  finally { loggingSaving.value = false }
}
async function testFoundryConnectivity() { foundryTesting.value = true; try { foundryStatus.value = (await usersApi.testFoundryConnectivity()).data } finally { foundryTesting.value = false } }
</script>
<template>
  <div class="p-4 max-w-lg mx-auto">
    <div class="flex items-center justify-between mb-6">
      <router-link to="/profile" class="text-sm text-[#96BEE6] hover:text-white">Back</router-link>
      <h2 class="text-xl font-semibold">Admin Panel</h2>
      <div class="relative">
        <button @click="showMenu = !showMenu" class="text-[#96BEE6] hover:text-white p-1">☰</button>
        <Transition name="dropdown">
          <div v-if="showMenu" class="absolute right-0 top-8 z-50 w-44 bg-[#041e3e] border border-[#1e407c] rounded-xl shadow-xl overflow-hidden">
            <button v-for="item in ([{ key: 'users', label: 'Users' }, { key: 'prompts', label: 'AI Prompts' }, { key: 'oidc', label: 'OIDC' }, { key: 'settings', label: 'Settings' }, { key: 'foundry', label: 'Foundry' }, { key: 'logging', label: 'Logging' }] as const)" :key="item.key" @click="selectTab(item.key)" class="w-full text-left px-4 py-3 text-sm transition-colors" :class="activeTab === item.key ? 'bg-[#1e407c]/30 text-white' : 'text-[#96BEE6] hover:bg-[#0a2a52]'">{{ item.label }}</button>
            <button @click="goToActivity" class="w-full text-left px-4 py-3 text-sm text-[#96BEE6] hover:bg-[#0a2a52] transition-colors border-t border-[#0a2a52]">Activity</button>
          </div>
        </Transition>
      </div>
    </div>
    <div v-if="showMenu" class="fixed inset-0 z-40" @click="showMenu = false" />
    <div v-if="isLoading" class="text-[#96BEE6]/70 text-center py-12">Loading...</div>

    <template v-else-if="activeTab === 'users'">
      <div class="bg-[#041e3e]/50 border border-[#0a2a52] rounded-xl p-3 mb-4"><p class="text-xs text-[#96BEE6]">Role changes apply to every account type, including local password, linked, Entra ID, and OIDC accounts. Password reset is only shown for local password accounts.</p></div>
      <input v-model="searchQuery" placeholder="Search users..." class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-3 text-white placeholder-[#4a7aa5] focus:outline-none focus:border-[#1e407c] mb-4" />
      <div v-if="roleMessage" class="text-sm mb-4" :class="roleError ? 'text-red-400' : 'text-green-400'">{{ roleMessage }}</div>
      <div class="space-y-3">
        <div v-for="user in filteredUsers" :key="user.id" class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4">
          <div class="flex items-start justify-between gap-3">
            <div class="min-w-0 flex-1">
              <p class="font-medium text-white truncate">{{ user.displayName }}</p>
              <p class="text-sm text-[#96BEE6]/70 truncate">{{ user.email }}</p>
              <div class="flex flex-wrap items-center gap-2 mt-2">
                <span class="rounded-full border px-2 py-0.5 text-[10px] uppercase tracking-wide" :class="user.role === 'admin' ? 'border-green-700 text-green-400' : 'border-[#1e407c]/50 text-[#96BEE6]'">{{ user.role }}</span>
                <span class="rounded-full border border-[#1e407c]/50 px-2 py-0.5 text-[10px] text-[#96BEE6]/80">{{ authProviderLabel(user.authProvider) }}</span>
                <span v-if="user.id === auth.user?.id" class="rounded-full border border-[#1e407c]/50 px-2 py-0.5 text-[10px] text-[#96BEE6]/80">You</span>
              </div>
              <p class="text-xs text-[#4a7aa5]/60 mt-2">Joined {{ new Date(user.createdAt).toLocaleDateString() }}</p>
            </div>
            <button @click="toggleRole(user)" :disabled="roleUpdatingUserId === user.id" class="text-xs px-3 py-2 min-h-[44px] rounded-lg border transition-colors disabled:opacity-50 shrink-0" :class="user.role === 'admin' ? 'border-red-800/50 text-red-400 hover:bg-red-900/30' : 'border-[#1e407c] text-white bg-[#1e407c] hover:bg-[#2a5299]'">{{ roleUpdatingUserId === user.id ? 'Saving...' : (user.role === 'admin' ? 'Demote' : 'Promote') }}</button>
          </div>
          <div class="flex gap-2 mt-3 pt-3 border-t border-[#0a2a52]"><button v-if="canResetPassword(user)" @click="startResetPassword(user.id)" class="text-xs px-3 py-2.5 min-h-[44px] rounded-lg bg-[#0a2a52] text-white/80 hover:bg-[#1e407c] transition-colors">Reset Password</button><button v-if="user.id !== auth.user?.id" @click="startDeleteUser(user.id)" class="text-xs px-3 py-2.5 min-h-[44px] rounded-lg bg-red-900/30 text-red-400 hover:bg-red-900/50 border border-red-800/50 transition-colors">Delete</button></div>
        </div>
        <p v-if="filteredUsers.length === 0" class="text-[#96BEE6]/70 text-center py-8">No users match your search</p>
      </div>
    </template>

    <template v-else-if="activeTab === 'prompts'">
      <div class="bg-[#041e3e]/50 border border-[#0a2a52] rounded-xl p-3 mb-4"><p class="text-xs text-[#96BEE6]">Agent prompts are managed as files in <code class="text-[#96BEE6]/80 bg-[#0a2a52] px-1 rounded">AgentInitiator/Prompts/</code>. To change a prompt, edit the file and re-run <code class="text-[#96BEE6]/80 bg-[#0a2a52] px-1 rounded">agent:init</code>.</p></div>
      <div class="space-y-3"><div v-for="prompt in prompts" :key="prompt.id" class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4"><div class="flex items-center justify-between mb-2 cursor-pointer" @click="togglePromptExpand(prompt.id)"><div><p class="font-medium text-white">{{ prompt.name }}</p><p class="text-xs text-[#96BEE6]/70">{{ prompt.description }}</p></div><span class="text-[#96BEE6]/70 text-sm">⌄</span></div><div v-if="expandedPromptId === prompt.id"><pre class="text-xs text-[#96BEE6] bg-[#001E44] rounded-lg p-3 whitespace-pre-wrap max-h-60 overflow-y-auto border border-[#0a2a52]">{{ prompt.content }}</pre><p class="text-xs text-[#4a7aa5]/60 mt-2">Last updated {{ new Date(prompt.updatedAt).toLocaleString() }}<span v-if="prompt.updatedBy"> by {{ prompt.updatedBy }}</span></p></div></div><p v-if="prompts.length === 0" class="text-[#96BEE6]/70 text-center py-8">No prompts configured. Start the API to seed defaults.</p></div>
    </template>
    <template v-else-if="activeTab === 'oidc'">
      <div class="space-y-4">
        <div class="bg-[#041e3e]/50 border border-[#0a2a52] rounded-xl p-3"><p class="text-xs text-[#96BEE6]">Configure external sign-in providers. Set the public web origin in Admin Settings before using production OIDC callbacks.</p></div>
        <div v-if="oidcMessage" class="text-sm" :class="oidcError ? 'text-red-400' : 'text-green-400'">{{ oidcMessage }}</div>
        <div class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 space-y-3">
          <h3 class="text-sm font-semibold text-white">{{ editingOidcId ? 'Edit Provider' : 'Add Provider' }}</h3>
          <div class="grid gap-3">
            <label class="block"><span class="block text-xs text-[#96BEE6] mb-1">Provider Type</span><select v-model="oidcForm.providerType" class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-3 py-2.5 text-sm text-white"><option value="entra">Entra ID</option><option value="pocket_id">Pocket ID</option><option value="generic">Generic OIDC</option></select></label>
            <label class="block"><span class="block text-xs text-[#96BEE6] mb-1">Name</span><input v-model="oidcForm.name" placeholder="entra-id" class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-3 py-2.5 text-sm text-white placeholder-[#4a7aa5]" /></label>
            <label class="block"><span class="block text-xs text-[#96BEE6] mb-1">Display Name</span><input v-model="oidcForm.displayName" placeholder="Microsoft Entra ID" class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-3 py-2.5 text-sm text-white placeholder-[#4a7aa5]" /></label>
            <label class="block"><span class="block text-xs text-[#96BEE6] mb-1">Issuer URL</span><input v-model="oidcForm.issuerUrl" placeholder="https://login.microsoftonline.com/{tenant}/v2.0" class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-3 py-2.5 text-sm text-white placeholder-[#4a7aa5]" /></label>
            <label class="block"><span class="block text-xs text-[#96BEE6] mb-1">Client ID</span><input v-model="oidcForm.clientId" class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-3 py-2.5 text-sm text-white" /></label>
            <label class="block"><span class="block text-xs text-[#96BEE6] mb-1">Client Secret</span><input v-model="oidcForm.clientSecret" type="password" :placeholder="editingOidcId ? 'Leave blank to keep current secret' : 'Client secret'" class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-3 py-2.5 text-sm text-white placeholder-[#4a7aa5]" /></label>
            <label class="block"><span class="block text-xs text-[#96BEE6] mb-1">Scopes</span><input :value="scopesText(oidcForm)" @input="handleScopesInput" placeholder="openid profile email" class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-3 py-2.5 text-sm text-white placeholder-[#4a7aa5]" /></label>
            <label class="block"><span class="block text-xs text-[#96BEE6] mb-1">Callback Path</span><input v-model="oidcForm.callbackPath" placeholder="/auth/oidc/callback/{providerId}" class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-3 py-2.5 text-sm text-white placeholder-[#4a7aa5]" /></label>
          </div>
          <div class="flex items-center justify-between"><label class="text-sm text-[#96BEE6]">Enabled</label><button type="button" @click="oidcForm.enabled = !oidcForm.enabled" class="relative w-11 h-6 rounded-full transition-colors" :class="oidcForm.enabled ? 'bg-[#1e407c]' : 'bg-[#0a2a52]'"><span class="absolute top-0.5 left-0.5 w-5 h-5 bg-white rounded-full transition-transform" :class="oidcForm.enabled ? 'translate-x-5' : ''" /></button></div>
          <div class="flex items-center justify-between"><label class="text-sm text-[#96BEE6]">Require verified email</label><button type="button" @click="oidcForm.requireVerifiedEmail = !oidcForm.requireVerifiedEmail" class="relative w-11 h-6 rounded-full transition-colors" :class="oidcForm.requireVerifiedEmail ? 'bg-[#1e407c]' : 'bg-[#0a2a52]'"><span class="absolute top-0.5 left-0.5 w-5 h-5 bg-white rounded-full transition-transform" :class="oidcForm.requireVerifiedEmail ? 'translate-x-5' : ''" /></button></div>
          <div class="flex gap-2 justify-end"><button v-if="editingOidcId" type="button" @click="resetOidcForm" class="px-4 py-2 text-sm rounded-xl bg-[#0a2a52] text-[#96BEE6] hover:bg-[#1e407c]">Cancel</button><button type="button" @click="saveOidcProvider" :disabled="oidcSaving || !oidcForm.name.trim() || !oidcForm.displayName.trim() || !oidcForm.issuerUrl.trim() || !oidcForm.clientId.trim()" class="px-4 py-2 text-sm rounded-xl bg-[#1e407c] text-white hover:bg-[#2a5299] disabled:bg-[#0a2a52] disabled:text-[#4a7aa5]/60">{{ oidcSaving ? 'Saving...' : (editingOidcId ? 'Save Provider' : 'Create Provider') }}</button></div>
        </div>
        <div v-if="oidcLoading" class="text-[#96BEE6]/70 text-center py-8">Loading OIDC providers...</div>
        <div v-else class="space-y-3"><div v-for="provider in oidcProviders" :key="provider.id" class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4"><div class="flex flex-wrap items-center gap-2"><p class="font-medium text-white">{{ provider.displayName }}</p><span class="rounded-full border border-[#1e407c]/50 px-2 py-0.5 text-[10px] text-[#96BEE6]">{{ oidcProviderTypeLabel(provider.providerType) }}</span><span class="rounded-full border px-2 py-0.5 text-[10px]" :class="provider.enabled ? 'border-green-700 text-green-400' : 'border-[#1e407c]/50 text-[#96BEE6]/70'">{{ provider.enabled ? 'Enabled' : 'Disabled' }}</span></div><p class="text-xs text-[#96BEE6]/70 break-all mt-1">{{ provider.issuerUrl }}</p><p class="text-xs text-[#4a7aa5] break-all">Client ID: {{ provider.clientId }}</p><p class="text-xs text-[#4a7aa5]">Scopes: {{ provider.scopes.join(' ') || '—' }}</p><p class="text-xs text-[#4a7aa5]">Test: {{ provider.lastTestStatus }}<template v-if="provider.lastTestMessage"> · {{ provider.lastTestMessage }}</template></p><div class="flex flex-wrap gap-2 mt-3 pt-3 border-t border-[#0a2a52]"><button @click="editOidcProvider(provider)" class="text-xs px-3 py-2 rounded-lg bg-[#0a2a52] text-white/80 hover:bg-[#1e407c]">Edit</button><button @click="testOidcProvider(provider)" :disabled="oidcTestingId === provider.id" class="text-xs px-3 py-2 rounded-lg bg-[#0a2a52] text-[#96BEE6] hover:bg-[#1e407c] disabled:text-[#4a7aa5]/60">{{ oidcTestingId === provider.id ? 'Testing...' : 'Test' }}</button><button @click="deleteOidcProvider(provider)" class="text-xs px-3 py-2 rounded-lg bg-red-900/30 text-red-400 hover:bg-red-900/50 border border-red-800/50">Delete</button></div></div><p v-if="oidcProviders.length === 0" class="text-[#96BEE6]/70 text-center py-8">No OIDC providers configured.</p></div>
      </div>
    </template>

    <template v-else-if="activeTab === 'settings'">
      <div class="space-y-4"><div class="bg-[#041e3e]/50 border border-[#0a2a52] rounded-xl p-3"><p class="text-xs text-[#96BEE6]">Configure required auth settings here instead of relying on deployment-only environment knowledge.</p></div><div v-if="settingsLoading" class="text-[#96BEE6]/70 text-center py-8">Loading admin settings...</div><div v-else class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 space-y-4"><label class="block"><span class="block text-sm font-semibold text-white mb-1">OIDC public web origin</span><input v-model="settingsForm.oidcPublicOrigin" placeholder="https://app.example.com" class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-3 py-2.5 text-sm text-white placeholder-[#4a7aa5]" /><span class="block text-xs text-[#96BEE6]/70 mt-2">Used to build redirect URIs for OIDC login and account linking. Enter only scheme and host, no path.</span></label><div v-if="settingsMessage" class="text-sm" :class="settingsError ? 'text-red-400' : 'text-green-400'">{{ settingsMessage }}</div><div class="flex gap-2 justify-end"><button @click="resetSettingsForm" class="text-xs px-4 py-2 rounded-xl bg-[#0a2a52] text-[#96BEE6] hover:bg-[#1e407c] transition-colors">Reset</button><button @click="saveAdminSettings" :disabled="settingsSaving" class="text-xs px-4 py-2 rounded-xl bg-[#1e407c] text-white hover:bg-[#2a5299] transition-colors disabled:opacity-50">{{ settingsSaving ? 'Saving...' : 'Save Settings' }}</button></div></div></div>
    </template>
    <template v-else-if="activeTab === 'foundry'">
      <div v-if="foundryStatus" class="space-y-4">
        <div class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4"><h3 class="text-sm font-semibold text-white mb-3">Configuration</h3><div class="space-y-2"><div class="flex justify-between text-xs"><span class="text-[#96BEE6]/70">Project Endpoint</span><span class="text-white/80 font-mono truncate ml-4 max-w-[60%] text-right">{{ foundryStatus.projectEndpoint || 'Not configured' }}</span></div><div class="flex justify-between text-xs"><span class="text-[#96BEE6]/70">Vision Model</span><span class="text-white/80 font-mono">{{ foundryStatus.visionModel || '—' }}</span></div><div class="flex justify-between text-xs"><span class="text-[#96BEE6]/70">Reasoning Model</span><span class="text-white/80 font-mono">{{ foundryStatus.reasoningModel || '—' }}</span></div><div class="flex justify-between text-xs"><span class="text-[#96BEE6]/70">Status</span><span :class="foundryStatus.isProjectConfigured ? 'text-green-400' : 'text-red-400'" class="font-medium">{{ foundryStatus.isProjectConfigured ? 'Configured' : 'Not Configured' }}</span></div></div></div>
        <div v-if="foundryStatus.agentValidation" class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4"><h3 class="text-sm font-semibold text-white mb-3">Registered Agents</h3><div v-for="agentName in foundryStatus.agentValidation.foundAgents" :key="agentName" class="flex items-center justify-between text-xs py-1 border-b border-[#0a2a52] last:border-b-0"><span class="text-white/80 font-mono">{{ agentName }}</span><span class="text-green-400 font-medium">Valid</span></div><div v-for="agentName in foundryStatus.agentValidation.missingAgents" :key="agentName" class="flex items-center justify-between text-xs py-1 border-b border-[#0a2a52] last:border-b-0"><span class="text-white/80 font-mono">{{ agentName }}</span><span class="text-red-400 font-medium">Not Found</span></div></div>
        <div class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4"><div class="flex items-center justify-between mb-3"><h3 class="text-sm font-semibold text-white">Connectivity Test</h3><button @click="testFoundryConnectivity" :disabled="foundryTesting || !foundryStatus.isProjectConfigured" class="text-xs px-4 py-2 min-h-[44px] rounded-xl bg-[#1e407c] text-white hover:bg-[#2a5299] transition-colors disabled:opacity-50">{{ foundryTesting ? 'Testing...' : 'Test Connection' }}</button></div><div v-if="foundryStatus.connectivityTest" class="text-xs space-y-1"><div class="flex justify-between"><span class="text-[#96BEE6]/70">Status</span><span :class="foundryStatus.connectivityTest.status === 'ok' ? 'text-green-400' : 'text-red-400'" class="font-medium">{{ foundryStatus.connectivityTest.status === 'ok' ? 'Connected' : 'Failed' }}</span></div><div class="flex justify-between"><span class="text-[#96BEE6]/70">Latency</span><span class="text-white/80">{{ foundryStatus.connectivityTest.latencyMs }}ms</span></div><p v-if="foundryStatus.connectivityTest.message" class="text-[#96BEE6] bg-[#001E44] rounded-lg p-2 break-words">{{ foundryStatus.connectivityTest.message }}</p><p class="text-[#4a7aa5]/60 mt-1">Tested {{ new Date(foundryStatus.connectivityTest.testedAt).toLocaleString() }}</p></div><p v-else class="text-xs text-[#96BEE6]/70">No test run yet. Click "Test Connection" to verify Foundry connectivity.</p></div>
      </div><p v-else class="text-[#96BEE6]/70 text-center py-8">Could not load Foundry status.</p>
    </template>

    <template v-else-if="activeTab === 'logging'">
      <div v-if="loggingData" class="space-y-4"><div class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4"><p class="text-sm text-[#96BEE6] mb-4">Configure log verbosity per category. Changes take effect immediately without restart.</p><div class="flex items-center justify-between py-3 border-b border-[#0a2a52]"><div><p class="font-medium text-white">Default</p><p class="text-xs text-[#96BEE6]/70">Catch-all for uncategorized loggers</p></div><select v-model="editedDefaultLevel" :class="getLevelColor(editedDefaultLevel)" class="bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-3 py-1.5 text-sm"><option v-for="level in loggingData.availableLevels" :key="level" :value="level">{{ level }}</option></select></div><div v-for="(_, category) in editedLevels" :key="category" class="flex items-center justify-between py-3 border-b border-[#0a2a52] last:border-b-0"><div class="min-w-0 flex-1 mr-3"><p class="text-sm text-white font-mono truncate">{{ category }}</p></div><select v-model="editedLevels[category]" :class="getLevelColor(editedLevels[category])" class="bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-3 py-1.5 text-sm"><option v-for="level in loggingData.availableLevels" :key="level" :value="level">{{ level }}</option></select></div></div><div class="flex items-center justify-between"><p v-if="loggingMessage" class="text-xs" :class="loggingMessage.includes('Failed') ? 'text-red-400' : 'text-green-400'">{{ loggingMessage }}</p><div class="flex gap-2 ml-auto"><button @click="resetLoggingForm" class="text-xs px-4 py-2 rounded-xl bg-[#0a2a52] text-[#96BEE6] hover:bg-[#1e407c] transition-colors">Reset</button><button @click="saveLoggingSettings" :disabled="loggingSaving" class="text-xs px-4 py-2 rounded-xl bg-[#1e407c] text-white hover:bg-[#2a5299] transition-colors disabled:opacity-50">{{ loggingSaving ? 'Saving...' : 'Save Log Levels' }}</button></div></div></div><p v-else class="text-[#96BEE6]/70 text-center py-8">Could not load logging settings.</p>
    </template>

    <Teleport to="body"><div v-if="resetPasswordUserId" class="fixed inset-0 bg-black/60 flex items-center justify-center z-50 p-4" @click.self="resetPasswordUserId = null"><div class="bg-[#041e3e] border border-[#1e407c]/50 rounded-2xl p-6 w-full max-w-sm"><h3 class="text-lg font-semibold text-white mb-4">Reset Password</h3><p class="text-sm text-[#96BEE6] mb-3">Enter a new password for {{ users.find(u => u.id === resetPasswordUserId)?.displayName }}</p><input v-model="newPassword" type="password" placeholder="New password (min 8 characters)" class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-3 text-white placeholder-[#4a7aa5] mb-3" @keyup.enter="confirmResetPassword" /><p v-if="resetMessage" class="text-xs mb-3" :class="resetMessage.includes('Failed') ? 'text-red-400' : 'text-green-400'">{{ resetMessage }}</p><div class="flex gap-2 justify-end"><button @click="resetPasswordUserId = null" class="px-4 py-2 text-sm rounded-xl bg-[#0a2a52] text-[#96BEE6] hover:bg-[#1e407c]">Cancel</button><button @click="confirmResetPassword" class="px-4 py-2 text-sm rounded-xl bg-[#1e407c] text-white hover:bg-[#2a5299]">Reset</button></div></div></div></Teleport>
    <Teleport to="body"><div v-if="deleteConfirmUserId" class="fixed inset-0 bg-black/60 flex items-center justify-center z-50 p-4" @click.self="deleteConfirmUserId = null"><div class="bg-[#041e3e] border border-[#1e407c]/50 rounded-2xl p-6 w-full max-w-sm"><h3 class="text-lg font-semibold text-red-400 mb-4">Delete User</h3><p class="text-sm text-white/80 mb-4">Are you sure you want to delete <strong>{{ users.find(u => u.id === deleteConfirmUserId)?.displayName }}</strong>? This action cannot be undone.</p><div class="flex gap-2 justify-end"><button @click="deleteConfirmUserId = null" class="px-4 py-2 text-sm rounded-xl bg-[#0a2a52] text-[#96BEE6] hover:bg-[#1e407c]">Cancel</button><button @click="confirmDeleteUser" class="px-4 py-2 text-sm rounded-xl bg-red-700 text-white hover:bg-red-600">Delete</button></div></div></div></Teleport>
  </div>
</template>

<style scoped>
.dropdown-enter-active,
.dropdown-leave-active { transition: opacity 0.15s ease, transform 0.15s ease; }
.dropdown-enter-from,
.dropdown-leave-to { opacity: 0; transform: translateY(-4px); }
</style>
