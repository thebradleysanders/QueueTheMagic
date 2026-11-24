import { library } from '@fortawesome/fontawesome-svg-core';
import { faGithub } from '@fortawesome/free-brands-svg-icons';
import {
    faCalendarDays,
    faBolt,
    faChartBar,
    faCheck,
    faChevronDown,
    faChevronLeft,
    faChevronRight,
    faChevronUp,
    faGripVertical,
    faCircleInfo,
    faClover,
    faGear,
    faGhost,
    faGift,
    faHandsClapping,
    faHeadphones,
    faHeart,
    faHouse,
    faLeaf,
    faLightbulb,
    faListOl,
    faMusic,
    faPlay,
    faRadio,
    faRotate,
    faSeedling,
    faSnowflake,
    faStar,
    faStop,
    faTriangleExclamation,
    faUserGear,
    faWandMagicSparkles,
    faXmark
} from '@fortawesome/free-solid-svg-icons';
import { FontAwesomeIcon } from '@fortawesome/vue-fontawesome';
import Aura from '@primevue/themes/aura';
import { createPinia } from 'pinia';
import 'primeicons/primeicons.css';
import PrimeVue from 'primevue/config';

import { createApp } from 'vue';

import App from './App.vue';
import './assets/main.css';
import router from './router';

// Register only the icons we use (the full set adds ~1.5 MB to the bundle).
library.add(
    faGithub,
    faBolt, faCalendarDays, faChartBar, faCheck, faChevronDown, faChevronLeft, faChevronRight, faChevronUp, faGripVertical, faCircleInfo, faClover, faGear, faGhost, faGift, faHandsClapping, faHeadphones,
    faHeart, faHouse, faLeaf, faLightbulb, faListOl, faMusic, faPlay, faRadio, faRotate, faSeedling, faSnowflake, faStar, faStop,
    faTriangleExclamation, faUserGear, faWandMagicSparkles, faXmark
);

// iOS Safari ignores user-scalable=no, so block its pinch gesture directly.
['gesturestart', 'gesturechange', 'gestureend'].forEach(type => document.addEventListener(type, e => e.preventDefault()));

const app = createApp(App);

app.component('font-awesome-icon', FontAwesomeIcon);
app.use(createPinia());
app.use(router);
app.use(PrimeVue, {
    theme: {
        preset: Aura,
        options: {
            darkModeSelector: '.dark'
        }
    }
});

app.mount('#app');
