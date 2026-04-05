<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAdminStore } from '@/stores/admin'

const admin = useAdminStore()
const router = useRouter()
const password = ref('')
const error = ref(null)
const loading = ref(false)

async function login() {
  loading.value = true
  error.value = null
  try {
    await admin.login(password.value)
    router.push('/admin')
  } catch (e) {
    if (e.response?.status === 401) {
      error.value = 'Invalid password.'
    } else {
      error.value = `Cannot reach API (${e.message}). Is the backend running on port 5288?`
    }
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <main class="min-h-screen flex items-center justify-center px-4">
    <div class="w-full max-w-sm">
      <h1 class="text-2xl font-bold text-center mb-8">Admin Login</h1>
      <form @submit.prevent="login" class="bg-gray-900 rounded-2xl p-6 border border-gray-800">
        <div class="mb-4">
          <label class="block text-sm text-gray-400 mb-2">Password</label>
          <input
            v-model="password"
            type="password"
            class="w-full bg-gray-800 border border-gray-700 rounded-lg px-4 py-2.5 text-white focus:outline-none focus:border-green-500"
            placeholder="Admin password"
            autofocus
          />
        </div>
        <div v-if="error" class="mb-4 text-sm text-red-400">{{ error }}</div>
        <button
          type="submit"
          :disabled="loading"
          class="w-full py-2.5 bg-green-600 hover:bg-green-500 disabled:opacity-50 text-white font-semibold rounded-lg"
        >
          {{ loading ? 'Logging in...' : 'Login' }}
        </button>
      </form>
    </div>
  </main>
</template>
