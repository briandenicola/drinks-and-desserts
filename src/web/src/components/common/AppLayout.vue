<script setup lang="ts">
import { useAuthStore } from '../../stores/auth'
import { useRoute } from 'vue-router'

const auth = useAuthStore()
const route = useRoute()

const navItems = [
  { name: 'Capture', path: '/', icon: '📸' },
  { name: 'Collection', path: '/items', icon: '🥃' },
  { name: 'History', path: '/history', icon: '📋' },
  { name: 'Profile', path: '/profile', icon: '👤' },
]
</script>

<template>
  <div class="min-h-screen flex flex-col">
    <!-- Header -->
    <header class="bg-stone-900 border-b border-stone-800 px-4 py-3 flex items-center justify-between">
      <h1 class="text-lg font-bold text-amber-500">Whiskey & Smokes</h1>
      <button
        v-if="auth.isAuthenticated"
        @click="auth.logout()"
        class="text-sm text-stone-400 hover:text-stone-200"
      >
        Sign Out
      </button>
    </header>

    <!-- Main Content -->
    <main class="flex-1 overflow-y-auto pb-20">
      <slot />
    </main>

    <!-- Bottom Navigation (mobile) -->
    <nav v-if="auth.isAuthenticated" class="fixed bottom-0 inset-x-0 bg-stone-900 border-t border-stone-800 flex justify-around py-2 safe-area-bottom">
      <router-link
        v-for="item in navItems"
        :key="item.path"
        :to="item.path"
        class="flex flex-col items-center px-3 py-1 text-xs transition-colors"
        :class="route.path === item.path ? 'text-amber-500' : 'text-stone-500'"
      >
        <span class="text-xl mb-0.5">{{ item.icon }}</span>
        <span>{{ item.name }}</span>
      </router-link>
    </nav>
  </div>
</template>

<style scoped>
.safe-area-bottom {
  padding-bottom: max(0.5rem, env(safe-area-inset-bottom));
}
</style>
