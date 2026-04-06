<script setup>
    import { useRouter } from 'vue-router';

    import { ref } from 'vue';

    import { useAdminStore } from '@/stores/admin';

    const admin = useAdminStore();
    const router = useRouter();
    const password = ref('');
    const error = ref(null);
    const loading = ref(false);

    async function login() {
        loading.value = true;
        error.value = null;
        try {
            await admin.login(password.value);
            router.push('/admin');
        } catch (e) {
            if (e.response?.status === 401) {
                error.value = 'Invalid password.';
            } else {
                error.value = `Cannot reach API (${e.message}). Is the backend running on port 5288?`;
            }
        } finally {
            loading.value = false;
        }
    }
</script>

<template>
    <main class="flex min-h-screen items-center justify-center px-4">
        <div class="w-full max-w-sm">
            <h1 class="mb-8 text-center text-2xl font-bold">Admin Login</h1>
            <form @submit.prevent="login" class="rounded-2xl border border-gray-800 bg-gray-900 p-6">
                <div class="mb-4">
                    <label class="mb-2 block text-sm text-gray-400">Password</label>
                    <input v-model="password" type="password" class="w-full rounded-lg border border-gray-700 bg-gray-800 px-4 py-2.5 text-white focus:border-green-500 focus:outline-none" placeholder="Admin password" autofocus />
                </div>
                <div v-if="error" class="mb-4 text-sm text-red-400">{{ error }}</div>
                <button type="submit" :disabled="loading" class="w-full rounded-lg bg-green-600 py-2.5 font-semibold text-white hover:bg-green-500 disabled:opacity-50">
                    {{ loading ? 'Logging in...' : 'Login' }}
                </button>
            </form>
        </div>
    </main>
</template>
