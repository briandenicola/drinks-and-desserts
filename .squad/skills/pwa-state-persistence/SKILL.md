# PWA State Persistence

**Category:** State Management  
**Applies to:** Vue 3.5, Pinia, Virtual Scrolling  
**Last updated:** 2026-06-19

## Overview

Pattern for preserving list/page state across navigation in PWA mode, providing native-app-like experience while maintaining fresh-data behavior in browser mode.

## When to Use

- List views with pagination/virtual scrolling
- PWA-enabled apps where users expect "native app" behavior
- Views that load expensive data you want to preserve when user navigates to detail and back

## Implementation

### 1. Create PWA Detection Composable

```typescript
// composables/usePwa.ts
import { computed } from 'vue'

export function usePwa() {
  const isPwa = computed(() => {
    return (
      window.matchMedia('(display-mode: standalone)').matches ||
      (window.navigator as Navigator & { standalone?: boolean }).standalone === true
    )
  })

  return { isPwa }
}
```

### 2. Extend Store with Persistence State

```typescript
// stores/items.ts
export const useItemsStore = defineStore('items', () => {
  const items = ref<Item[]>([])
  const hasInitialLoad = ref(false)
  const scrollPosition = ref(0)
  // ... other state

  async function loadItems(
    type?: string,
    reset = false,
    sortBy?: string,
    sortDirection?: 'asc' | 'desc',
    groupBy?: string,
    skipIfLoaded = false  // ← Add this
  ) {
    // Skip load if already loaded and skipIfLoaded is true (PWA mode)
    if (skipIfLoaded && hasInitialLoad.value && items.value.length > 0) {
      return
    }

    if (reset || sortChanged) {
      items.value = []
      hasInitialLoad.value = false
      scrollPosition.value = 0
    }

    // ... load logic
    hasInitialLoad.value = true
  }

  return {
    items, hasInitialLoad, scrollPosition, loadItems,
    // ... other exports
  }
})
```

### 3. Update View with PWA-Aware Lifecycle

```typescript
// views/ItemsView.vue
import { usePwa } from '../composables/usePwa'

const { isPwa } = usePwa()
const itemsStore = useItemsStore()
const virtualizer = useVirtualizer(/* ... */)

onMounted(() => {
  const skipIfLoaded = isPwa.value
  await itemsStore.loadItems(filter, true, sort, direction, groupBy, skipIfLoaded)
  
  // Restore scroll position in PWA mode
  if (skipIfLoaded && itemsStore.hasInitialLoad) {
    requestAnimationFrame(() => {
      virtualizer.value.scrollToOffset(itemsStore.scrollPosition, { behavior: 'auto' })
    })
  }
})

onUnmounted(() => {
  // Save scroll position in PWA mode
  if (isPwa.value) {
    itemsStore.scrollPosition = virtualizer.value.scrollOffset ?? 0
  }
})
```

## What Gets Persisted

- ✅ Loaded records (items/venues arrays)
- ✅ Continuation tokens (pagination state)
- ✅ Active filters, sort, groupBy settings
- ✅ Scroll position (virtual scrolling offset)
- ✅ Tab state (if applicable, e.g., collection vs wishlist)

## Key Considerations

- **PWA-only:** Preserving state globally could mask server-side changes; PWA mode has offline/caching expectations
- **Virtual scrolling:** Requires explicit `scrollToOffset` call in `requestAnimationFrame` to restore position
- **skipIfLoaded:** Prevents unnecessary API calls while allowing explicit refresh via user action
- **Unmount timing:** Save scroll position in `onUnmounted` before virtualizer is destroyed

## Alternatives

- **LocalStorage:** Adds serialization complexity; Pinia stores already in-memory
- **Router scroll behavior:** Doesn't preserve loaded data/pagination state
- **Global persistence:** Would mask updates in browser mode

## Related Patterns

- Server-side sorting/grouping (server is source of truth)
- Pull-to-refresh (explicit user action to reload)
- Virtual scrolling with infinite scroll

## Examples

- `src/web/src/stores/items.ts` — Dual collection/wishlist state
- `src/web/src/stores/venues.ts` — Single list state
- `src/web/src/views/ItemsView.vue` — Tab-aware persistence
- `src/web/src/views/VenuesView.vue` — Simple list persistence
