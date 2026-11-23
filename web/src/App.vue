<script setup>
    import axios from 'axios';
    import { RouterLink, RouterView, useRoute } from 'vue-router';

    import { computed, onMounted } from 'vue';

    import { useSeason } from '@/composables/useSeason';
    import { useSignalR } from '@/composables/useSignalR';
    import { useTheme } from '@/composables/useTheme';

    import { useSessionStore } from '@/stores/session';
    import { useShowStore } from '@/stores/show';

    import SeasonDecor from '@/components/SeasonDecor.vue';

    import { version } from '../package.json';

    const showStore = useShowStore();
    const sessionStore = useSessionStore();
    useSignalR();

    // Count one visit per page load (the server merges re-opens within 30 min). Never for admin pages.
    function recordVisit() {
        if (window.location.pathname.startsWith('/admin')) return;
        axios.post('/api/show/visit', { sessionToken: sessionStore.sessionToken }).catch(() => {});
    }
    useTheme();
    const { season } = useSeason();

    onMounted(() => {
        recordVisit();
        showStore.fetchNowPlaying();
        showStore.fetchQueue();
    });

    const route = useRoute();
    const isAdmin = computed(() => route.path.startsWith('/admin'));
    const pendingCount = computed(() => showStore.queue.filter(q => q.status === 'Pending').length);
    const isDark = computed(() => true);

    const platformIcon = {
        facebook: 'pi-facebook',
        instagram: 'pi-instagram',
        twitter: 'pi-twitter',
        youtube: 'pi-youtube',
        github: 'pi-github',
        linkedin: 'pi-linkedin',
        discord: 'pi-discord',
        twitch: 'pi-twitch'
    };
    // Only render http(s) links — a stored "javascript:" URL must never become a clickable href.
    const socialLinks = computed(() => (showStore.config.socialLinks ?? []).filter(l => /^https?:\/\//i.test(l?.url ?? '')));
    const platformColor = {
        facebook: '#4267B2',
        instagram: '#E1306C',
        twitter: '#1DA1F2',
        youtube: '#FF0000',
        github: null, // resolved per theme in colorFor()
        linkedin: '#0077B5',
        discord: '#5865F2',
        twitch: '#9146FF'
    };
    function iconFor(platform) {
        return platformIcon[platform.toLowerCase()] ?? 'pi-external-link';
    }
    function colorFor(platform) {
        const key = platform.toLowerCase();
        if (key === 'github') return isDark.value ? '#ffffff' : '#24292e';
        return platformColor[key] ?? '#9ca3af';
    }
</script>

<template>
    <div class="bg-base text-main flex min-h-screen flex-col">
        <SeasonDecor v-if="!isAdmin" variant="particles" />
        <!-- Top bar (public) -->
        <div v-if="!isAdmin" class="sticky top-0 z-30"><header class="bg-card border-base flex items-center justify-between border-b px-4 pt-6 pb-2 shadow-2xl md:px-8 md:pt-4 md:pb-3">
            <div class="flex items-center gap-2 md:gap-3">
                <span class="text-2xl leading-none md:text-3xl" aria-hidden="true">{{ season.emoji }}</span>
                <div class="min-w-0">
                    <span class="text-main block truncate text-base leading-tight font-bold md:text-2xl">{{ showStore.config.siteName }}</span>
                    <span class="text-ac1-strong hidden text-[10px] font-semibold tracking-widest uppercase sm:block">{{ season.tagline }}</span>
                </div>
            </div>
            <div class="flex items-center gap-6 md:gap-4">
                <RouterLink to="/admin/login" class="hover:text-hint !text-blue-300" aria-label="Admin login">
                    <font-awesome-icon :icon="['fas', 'user-gear']" class="text-lg md:text-2xl" />
                </RouterLink>
            </div>
        </header>
        <SeasonDecor variant="lights" />
        </div>

        <!-- Admin top bar (always dark) -->
        <header v-if="isAdmin" class="flex items-center gap-4 border-b border-gray-800 bg-gray-900 px-4 py-3">
            <RouterLink to="/" class="text-lg font-bold text-green-400">{{ showStore.config.siteName }}</RouterLink>
        </header>

        <!-- Off-season splash -->
        <div v-if="!isAdmin && !showStore.config.isSeasonActive" class="flex flex-1 flex-col items-center justify-center px-6 py-16 text-center">
            <div class="mb-6 text-7xl">{{ season.emoji }}</div>
            <h1 class="text-main mb-3 text-2xl font-black">{{ showStore.config.siteName }}</h1>
            <p class="text-sub mb-8 max-w-sm text-base leading-relaxed">
                {{ showStore.config.offSeasonMessage || 'The light show is not running right now. Check back soon!' }}
            </p>
            <div v-if="socialLinks.length" class="flex flex-wrap items-center justify-center gap-4">
                <a
                    v-for="link in socialLinks"
                    :key="link.platform"
                    :href="link.url"
                    target="_blank"
                    rel="noopener"
                    class="flex items-center gap-2 rounded-xl px-4 py-2.5 text-sm font-semibold transition hover:opacity-80 active:scale-95"
                    :style="`color: ${colorFor(link.platform)}; background: ${colorFor(link.platform)}18; border: 1px solid ${colorFor(link.platform)}40`"
                >
                    <i :class="`pi ${iconFor(link.platform)} text-base`"></i>
                    <span>{{ link.platform }}</span>
                </a>
            </div>
            <RouterLink to="/admin/login" class="text-ghost hover:text-hint mt-12 text-xs">
                <font-awesome-icon :icon="['fas', 'user-gear']" class="text-lg md:text-2xl" /> Admin Login
            </RouterLink>
        </div>

        <!-- Main content -->
        <main v-if="isAdmin || showStore.config.isSeasonActive" class="flex-1" :class="isAdmin ? '' : socialLinks.length ? 'pb-28' : 'pb-20'">
            <RouterView />
        </main>

        <!-- Bottom nav (public only, season active) -->
        <nav v-if="!isAdmin && showStore.config.isSeasonActive" class="nav-bg nav-border fixed inset-x-0 bottom-0 z-40 border-t backdrop-blur">
            <!-- Social links strip -->
            <div v-if="socialLinks.length" class="border-base flex items-center justify-center gap-5 border-b bg-white/4 py-2">
                <a v-for="link in socialLinks" :key="link.platform" :href="link.url" target="_blank" rel="noopener" class="flex items-center gap-2 text-xs font-medium transition hover:opacity-80 active:scale-95" :style="`color: ${colorFor(link.platform)}`">
                    <i :class="`pi ${iconFor(link.platform)} !text-xl`"></i>
                    <span class="text-hint text-xs">{{ link.platform }}</span>
                </a>
            </div>

            <!-- Tab bar -->
            <div class="flex">
                <RouterLink to="/" class="flex flex-1 flex-col items-center gap-1 py-2.5 text-xs transition" :class="route.path === '/' ? 'text-ac1' : 'text-hint hover:text-sub'">
                    <font-awesome-icon :icon="['fas', 'house']" class="text-lg leading-none" />
                    <span>Home</span>
                </RouterLink>
                <RouterLink to="/queue" class="relative flex flex-1 flex-col items-center gap-1 py-2.5 text-xs transition" :class="route.path === '/queue' ? 'text-ac1' : 'text-hint hover:text-sub'">
                    <font-awesome-icon :icon="['fas', 'list-ol']" class="text-lg leading-none" />
                    <span>Queue</span>
                    <span v-if="pendingCount > 0" class="absolute top-1.5 right-[calc(50%-14px)] flex h-4 w-4 items-center justify-center rounded-full bg-ac1 text-[10px] leading-none font-bold text-white">
                        {{ pendingCount > 9 ? '9+' : pendingCount }}
                    </span>
                </RouterLink>
                <RouterLink to="/playlists" class="flex flex-1 flex-col items-center gap-1 py-2.5 text-xs transition" :class="route.path === '/playlists' ? 'text-ac1' : 'text-hint hover:text-sub'">
                    <font-awesome-icon :icon="['fas', 'music']" class="text-lg leading-none" />
                    <span>Songs</span>
                </RouterLink>
                <RouterLink to="/info" class="flex flex-1 flex-col items-center gap-1 py-2.5 text-xs transition" :class="route.path === '/info' ? 'text-ac1' : 'text-hint hover:text-sub'">
                    <font-awesome-icon :icon="['fas', 'circle-info']" class="text-lg leading-none" />
                    <span>Info</span>
                </RouterLink>
            </div>
        </nav>
    </div>
</template>
