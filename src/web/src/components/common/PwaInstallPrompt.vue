<script setup lang="ts">
import { ref, onMounted } from 'vue'

const show = ref(false)
const DISMISSED_KEY = 'pwa-install-dismissed'

onMounted(() => {
  const isIos = /iphone|ipad|ipod/i.test(navigator.userAgent)
  const isStandalone = ('standalone' in window.navigator && (window.navigator as any).standalone) ||
    window.matchMedia('(display-mode: standalone)').matches
  const wasDismissed = localStorage.getItem(DISMISSED_KEY)

  if (isIos && !isStandalone && !wasDismissed) {
    show.value = true
  }
})

function dismiss() {
  show.value = false
  localStorage.setItem(DISMISSED_KEY, Date.now().toString())
}
</script>

<template>
  <Transition name="slide-up">
    <div v-if="show" class="fixed bottom-20 inset-x-0 z-50 flex justify-center px-4">
      <div class="bg-[#041e3e] border border-[#1e407c] rounded-xl shadow-lg max-w-sm w-full p-4">
        <div class="flex items-start gap-3">
          <div class="flex-shrink-0 mt-0.5">
            <svg xmlns="http://www.w3.org/2000/svg" class="w-6 h-6 text-[#96BEE6]" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
            </svg>
          </div>
          <div class="flex-1">
            <p class="text-sm text-white font-medium mb-1">Install Drinks & Desserts</p>
            <p class="text-xs text-[#5a8ab5] leading-relaxed">
              Tap the
              <svg xmlns="http://www.w3.org/2000/svg" class="inline w-4 h-4 text-[#96BEE6] align-text-bottom" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
              </svg>
              share button, then "Add to Home Screen" for the best experience.
            </p>
          </div>
          <button @click="dismiss" class="flex-shrink-0 text-[#4a7aa5] hover:text-white p-1">
            <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
      </div>
    </div>
  </Transition>
</template>

<style scoped>
.slide-up-enter-active,
.slide-up-leave-active {
  transition: all 0.3s ease;
}
.slide-up-enter-from,
.slide-up-leave-to {
  opacity: 0;
  transform: translateY(1rem);
}
</style>
