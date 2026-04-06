import { createRouter, createWebHistory } from 'vue-router';

import { useAdminStore } from '@/stores/admin';

const router = createRouter({
    history: createWebHistory(import.meta.env.BASE_URL),
    routes: [
        { path: '/', name: 'home', component: () => import('../views/HomeView.vue') },
        { path: '/queue', name: 'queue', component: () => import('../views/QueueView.vue') },
        { path: '/playlists', name: 'playlists', component: () => import('../views/PlaylistsView.vue') },
        { path: '/info', name: 'info', component: () => import('../views/InfoView.vue') },
        { path: '/admin/login', name: 'admin-login', component: () => import('../views/AdminLoginView.vue') },
        {
            path: '/admin',
            name: 'admin',
            component: () => import('../views/AdminDashboardView.vue'),
            beforeEnter: () => {
                const admin = useAdminStore();
                if (!admin.token) return '/admin/login';
            }
        }
    ]
});

export default router;
