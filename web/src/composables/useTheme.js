import { onMounted } from 'vue';

// The public site is dark-only.
export function useTheme() {
    onMounted(() => document.documentElement.classList.add('dark'));
    document.documentElement.classList.add('dark');
}
