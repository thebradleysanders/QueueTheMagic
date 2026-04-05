import { ref } from 'vue'
import { loadStripe } from '@stripe/stripe-js'
import axios from 'axios'
import { useSessionStore } from '@/stores/session'
import { useShowStore } from '@/stores/show'

let stripePromise = null
let stripeKey = null

function getStripe() {
  const key = useShowStore().config.stripePublishableKey
  if (!stripePromise || key !== stripeKey) {
    stripeKey = key
    stripePromise = loadStripe(key)
  }
  return stripePromise
}

export function usePayment() {
  const sessionStore = useSessionStore()
  const loading = ref(false)
  const error = ref(null)

  async function createIntent(type, songId, amount = null) {
    const { data } = await axios.post('/api/payment/create-intent', {
      type,
      songId,
      sessionToken: sessionStore.sessionToken,
      amount,
    })
    return data.clientSecret
  }

  async function confirmPayment(clientSecret, returnUrl) {
    const stripe = await getStripe()
    const result = await stripe.confirmPayment({
      clientSecret,
      confirmParams: { return_url: returnUrl },
      redirect: 'if_required',
    })
    if (result.error) throw new Error(result.error.message)
    return result.paymentIntent?.id
  }

  return { loading, error, createIntent, confirmPayment }
}
