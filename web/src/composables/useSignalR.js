import * as signalR from '@microsoft/signalr';

import { onMounted, onUnmounted } from 'vue';

import { useCommunityStore } from '@/stores/community';
import { useShowStore } from '@/stores/show';

let connection = null;

export function useSignalR() {
    const showStore = useShowStore();
    const community = useCommunityStore();

    function connect() {
        if (connection) return;

        connection = new signalR.HubConnectionBuilder().withUrl('/showHub').withAutomaticReconnect().build();

        connection.on('QueueUpdated', queue => {
            showStore.queue = queue;
        });

        connection.on('NowPlayingChanged', nowPlaying => {
            showStore.setNowPlaying(nowPlaying);
        });

        connection.on('ShowConfigUpdated', config => {
            showStore.config = { ...config, theme: 'dark' };
        });

        connection.on('Activity', a => community.onActivity(a));
        connection.on('Cheer', c => community.onCheer(c));
        connection.on('CommunityUpdated', c => community.apply(c));
        connection.on('SupportersChanged', () => community.supportersVersion++);

        connection.start().catch(err => console.error('SignalR error:', err));
    }

    function disconnect() {
        connection?.stop();
        connection = null;
    }

    onMounted(connect);
    onUnmounted(() => {
        // Keep connection alive across component remounts
    });

    return { connect, disconnect };
}
