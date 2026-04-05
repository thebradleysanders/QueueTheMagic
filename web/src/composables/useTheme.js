import { watch } from 'vue'
import { useShowStore } from '@/stores/show'

export function useTheme() {
  const showStore = useShowStore()

  function applyTheme(theme) {
    const root = document.documentElement
    if (theme === 'dark') {
      root.classList.add('dark')
    } else {
      root.classList.remove('dark')
    }
    localStorage.setItem('theme', theme)
  }

  function toggleTheme() {
    const next = showStore.config.theme === 'dark' ? 'light' : 'dark'
    showStore.config.theme = next
    applyTheme(next)
  }

  watch(() => showStore.config.theme, (t) => applyTheme(t), { immediate: true })

  return { toggleTheme }
}
