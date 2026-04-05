import { defineStore } from 'pinia'
import { ref } from 'vue'

export const useSessionStore = defineStore('session', () => {
  const sessionToken = ref(localStorage.getItem('sessionToken') || generateToken())

  function generateToken() {
    const token = typeof crypto.randomUUID === 'function'
      ? crypto.randomUUID()
      : Array.from(crypto.getRandomValues(new Uint8Array(16)))
          .map((b, i) => ([4,6,8,10].includes(i) ? '-' : '') + b.toString(16).padStart(2, '0'))
          .join('')
    localStorage.setItem('sessionToken', token)
    return token
  }

  return { sessionToken }
})
