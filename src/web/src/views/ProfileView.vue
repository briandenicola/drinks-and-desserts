<script setup lang="ts">
import { ref, inject, onMounted } from 'vue'
import { useAuthStore } from '../stores/auth'
import { usersApi } from '../services/users'
import { RefreshKey } from '../composables/refreshKey'

const auth = useAuthStore()
const displayName = ref('')
const isSaving = ref(false)
const saveMessage = ref('')

// Password change
const currentPassword = ref('')
const newPassword = ref('')
const confirmPassword = ref('')
const isChangingPassword = ref(false)
const passwordMessage = ref('')
const passwordError = ref(false)

const registerRefresh = inject(RefreshKey)
registerRefresh?.(async () => {
  await auth.loadUser()
  if (auth.user) displayName.value = auth.user.displayName
})

onMounted(() => {
  if (auth.user) {
    displayName.value = auth.user.displayName
  }
})

async function saveProfile() {
  isSaving.value = true
  saveMessage.value = ''
  try {
    await usersApi.updateMe({ displayName: displayName.value })
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
