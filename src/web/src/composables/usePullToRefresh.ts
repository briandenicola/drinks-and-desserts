import { ref, type Ref } from 'vue'

const PULL_THRESHOLD = 80
const MAX_PULL = 120

export interface PullToRefreshState {
  isPulling: Ref<boolean>
  isRefreshing: Ref<boolean>
  pullDistance: Ref<number>
  pullProgress: Ref<number>
}

/**
 * Composable for pull-to-refresh gesture detection.
 * Attach the returned handlers to the scrollable container.
 * Call setRefreshCallback to register the async function invoked on pull.
 */
export function usePullToRefresh(): PullToRefreshState & {
  setRefreshCallback: (fn: (() => Promise<void>) | null) => void
  onTouchStart: (e: TouchEvent) => void
  onTouchMove: (e: TouchEvent) => void
  onTouchEnd: () => void
} {
  const isPulling = ref(false)
  const isRefreshing = ref(false)
  const pullDistance = ref(0)
  const pullProgress = ref(0)

  let startY = 0
  let refreshCallback: (() => Promise<void>) | null = null

  function setRefreshCallback(fn: (() => Promise<void>) | null) {
    refreshCallback = fn
  }

  function onTouchStart(e: TouchEvent) {
    if (isRefreshing.value) return
    // Check scroll position of the element the handler is attached to
    const target = e.currentTarget as HTMLElement | null
    const scrollTop = target
      ? target.scrollTop
      : document.documentElement.scrollTop || document.body.scrollTop
    if (scrollTop <= 0) {
      startY = e.touches[0].clientY
      isPulling.value = true
    }
  }

  function onTouchMove(e: TouchEvent) {
    if (!isPulling.value || isRefreshing.value) return
    const currentY = e.touches[0].clientY
    const delta = currentY - startY
    if (delta > 0) {
      pullDistance.value = Math.min(delta * 0.5, MAX_PULL)
      pullProgress.value = Math.min(pullDistance.value / PULL_THRESHOLD, 1)
    } else {
      pullDistance.value = 0
      pullProgress.value = 0
    }
  }

  async function onTouchEnd() {
    if (!isPulling.value || isRefreshing.value) return

    if (pullDistance.value >= PULL_THRESHOLD && refreshCallback) {
      isRefreshing.value = true
      pullDistance.value = 50
      try {
        await refreshCallback()
      } finally {
        isRefreshing.value = false
      }
    }

    isPulling.value = false
    pullDistance.value = 0
    pullProgress.value = 0
  }

  return {
    isPulling,
    isRefreshing,
    pullDistance,
    pullProgress,
    setRefreshCallback,
    onTouchStart,
    onTouchMove,
    onTouchEnd,
  }
}
