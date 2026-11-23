<template>
  <svg viewBox="0 0 64 64" class="star-logo" aria-hidden="true">
    <defs>
      <linearGradient id="qtm-star" x1="0" y1="0" x2="1" y2="1">
        <stop offset="0" stop-color="rgb(var(--ac2))" />
        <stop offset="1" stop-color="rgb(var(--ac1))" />
      </linearGradient>
    </defs>
    <path
      d="M32 7l6.2 14.6 15.8 1.4-12 10.4 3.6 15.4L32 40.6 18.4 48.8 22 33.4 10 23l15.8-1.4z"
      fill="url(#qtm-star)"
    />
    <rect x="24" y="52" width="16" height="4" rx="2" fill="rgb(var(--ac3))" />
  </svg>
</template>

<style scoped>
.star-logo {
  width: 1em;
  height: 1em;
  filter: drop-shadow(0 0 6px rgb(var(--ac1) / 0.6));
}
</style>
