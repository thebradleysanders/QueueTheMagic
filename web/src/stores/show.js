import { defineStore } from 'pinia'
import { ref } from 'vue'
import axios from 'axios'

export const useShowStore = defineStore('show', () => {
  const queue = ref([])
  const nowPlaying = ref(null)
  const config = ref({
    siteName: 'Holiday Light Show',
    fmRadioStation: '',
    socialLinks: [],
    theme: 'dark',
    songRequestCost: 1.00,
    bumpCost: 1.00,
    donateCost: 5.00,
    isOpen: true,
    showSchedule: [],
    isSeasonActive: true,
    offSeasonMessage: '',
    stripePublishableKey: '',
  })

  let _tickerHandle = null

  function _stopTicker() {
    if (_tickerHandle !== null) {
      clearInterval(_tickerHandle)
      _tickerHandle = null
    }
  }

  function _startTicker() {
    _stopTicker()
    _tickerHandle = setInterval(() => {
      const np = nowPlaying.value
      if (!np?.isPlaying) { _stopTicker(); return }
      if ((np.secondsRemaining ?? 0) > 0) {
        nowPlaying.value = { ...np, secondsPlayed: (np.secondsPlayed ?? 0) + 1, secondsRemaining: np.secondsRemaining - 1 }
      }
    }, 1000)
  }

  function setNowPlaying(data) {
    nowPlaying.value = data
    if (data?.isPlaying) {
      _startTicker()
    } else {
      _stopTicker()
    }
  }

  async function fetchNowPlaying() {
    try {
      const { data } = await axios.get('/api/show/nowplaying')
      setNowPlaying(data.nowPlaying)
      config.value = data.config
    } catch (e) {
      console.error('Failed to fetch now playing', e)
    }
  }

  async function fetchQueue() {
    try {
      const { data } = await axios.get('/api/queue')
      queue.value = data
    } catch (e) {
      console.error('Failed to fetch queue', e)
    }
  }

  return { queue, nowPlaying, config, fetchNowPlaying, fetchQueue, setNowPlaying }
})
