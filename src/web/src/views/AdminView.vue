<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { usersApi, type User, type Prompt, type LoggingSettings, type LoggingSettingsResponse, type FoundryStatus } from '../services/users'
import { capturesApi, type CaptureResponse } from '../services/captures'
import { venuesApi, type Venue } from '../services/venues'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const users = ref<User[]>([])
const prompts = ref<Prompt[]>([])
const isLoading = ref(true)
const searchQuery = ref('')
const activeTab = ref<'users' | 'prompts' | 'logging' | 'foundry' | 'activity'>('users')
const showMenu = ref(false)

function selectTab(tab: typeof activeTab.value) {
  activeTab.value = tab
  showMenu.value = false
  if (tab === 'activity' && !captures.value.length && !venues.value.length) {
    loadActivity()
  }
}

// Activity state
const captures = ref<CaptureResponse[]>([])
const venues = ref<Venue[]>([])
const activityLoading = ref(false)

// Reset password state
const resetPasswordUserId = ref<string | null>(null)
const newPassword = ref('')
const resetMessage = ref('')

// Delete user state
const deleteConfirmUserId = ref<string | null>(null)

// Prompt viewing state (read-only — prompts are managed as files in AgentInitiator/Prompts/)
const expandedPromptId = ref<string | null>(null)

// Logging state
const loggingData = ref<LoggingSettingsResponse | null>(null)
const editedLevels = ref<Record<string, string>>({})
const editedDefaultLevel = ref('Information')
const loggingMessage = ref('')
const loggingSaving = ref(false)

// Foundry state
const foundryStatus = ref<FoundryStatus | null>(null)
const foundryTesting = ref(false)

const filteredUsers = computed(() => {
  if (!searchQuery.value) return users.value
  const q = searchQuery.value.toLowerCase()
  return users.value.filter(u =>
    u.displayName.toLowerCase().includes(q) ||
    u.email.toLowerCase().includes(q)
  )
})

onMounted(async () => {
  try {
    const [usersRes, promptsRes, loggingRes, foundryRes] = await Promise.all([
      usersApi.listUsers(),
      usersApi.listPrompts(),
      usersApi.getLoggingSettings(),
      usersApi.getFoundryStatus()
    ])
    users.value = usersRes.data
    prompts.value = promptsRes.data
    loggingData.value = loggingRes.data
    foundryStatus.value = foundryRes.data
    resetLoggingForm()
  } finally {
    isLoading.value = false
  }
})

async function toggleRole(user: User) {
  const newRole = user.role === 'admin' ? 'user' : 'admin'
  const { data } = await usersApi.updateRole(user.id, newRole)
  const index = users.value.findIndex(u => u.id === user.id)
  if (index !== -1) users.value[index] = data
}

function startResetPassword(userId: string) {
  resetPasswordUserId.value = userId
  newPassword.value = ''
  resetMessage.value = ''
}

async function confirmResetPassword() {
  if (!resetPasswordUserId.value || newPassword.value.length < 8) {
    resetMessage.value = 'Password must be at least 8 characters'
    return
  }
  try {
    await usersApi.resetPassword(resetPasswordUserId.value, newPassword.value)
    resetMessage.value = 'Password reset successfully'
    setTimeout(() => { resetPasswordUserId.value = null }, 1500)
  } catch {
    resetMessage.value = 'Failed to reset password'
  }
}

function startDeleteUser(userId: string) {
  deleteConfirmUserId.value = userId
}

async function confirmDeleteUser() {
  if (!deleteConfirmUserId.value) return
  try {
    await usersApi.deleteUser(deleteConfirmUserId.value)
    users.value = users.value.filter(u => u.id !== deleteConfirmUserId.value)
    deleteConfirmUserId.value = null
  } catch {
    // Could show error toast
  }
}

function togglePromptExpand(promptId: string) {
  expandedPromptId.value = expandedPromptId.value === promptId ? null : promptId
}

