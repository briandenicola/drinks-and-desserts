import { ref } from 'vue'
import { useBreakpoint } from './useBreakpoint'

export function useDesktopLayout() {
  const { isDesktop } = useBreakpoint()
  const sidebarCollapsed = ref(false)

  function toggleSidebar() {
    sidebarCollapsed.value = !sidebarCollapsed.value
  }

  return { isDesktop, sidebarCollapsed, toggleSidebar }
}
