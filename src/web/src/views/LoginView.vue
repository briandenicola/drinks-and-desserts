<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAuthStore } from '../stores/auth'
import { isEntraConfigured } from '../services/msal'
import { getErrorMessage } from '../services/errors'

const auth = useAuthStore()

const isRegister = ref(false)
const email = ref('')
const password = ref('')
const displayName = ref('')
const isSubmitting = ref(false)
const errorMessage = ref('')
const entraAvailable = ref(false)

onMounted(() => {
  // Check after a short delay to allow MSAL init to complete
  setTimeout(() => {
    entraAvailable.value = isEntraConfigured()
  }, 500)
})

async function submit() {
  isSubmitting.value = true
  errorMessage.value = ''

  try {
    if (isRegister.value) {
      await auth.register({
        email: email.value,
        password: password.value,
        displayName: displayName.value,
      })
    } else {
      await auth.login({
        email: email.value,
        password: password.value,
      })
    }
  } catch (e: unknown) {
    errorMessage.value = getErrorMessage(e, isRegister.value ? 'Registration failed' : 'Login failed')
  } finally {
    isSubmitting.value = false
  }
}

async function signInWithMicrosoft() {
  isSubmitting.value = true
  errorMessage.value = ''
  try {
    await auth.loginEntra()
  } catch (e: unknown) {
    errorMessage.value = getErrorMessage(e, 'Microsoft sign-in failed')
  } finally {
    isSubmitting.value = false
  }
}

function toggleMode() {
  isRegister.value = !isRegister.value
  errorMessage.value = ''
}
</script>

<template>
  <div class="flex flex-col items-center justify-center min-h-screen px-6">
    <div class="text-center mb-10">
      <h1 class="text-5xl font-bold text-[#96BEE6] mb-2">Drinks &amp; Desserts</h1>
      <p class="text-[#96BEE6] text-lg">Track your drinks, desserts & cigars</p>
    </div>

    <!-- Microsoft sign-in -->
    <div v-if="entraAvailable" class="w-full max-w-sm mb-6">
      <button
        @click="signInWithMicrosoft"
        :disabled="isSubmitting"
        class="w-full flex items-center justify-center gap-3 bg-white hover:bg-gray-100 disabled:bg-gray-300 text-gray-800 font-semibold py-4 rounded-xl transition-colors text-lg border border-gray-300"
      >
        <svg xmlns="http://www.w3.org/2000/svg" width="21" height="21" viewBox="0 0 21 21">
          <rect x="1" y="1" width="9" height="9" fill="#f25022"/>
          <rect x="11" y="1" width="9" height="9" fill="#7fba00"/>
          <rect x="1" y="11" width="9" height="9" fill="#00a4ef"/>
          <rect x="11" y="11" width="9" height="9" fill="#ffb900"/>
        </svg>
        Sign in with Microsoft
      </button>

      <div class="flex items-center gap-4 my-6">
        <div class="flex-1 border-t border-[#1e407c]/50"></div>
        <span class="text-[#96BEE6]/70 text-sm">or use a local account</span>
        <div class="flex-1 border-t border-[#1e407c]/50"></div>
      </div>
    </div>

    <form @submit.prevent="submit" class="w-full max-w-sm space-y-4">
      <h2 class="text-xl font-semibold text-center mb-2">
        {{ isRegister ? 'Create Account' : 'Sign In' }}
      </h2>

      <!-- Error message -->
      <div v-if="errorMessage" class="bg-red-900/40 border border-red-700 text-red-300 px-4 py-3 rounded-xl text-sm">
        {{ errorMessage }}
      </div>

      <!-- Display name (register only) -->
      <div v-if="isRegister">
        <label class="block text-sm text-[#96BEE6] mb-1">Display Name</label>
        <input
          v-model="displayName"
          type="text"
          required
          autocomplete="name"
          placeholder="Your name"
          class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-3 text-white placeholder-[#4a7aa5] focus:outline-none focus:border-[#1e407c]"
        />
      </div>

      <!-- Email -->
      <div>
        <label class="block text-sm text-[#96BEE6] mb-1">Email</label>
        <input
          v-model="email"
          type="email"
          required
          autocomplete="email"
          placeholder="you@example.com"
          class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-3 text-white placeholder-[#4a7aa5] focus:outline-none focus:border-[#1e407c]"
        />
      </div>

      <!-- Password -->
      <div>
        <label class="block text-sm text-[#96BEE6] mb-1">Password</label>
        <input
          v-model="password"
          type="password"
          required
          minlength="8"
          autocomplete="current-password"
          placeholder="Min 8 characters"
          class="w-full bg-[#0a2a52] border border-[#1e407c]/50 rounded-xl px-4 py-3 text-white placeholder-[#4a7aa5] focus:outline-none focus:border-[#1e407c]"
        />
      </div>

      <!-- Submit -->
      <button
        type="submit"
        :disabled="isSubmitting"
        class="w-full bg-[#1e407c] hover:bg-[#2a5299] disabled:bg-[#1e407c] disabled:text-[#96BEE6]/70 text-white font-semibold py-4 rounded-xl transition-colors text-lg"
      >
        {{ isSubmitting ? 'Please wait...' : (isRegister ? 'Create Account' : 'Sign In') }}
      </button>

      <!-- Toggle register/login -->
      <p class="text-center text-sm text-[#96BEE6]/70">
        {{ isRegister ? 'Already have an account?' : "Don't have an account?" }}
        <button type="button" @click="toggleMode" class="text-[#96BEE6] hover:text-[#96BEE6] ml-1">
          {{ isRegister ? 'Sign In' : 'Create Account' }}
        </button>
      </p>
    </form>

    <p class="text-[#4a7aa5]/60 text-sm mt-8 text-center max-w-xs">
      Snap a photo at the bar. Let AI do the rest. Refine later.
    </p>
  </div>
</template>
