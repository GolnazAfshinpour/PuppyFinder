<script setup>
import { onMounted, ref } from 'vue'
import { EMBED_META } from '../content/articles.js'
import { setMeta } from '../meta.js'
import SiteFooter from './SiteFooter.vue'
import SiteHeader from './SiteHeader.vue'

// The pitch page for rescues and shelters: a free, no-signup scam-check widget for their own
// warning pages. This is also the project's partnership surface — a rescue that embeds the
// check links to the site, which is how a small site earns the links that make the guides
// findable. The snippet includes a visible credit link on the HOST page deliberately: an
// iframe's contents pass no link value, and honesty about that beats hiding it.

const origin = ref('https://puppyfinder.example')
const copied = ref(false)

onMounted(() => {
  origin.value = window.location.origin
  document.title = EMBED_META.title
  setMeta('name', 'description', EMBED_META.description)
  setMeta('property', 'og:title', EMBED_META.title)
  setMeta('property', 'og:description', EMBED_META.description)
})

function snippet() {
  return `<iframe src="${origin.value}/widget/fee-check"\n`
    + '  title="Puppy fee scam check"\n'
    + '  style="width:100%;max-width:640px;height:720px;border:0;border-radius:12px"\n'
    + '  loading="lazy"></iframe>\n'
    + `<p><a href="${origin.value}/puppy-shipping-fee-scam">Puppy shipping fee scam check</a>`
    + ' by PuppyFinder</p>'
}

async function copy() {
  try {
    await navigator.clipboard.writeText(snippet())
    copied.value = true
    setTimeout(() => (copied.value = false), 2000)
  } catch {
    // The textarea below is selectable either way.
  }
}
</script>

<template>
  <SiteHeader />

  <main id="main" class="mx-auto max-w-3xl px-4 pt-8 pb-16 sm:px-6">
    <header class="mb-6">
      <h1 class="font-display text-3xl leading-[1.1] font-semibold tracking-tight sm:text-4xl">
        A scam check for your rescue's website
      </h1>
      <p class="text-base-content/70 mt-3 max-w-prose">
        If your rescue or shelter warns adopters about puppy scams, this widget turns the
        warning into something they can use: they type the fee a "seller" is demanding, and
        it's checked against the fees documented in published BBB and IPATA scam reports.
        Free, no signup, no tracking, nothing to maintain.
      </p>
    </header>

    <h2 class="font-display mb-2 text-2xl font-semibold">Paste this into your page</h2>
    <div class="bg-base-200 rounded-box relative p-4">
      <pre class="max-w-full overflow-x-auto text-xs leading-relaxed"><code>{{ snippet() }}</code></pre>
      <button type="button" class="btn btn-primary btn-sm absolute top-3 right-3" @click="copy">
        {{ copied ? '✓ Copied' : 'Copy' }}
      </button>
    </div>
    <p class="mt-2 max-w-prose text-xs opacity-60">
      The credit line under the iframe is part of the deal — it's how other rescues find the
      widget, and you're welcome to reword it as long as the link stays.
    </p>

    <h2 class="font-display mt-8 mb-2 text-2xl font-semibold">What it looks like</h2>
    <iframe
      src="/widget/fee-check"
      title="Puppy fee scam check (live preview)"
      class="rounded-box border-base-300 h-[720px] w-full max-w-xl border"
      loading="lazy"
    />

    <h2 class="font-display mt-8 mb-2 text-2xl font-semibold">What the check knows</h2>
    <ul class="list-inside list-disc space-y-2 text-sm">
      <li class="max-w-prose">
        The invented fees from published scam reports — crate rental, "refundable" shipping
        insurance, permits, customs on domestic shipments, quarantine and "release" fees —
        and the real costs too (deposits, transport, health certificates, adoption fees), so
        a legitimate charge is never called a scam.
      </li>
      <li class="max-w-prose">
        Whether the person has already paid — the advice changes from "don't send it" to
        "stop", because most of the loss lands on payments two, three and four.
      </li>
      <li class="max-w-prose">
        Who is asking: a transport company that made contact on its own is the scam's
        documented second act, whatever the fee is called.
      </li>
    </ul>

    <p class="mt-6 max-w-prose text-sm">
      Questions, or want a version tuned for your organisation's page? Open an issue on
      <a
        href="https://github.com/GolnazAfshinpour/PuppyFinder"
        target="_blank"
        rel="noopener noreferrer"
        class="link"
      >the project's GitHub</a> — the whole app is open source.
    </p>
  </main>

  <SiteFooter />
</template>
