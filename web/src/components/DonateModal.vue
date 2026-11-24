<script setup>
    import SupporterNameField from '@/components/SupporterNameField.vue';
    import { useSupporterName } from '@/composables/useSupporterName';
    import Celebrate from '@/components/Celebrate.vue';
    import axios from 'axios';

    import { computed, nextTick, onMounted, onUnmounted, ref } from 'vue';

    import { usePayment } from '@/composables/usePayment';

    import { useSessionStore } from '@/stores/session';
    import { useShowStore } from '@/stores/show';

    const emit = defineEmits(['close']);
    const showStore = useShowStore();
    const sessionStore = useSessionStore();
    const { displayName } = useSupporterName();
    const { createIntent, mountPaymentElement, confirmPayment } = usePayment();

    const isDev = import.meta.env.DEV;

    function onKey(e) {
        if (e.key === 'Escape' && !loading.value) emit('close');
    }
    onMounted(() => window.addEventListener('keydown', onKey));
    onUnmounted(() => window.removeEventListener('keydown', onKey));

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
                    amount: amount.value,
                    displayName: displayName.value
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
                amount: amount.value,
                displayName: displayName.value
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
    <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4" role="dialog" aria-modal="true" aria-label="Donate" @click.self="emit('close')">
        <div class="bg-card border-base w-full max-w-sm overflow-hidden rounded-xl border">
            <div class="border-base flex items-center justify-between border-b px-6 py-4">
                <h2 class="text-main text-lg font-semibold">Donate</h2>
                <button @click="emit('close')" aria-label="Close" class="text-hint hover:text-main text-lg"><font-awesome-icon :icon="['fas', 'xmark']" /></button>
            </div>

            <div v-if="step === 'select'" class="p-5">
                <p class="text-hint mb-4 text-sm">Support the show. No song required.</p>

                <div v-if="errorMsg" class="mb-4 rounded-lg bg-red-500/20 p-3 text-sm text-red-500">{{ errorMsg }}</div>

                <div class="mb-4 grid grid-cols-4 gap-2">
                    <button v-for="v in presets" :key="v" @click="pickPreset(v)" class="rounded-lg border py-2.5 text-sm font-semibold transition" :class="selectedAmount === v ? 'border-ac1-soft bg-ac1-soft text-ac1-strong' : 'bg-lift text-sub border-base hover:bg-inset'">
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
                        class="bg-lift border-base text-main w-full rounded-lg border py-2 pr-3 pl-7 text-sm focus-ac1"
                        :class="{ 'border-ac1-soft': selectedAmount === null && amountValid }"
                    />
                </div>

                <SupporterNameField class="mb-4" />

                <button @click="donate" :disabled="!amountValid || loading" class="btn-ac w-full rounded-xl py-3 font-semibold transition disabled:opacity-40">
                    {{ loading ? 'Processing...' : isDev ? `Donate $${amount ?? '-'} (dev)` : `Donate $${amount?.toFixed(2) ?? '-'}` }}
                </button>
            </div>

            <div v-if="step === 'payment'" class="p-5">
                <div v-if="errorMsg" class="mb-3 rounded-lg bg-red-500/20 p-3 text-sm text-red-400">{{ errorMsg }}</div>
                <div ref="paymentElementRef"></div>
                <button @click="submitPayment" :disabled="loading" class="btn-ac mt-4 w-full rounded-xl py-3 font-semibold transition disabled:opacity-40">
                    {{ loading ? 'Processing...' : `Donate $${amount?.toFixed(2)}` }}
                </button>
            </div>

            <div v-if="step === 'paying'" class="text-hint p-5 text-center">Processing payment...</div>

            <div v-if="step === 'done'" class="p-8 text-center">
                <Celebrate :count="28" :spread="140"><font-awesome-icon :icon="['fas', 'heart']" class="text-ac2 mb-3 text-3xl" /></Celebrate>
                <p class="text-main text-lg font-semibold">Thank you!</p>
                <p class="text-hint mt-1 text-sm">Your donation helps keep the lights on.</p>
                <button @click="emit('close')" class="btn-ac mt-4 rounded-lg px-6 py-2 text-sm">Done</button>
            </div>
        </div>
    </div>
</template>
