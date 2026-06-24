<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { authApi, type OIDCLinkedIdentity } from '../services/auth'
import { getErrorMessage } from '../services/errors'

type CallbackStatus = 'loading' | 'success' | 'error'

const route = useRoute()
const router = useRouter()

const status = ref<CallbackStatus>('loading')
const identity = ref<OIDCLinkedIdentity | null>(null)
const message = ref('')

const title = computed(() => {
  if (status.value === 'loading') return 'Linking Sign-in Provider'
  if (status.value === 'success') return 'Provider Linked'
  return 'Linking Failed'
})

const subtitle = computed(() => {
  if (status.value === 'loading') return 'Finishing the secure provider handshake...'
  if (status.value === 'success') return 'Your external sign-in provider is now connected to this account.'
  return 'The provider could not be linked to your account.'
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

  router.replace({ name: 'OidcLinkCallback', params: { providerId: providerIdParam ?? '' } })

  if (!providerId) {
    setError('The provider callback was missing a valid provider. Start linking again from Profile.')
    return
  }

  if (providerError) {
    setError(mapProviderError(providerError))
    return
  }

  if (!code || !state) {
    setError('The provider callback was incomplete. Start linking again from Profile.')
    return
  }

  try {
    const response = await authApi.completeOidcLinkCallback(providerId, code, state)
    identity.value = response.data.identity
    message.value = response.data.message || 'OIDC identity linked.'
    status.value = 'success'
  } catch (e: unknown) {
    setError(mapCallbackError(e))
  }
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
    return 'Linking was cancelled or denied at the provider. You can try again from Profile.'
  }
  return 'The provider returned an error before linking completed. Try again or ask an administrator to review the provider setup.'
}

function mapCallbackError(error: unknown) {
  const response = getErrorResponse(error)
  const messageText = getErrorMessage(error, '')
  const normalized = messageText.toLowerCase()

  if (response?.status === 409) {
    if (normalized.includes('another user') || normalized.includes('already linked')) {
      return 'This provider account is already linked to another user. Sign in with a different provider account or ask an administrator for help.'
    }
    return 'This provider account cannot be linked automatically. Sign in locally with the intended account, then try linking again.'
  }

  if (normalized.includes('state') || normalized.includes('claims') || response?.status === 400 || response?.status === 401) {
    return 'The provider response could not be validated. Start the linking flow again from Profile.'
  }

  if (normalized.includes('configuration') || normalized.includes('discovery') || response?.status === 500) {
    return 'The sign-in provider is not configured correctly. Ask an administrator to test the provider settings.'
  }

  return messageText || 'The provider could not be linked. Start the linking flow again from Profile.'
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

      <div v-if="identity" class="mb-4 rounded-xl border border-[#1e407c]/50 bg-[#0a2a52] p-3 text-left text-sm">
        <div class="flex justify-between gap-3">
          <span class="text-[#96BEE6]/70">Provider</span>
          <strong class="text-right text-white">{{ identity.providerDisplayName }}</strong>
        </div>
        <div v-if="identity.email" class="mt-2 flex justify-between gap-3">
          <span class="text-[#96BEE6]/70">Email</span>
          <strong class="text-right text-white break-all">{{ identity.email }}</strong>
        </div>
      </div>

      <p v-if="message" class="mb-4 text-sm" :class="status === 'error' ? 'text-red-300' : 'text-[#96BEE6]'">
        {{ message }}
      </p>

      <router-link
        to="/profile"
        class="block w-full rounded-xl bg-[#1e407c] py-3 font-medium text-white transition-colors hover:bg-[#2a5299]"
      >
        Back to Profile
      </router-link>
    </div>
  </div>
</template>
