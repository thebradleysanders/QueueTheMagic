<script setup>
    import { RouterLink, RouterView, useRoute } from 'vue-router';

    import { computed, onMounted } from 'vue';

    import { useSignalR } from '@/composables/useSignalR';
    import { useTheme } from '@/composables/useTheme';

    import { useShowStore } from '@/stores/show';

    import { version } from '../package.json';

    const showStore = useShowStore();
    useSignalR();
    const { toggleTheme } = useTheme();

    onMounted(() => {
        showStore.fetchNowPlaying();
        showStore.fetchQueue();
    });

    const route = useRoute();
    const isAdmin = computed(() => route.path.startsWith('/admin'));
    const pendingCount = computed(() => showStore.queue.filter(q => q.status === 'Pending').length);
    const isDark = computed(() => showStore.config.theme !== 'light');

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
    const platformColor = {
        facebook: '#4267B2',
        instagram: '#E1306C',
        twitter: '#1DA1F2',
        youtube: '#FF0000',
        github: isDark.value ? '#ffffff' : '#24292e',
        linkedin: '#0077B5',
        discord: '#5865F2',
        twitch: '#9146FF'
    };
    function iconFor(platform) {
        return platformIcon[platform.toLowerCase()] ?? 'pi-external-link';
    }
    function colorFor(platform) {
        return platformColor[platform.toLowerCase()] ?? '#9ca3af';
    }
</script>

<template>
    <div class="bg-base text-main flex min-h-screen flex-col">
        <!-- Top bar (public) -->
        <header v-if="!isAdmin" class="bg-card border-base flex items-center justify-between border-b px-4 pt-6 pb-2 md:px-8 md:pt-4 md:pb-3 sticky top-0 z-30 shadow-2xl">
            <div class="flex items-center gap-2 md:gap-3">
                <span class="text-main text-base leading-tight font-bold md:text-2xl">{{ showStore.config.siteName }}</span>
            </div>
            <div class="flex items-center gap-6 md:gap-4">
                <!-- Theme toggle -->
                <button @click="toggleTheme" class="text-hint hover:text-sub text-lg leading-none transition md:text-2xl" :title="isDark ? 'Switch to light mode' : 'Switch to dark mode'">
                    {{ isDark ? '☀️' : '🌙' }}
                </button>
                <!-- GitHub -->
                <a href="https://github.com/thebradleysanders/QueueTheMagic" target="_blank" rel="noopener" class="text-hint hover:text-sub transition" title="View on GitHub">
                    <i class="fa-brands fa-github text-lg leading-none md:text-2xl"></i>
                </a>
                <RouterLink to="/admin/login" class="hover:text-hint !text-blue-300">
                    <font-awesome-icon :icon="['fas', 'user-gear']" class="text-lg md:text-2xl" />
                </RouterLink>
            </div>
        </header>

        <!-- Admin top bar (always dark) -->
        <header v-if="isAdmin" class="flex items-center gap-4 border-b border-gray-800 bg-gray-900 px-4 py-3">
            <RouterLink to="/" class="text-lg font-bold text-green-400">{{ showStore.config.siteName }}</RouterLink>
        </header>

        <!-- Off-season splash -->
        <div v-if="!isAdmin && !showStore.config.isSeasonActive" class="flex flex-1 flex-col items-center justify-center px-6 py-16 text-center">
            <div class="mb-6 text-7xl">🎄</div>
            <h1 class="text-main mb-3 text-2xl font-black">{{ showStore.config.siteName }}</h1>
            <p class="text-sub mb-8 max-w-sm text-base leading-relaxed">
                {{ showStore.config.offSeasonMessage || 'The holiday light show is not running right now. Check back soon!' }}
            </p>
            <div v-if="showStore.config.socialLinks?.length" class="flex flex-wrap items-center justify-center gap-4">
                <a
                    v-for="link in showStore.config.socialLinks"
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
        <main v-if="isAdmin || showStore.config.isSeasonActive" class="flex-1" :class="isAdmin ? '' : showStore.config.socialLinks?.length ? 'pb-28' : 'pb-20'">
            <RouterView />
        </main>

        <!-- Bottom nav (public only, season active) -->
        <nav v-if="!isAdmin && showStore.config.isSeasonActive" class="nav-bg nav-border fixed inset-x-0 bottom-0 z-40 border-t backdrop-blur">
            <!-- Social links strip -->
            <div v-if="showStore.config.socialLinks?.length" class="border-base flex items-center justify-center gap-5 border-b bg-white/4 py-2">
                <a v-for="link in showStore.config.socialLinks" :key="link.platform" :href="link.url" target="_blank" rel="noopener" class="flex items-center gap-2 text-xs font-medium transition hover:opacity-80 active:scale-95" :style="`color: ${colorFor(link.platform)}`">
                    <i :class="`pi ${iconFor(link.platform)} !text-xl`"></i>
                    <span class="text-hint text-xs">{{ link.platform }}</span>
                </a>
            </div>

            <!-- Tab bar -->
            <div class="flex">
                <RouterLink to="/" class="flex flex-1 flex-col items-center gap-1 py-2.5 text-xs transition" :class="route.path === '/' ? 'text-green-500' : 'text-hint hover:text-sub'">
                    <font-awesome-icon :icon="['fas', 'house']" class="text-lg leading-none" />
                    <span>Home</span>
                </RouterLink>
                <RouterLink to="/queue" class="relative flex flex-1 flex-col items-center gap-1 py-2.5 text-xs transition" :class="route.path === '/queue' ? 'text-green-500' : 'text-hint hover:text-sub'">
                    <font-awesome-icon :icon="['fas', 'list-ol']" class="text-lg leading-none" />
                    <span>Queue</span>
                    <span v-if="pendingCount > 0" class="absolute top-1.5 right-[calc(50%-14px)] flex h-4 w-4 items-center justify-center rounded-full bg-green-500 text-[10px] leading-none font-bold text-white">
                        {{ pendingCount > 9 ? '9+' : pendingCount }}
                    </span>
                </RouterLink>
                <RouterLink to="/playlists" class="flex flex-1 flex-col items-center gap-1 py-2.5 text-xs transition" :class="route.path === '/playlists' ? 'text-green-500' : 'text-hint hover:text-sub'">
                    <font-awesome-icon :icon="['fas', 'music']" class="text-lg leading-none" />
                    <span>Songs</span>
                </RouterLink>
                <RouterLink to="/info" class="flex flex-1 flex-col items-center gap-1 py-2.5 text-xs transition" :class="route.path === '/info' ? 'text-green-500' : 'text-hint hover:text-sub'">
                    <font-awesome-icon :icon="['fas', 'circle-info']" class="text-lg leading-none" />
                    <span>Info</span>
                </RouterLink>
            </div>
        </nav>
    </div>
</template>
