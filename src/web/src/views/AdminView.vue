<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { usersApi, type User, type Prompt, type LoggingSettings, type LoggingSettingsResponse, type FoundryStatus } from '../services/users'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const users = ref<User[]>([])
const prompts = ref<Prompt[]>([])
const isLoading = ref(true)
const searchQuery = ref('')
const activeTab = ref<'users' | 'prompts' | 'logging' | 'foundry'>('users')

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
    case 'Trace': return 'text-stone-500'
    case 'Debug': return 'text-blue-400'
    case 'Information': return 'text-green-400'
    case 'Warning': return 'text-yellow-400'
    case 'Error': return 'text-red-400'
    case 'Critical': return 'text-red-600'
    case 'None': return 'text-stone-600'
    default: return 'text-stone-400'
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
</script>

<template>
  <div class="p-4 max-w-lg mx-auto">
    <div class="flex items-center justify-between mb-6">
      <h2 class="text-xl font-semibold">Admin Panel</h2>
      <router-link to="/profile" class="text-sm text-stone-400 hover:text-stone-200">← Back</router-link>
    </div>

    <!-- Stats -->
    <div class="bg-stone-900 border border-stone-800 rounded-xl p-4 mb-4 flex justify-around text-center">
      <div>
        <p class="text-2xl font-bold text-amber-500">{{ users.length }}</p>
        <p class="text-xs text-stone-500">Total Users</p>
      </div>
      <div>
        <p class="text-2xl font-bold text-amber-500">{{ users.filter(u => u.role === 'admin').length }}</p>
        <p class="text-xs text-stone-500">Admins</p>
      </div>
      <div>
        <p class="text-2xl font-bold text-amber-500">{{ prompts.length }}</p>
        <p class="text-xs text-stone-500">Prompts</p>
      </div>
    </div>

    <!-- Tabs -->
    <div class="flex gap-2 mb-4">
      <button
        @click="activeTab = 'users'"
        class="flex-1 py-2 rounded-xl text-sm font-medium transition-colors"
        :class="activeTab === 'users'
          ? 'bg-amber-700/30 text-amber-500 border border-amber-700'
          : 'bg-stone-800 text-stone-400 border border-stone-700 hover:bg-stone-750'"
      >
        Users
      </button>
      <button
        @click="activeTab = 'prompts'"
        class="flex-1 py-2 rounded-xl text-sm font-medium transition-colors"
        :class="activeTab === 'prompts'
          ? 'bg-amber-700/30 text-amber-500 border border-amber-700'
          : 'bg-stone-800 text-stone-400 border border-stone-700 hover:bg-stone-750'"
      >
        AI Prompts
      </button>
      <button
        @click="activeTab = 'foundry'"
        class="flex-1 py-2 rounded-xl text-sm font-medium transition-colors"
        :class="activeTab === 'foundry'
          ? 'bg-amber-700/30 text-amber-500 border border-amber-700'
          : 'bg-stone-800 text-stone-400 border border-stone-700 hover:bg-stone-750'"
      >
        Foundry
      </button>
      <button
        @click="activeTab = 'logging'"
        class="flex-1 py-2 rounded-xl text-sm font-medium transition-colors"
        :class="activeTab === 'logging'
          ? 'bg-amber-700/30 text-amber-500 border border-amber-700'
          : 'bg-stone-800 text-stone-400 border border-stone-700 hover:bg-stone-750'"
      >
        Logging
      </button>
    </div>

    <div v-if="isLoading" class="text-stone-500 text-center py-12">Loading...</div>

    <!-- ── Users Tab ─────────────────────────────── -->
    <template v-else-if="activeTab === 'users'">
      <input
        v-model="searchQuery"
        placeholder="Search users..."
        class="w-full bg-stone-800 border border-stone-700 rounded-xl px-4 py-3 text-stone-100 placeholder-stone-600 focus:outline-none focus:border-amber-700 mb-4"
      />

      <div class="space-y-3">
        <div
          v-for="user in filteredUsers"
          :key="user.id"
          class="bg-stone-900 border border-stone-800 rounded-xl p-4"
        >
          <div class="flex items-center justify-between">
            <div class="min-w-0 flex-1 mr-3">
              <p class="font-medium text-stone-100 truncate">{{ user.displayName }}</p>
              <p class="text-sm text-stone-500 truncate">{{ user.email }}</p>
              <p class="text-xs text-stone-600 mt-1">
                Joined {{ new Date(user.createdAt).toLocaleDateString() }}
              </p>
            </div>

            <div class="flex items-center gap-2 shrink-0">
              <button
                @click="toggleRole(user)"
                :disabled="user.id === auth.user?.id"
                class="text-xs px-3 py-1.5 rounded-full border transition-colors disabled:opacity-50"
                :class="user.role === 'admin'
                  ? 'border-amber-600 text-amber-500 hover:bg-amber-700/20'
                  : 'border-stone-700 text-stone-400 hover:bg-stone-800'"
              >
                {{ user.role }}
              </button>
            </div>
          </div>

          <!-- Action buttons -->
          <div class="flex gap-2 mt-3 pt-3 border-t border-stone-800">
            <button
              @click="startResetPassword(user.id)"
              class="text-xs px-3 py-1.5 rounded-lg bg-stone-800 text-stone-300 hover:bg-stone-700 transition-colors"
            >
              Reset Password
            </button>
            <button
              v-if="user.id !== auth.user?.id"
              @click="startDeleteUser(user.id)"
              class="text-xs px-3 py-1.5 rounded-lg bg-red-900/30 text-red-400 hover:bg-red-900/50 border border-red-800/50 transition-colors"
            >
              Delete
            </button>
          </div>
        </div>

        <p v-if="filteredUsers.length === 0" class="text-stone-500 text-center py-8">
          No users match your search
        </p>
      </div>
    </template>

    <!-- ── Prompts Tab (Read-Only) ────────────────── -->
    <template v-else-if="activeTab === 'prompts'">
      <div class="bg-stone-900/50 border border-stone-800 rounded-xl p-3 mb-4">
        <p class="text-xs text-stone-400">
          Agent prompts are managed as files in <code class="text-amber-500/80 bg-stone-800 px-1 rounded">AgentInitiator/Prompts/</code>.
          To change a prompt, edit the file and re-run the <code class="text-amber-500/80 bg-stone-800 px-1 rounded">agent:init</code> task.
        </p>
      </div>

      <div class="space-y-3">
        <div
          v-for="prompt in prompts"
          :key="prompt.id"
          class="bg-stone-900 border border-stone-800 rounded-xl p-4"
        >
          <div class="flex items-center justify-between mb-2 cursor-pointer" @click="togglePromptExpand(prompt.id)">
            <div>
              <p class="font-medium text-stone-100">{{ prompt.name }}</p>
              <p class="text-xs text-stone-500">{{ prompt.description }}</p>
            </div>
            <span class="text-stone-500 text-sm transition-transform" :class="expandedPromptId === prompt.id ? 'rotate-180' : ''">
              &#9662;
            </span>
          </div>

          <div v-if="expandedPromptId === prompt.id">
            <pre class="text-xs text-stone-400 bg-stone-950 rounded-lg p-3 whitespace-pre-wrap max-h-60 overflow-y-auto border border-stone-800">{{ prompt.content }}</pre>
            <p class="text-xs text-stone-600 mt-2">
              Last updated {{ new Date(prompt.updatedAt).toLocaleString() }}
              <span v-if="prompt.updatedBy"> by {{ prompt.updatedBy }}</span>
            </p>
          </div>
        </div>

        <p v-if="prompts.length === 0" class="text-stone-500 text-center py-8">
          No prompts configured. Start the API to seed defaults.
        </p>
      </div>
    </template>

    <!-- ── Foundry Tab ───────────────────────────── -->
    <template v-else-if="activeTab === 'foundry'">
      <div v-if="foundryStatus" class="space-y-4">
        <!-- Configuration -->
        <div class="bg-stone-900 border border-stone-800 rounded-xl p-4">
          <h3 class="text-sm font-semibold text-stone-200 mb-3">Configuration</h3>
          <div class="space-y-2">
            <div class="flex justify-between text-xs">
              <span class="text-stone-500">Project Endpoint</span>
              <span class="text-stone-300 font-mono truncate ml-4 max-w-[60%] text-right">{{ foundryStatus.projectEndpoint || 'Not configured' }}</span>
            </div>
            <div class="flex justify-between text-xs">
              <span class="text-stone-500">Vision Model</span>
              <span class="text-stone-300 font-mono">{{ foundryStatus.visionModel || '—' }}</span>
            </div>
            <div class="flex justify-between text-xs">
              <span class="text-stone-500">Reasoning Model</span>
              <span class="text-stone-300 font-mono">{{ foundryStatus.reasoningModel || '—' }}</span>
            </div>
            <div class="flex justify-between text-xs">
              <span class="text-stone-500">Status</span>
              <span :class="foundryStatus.isProjectConfigured ? 'text-green-400' : 'text-red-400'" class="font-medium">
                {{ foundryStatus.isProjectConfigured ? 'Configured' : 'Not Configured' }}
              </span>
            </div>
          </div>
        </div>

        <!-- Agent Validation -->
        <div v-if="foundryStatus.agentValidation && foundryStatus.agentValidation.foundAgents.length > 0" class="bg-stone-900 border border-stone-800 rounded-xl p-4">
          <h3 class="text-sm font-semibold text-stone-200 mb-3">Registered Agents</h3>
          <div class="space-y-2">
            <div
              v-for="agentName in foundryStatus.agentValidation.foundAgents"
              :key="agentName"
              class="flex items-center justify-between text-xs py-1 border-b border-stone-800 last:border-b-0"
            >
              <span class="text-stone-300 font-mono">{{ agentName }}</span>
              <span class="text-green-400 font-medium">Valid</span>
            </div>
            <div
              v-for="agentName in foundryStatus.agentValidation.missingAgents"
              :key="agentName"
              class="flex items-center justify-between text-xs py-1 border-b border-stone-800 last:border-b-0"
            >
              <span class="text-stone-300 font-mono">{{ agentName }}</span>
              <span class="text-red-400 font-medium">Not Found</span>
            </div>
          </div>
        </div>

        <!-- Connectivity Test -->
        <div class="bg-stone-900 border border-stone-800 rounded-xl p-4">
          <div class="flex items-center justify-between mb-3">
            <h3 class="text-sm font-semibold text-stone-200">Connectivity Test</h3>
            <button
              @click="testFoundryConnectivity"
              :disabled="foundryTesting || !foundryStatus.isProjectConfigured"
              class="text-xs px-4 py-2 rounded-xl bg-amber-700 text-white hover:bg-amber-600 transition-colors disabled:opacity-50"
            >
              {{ foundryTesting ? 'Testing...' : 'Test Connection' }}
            </button>
          </div>

          <div v-if="foundryStatus.connectivityTest" class="text-xs space-y-1">
            <div class="flex justify-between">
              <span class="text-stone-500">Status</span>
              <span :class="foundryStatus.connectivityTest.status === 'ok' ? 'text-green-400' : 'text-red-400'" class="font-medium">
                {{ foundryStatus.connectivityTest.status === 'ok' ? 'Connected' : 'Failed' }}
              </span>
            </div>
            <div class="flex justify-between">
              <span class="text-stone-500">Latency</span>
              <span class="text-stone-300">{{ foundryStatus.connectivityTest.latencyMs }}ms</span>
            </div>
            <div v-if="foundryStatus.connectivityTest.message" class="mt-2">
              <p class="text-stone-400 bg-stone-950 rounded-lg p-2 break-words">{{ foundryStatus.connectivityTest.message }}</p>
            </div>
            <p class="text-stone-600 mt-1">
              Tested {{ new Date(foundryStatus.connectivityTest.testedAt).toLocaleString() }}
            </p>
          </div>
          <p v-else class="text-xs text-stone-500">No test run yet. Click "Test Connection" to verify Foundry connectivity.</p>
        </div>
      </div>

      <p v-else class="text-stone-500 text-center py-8">
        Could not load Foundry status.
      </p>
    </template>

    <!-- ── Logging Tab ───────────────────────────── -->
    <template v-else-if="activeTab === 'logging'">
      <div v-if="loggingData" class="space-y-4">
        <div class="bg-stone-900 border border-stone-800 rounded-xl p-4">
          <p class="text-sm text-stone-400 mb-4">
            Configure log verbosity per category. Changes take effect immediately without restart.
          </p>

          <!-- Default level -->
          <div class="flex items-center justify-between py-3 border-b border-stone-800">
            <div>
              <p class="font-medium text-stone-100">Default</p>
              <p class="text-xs text-stone-500">Catch-all for uncategorized loggers</p>
            </div>
            <select
              v-model="editedDefaultLevel"
              :class="getLevelColor(editedDefaultLevel)"
              class="bg-stone-800 border border-stone-700 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:border-amber-700"
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
            class="flex items-center justify-between py-3 border-b border-stone-800 last:border-b-0"
          >
            <div class="min-w-0 flex-1 mr-3">
              <p class="text-sm text-stone-200 font-mono truncate">{{ category }}</p>
            </div>
            <select
              v-model="editedLevels[category]"
              :class="getLevelColor(editedLevels[category])"
              class="bg-stone-800 border border-stone-700 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:border-amber-700"
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
              class="text-xs px-4 py-2 rounded-xl bg-stone-800 text-stone-400 hover:bg-stone-700 transition-colors"
            >
              Reset
            </button>
            <button
              @click="saveLoggingSettings"
              :disabled="loggingSaving"
              class="text-xs px-4 py-2 rounded-xl bg-amber-700 text-white hover:bg-amber-600 transition-colors disabled:opacity-50"
            >
              {{ loggingSaving ? 'Saving...' : 'Save Log Levels' }}
            </button>
          </div>
        </div>
      </div>

      <p v-else class="text-stone-500 text-center py-8">
        Could not load logging settings.
      </p>
    </template>

    <!-- ── Reset Password Modal ──────────────────── -->
    <Teleport to="body">
      <div v-if="resetPasswordUserId" class="fixed inset-0 bg-black/60 flex items-center justify-center z-50 p-4" @click.self="resetPasswordUserId = null">
        <div class="bg-stone-900 border border-stone-700 rounded-2xl p-6 w-full max-w-sm">
          <h3 class="text-lg font-semibold text-stone-100 mb-4">Reset Password</h3>
          <p class="text-sm text-stone-400 mb-3">
            Enter a new password for {{ users.find(u => u.id === resetPasswordUserId)?.displayName }}
          </p>
          <input
            v-model="newPassword"
            type="password"
            placeholder="New password (min 8 characters)"
            class="w-full bg-stone-800 border border-stone-700 rounded-xl px-4 py-3 text-stone-100 placeholder-stone-600 focus:outline-none focus:border-amber-700 mb-3"
            @keyup.enter="confirmResetPassword"
          />
          <p v-if="resetMessage" class="text-xs mb-3" :class="resetMessage.includes('Failed') ? 'text-red-400' : 'text-green-400'">
            {{ resetMessage }}
          </p>
          <div class="flex gap-2 justify-end">
            <button @click="resetPasswordUserId = null" class="px-4 py-2 text-sm rounded-xl bg-stone-800 text-stone-400 hover:bg-stone-700">Cancel</button>
            <button @click="confirmResetPassword" class="px-4 py-2 text-sm rounded-xl bg-amber-700 text-white hover:bg-amber-600">Reset</button>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- ── Delete Confirmation Modal ─────────────── -->
    <Teleport to="body">
      <div v-if="deleteConfirmUserId" class="fixed inset-0 bg-black/60 flex items-center justify-center z-50 p-4" @click.self="deleteConfirmUserId = null">
        <div class="bg-stone-900 border border-stone-700 rounded-2xl p-6 w-full max-w-sm">
          <h3 class="text-lg font-semibold text-red-400 mb-4">Delete User</h3>
          <p class="text-sm text-stone-300 mb-4">
            Are you sure you want to delete <strong>{{ users.find(u => u.id === deleteConfirmUserId)?.displayName }}</strong>? This action cannot be undone.
          </p>
          <div class="flex gap-2 justify-end">
            <button @click="deleteConfirmUserId = null" class="px-4 py-2 text-sm rounded-xl bg-stone-800 text-stone-400 hover:bg-stone-700">Cancel</button>
            <button @click="confirmDeleteUser" class="px-4 py-2 text-sm rounded-xl bg-red-700 text-white hover:bg-red-600">Delete</button>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>
