<script setup lang="ts">
import { onMounted } from 'vue'
import { useAuthStore } from './stores/auth'
import AppLayout from './components/common/AppLayout.vue'

const auth = useAuthStore()

onMounted(() => {
  auth.initialize()
})
</script>

<template>
  <div v-if="auth.isLoading" class="flex items-center justify-center min-h-screen">
    <div class="text-th-accent/70">Loading...</div>
  </div>

  <AppLayout v-else>
    <RouterView />
  </AppLayout>
</template>
