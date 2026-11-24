import axios from 'axios';
import { defineStore } from 'pinia';

import { computed, ref } from 'vue';

const KEY = 'qtm.guest';

function load() {
    try {
        return JSON.parse(localStorage.getItem(KEY) || 'null');
    } catch {
        return null;
    }
}

/**
 * Friends & family sign-in. A guest signs in with a pass code the show owner gave them and can then
 * request songs for free, at any time, even before the public season opens.
 */
export const useGuestStore = defineStore('guest', () => {
    const saved = load();
    const token = ref(saved?.token ?? null);
    const name = ref(saved?.name ?? '');
    const signedIn = computed(() => !!token.value);

    function persist() {
        try {
            token.value ? localStorage.setItem(KEY, JSON.stringify({ token: token.value, name: name.value })) : localStorage.removeItem(KEY);
        } catch {
            /* private mode: still works for this tab */
        }
    }

    const headers = () => ({ 'X-Guest-Token': token.value ?? '' });

    async function signIn(code) {
        const { data } = await axios.post('/api/guest/signin', { code });
        token.value = data.token;
        name.value = data.name;
        persist();
    }

    function signOut() {
        token.value = null;
        name.value = '';
        persist();
    }

    /** Drops the sign-in if the pass was revoked or expired. */
    async function verify() {
        if (!token.value) return;
        try {
            await axios.get('/api/guest/me', { headers: headers() });
        } catch (e) {
            if (e.response?.status === 401 || e.response?.status === 404) signOut();
        }
    }

    async function requestSong(songId) {
        try {
            const { data } = await axios.post('/api/guest/queue', { songId }, { headers: headers() });
            return data;
        } catch (e) {
            if (e.response?.status === 401) signOut();
            throw e;
        }
    }

    return { token, name, signedIn, signIn, signOut, verify, requestSong };
});