function resetLoggingForm() {
  if (!loggingData.value) return
  editedDefaultLevel.value = loggingData.value.settings.defaultLevel
  // Merge available categories with current settings
  const merged: Record<string, string> = {}
  for (const [cat, defaultLevel] of Object.entries(loggingData.value.availableCategories)) {
    merged[cat] = loggingData.value.settings.categoryLevels[cat] ?? defaultLevel
  }
  // Also include any custom categories from settings not in defaults
  for (const [cat, level] of Object.entries(loggingData.value.settings.categoryLevels)) {
    if (!(cat in merged)) merged[cat] = level
  }
  editedLevels.value = merged
}

function getLevelColor(level: string): string {
  switch (level) {
    case 'Trace': return 'text-[#96BEE6]/70'
    case 'Debug': return 'text-blue-400'
    case 'Information': return 'text-green-400'
    case 'Warning': return 'text-yellow-400'
    case 'Error': return 'text-red-400'
    case 'Critical': return 'text-red-600'
    case 'None': return 'text-[#4a7aa5]/60'
    default: return 'text-[#96BEE6]'
  }
}

async function saveLoggingSettings() {
  loggingSaving.value = true
  loggingMessage.value = ''
  try {
    // Only send categories that differ from "Default" override
    const categoryLevels: Record<string, string> = {}
    for (const [cat, level] of Object.entries(editedLevels.value)) {
      if (cat !== 'Default') categoryLevels[cat] = level
    }
    const settings: LoggingSettings = {
      defaultLevel: editedDefaultLevel.value,
      categoryLevels
    }
    const { data } = await usersApi.updateLoggingSettings(settings)
    if (loggingData.value) {
      loggingData.value.settings = data
    }
    loggingMessage.value = 'Log levels updated — changes take effect immediately'
    setTimeout(() => { loggingMessage.value = '' }, 3000)
  } catch {
    loggingMessage.value = 'Failed to save logging settings'
  } finally {
    loggingSaving.value = false
  }
}

async function testFoundryConnectivity() {
  foundryTesting.value = true
  try {
    const { data } = await usersApi.testFoundryConnectivity()
    foundryStatus.value = data
  } catch {
    // handled by status display
  } finally {
    foundryTesting.value = false
  }
}

async function loadActivity() {
  activityLoading.value = true
  try {
    const [capturesRes, venuesRes] = await Promise.all([
      capturesApi.list(),
      venuesApi.list(),
    ])
    captures.value = capturesRes.data.items
    venues.value = venuesRes.data.items
  } finally {
    activityLoading.value = false
  }
}
</script>

