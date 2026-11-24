import { computed, ref, watch } from 'vue';

const KEY = 'qtm.supporterName';
const SHOW_KEY = 'qtm.showName';

function read(key, fallback) {
    try {
        return localStorage.getItem(key) ?? fallback;
    } catch {
        return fallback;
    }
}
function write(key, value) {
    try {
        localStorage.setItem(key, value);
    } catch {
        /* private mode etc. */
    }
}

// Shared across modals and remembered on this phone, so a returning supporter doesn't retype it.
// Anonymous is the default; a name is only ever sent when the person opts in.
const name = ref(read(KEY, ''));
const showName = ref(read(SHOW_KEY, 'false') === 'true');
watch(name, v => write(KEY, v));
watch(showName, v => write(SHOW_KEY, String(v)));

export function useSupporterName() {
    /** What to send to the API: the trimmed name, or null for anonymous. */
    const displayName = computed(() => (showName.value && name.value.trim() ? name.value.trim().slice(0, 30) : null));
    return { name, showName, displayName };
}
