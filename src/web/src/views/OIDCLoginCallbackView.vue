<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { authApi } from '../services/auth'
import { getErrorMessage } from '../services/errors'
import { useAuthStore } from '../stores/auth'

type CallbackStatus = 'loading' | 'success' | 'error'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const status = ref<CallbackStatus>('loading')
const message = ref('')

const title = computed(() => {
  if (status.value === 'loading') return 'Signing You In'
  if (status.value === 'success') return 'Sign In Complete'
  return 'Sign In Failed'
})

const subtitle = computed(() => {
  if (status.value === 'loading') return 'Finishing the secure provider handshake...'
  if (status.value === 'success') return 'Your secure session is ready.'
  return 'The provider sign-in could not be completed.'
})

onMounted(() => {
  completeCallback()
})

async function completeCallback() {
  const providerIdParam = firstParamValue(route.params.providerId)
  const providerId = providerIdParam?.trim() ?? ''
  const code = firstQueryValue('code')
  const state = firstQueryValue('state')
  const providerError = firstQueryValue('error') || firstQueryValue('error_description')

  router.replace({ name: 'OidcLoginCallback', params: { providerId: providerIdParam ?? '' } })

  if (!providerId) {
    setError('The provider callback was missing a valid provider. Start sign-in again.')
    return
  }

  if (providerError) {
    setError(mapProviderError(providerError))
    return
  }

  if (!code || !state) {
    setError('The provider callback was incomplete. Start sign-in again.')
    return
  }

  try {
    const response = await authApi.completeOidcLoginCallback(providerId, code, state)
    auth.applyAuthResponse(response.data)
    message.value = 'You are signed in. Continue to your collection.'
    status.value = 'success'
  } catch (e: unknown) {
    setError(mapCallbackError(e))
  }
}

function continueToApp() {
  router.replace('/')
}

function setError(text: string) {
  status.value = 'error'
  message.value = text
}

function firstQueryValue(name: string) {
  const value = route.query[name]
  if (Array.isArray(value)) return value[0] ?? ''
  return value?.toString() ?? ''
}

function firstParamValue(value: string | string[] | undefined) {
  if (Array.isArray(value)) return value[0] ?? ''
  return value
}

function mapProviderError(error: string) {
  const normalized = error.toLowerCase()
  if (normalized.includes('access_denied') || normalized.includes('cancel') || normalized.includes('denied')) {
    return 'Sign-in was cancelled or denied at the provider. You can try again or use your local password.'
  }
  return 'The provider returned an error before sign-in completed. Try again or ask an administrator to review the provider setup.'
}

function mapCallbackError(error: unknown) {
  const response = getErrorResponse(error)
  const messageText = getErrorMessage(error, '')
  const normalized = messageText.toLowerCase()

  if (response?.status === 409) {
    return 'This provider account matches an existing local account. Sign in locally, then link the provider from Profile.'
  }

  if (normalized.includes('state') || normalized.includes('claims') || response?.status === 400 || response?.status === 401) {
    return 'The provider response could not be validated. Start sign-in again.'
  }

  if (normalized.includes('configuration') || normalized.includes('discovery') || response?.status === 500) {
    if (normalized.includes('public origin')) {
      return 'OIDC public web origin is not configured. Sign in locally and set it in Admin Panel > Settings.'
    }
    return 'The sign-in provider is not configured correctly. Ask an administrator to test the provider settings.'
  }

  return messageText || 'OIDC sign-in failed. Try again or use your local password.'
}

function getErrorResponse(error: unknown): { status?: number } | null {
  if (typeof error !== 'object' || error === null || !('response' in error)) return null
  const response = (error as { response?: unknown }).response
  if (typeof response !== 'object' || response === null) return null
  return response as { status?: number }
}
</script>

<template>
  <div class="flex min-h-screen items-center justify-center px-6">
    <div class="w-full max-w-sm rounded-2xl border border-[#0a2a52] bg-[#041e3e] p-6 text-center shadow-xl">
      <div
        class="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full border"
        :class="{
          'animate-spin border-[#96BEE6]/30 border-t-[#96BEE6]': status === 'loading',
          'border-green-500 text-green-400': status === 'success',
          'border-red-500 text-red-400': status === 'error',
        }"
      >
        <span v-if="status === 'success'">✓</span>
        <span v-else-if="status === 'error'">!</span>
      </div>

      <h1 class="mb-2 text-2xl font-semibold text-white">{{ title }}</h1>
      <p class="mb-4 text-sm text-[#96BEE6]/80">{{ subtitle }}</p>

      <p v-if="message" class="mb-4 text-sm" :class="status === 'error' ? 'text-red-300' : 'text-[#96BEE6]'">
        {{ message }}
      </p>

      <button
        v-if="status === 'success'"
        type="button"
        @click="continueToApp"
        class="w-full rounded-xl bg-[#1e407c] py-3 font-medium text-white transition-colors hover:bg-[#2a5299]"
      >
        Continue to Collection
      </button>
      <router-link
        v-else-if="status === 'error'"
        to="/login"
        class="block w-full rounded-xl border border-[#1e407c]/50 bg-[#0a2a52] py-3 font-medium text-[#96BEE6] transition-colors hover:bg-[#1e407c]"
      >
        Back to Sign In
      </router-link>
    </div>
  </div>
</template>
