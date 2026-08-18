import { createApp, h } from 'vue'
import './style.css'
import App from './App.vue'
import SafetyPage from './components/SafetyPage.vue'
import { parseRoute } from './router.js'

// Two page kinds, chosen once at load. The safety guide carries no app state and the search
// app's state is entirely in its query string, so crossing between them is a real navigation
// — see router.js for why that is deliberate rather than a shortcut.
const route = parseRoute(window.location.pathname)

createApp(
  route.name === 'safety'
    ? { render: () => h(SafetyPage, { anchor: route.anchor }) }
    : App,
).mount('#app')
