<script setup>
import { onMounted, computed } from 'vue'
import { RouterLink, RouterView, useRoute } from 'vue-router'
import { useShowStore } from '@/stores/show'
import { useSignalR } from '@/composables/useSignalR'
import { useTheme } from '@/composables/useTheme'
import { version } from '../package.json'

const showStore = useShowStore()
useSignalR()
const { toggleTheme } = useTheme()

onMounted(() => {
  showStore.fetchNowPlaying()
  showStore.fetchQueue()
})

const route = useRoute()
const isAdmin = computed(() => route.path.startsWith('/admin'))
const pendingCount = computed(() => showStore.queue.filter(q => q.status === 'Pending').length)
const isDark = computed(() => showStore.config.theme !== 'light')

const platformIcon = {
  facebook: 'pi-facebook',
  instagram: 'pi-instagram',
  twitter: 'pi-twitter',
  youtube: 'pi-youtube',
  github: 'pi-github',
  linkedin: 'pi-linkedin',
  discord: 'pi-discord',
  twitch: 'pi-twitch',
}
const platformColor = {
  facebook: '#4267B2',
  instagram: '#E1306C',
  twitter: '#1DA1F2',
  youtube: '#FF0000',
  github: isDark.value ? '#ffffff' : '#24292e',
  linkedin: '#0077B5',
  discord: '#5865F2',
  twitch: '#9146FF',
}
function iconFor(platform) {
  return platformIcon[platform.toLowerCase()] ?? 'pi-external-link'
}
function colorFor(platform) {
  return platformColor[platform.toLowerCase()] ?? '#9ca3af'
}
</script>

<template>
  <div class="min-h-screen bg-base text-main flex flex-col">

    <!-- Top bar (public) -->
    <header v-if="!isAdmin" class="bg-card px-4 md:px-8 pt-3 md:pt-4 pb-2 md:pb-3 flex items-center justify-between border-b border-base">
      <div class="flex items-center gap-2 md:gap-3">
        <span class="text-xl md:text-3xl">🎄</span>
        <span class="font-bold text-main text-base md:text-2xl leading-tight">{{ showStore.config.siteName }}</span>
      </div>
      <div class="flex items-center gap-3 md:gap-4">
        <!-- Theme toggle -->
        <button @click="toggleTheme"
          class="text-hint hover:text-sub transition text-lg md:text-2xl leading-none"
          :title="isDark ? 'Switch to light mode' : 'Switch to dark mode'">
          {{ isDark ? '☀️' : '🌙' }}
        </button>
        <!-- GitHub -->
        <a href="https://github.com/thebradleysanders/XlightsQueue"
          target="_blank" rel="noopener"
          class="text-hint hover:text-sub transition"
          title="View on GitHub">
          <i class="fa-brands fa-github text-lg md:text-2xl leading-none"></i>
        </a>
        <RouterLink to="/admin/login" class="text-ghost hover:text-hint text-xs md:text-sm">Admin</RouterLink>
      </div>
    </header>

    <!-- Admin top bar (always dark) -->
    <header v-if="isAdmin" class="bg-gray-900 border-b border-gray-800 px-4 py-3 flex items-center gap-4">
      <RouterLink to="/" class="text-lg font-bold text-green-400">🎄 {{ showStore.config.siteName }}</RouterLink>
    </header>

    <!-- Off-season splash -->
    <div v-if="!isAdmin && !showStore.config.isSeasonActive"
      class="flex-1 flex flex-col items-center justify-center px-6 py-16 text-center">
      <div class="text-7xl mb-6">🎄</div>
      <h1 class="text-2xl font-black text-main mb-3">{{ showStore.config.siteName }}</h1>
      <p class="text-sub text-base max-w-sm leading-relaxed mb-8">
        {{ showStore.config.offSeasonMessage || 'The holiday light show is not running right now. Check back soon!' }}
      </p>
      <div v-if="showStore.config.socialLinks?.length" class="flex flex-wrap items-center justify-center gap-4">
        <a
          v-for="link in showStore.config.socialLinks"
          :key="link.platform"
          :href="link.url"
          target="_blank"
          rel="noopener"
          class="flex items-center gap-2 px-4 py-2.5 rounded-xl text-sm font-semibold transition hover:opacity-80 active:scale-95"
          :style="`color: ${colorFor(link.platform)}; background: ${colorFor(link.platform)}18; border: 1px solid ${colorFor(link.platform)}40`"
        >
          <i :class="`pi ${iconFor(link.platform)} text-base`"></i>
          <span>{{ link.platform }}</span>
        </a>
      </div>
      <RouterLink to="/admin/login" class="mt-12 text-ghost hover:text-hint text-xs">Admin</RouterLink>
    </div>

    <!-- Main content -->
    <main v-if="isAdmin || showStore.config.isSeasonActive" class="flex-1"
      :class="isAdmin ? '' : showStore.config.socialLinks?.length ? 'pb-28' : 'pb-20'">
      <RouterView />
    </main>

    <!-- Bottom nav (public only, season active) -->
    <nav v-if="!isAdmin && showStore.config.isSeasonActive"
      class="fixed bottom-0 inset-x-0 nav-bg backdrop-blur border-t nav-border z-40">

      <!-- Social links strip -->
      <div v-if="showStore.config.socialLinks?.length"
        class="flex items-center justify-center gap-5 pt-2.5 pb-1 border-b border-base">
        <a
          v-for="link in showStore.config.socialLinks"
          :key="link.platform"
          :href="link.url"
          target="_blank"
          rel="noopener"
          class="flex items-center gap-2 text-xs font-medium transition hover:opacity-80 active:scale-95"
          :style="`color: ${colorFor(link.platform)}`"
        >
          <i :class="`pi ${iconFor(link.platform)} text-xl`"></i>
          <span class="text-hint text-xs">{{ link.platform }}</span>
        </a>
      </div>

      <!-- Tab bar -->
      <div class="flex">
        <RouterLink to="/" class="flex-1 flex flex-col items-center gap-1 py-2.5 text-xs transition"
          :class="route.path === '/' ? 'text-green-500' : 'text-hint hover:text-sub'">
          <i class="fa-solid fa-house text-lg leading-none"></i>
          <span>Home</span>
        </RouterLink>
        <RouterLink to="/queue" class="flex-1 flex flex-col items-center gap-1 py-2.5 text-xs transition relative"
          :class="route.path === '/queue' ? 'text-green-500' : 'text-hint hover:text-sub'">
          <i class="fa-solid fa-list-ol text-lg leading-none"></i>
          <span>Queue</span>
          <span v-if="pendingCount > 0"
            class="absolute top-1.5 right-[calc(50%-14px)] bg-green-500 text-white text-[10px] font-bold rounded-full w-4 h-4 flex items-center justify-center leading-none">
            {{ pendingCount > 9 ? '9+' : pendingCount }}
          </span>
        </RouterLink>
        <RouterLink to="/playlists" class="flex-1 flex flex-col items-center gap-1 py-2.5 text-xs transition"
          :class="route.path === '/playlists' ? 'text-green-500' : 'text-hint hover:text-sub'">
          <i class="fa-solid fa-music text-lg leading-none"></i>
          <span>Songs</span>
        </RouterLink>
        <RouterLink to="/info" class="flex-1 flex flex-col items-center gap-1 py-2.5 text-xs transition"
          :class="route.path === '/info' ? 'text-green-500' : 'text-hint hover:text-sub'">
          <i class="fa-solid fa-circle-info text-lg leading-none"></i>
          <span>Info</span>
        </RouterLink>
      </div>
    </nav>

  </div>
</template>
