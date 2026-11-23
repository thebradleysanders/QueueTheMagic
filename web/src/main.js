import { library } from '@fortawesome/fontawesome-svg-core';
import { faGithub } from '@fortawesome/free-brands-svg-icons';
import {
    faCalendarDays,
    faChartBar,
    faChevronLeft,
    faChevronRight,
    faCircleInfo,
    faClover,
    faGear,
    faGhost,
    faGift,
    faHeart,
    faHouse,
    faLeaf,
    faLightbulb,
    faListOl,
    faMusic,
    faRotate,
    faSeedling,
    faSnowflake,
    faStar,
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
    faCalendarDays, faChartBar, faChevronLeft, faChevronRight, faCircleInfo, faClover, faGear, faGhost, faGift,
    faHeart, faHouse, faLeaf, faLightbulb, faListOl, faMusic, faRotate, faSeedling, faSnowflake, faStar,
    faTriangleExclamation, faUserGear, faWandMagicSparkles, faXmark
);

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
