import axios from 'axios';
import { defineStore } from 'pinia';

import { ref } from 'vue';

export const useAdminStore = defineStore('admin', () => {
    const token = ref(localStorage.getItem('adminToken') || null);
    const config = ref(null);
    const reports = ref(null);
    const diagnostics = ref(null);

    function setToken(t) {
        token.value = t;
        if (t) localStorage.setItem('adminToken', t);
        else localStorage.removeItem('adminToken');
    }

    function logout() {
        setToken(null);
    }

    function authHeaders() {
        return token.value ? { Authorization: `Bearer ${token.value}` } : {};
    }

    async function login(password) {
        const { data } = await axios.post('/api/admin/login', { password });
        setToken(data.token);
    }

    async function fetchConfig() {
        const { data } = await axios.get('/api/admin/config', { headers: authHeaders() });
        config.value = data;
    }

    async function saveConfig(updated) {
        const { data } = await axios.put('/api/admin/config', updated, { headers: authHeaders() });
        config.value = data;
    }

    async function fetchReports() {
        const { data } = await axios.get('/api/admin/reports', { headers: authHeaders() });
        reports.value = data;
    }

    async function fetchDiagnostics() {
        const { data } = await axios.get('/api/admin/diagnostics', { headers: authHeaders() });
        diagnostics.value = data;
    }

    async function testMqtt() {
        await axios.post('/api/admin/mqtt/test', {}, { headers: authHeaders() });
    }

    async function syncSongs() {
        const { data } = await axios.post('/api/admin/sync-songs', {}, { headers: authHeaders() });
        return data;
    }

    async function syncPlaylists() {
        const { data } = await axios.post('/api/admin/sync-playlists', {}, { headers: authHeaders() });
        return data;
    }

    const playlists = ref([]);
    const ratings = ref([]);

    async function fetchPlaylists() {
        const { data } = await axios.get('/api/admin/playlists', { headers: authHeaders() });
        playlists.value = data;
    }

    async function updatePlaylist(id, name, isEnabled) {
        const { data } = await axios.put(`/api/admin/playlists/${id}`, { name, isEnabled }, { headers: authHeaders() });
        const idx = playlists.value.findIndex(p => p.id === id);
        if (idx !== -1) playlists.value[idx] = { ...playlists.value[idx], name: data.name, isEnabled: data.isEnabled };
    }

    async function skipQueueItem(id) {
        await axios.post(`/api/admin/queue/${id}/skip`, {}, { headers: authHeaders() });
    }

    async function syncSchedule() {
        const { data } = await axios.post('/api/admin/sync-schedule', {}, { headers: authHeaders() });
        return data;
    }

    async function fetchRatings() {
        const { data } = await axios.get('/api/admin/ratings', { headers: authHeaders() });
        ratings.value = data;
    }

    return {
        token,
        config,
        reports,
        diagnostics,
        playlists,
        ratings,
        login,
        logout,
        fetchConfig,
        saveConfig,
        fetchReports,
        fetchDiagnostics,
        testMqtt,
        syncSongs,
        syncPlaylists,
        fetchPlaylists,
        updatePlaylist,
        skipQueueItem,
        fetchRatings,
        syncSchedule,
        authHeaders
    };
});
