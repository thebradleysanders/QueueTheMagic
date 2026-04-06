<script setup>
    import axios from 'axios';

    import { computed, nextTick, ref } from 'vue';

    import { usePayment } from '@/composables/usePayment';

    import { useSessionStore } from '@/stores/session';
    import { useShowStore } from '@/stores/show';

    const emit = defineEmits(['close']);
    const showStore = useShowStore();
    const sessionStore = useSessionStore();
    const { createIntent, mountPaymentElement, confirmPayment } = usePayment();

    const isDev = import.meta.env.DEV;
    const min = computed(() => Number(showStore.config.donateCost) || 1);
    const presets = computed(() => {
        const m = min.value;
        return [m, m * 2, m * 5, m * 10].filter((v, i, a) => a.indexOf(v) === i);
    });

    const selectedAmount = ref(null);
    const customAmount = ref('');
    const step = ref('select');
    const errorMsg = ref(null);
    const loading = ref(false);
    const paymentElementRef = ref(null);

    const amount = computed(() => {
        if (selectedAmount.value !== null) return selectedAmount.value;
        const v = parseFloat(customAmount.value);
        return isNaN(v) ? null : v;
    });

    const amountValid = computed(() => amount.value !== null && amount.value >= min.value);

    function pickPreset(v) {
        selectedAmount.value = v;
        customAmount.value = '';
    }

    function onCustomInput() {
        selectedAmount.value = null;
    }

    async function donate() {
        if (!amountValid.value) return;
        loading.value = true;
        errorMsg.value = null;
        try {
            if (isDev) {
                await axios.post('/api/payment/donate', {
                    paymentIntentId: 'dev_test',
                    sessionToken: sessionStore.sessionToken,
                    amount: amount.value
                });
                step.value = 'done';
                return;
            }
            const clientSecret = await createIntent('donate', null, amount.value);
            step.value = 'payment';
            await nextTick();
            await mountPaymentElement(clientSecret, paymentElementRef.value);
        } catch (e) {
            errorMsg.value = e.response?.data?.error || e.message || 'Something went wrong.';
            step.value = 'select';
        } finally {
            loading.value = false;
        }
    }

    async function submitPayment() {
        loading.value = true;
        errorMsg.value = null;
        try {
            const paymentIntentId = await confirmPayment(window.location.href);
            await axios.post('/api/payment/donate', {
                paymentIntentId,
                sessionToken: sessionStore.sessionToken,
                amount: amount.value
            });
            step.value = 'done';
        } catch (e) {
            errorMsg.value = e.response?.data?.error || e.message || 'Something went wrong.';
        } finally {
            loading.value = false;
        }
    }
</script>

<template>
    <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4" @click.self="emit('close')">
        <div class="bg-card border-base w-full max-w-sm overflow-hidden rounded-2xl border">
            <div class="border-base flex items-center justify-between border-b px-6 py-4">
                <h2 class="text-main text-lg font-bold">Donate Just Because ❤️</h2>
                <button @click="emit('close')" class="text-hint hover:text-main text-xl">✕</button>
            </div>

            <div v-if="step === 'select'" class="p-5">
                <p class="text-hint mb-4 text-sm">Support the show — no song required!</p>

                <div v-if="errorMsg" class="mb-4 rounded-lg bg-red-500/20 p-3 text-sm text-red-500">{{ errorMsg }}</div>

                <div class="mb-4 grid grid-cols-4 gap-2">
                    <button v-for="v in presets" :key="v" @click="pickPreset(v)" class="rounded-lg border py-2.5 text-sm font-semibold transition" :class="selectedAmount === v ? 'border-green-500/40 bg-green-500/20 text-green-600' : 'bg-lift text-sub border-base hover:bg-inset'">
                        ${{ v % 1 === 0 ? v : v.toFixed(2) }}
                    </button>
                </div>

                <div class="relative mb-5">
                    <span class="text-hint absolute top-1/2 left-3 -translate-y-1/2 text-sm">$</span>
                    <input
                        v-model="customAmount"
                        @input="onCustomInput"
                        type="number"
                        :min="min"
                        step="0.01"
                        placeholder="Other amount"
                        class="bg-lift border-base text-main w-full rounded-lg border py-2 pr-3 pl-7 text-sm focus:border-green-500 focus:outline-none"
                        :class="{ 'border-green-500/50': selectedAmount === null && amountValid }"
                    />
                </div>

                <button @click="donate" :disabled="!amountValid || loading" class="w-full rounded-xl bg-green-600 py-3 font-semibold text-white transition hover:bg-green-500 disabled:opacity-40">
                    {{ loading ? 'Processing...' : isDev ? `⚡ Donate $${amount ?? '—'} (dev)` : `Donate $${amount?.toFixed(2) ?? '—'}` }}
                </button>
            </div>

            <div v-if="step === 'payment'" class="p-5">
                <div v-if="errorMsg" class="mb-3 rounded-lg bg-red-500/20 p-3 text-sm text-red-400">{{ errorMsg }}</div>
                <div ref="paymentElementRef"></div>
                <button @click="submitPayment" :disabled="loading" class="mt-4 w-full rounded-xl bg-green-600 py-3 font-semibold text-white transition hover:bg-green-500 disabled:opacity-40">
                    {{ loading ? 'Processing...' : `Donate $${amount?.toFixed(2)}` }}
                </button>
            </div>

            <div v-if="step === 'paying'" class="text-hint p-5 text-center">Processing payment...</div>

            <div v-if="step === 'done'" class="p-8 text-center">
                <div class="mb-3 text-4xl">🙏</div>
                <p class="text-main text-lg font-bold">Thank you!</p>
                <p class="text-hint mt-1 text-sm">Your donation keeps the lights on.</p>
                <button @click="emit('close')" class="mt-4 rounded-lg bg-green-600 px-6 py-2 text-sm text-white hover:bg-green-500">Done</button>
            </div>
        </div>
    </div>
</template>