<template>
  <div class="p-4 max-w-lg mx-auto">
    <div class="flex items-center justify-between mb-6">
      <router-link to="/profile" class="text-sm text-[#96BEE6] hover:text-white">Back</router-link>
      <h2 class="text-xl font-semibold">Admin Panel</h2>
      <div class="relative">
        <button @click="showMenu = !showMenu" class="text-[#96BEE6] hover:text-white p-1">
          <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M4 6h16M4 12h16M4 18h16" />
          </svg>
        </button>
        <Transition name="dropdown">
          <div v-if="showMenu" class="absolute right-0 top-8 z-50 w-44 bg-[#041e3e] border border-[#1e407c] rounded-xl shadow-xl overflow-hidden">
            <button
              v-for="item in ([
                { key: 'users', label: 'Users' },
                { key: 'prompts', label: 'AI Prompts' },
                { key: 'foundry', label: 'Foundry' },
                { key: 'logging', label: 'Logging' },
                { key: 'activity', label: 'Activity' },
              ] as const)"
              :key="item.key"
              @click="selectTab(item.key)"
              class="w-full text-left px-4 py-3 text-sm transition-colors"
              :class="activeTab === item.key
                ? 'bg-[#1e407c]/30 text-white'
                : 'text-[#96BEE6] hover:bg-[#0a2a52]'"
            >
              {{ item.label }}
            </button>
          </div>
        </Transition>
      </div>
    </div>

    <!-- Click-away overlay for menu -->
    <div v-if="showMenu" class="fixed inset-0 z-40" @click="showMenu = false" />

    <div v-if="isLoading" class="text-[#96BEE6]/70 text-center py-12">Loading...</div>

    <!-- ── Users Tab ─────────────────────────────── -->
    <template v-else-if="activeTab === 'users'">
      <input
        v-model="searchQuery"
        placeholder="Search users..."
        class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-3 text-white placeholder-[#4a7aa5] focus:outline-none focus:border-[#1e407c] mb-4"
      />

      <div class="space-y-3">
        <div
          v-for="user in filteredUsers"
          :key="user.id"
          class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4"
        >
          <div class="flex items-center justify-between">
            <div class="min-w-0 flex-1 mr-3">
              <p class="font-medium text-white truncate">{{ user.displayName }}</p>
              <p class="text-sm text-[#96BEE6]/70 truncate">{{ user.email }}</p>
              <p class="text-xs text-[#4a7aa5]/60 mt-1">
                Joined {{ new Date(user.createdAt).toLocaleDateString() }}
              </p>
            </div>

            <div class="flex items-center gap-2 shrink-0">
              <button
                @click="toggleRole(user)"
                :disabled="user.id === auth.user?.id"
                class="text-xs px-3 py-1.5 rounded-full border transition-colors disabled:opacity-50"
                :class="user.role === 'admin'
                  ? 'border-[#1e407c] text-[#96BEE6] hover:bg-[#1e407c]/20'
                  : 'border-[#1e407c]/50 text-[#96BEE6] hover:bg-[#0a2a52]'"
              >
                {{ user.role }}
              </button>
            </div>
          </div>

          <!-- Action buttons -->
          <div class="flex gap-2 mt-3 pt-3 border-t border-[#0a2a52]">
            <button
              @click="startResetPassword(user.id)"
              class="text-xs px-3 py-2.5 min-h-[44px] rounded-lg bg-[#0a2a52] text-white/80 hover:bg-[#1e407c] transition-colors"
            >
              Reset Password
            </button>
            <button
              v-if="user.id !== auth.user?.id"
              @click="startDeleteUser(user.id)"
              class="text-xs px-3 py-2.5 min-h-[44px] rounded-lg bg-red-900/30 text-red-400 hover:bg-red-900/50 border border-red-800/50 transition-colors"
            >
              Delete
            </button>
          </div>
        </div>

        <p v-if="filteredUsers.length === 0" class="text-[#96BEE6]/70 text-center py-8">
          No users match your search
        </p>
      </div>
    </template>

    <!-- ── Prompts Tab (Read-Only) ────────────────── -->
    <template v-else-if="activeTab === 'prompts'">
      <div class="bg-[#041e3e]/50 border border-[#0a2a52] rounded-xl p-3 mb-4">
        <p class="text-xs text-[#96BEE6]">
          Agent prompts are managed as files in <code class="text-[#96BEE6]/80 bg-[#0a2a52] px-1 rounded">AgentInitiator/Prompts/</code>.
          To change a prompt, edit the file and re-run the <code class="text-[#96BEE6]/80 bg-[#0a2a52] px-1 rounded">agent:init</code> task.
        </p>
      </div>

      <div class="space-y-3">
        <div
          v-for="prompt in prompts"
          :key="prompt.id"
          class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4"
        >
          <div class="flex items-center justify-between mb-2 cursor-pointer" @click="togglePromptExpand(prompt.id)">
            <div>
              <p class="font-medium text-white">{{ prompt.name }}</p>
              <p class="text-xs text-[#96BEE6]/70">{{ prompt.description }}</p>
            </div>
            <span class="text-[#96BEE6]/70 text-sm transition-transform" :class="expandedPromptId === prompt.id ? 'rotate-180' : ''">
              &#9662;
            </span>
          </div>

          <div v-if="expandedPromptId === prompt.id">
            <pre class="text-xs text-[#96BEE6] bg-[#001E44] rounded-lg p-3 whitespace-pre-wrap max-h-60 overflow-y-auto border border-[#0a2a52]">{{ prompt.content }}</pre>
            <p class="text-xs text-[#4a7aa5]/60 mt-2">
              Last updated {{ new Date(prompt.updatedAt).toLocaleString() }}
              <span v-if="prompt.updatedBy"> by {{ prompt.updatedBy }}</span>
            </p>
          </div>
        </div>

        <p v-if="prompts.length === 0" class="text-[#96BEE6]/70 text-center py-8">
          No prompts configured. Start the API to seed defaults.
        </p>
      </div>
    </template>

    <!-- ── Foundry Tab ───────────────────────────── -->
    <template v-else-if="activeTab === 'foundry'">
      <div v-if="foundryStatus" class="space-y-4">
        <!-- Configuration -->
        <div class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4">
          <h3 class="text-sm font-semibold text-white mb-3">Configuration</h3>
          <div class="space-y-2">
            <div class="flex justify-between text-xs">
              <span class="text-[#96BEE6]/70">Project Endpoint</span>
              <span class="text-white/80 font-mono truncate ml-4 max-w-[60%] text-right">{{ foundryStatus.projectEndpoint || 'Not configured' }}</span>
            </div>
            <div class="flex justify-between text-xs">
              <span class="text-[#96BEE6]/70">Vision Model</span>
              <span class="text-white/80 font-mono">{{ foundryStatus.visionModel || '—' }}</span>
            </div>
            <div class="flex justify-between text-xs">
              <span class="text-[#96BEE6]/70">Reasoning Model</span>
              <span class="text-white/80 font-mono">{{ foundryStatus.reasoningModel || '—' }}</span>
            </div>
            <div class="flex justify-between text-xs">
              <span class="text-[#96BEE6]/70">Status</span>
              <span :class="foundryStatus.isProjectConfigured ? 'text-green-400' : 'text-red-400'" class="font-medium">
                {{ foundryStatus.isProjectConfigured ? 'Configured' : 'Not Configured' }}
              </span>
            </div>
          </div>
        </div>

        <!-- Agent Validation -->
        <div v-if="foundryStatus.agentValidation && foundryStatus.agentValidation.foundAgents.length > 0" class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4">
          <h3 class="text-sm font-semibold text-white mb-3">Registered Agents</h3>
          <div class="space-y-2">
            <div
              v-for="agentName in foundryStatus.agentValidation.foundAgents"
              :key="agentName"
              class="flex items-center justify-between text-xs py-1 border-b border-[#0a2a52] last:border-b-0"
            >
              <span class="text-white/80 font-mono">{{ agentName }}</span>
              <span class="text-green-400 font-medium">Valid</span>
            </div>
            <div
              v-for="agentName in foundryStatus.agentValidation.missingAgents"
              :key="agentName"
              class="flex items-center justify-between text-xs py-1 border-b border-[#0a2a52] last:border-b-0"
            >
              <span class="text-white/80 font-mono">{{ agentName }}</span>
              <span class="text-red-400 font-medium">Not Found</span>
            </div>
          </div>
        </div>

        <!-- Connectivity Test -->
        <div class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4">
          <div class="flex items-center justify-between mb-3">
            <h3 class="text-sm font-semibold text-white">Connectivity Test</h3>
            <button
              @click="testFoundryConnectivity"
              :disabled="foundryTesting || !foundryStatus.isProjectConfigured"
              class="text-xs px-4 py-2 min-h-[44px] rounded-xl bg-[#1e407c] text-white hover:bg-[#2a5299] transition-colors disabled:opacity-50"
            >
              {{ foundryTesting ? 'Testing...' : 'Test Connection' }}
            </button>
          </div>

          <div v-if="foundryStatus.connectivityTest" class="text-xs space-y-1">
            <div class="flex justify-between">
              <span class="text-[#96BEE6]/70">Status</span>
              <span :class="foundryStatus.connectivityTest.status === 'ok' ? 'text-green-400' : 'text-red-400'" class="font-medium">
                {{ foundryStatus.connectivityTest.status === 'ok' ? 'Connected' : 'Failed' }}
              </span>
            </div>
            <div class="flex justify-between">
              <span class="text-[#96BEE6]/70">Latency</span>
              <span class="text-white/80">{{ foundryStatus.connectivityTest.latencyMs }}ms</span>
            </div>
            <div v-if="foundryStatus.connectivityTest.message" class="mt-2">
              <p class="text-[#96BEE6] bg-[#001E44] rounded-lg p-2 break-words">{{ foundryStatus.connectivityTest.message }}</p>
            </div>
            <p class="text-[#4a7aa5]/60 mt-1">
              Tested {{ new Date(foundryStatus.connectivityTest.testedAt).toLocaleString() }}
            </p>
          </div>
          <p v-else class="text-xs text-[#96BEE6]/70">No test run yet. Click "Test Connection" to verify Foundry connectivity.</p>
        </div>
      </div>

      <p v-else class="text-[#96BEE6]/70 text-center py-8">
        Could not load Foundry status.
      </p>
    </template>

    <!-- ── Logging Tab ───────────────────────────── -->
    <template v-else-if="activeTab === 'logging'">
      <div v-if="loggingData" class="space-y-4">
        <div class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4">
          <p class="text-sm text-[#96BEE6] mb-4">
            Configure log verbosity per category. Changes take effect immediately without restart.
          </p>

          <!-- Default level -->
          <div class="flex items-center justify-between py-3 border-b border-[#0a2a52]">
            <div>
              <p class="font-medium text-white">Default</p>
              <p class="text-xs text-[#96BEE6]/70">Catch-all for uncategorized loggers</p>
            </div>
            <select
              v-model="editedDefaultLevel"
              :class="getLevelColor(editedDefaultLevel)"
              class="bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-3 py-1.5 text-sm focus:outline-none focus:border-[#1e407c]"
            >
              <option v-for="level in loggingData.availableLevels" :key="level" :value="level">
                {{ level }}
              </option>
            </select>
          </div>

          <!-- Per-category levels -->
          <div
            v-for="(_, category) in editedLevels"
            :key="category"
            class="flex items-center justify-between py-3 border-b border-[#0a2a52] last:border-b-0"
          >
            <div class="min-w-0 flex-1 mr-3">
              <p class="text-sm text-white font-mono truncate">{{ category }}</p>
            </div>
            <select
              v-model="editedLevels[category]"
              :class="getLevelColor(editedLevels[category])"
              class="bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-3 py-1.5 text-sm focus:outline-none focus:border-[#1e407c]"
            >
              <option v-for="level in loggingData.availableLevels" :key="level" :value="level">
                {{ level }}
              </option>
            </select>
          </div>
        </div>

        <!-- Save / feedback -->
        <div class="flex items-center justify-between">
          <p v-if="loggingMessage" class="text-xs" :class="loggingMessage.includes('Failed') ? 'text-red-400' : 'text-green-400'">
            {{ loggingMessage }}
          </p>
          <div class="flex gap-2 ml-auto">
            <button
              @click="resetLoggingForm"
              class="text-xs px-4 py-2 rounded-xl bg-[#0a2a52] text-[#96BEE6] hover:bg-[#1e407c] transition-colors"
            >
              Reset
            </button>
            <button
              @click="saveLoggingSettings"
              :disabled="loggingSaving"
              class="text-xs px-4 py-2 rounded-xl bg-[#1e407c] text-white hover:bg-[#2a5299] transition-colors disabled:opacity-50"
            >
              {{ loggingSaving ? 'Saving...' : 'Save Log Levels' }}
            </button>
          </div>
        </div>
      </div>

      <p v-else class="text-[#96BEE6]/70 text-center py-8">
        Could not load logging settings.
      </p>
    </template>

    <!-- ── Activity Tab ───────────────────────────── -->
    <template v-else-if="activeTab === 'activity'">
      <div v-if="activityLoading" class="text-[#96BEE6]/70 text-center py-12">Loading activity...</div>

      <div v-else class="space-y-3">
        <!-- Quick stats -->
        <div class="bg-[#041e3e] border border-[#0a2a52] rounded-xl p-4 flex justify-around text-center mb-2">
          <div>
            <p class="text-2xl font-bold text-[#96BEE6]">{{ captures.length }}</p>
            <p class="text-xs text-[#96BEE6]/70">Captures</p>
          </div>
          <div>
            <p class="text-2xl font-bold text-green-400">{{ captures.filter(c => c.status === 'completed').length }}</p>
            <p class="text-xs text-[#96BEE6]/70">Completed</p>
          </div>
          <div>
            <p class="text-2xl font-bold text-red-400">{{ captures.filter(c => c.status === 'failed').length }}</p>
            <p class="text-xs text-[#96BEE6]/70">Failed</p>
          </div>
          <div>
            <p class="text-2xl font-bold text-[#96BEE6]">{{ venues.length }}</p>
            <p class="text-xs text-[#96BEE6]/70">Venues</p>
          </div>
        </div>

        <!-- View full history link -->
        <router-link
          to="/history"
          class="block text-center text-sm text-[#96BEE6] hover:text-white py-3 bg-[#041e3e] border border-[#0a2a52] rounded-xl hover:border-[#1e407c]/50 transition-colors"
        >
          View full activity history →
        </router-link>
      </div>
    </template>

    <!-- ── Reset Password Modal ──────────────────── -->
    <Teleport to="body">
      <div v-if="resetPasswordUserId" class="fixed inset-0 bg-black/60 flex items-center justify-center z-50 p-4" @click.self="resetPasswordUserId = null">
        <div class="bg-[#041e3e] border border-[#1e407c]/50 rounded-2xl p-6 w-full max-w-sm">
          <h3 class="text-lg font-semibold text-white mb-4">Reset Password</h3>
          <p class="text-sm text-[#96BEE6] mb-3">
            Enter a new password for {{ users.find(u => u.id === resetPasswordUserId)?.displayName }}
          </p>
          <input
            v-model="newPassword"
            type="password"
            placeholder="New password (min 8 characters)"
            class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-3 text-white placeholder-[#4a7aa5] focus:outline-none focus:border-[#1e407c] mb-3"
            @keyup.enter="confirmResetPassword"
          />
          <p v-if="resetMessage" class="text-xs mb-3" :class="resetMessage.includes('Failed') ? 'text-red-400' : 'text-green-400'">
            {{ resetMessage }}
          </p>
          <div class="flex gap-2 justify-end">
            <button @click="resetPasswordUserId = null" class="px-4 py-2 text-sm rounded-xl bg-[#0a2a52] text-[#96BEE6] hover:bg-[#1e407c]">Cancel</button>
            <button @click="confirmResetPassword" class="px-4 py-2 text-sm rounded-xl bg-[#1e407c] text-white hover:bg-[#2a5299]">Reset</button>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- ── Delete Confirmation Modal ─────────────── -->
    <Teleport to="body">
      <div v-if="deleteConfirmUserId" class="fixed inset-0 bg-black/60 flex items-center justify-center z-50 p-4" @click.self="deleteConfirmUserId = null">
        <div class="bg-[#041e3e] border border-[#1e407c]/50 rounded-2xl p-6 w-full max-w-sm">
          <h3 class="text-lg font-semibold text-red-400 mb-4">Delete User</h3>
          <p class="text-sm text-white/80 mb-4">
            Are you sure you want to delete <strong>{{ users.find(u => u.id === deleteConfirmUserId)?.displayName }}</strong>? This action cannot be undone.
          </p>
          <div class="flex gap-2 justify-end">
            <button @click="deleteConfirmUserId = null" class="px-4 py-2 text-sm rounded-xl bg-[#0a2a52] text-[#96BEE6] hover:bg-[#1e407c]">Cancel</button>
            <button @click="confirmDeleteUser" class="px-4 py-2 text-sm rounded-xl bg-red-700 text-white hover:bg-red-600">Delete</button>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.dropdown-enter-active,
.dropdown-leave-active {
  transition: opacity 0.15s ease, transform 0.15s ease;
}
.dropdown-enter-from,
.dropdown-leave-to {
  opacity: 0;
  transform: translateY(-4px);
}
</style>
