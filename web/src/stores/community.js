import axios from 'axios';
import { defineStore } from 'pinia';

import { ref } from 'vue';

import { useSessionStore } from '@/stores/session';

/**
 * The audience side of the show: donation goal, cheers, how many people are watching,
 * and the live activity feed that pops up as toasts.
 */
export const useCommunityStore = defineStore('community', () => {
    const raised = ref(0);
    const supporters = ref(0);
    const goal = ref(0);
    const goalLabel = ref('');
    const cheersToday = ref(0);
    const watchingTonight = ref(0);

    const toasts = ref([]); // { id, kind, title, amount }
    // Bumped whenever something happens so decorations (the light string) can react.
    const pulse = ref({ n: 0, kind: null });
    // Bumped when an admin edits the supporters list so open lists refresh.
    const supportersVersion = ref(0);

    let toastId = 0;

    function apply(data) {
        raised.value = data.raised;
        supporters.value = data.supporters;
        goal.value = data.goal;
        goalLabel.value = data.goalLabel;
        cheersToday.value = data.cheersToday;
        watchingTonight.value = data.watchingTonight;
    }

    async function fetchCommunity() {
        try {
            const { data } = await axios.get('/api/show/community');
            apply(data);
        } catch {
            /* non-fatal */
        }
    }

    function flash(kind) {
        pulse.value = { n: pulse.value.n + 1, kind };
    }

    function onActivity(a) {
        flash(a.kind);
        const id = ++toastId;
        toasts.value = [{ id, ...a }, ...toasts.value].slice(0, 3);
        setTimeout(() => (toasts.value = toasts.value.filter(t => t.id !== id)), 5000);
    }

    function onCheer({ cheersToday: total }) {
        cheersToday.value = Math.max(cheersToday.value, total);
        flash('cheer');
    }

    /** Returns true if the cheer counted (false = cooling down). */
    async function cheer() {
        const session = useSessionStore();
        try {
            const { data } = await axios.post('/api/show/cheer', { sessionToken: session.sessionToken });
            cheersToday.value = Math.max(cheersToday.value, data.cheersToday);
            return true;
        } catch {
            return false;
        }
    }

    return { supportersVersion, raised, supporters, goal, goalLabel, cheersToday, watchingTonight, toasts, pulse, apply, fetchCommunity, onActivity, onCheer, cheer, flash };
});
