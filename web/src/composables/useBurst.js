/**
 * A little burst of coloured "lights" from an element: used for cheers and thank-yous.
 * Pure DOM + CSS (see .burst-dot in main.css), cleans up after itself, honours reduced motion.
 */
export function burst(el, { count = 18, spread = 90 } = {}) {
    if (!el || window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) return;
    const rect = el.getBoundingClientRect();
    const cx = rect.left + rect.width / 2;
    const cy = rect.top + rect.height / 2;

    for (let i = 0; i < count; i++) {
        const dot = document.createElement('span');
        const angle = (Math.PI * 2 * i) / count + Math.random() * 0.4;
        const dist = spread * (0.55 + Math.random() * 0.6);
        dot.className = 'burst-dot';
        dot.style.left = `${cx}px`;
        dot.style.top = `${cy}px`;
        dot.style.setProperty('--dx', `${Math.cos(angle) * dist}px`);
        dot.style.setProperty('--dy', `${Math.sin(angle) * dist}px`);
        dot.style.setProperty('--c', `rgb(var(--ac${(i % 3) + 1}))`);
        dot.style.animationDelay = `${Math.random() * 60}ms`;
        document.body.appendChild(dot);
        dot.addEventListener('animationend', () => dot.remove(), { once: true });
    }
}
