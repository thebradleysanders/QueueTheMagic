import { computed, ref, watchEffect } from 'vue';

/**
 * Seasonal themes. Colours live in main.css under html[data-season="<id>"];
 * this file decides WHICH season is active and supplies the emoji/copy.
 *
 * Date ranges are [startMonth, startDay] → [endMonth, endDay], inclusive, 1-based months.
 * Preview any theme without waiting for the date:  /?season=halloween
 */
export const SEASONS = [
    { id: 'christmas', label: 'Christmas', emoji: '🎄', icon: 'snowflake', particles: ['❄️', '❄️', '✨', '⭐'], start: [11, 15], end: [12, 31], tagline: 'Merry & Bright' },
    { id: 'winter', label: 'Winter', emoji: '⛄', icon: 'snowflake', particles: ['❄️', '❅', '❆'], start: [1, 1], end: [1, 31], tagline: 'Winter Wonderland' },
    { id: 'valentines', label: "Valentine's Day", emoji: '💝', icon: 'heart', particles: ['💗', '💖', '💕'], start: [2, 1], end: [2, 14], tagline: 'Spread the Love' },
    { id: 'stpatricks', label: "St. Patrick's Day", emoji: '🍀', icon: 'clover', particles: ['🍀', '☘️', '✨'], start: [3, 1], end: [3, 17], tagline: 'Lucky Lights' },
    { id: 'spring', label: 'Spring', emoji: '🌷', icon: 'seedling', particles: ['🌸', '🌷', '🦋'], start: [3, 18], end: [5, 31], tagline: 'Spring Has Sprung' },
    { id: 'patriotic', label: '4th of July', emoji: '🎆', icon: 'star', particles: ['⭐', '✨', '🎇'], start: [6, 25], end: [7, 6], tagline: 'Land of the Free' },
    { id: 'halloween', label: 'Halloween', emoji: '🎃', icon: 'ghost', particles: ['🦇', '🕸️', '🍂', '👻'], start: [10, 1], end: [11, 3], tagline: 'Spooky Season' },
    { id: 'autumn', label: 'Autumn', emoji: '🍁', icon: 'leaf', particles: ['🍂', '🍁', '🍃'], start: [11, 4], end: [11, 14], tagline: 'Harvest Lights' },
    // Fallback for the rest of the year
    { id: 'default', label: 'Lights', emoji: '✨', icon: 'wand-magic-sparkles', particles: ['✨', '⭐'], tagline: 'Queue the Magic' }
];

function inRange(date, { start, end }) {
    if (!start) return false;
    const v = (date.getMonth() + 1) * 100 + date.getDate();
    return v >= start[0] * 100 + start[1] && v <= end[0] * 100 + end[1];
}

export function seasonForDate(date = new Date()) {
    return SEASONS.find(s => inRange(date, s)) ?? SEASONS[SEASONS.length - 1];
}

// ?season=halloween overrides the calendar (kept for the tab's lifetime so route changes don't drop it)
function readOverride() {
    try {
        const q = new URLSearchParams(window.location.search).get('season');
        if (q) sessionStorage.setItem('seasonOverride', q);
        return sessionStorage.getItem('seasonOverride');
    } catch {
        return null;
    }
}

const override = ref(readOverride());
const current = computed(() => SEASONS.find(s => s.id === override.value) ?? seasonForDate());

export function useSeason() {
    watchEffect(() => {
        document.documentElement.dataset.season = current.value.id;
    });
    return { season: current, seasons: SEASONS };
}
