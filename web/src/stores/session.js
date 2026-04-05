import { defineStore } from 'pinia'
import { ref } from 'vue'

export const useSessionStore = defineStore('session', () => {
  const sessionToken = ref(localStorage.getItem('sessionToken') || generateToken())

  function generateToken() {
    const token = crypto.randomUUID()
    localStorage.setItem('sessionToken', token)
    return token
  }

  return { sessionToken }
})
