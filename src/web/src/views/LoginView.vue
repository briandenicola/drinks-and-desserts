<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAuthStore } from '../stores/auth'
import { isEntraConfigured } from '../services/msal'

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
  } catch (e: any) {
    errorMessage.value = e.response?.data?.message ?? (isRegister.value ? 'Registration failed' : 'Login failed')
  } finally {
    isSubmitting.value = false
  }
}

async function signInWithMicrosoft() {
  isSubmitting.value = true
  errorMessage.value = ''
  try {
    await auth.loginEntra()
  } catch (e: any) {
    errorMessage.value = e.response?.data?.message ?? e.message ?? 'Microsoft sign-in failed'
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
      <h1 class="text-5xl font-bold text-amber-500 mb-2">Whiskey &amp; Smokes</h1>
      <p class="text-stone-400 text-lg">Track your whiskey, vodka, gin, wine, cocktails & cigars</p>
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
        <div class="flex-1 border-t border-stone-700"></div>
        <span class="text-stone-500 text-sm">or use a local account</span>
        <div class="flex-1 border-t border-stone-700"></div>
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
        <label class="block text-sm text-stone-400 mb-1">Display Name</label>
        <input
          v-model="displayName"
          type="text"
          required
          autocomplete="name"
          placeholder="Your name"
          class="w-full bg-stone-800 border border-stone-700 rounded-xl px-4 py-3 text-stone-100 placeholder-stone-600 focus:outline-none focus:border-amber-700"
        />
      </div>

      <!-- Email -->
      <div>
        <label class="block text-sm text-stone-400 mb-1">Email</label>
        <input
          v-model="email"
          type="email"
          required
          autocomplete="email"
          placeholder="you@example.com"
          class="w-full bg-stone-800 border border-stone-700 rounded-xl px-4 py-3 text-stone-100 placeholder-stone-600 focus:outline-none focus:border-amber-700"
        />
      </div>

      <!-- Password -->
      <div>
        <label class="block text-sm text-stone-400 mb-1">Password</label>
        <input
          v-model="password"
          type="password"
          required
          minlength="8"
          autocomplete="current-password"
          placeholder="Min 8 characters"
          class="w-full bg-stone-800 border border-stone-700 rounded-xl px-4 py-3 text-stone-100 placeholder-stone-600 focus:outline-none focus:border-amber-700"
        />
      </div>

      <!-- Submit -->
      <button
        type="submit"
        :disabled="isSubmitting"
        class="w-full bg-amber-700 hover:bg-amber-600 disabled:bg-stone-700 disabled:text-stone-500 text-white font-semibold py-4 rounded-xl transition-colors text-lg"
      >
        {{ isSubmitting ? 'Please wait...' : (isRegister ? 'Create Account' : 'Sign In') }}
      </button>

      <!-- Toggle register/login -->
      <p class="text-center text-sm text-stone-500">
        {{ isRegister ? 'Already have an account?' : "Don't have an account?" }}
        <button type="button" @click="toggleMode" class="text-amber-500 hover:text-amber-400 ml-1">
          {{ isRegister ? 'Sign In' : 'Create Account' }}
        </button>
      </p>
    </form>

    <p class="text-stone-600 text-sm mt-8 text-center max-w-xs">
      Snap a photo at the bar. Let AI do the rest. Refine later.
    </p>
  </div>
</template>
