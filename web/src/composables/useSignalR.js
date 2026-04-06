import * as signalR from '@microsoft/signalr';

import { onMounted, onUnmounted } from 'vue';

import { useShowStore } from '@/stores/show';

let connection = null;

export function useSignalR() {
    const showStore = useShowStore();

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
            showStore.config = config;
        });

        connection.on('QueueItemAdded', item => {
            // QueueUpdated broadcast handles this; here as fallback
        });

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
