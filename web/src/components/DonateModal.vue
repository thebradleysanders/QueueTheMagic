<script setup>
import { ref, computed } from 'vue'
import axios from 'axios'
import { useShowStore } from '@/stores/show'
import { useSessionStore } from '@/stores/session'
import { usePayment } from '@/composables/usePayment'

const emit = defineEmits(['close'])
const showStore = useShowStore()
const sessionStore = useSessionStore()
const { createIntent, confirmPayment } = usePayment()

const isDev = import.meta.env.DEV
const min = computed(() => Number(showStore.config.donateCost) || 1)
const presets = computed(() => {
  const m = min.value
  return [m, m * 2, m * 5, m * 10].filter((v, i, a) => a.indexOf(v) === i)
})

const selectedAmount = ref(null)
const customAmount = ref('')
const step = ref('select')
const errorMsg = ref(null)
const loading = ref(false)

const amount = computed(() => {
  if (selectedAmount.value !== null) return selectedAmount.value
  const v = parseFloat(customAmount.value)
  return isNaN(v) ? null : v
})

const amountValid = computed(() => amount.value !== null && amount.value >= min.value)

function pickPreset(v) {
  selectedAmount.value = v
  customAmount.value = ''
}

function onCustomInput() {
  selectedAmount.value = null
}

async function donate() {
  if (!amountValid.value) return
  loading.value = true
  errorMsg.value = null
  try {
    let paymentIntentId
    if (isDev) {
      paymentIntentId = 'dev_test'
    } else {
      const clientSecret = await createIntent('donate', null, amount.value)
      step.value = 'paying'
      paymentIntentId = await confirmPayment(clientSecret, window.location.href)
    }
    await axios.post('/api/payment/donate', {
      paymentIntentId,
      sessionToken: sessionStore.sessionToken,
      amount: amount.value,
    })
    step.value = 'done'
  } catch (e) {
    errorMsg.value = e.response?.data?.error || e.message || 'Something went wrong.'
    step.value = 'select'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="fixed inset-0 bg-black/60 flex items-center justify-center z-50 p-4" @click.self="emit('close')">
    <div class="bg-card rounded-2xl w-full max-w-sm border border-base overflow-hidden">

      <div class="flex items-center justify-between px-6 py-4 border-b border-base">
        <h2 class="text-lg font-bold text-main">Donate Just Because ❤️</h2>
        <button @click="emit('close')" class="text-hint hover:text-main text-xl">✕</button>
      </div>

      <div v-if="step === 'select'" class="p-5">
        <p class="text-sm text-hint mb-4">Support the show — no song required!</p>

        <div v-if="errorMsg" class="mb-4 p-3 bg-red-500/20 text-red-500 rounded-lg text-sm">{{ errorMsg }}</div>

        <div class="grid grid-cols-4 gap-2 mb-4">
          <button
            v-for="v in presets" :key="v"
            @click="pickPreset(v)"
            class="py-2.5 rounded-lg text-sm font-semibold transition border"
            :class="selectedAmount === v
              ? 'bg-green-500/20 text-green-600 border-green-500/40'
              : 'bg-lift text-sub border-base hover:bg-inset'"
          >
            ${{ v % 1 === 0 ? v : v.toFixed(2) }}
          </button>
        </div>

        <div class="relative mb-5">
          <span class="absolute left-3 top-1/2 -translate-y-1/2 text-hint text-sm">$</span>
          <input
            v-model="customAmount"
            @input="onCustomInput"
            type="number"
            :min="min"
            step="0.01"
            placeholder="Other amount"
            class="w-full bg-lift border border-base rounded-lg pl-7 pr-3 py-2 text-main text-sm focus:outline-none focus:border-green-500"
            :class="{ 'border-green-500/50': selectedAmount === null && amountValid }"
          />
        </div>

        <button
          @click="donate"
          :disabled="!amountValid || loading"
          class="w-full py-3 bg-green-600 hover:bg-green-500 disabled:opacity-40 text-white font-semibold rounded-xl transition"
        >
          {{ loading ? 'Processing...' : isDev ? `⚡ Donate $${amount ?? '—'} (dev)` : `Donate $${amount?.toFixed(2) ?? '—'}` }}
        </button>
      </div>

      <div v-if="step === 'paying'" class="p-5 text-center text-hint">
        Processing payment...
      </div>

      <div v-if="step === 'done'" class="p-8 text-center">
        <div class="text-4xl mb-3">🙏</div>
        <p class="text-lg font-bold text-main">Thank you!</p>
        <p class="text-sm text-hint mt-1">Your donation keeps the lights on.</p>
        <button @click="emit('close')" class="mt-4 px-6 py-2 bg-green-600 hover:bg-green-500 text-white rounded-lg text-sm">Done</button>
      </div>

    </div>
  </div>
</template>
