import { createApp, h } from 'vue'
import './style.css'
import App from './App.vue'
import ArticlePage from './components/ArticlePage.vue'
import DogPage from './components/DogPage.vue'
import EmbedPage from './components/EmbedPage.vue'
import SafetyPage from './components/SafetyPage.vue'
import WidgetPage from './components/WidgetPage.vue'
import { parseRoute } from './router.js'

// Page kind chosen once at load. None of these share state with the search app (whose state
// is entirely in its query string), so crossing between them is a real navigation — see
// router.js for why that is deliberate rather than a shortcut.
const route = parseRoute(window.location.pathname)

const page = {
  safety: () => ({ render: () => h(SafetyPage, { anchor: route.anchor }) }),
  article: () => ({ render: () => h(ArticlePage, { slug: route.slug }) }),
  dog: () => ({ render: () => h(DogPage, { id: route.id }) }),
  widget: () => WidgetPage,
  embed: () => EmbedPage,
  app: () => App,
}

createApp(page[route.name]()).mount('#app')
