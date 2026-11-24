<script setup>
    import axios from 'axios';
    import { RouterLink, RouterView, useRoute } from 'vue-router';

    import { computed, onMounted, ref } from 'vue';

    import { useSeason } from '@/composables/useSeason';
    import { useSignalR } from '@/composables/useSignalR';
    import { useTheme } from '@/composables/useTheme';

    import { useSessionStore } from '@/stores/session';
    import { useShowStore } from '@/stores/show';

    import ActivityToasts from '@/components/ActivityToasts.vue';
    import GuestSignInModal from '@/components/GuestSignInModal.vue';
    import LightString from '@/components/LightString.vue';
    import StarLogo from '@/components/StarLogo.vue';
    import { useCommunityStore } from '@/stores/community';
    import { useGuestStore } from '@/stores/guest';
    import { useListenStore } from '@/stores/listen';

    import { version } from '../package.json';

    const showStore = useShowStore();
    const community = useCommunityStore();
    const guest = useGuestStore();
    const showGuestModal = ref(false);
    // Friends & family can see the whole site even before the public season opens
    const siteVisible = computed(() => showStore.config.isSeasonActive || guest.signedIn);
    const listen = useListenStore();
    const sessionStore = useSessionStore();
    useSignalR();

    // Count one visit per page load (the server merges re-opens within 30 min). Never for admin pages.
    function recordVisit() {
        if (window.location.pathname.startsWith('/admin')) return;
        axios.post('/api/show/visit', { sessionToken: sessionStore.sessionToken }).catch(() => {});
    }
    useTheme();
    const { season } = useSeason();

    // "Opens Dec 1" for the splash when the season hasn't started yet
    function fmtDate(iso) {
        if (!iso) return null;
        const [y, m, d] = iso.split('-').map(Number);
        return new Date(y, m - 1, d).toLocaleDateString(undefined, { month: 'long', day: 'numeric' });
    }
    const opensOn = computed(() => {
        const c = showStore.config;
        if (c.showMode === 'off' || !c.seasonStart) return null;
        const today = new Date();
        const iso = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;
        return c.seasonStart > iso ? fmtDate(c.seasonStart) : null;
    });

    onMounted(() => {
        recordVisit();
        // Share link: https://your-site/?guest=ABC123 signs a friend in automatically
        const link = new URLSearchParams(window.location.search).get('guest');
        if (link) {
            guest.signIn(link).catch(() => {}).finally(() => window.history.replaceState({}, '', window.location.pathname));
        } else {
            guest.verify();
        }
        showStore.fetchNowPlaying();
        showStore.fetchQueue();
        if (!isAdmin.value) community.fetchCommunity();
    });

    const route = useRoute();
    const isAdmin = computed(() => route.path.startsWith('/admin'));
    const pendingCount = computed(() => showStore.queue.filter(q => q.status === 'Pending').length);
    const isDark = computed(() => true);
    const tabs = [
        { to: '/', label: 'Home', icon: 'house' },
        { to: '/queue', label: 'Queue', icon: 'list-ol' },
        { to: '/playlists', label: 'Songs', icon: 'music' },
        { to: '/info', label: 'Info', icon: 'circle-info' }
    ];

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
    // Only render http(s) links. A stored "javascript:" URL must never become a clickable href.
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
        <!-- Top bar (public) -->
        <div v-if="!isAdmin" class="nav-bg sticky top-0 z-30 backdrop-blur">
            <header class="flex items-center justify-between px-4 pt-3 md:px-8">
                <div class="flex min-w-0 items-center gap-2.5">
                    <StarLogo class="text-ac3 star-glow shrink-0 text-2xl" />
                    <div class="min-w-0">
                        <span class="text-main block truncate text-base leading-tight font-bold tracking-tight md:text-lg">{{ showStore.config.siteName }}</span>
                        <span class="text-ac1 block text-[10px] font-semibold tracking-[0.18em] uppercase">{{ season.tagline }}</span>
                    </div>
                </div>
                <div class="flex items-center gap-1">
                    <button v-if="guest.signedIn" @click="guest.signOut()" class="bg-ac1-soft text-ac1 flex max-w-[9rem] items-center gap-1.5 rounded-full px-3 py-1 text-xs font-semibold" aria-label="Sign out of friends and family">
                        <span class="truncate">{{ guest.name }}</span>
                        <font-awesome-icon :icon="['fas', 'xmark']" class="shrink-0 opacity-70" />
                    </button>
                    <button v-if="listen.state === 'playing'" @click="listen.stop()" class="bg-ac1-soft text-ac1 flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-semibold" aria-label="Stop listening">
                        <font-awesome-icon :icon="['fas', 'headphones']" /> Live
                        <font-awesome-icon :icon="['fas', 'xmark']" class="opacity-70" />
                    </button>
                    <RouterLink to="/admin/login" class="text-hint hover:text-main p-2 transition" aria-label="Admin login">
                        <font-awesome-icon :icon="['fas', 'user-gear']" class="text-base" />
                    </RouterLink>
                </div>
            </header>
            <LightString />
        </div>
        <ActivityToasts v-if="!isAdmin && siteVisible" />

        <!-- Admin top bar -->
        <header v-if="isAdmin" class="flex items-center gap-4 border-b border-gray-800 bg-gray-900 px-4 py-3">
            <RouterLink to="/" class="text-lg font-bold text-green-400">{{ showStore.config.siteName }}</RouterLink>
        </header>

        <!-- Off-season splash -->
        <div v-if="!isAdmin && !siteVisible" class="flex flex-1 flex-col items-center justify-center px-6 py-16 text-center">
            <StarLogo class="text-ac3 star-glow mb-6 text-6xl" />
            <h1 class="text-main mb-2 text-2xl font-semibold tracking-tight">{{ showStore.config.siteName }}</h1>
            <p v-if="opensOn" class="text-ac1 mb-3 text-sm font-semibold">Opens {{ opensOn }}</p>
            <p class="text-sub mb-8 max-w-sm text-sm leading-relaxed">
                {{ showStore.config.offSeasonMessage || 'The light show is not running right now. Check back soon!' }}
            </p>
            <div v-if="socialLinks.length" class="flex flex-wrap items-center justify-center gap-3">
                <a
                    v-for="link in socialLinks"
                    :key="link.platform"
                    :href="link.url"
                    target="_blank"
                    rel="noopener"
                    class="btn-quiet flex items-center gap-2 rounded-lg px-4 py-2 text-sm"
                >
                    <i :class="`pi ${iconFor(link.platform)}`"></i>
                    <span>{{ link.platform }}</span>
                </a>
            </div>
            <button v-if="showStore.config.guestPassesEnabled" @click="showGuestModal = true" class="btn-quiet mt-8 rounded-lg px-4 py-2 text-sm">Friends &amp; family sign in</button>
            <RouterLink to="/admin/login" class="text-hint hover:text-main mt-8 text-xs">Admin login</RouterLink>
        </div>

        <!-- Guest preview banner: shown to friends & family while the public can't see the show yet -->
        <div v-if="!isAdmin && guest.signedIn && !showStore.config.isSeasonActive" class="bg-ac1-soft text-ac1 px-4 py-2 text-center text-xs font-medium">
            Friends &amp; family preview. The show isn't open to the public yet.
        </div>

        <GuestSignInModal v-if="showGuestModal" @close="showGuestModal = false" />

        <!-- Main content -->
        <main v-if="isAdmin || siteVisible" class="flex-1" :class="isAdmin ? '' : socialLinks.length ? 'pb-28' : 'pb-20'">
            <RouterView />
        </main>

        <!-- Bottom nav (public only, season active) -->
        <nav v-if="!isAdmin && siteVisible" class="nav-bg nav-border fixed inset-x-0 bottom-0 z-40 border-t backdrop-blur">
            <div v-if="socialLinks.length" class="border-base flex items-center justify-center gap-5 border-b py-2">
                <a v-for="link in socialLinks" :key="link.platform" :href="link.url" target="_blank" rel="noopener" class="text-hint hover:text-main flex items-center gap-1.5 text-xs transition">
                    <i :class="`pi ${iconFor(link.platform)}`"></i>
                    <span>{{ link.platform }}</span>
                </a>
            </div>
            <div class="mx-auto flex max-w-2xl">
                <RouterLink v-for="tab in tabs" :key="tab.to" :to="tab.to" class="relative flex flex-1 flex-col items-center gap-1 py-2.5 text-xs font-medium transition" :class="route.path === tab.to ? 'text-main' : 'text-hint hover:text-sub'">
                    <font-awesome-icon :icon="['fas', tab.icon]" class="text-base leading-none" :class="route.path === tab.to ? 'text-ac1' : ''" />
                    <span>{{ tab.label }}</span>
                    <span v-if="tab.to === '/queue' && pendingCount > 0" class="bg-ac1 absolute top-1.5 right-[calc(50%-18px)] flex h-4 min-w-4 items-center justify-center rounded-full px-1 text-[10px] leading-none font-bold text-black tabular">
                        {{ pendingCount > 9 ? '9+' : pendingCount }}
                    </span>
                </RouterLink>
            </div>
        </nav>
    </div>
</template>
