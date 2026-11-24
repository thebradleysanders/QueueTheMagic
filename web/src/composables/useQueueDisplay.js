import { useShowStore } from '@/stores/show';

/** Small formatting helpers shared by the Home page and the Queue list. */
export function useQueueDisplay() {
    const showStore = useShowStore();

    function formatDuration(seconds) {
        if (!seconds) return '';
        const m = Math.floor(seconds / 60);
        const s = seconds % 60;
        return `${m}:${String(s).padStart(2, '0')}`;
    }

    /** Seconds until the queue item at `itemIndex` starts playing. */
    function estimatedWaitSeconds(itemIndex) {
        const items = showStore.queue;
        let secs = 0;
        if (items[0]?.status === 'Playing') secs += showStore.nowPlaying?.secondsRemaining || 0;
        for (let i = 0; i < itemIndex; i++) {
            if (items[i].status === 'Pending') secs += items[i].durationSeconds || 0;
        }
        return secs;
    }

    function formatWait(secs) {
        if (!secs) return null;
        if (secs < 60) return `~${secs}s`;
        return `~${Math.round(secs / 60)} min`;
    }

    return { formatDuration, estimatedWaitSeconds, formatWait };
}
