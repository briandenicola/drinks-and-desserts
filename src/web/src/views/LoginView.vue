<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { isEntraConfigured } from '../services/msal'
import { getErrorMessage } from '../services/errors'
import { authApi, type OIDCPublicProvider } from '../services/auth'

const auth = useAuthStore()
const route = useRoute()

const isRegister = ref(false)
const email = ref('')
const password = ref('')
const displayName = ref('')
const isSubmitting = ref(false)
const errorMessage = ref('')
const entraAvailable = ref(false)
const oidcProviders = ref<OIDCPublicProvider[]>([])
const oidcLoading = ref(false)
const oidcError = ref('')
const startingProviderId = ref<string | null>(null)

let entraTimer: ReturnType<typeof setTimeout> | undefined

onMounted(() => {
  oidcError.value = getOidcCallbackErrorMessage()
  loadOidcProviders()

  entraTimer = setTimeout(() => {
    entraAvailable.value = isEntraConfigured()
  }, 500)
})

onBeforeUnmount(() => {
  clearTimeout(entraTimer)
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

async function loadOidcProviders() {
  oidcLoading.value = true
  try {
    const response = await authApi.getOidcPublicProviders()
    oidcProviders.value = response.data.providers ?? []
  } catch {
    oidcProviders.value = []
  } finally {
    oidcLoading.value = false
  }
}

async function signInWithOidc(provider: OIDCPublicProvider) {
  oidcError.value = ''
  startingProviderId.value = provider.id
  try {
    const response = await authApi.startOidcLogin(provider.id, {
      redirectPath: '/',
      callbackPath: `/auth/oidc/callback/${provider.id}`,
    })

    if (!response.data.authorizationUrl) {
      oidcError.value = 'The sign-in provider did not return an authorization URL. Ask an administrator to check provider configuration.'
      return
    }

    window.location.assign(response.data.authorizationUrl)
  } catch (e: unknown) {
    oidcError.value = mapOidcError(e)
  } finally {
    startingProviderId.value = null
  }
}

function oidcButtonLabel(provider: OIDCPublicProvider) {
  const label = provider.displayName.trim() || provider.name || 'OIDC'
  return `Sign in with ${label}`
}

function getOidcCallbackErrorMessage() {
  const category = firstQueryValue('oidc_error') || firstQueryValue('error')
  const status = firstQueryValue('status')
  const message = firstQueryValue('message')

  if (category) return mapOidcErrorCategory(category, status, message)
  if (status && ['400', '401', '409', '500'].includes(status)) {
    return mapOidcErrorCategory('', status, message)
  }
  return ''
}

function firstQueryValue(name: string) {
  const value = route.query[name]
  if (Array.isArray(value)) return value[0] ?? ''
  return value?.toString() ?? ''
}

function mapOidcError(error: unknown) {
  const response = getErrorResponse(error)
  const message = getErrorMessage(error, '')
  return mapOidcErrorCategory(message, String(response?.status ?? ''), message)
}

function mapOidcErrorCategory(category: string, status?: string, message?: string) {
  const normalized = category.toLowerCase()
  if (normalized.includes('access_denied') || normalized.includes('denied') || normalized.includes('cancel')) {
    return 'Sign-in was cancelled or denied at the provider. You can try again or use your local password.'
  }
  if (normalized.includes('conflict') || status === '409') {
    return 'This provider account matches an existing local account. Sign in locally, then link the provider from Profile.'
  }
  if (normalized.includes('misconfig') || normalized.includes('configuration') || normalized.includes('discovery') || status === '500') {
    if (normalized.includes('public origin')) {
      return 'OIDC public web origin is not configured. Sign in locally and set it in Admin Panel > Settings.'
    }
    return 'The sign-in provider is not configured correctly. Ask an administrator to test the provider settings.'
  }
  if (normalized.includes('validation') || normalized.includes('state') || normalized.includes('nonce') || normalized.includes('issuer') || normalized.includes('audience') || normalized.includes('signature') || status === '400' || status === '401') {
    return 'The provider response could not be validated. Try again, or ask an administrator to review the provider setup.'
  }
  return message?.trim() || 'OIDC sign-in failed. Try again or use your local password.'
}

function getErrorResponse(error: unknown): { status?: number } | null {
  if (typeof error !== 'object' || error === null || !('response' in error)) return null
  const response = (error as { response?: unknown }).response
  if (typeof response !== 'object' || response === null) return null
  return response as { status?: number }
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

    <div v-if="oidcProviders.length || oidcLoading || oidcError" class="w-full max-w-sm mb-6">
      <div v-if="!entraAvailable" class="flex items-center gap-4 mb-6">
        <div class="flex-1 border-t border-[#1e407c]/50"></div>
        <span class="text-[#96BEE6]/70 text-sm">or use a secure provider</span>
        <div class="flex-1 border-t border-[#1e407c]/50"></div>
      </div>

      <div v-if="oidcError" class="bg-red-900/40 border border-red-700 text-red-300 px-4 py-3 rounded-xl text-sm mb-3">
        {{ oidcError }}
      </div>

      <button
        v-for="provider in oidcProviders"
        :key="provider.id"
        type="button"
        @click="signInWithOidc(provider)"
        :disabled="isSubmitting || oidcLoading || startingProviderId === provider.id"
        class="w-full flex items-center justify-center gap-3 bg-[#041e3e] hover:bg-[#0a2a52] disabled:bg-[#0a2a52] disabled:text-[#4a7aa5]/60 text-[#96BEE6] font-semibold py-4 rounded-xl transition-colors text-lg border border-[#1e407c]/50 mb-3"
      >
        <span class="inline-flex h-5 w-5 items-center justify-center rounded-full border border-[#96BEE6]/70 text-xs">↗</span>
        {{ startingProviderId === provider.id ? 'Redirecting...' : oidcButtonLabel(provider) }}
      </button>

      <p v-if="oidcLoading" class="text-[#96BEE6]/70 text-sm text-center">Loading sign-in providers...</p>
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
