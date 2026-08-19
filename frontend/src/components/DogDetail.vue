<script setup>
import { computed, onMounted, ref } from 'vue'
import { useModal } from '../useModal.js'
import { goodWith as splitGoodWith, goodWithBadges, joinList } from '../goodWith.js'

const props = defineProps({
  // The grid already holds the full listing, so pass it to avoid a round trip.
  // A shared or bookmarked ?dog= link has only the id — then we fetch.
  listing: { type: Object, default: null },
  listingId: { type: String, default: '' },
  favorite: { type: Boolean, default: false },
})

const emit = defineEmits(['close', 'toggle-favorite', 'search-similar'])

const fetched = ref(null)
const loading = ref(false)
const gone = ref(false) // adopted or pulled from the feed since the link was shared

const dog = computed(() => props.listing ?? fetched.value)

const imageFailed = ref(false)
const closeButton = ref(null)
const box = ref(null)

// Every photo the source published; the single imageUrl is the fallback for saved-dog
// snapshots and the county feeds, which carry one. Adopters consistently want more than one
// photo before committing, and RescueGroups was already sending them all.
const photos = computed(() => {
  if (!dog.value) return []
  if (dog.value.photos?.length) return dog.value.photos
  return dog.value.imageUrl ? [dog.value.imageUrl] : []
})
const photoIndex = ref(0)
const currentPhoto = computed(() => photos.value[photoIndex.value] ?? photos.value[0] ?? null)
function showPhoto(index) {
  photoIndex.value = index
  imageFailed.value = false // a broken photo shouldn't blank the ones that load
}

const metaLine = computed(() => {
  if (!dog.value) return ''
  const sex = dog.value.sex?.replace(/\s*\(.*\)\s*$/, '')
  const qualifier = dog.value.sex?.match(/\((.+)\)/)?.[1]
  return [
    sex,
    qualifier && qualifier[0].toUpperCase() + qualifier.slice(1),
    dog.value.age,
    dog.value.size,
  ].filter(Boolean).join(' · ')
})

const temperament = computed(() => (dog.value ? goodWithBadges(dog.value) : []))
// Named explicitly rather than left blank. The rescue not recording it is the common case, and
// the useful thing to tell an adopter is which question to ask when they call — not to imply
// the answer is no.
const unrecorded = computed(() => (dog.value ? splitGoodWith(dog.value).unknown : []))

// Escape, scroll lock, initial focus, the focus trap and focus restoration all come from the
// shared composable — this was the only dialog that had even the first three.
useModal(() => emit('close'), closeButton, box)

onMounted(async () => {
  if (!props.listing && props.listingId) {
    loading.value = true
    try {
      const res = await fetch(`/api/listings/${encodeURIComponent(props.listingId)}`)
      if (res.status === 404) gone.value = true
      else if (res.ok) fetched.value = await res.json()
      else gone.value = true
    } catch {
      gone.value = true
    } finally {
      loading.value = false
    }
  }
})


</script>

<template>
  <div class="modal modal-open" @click.self="emit('close')">
    <!-- aria-label only when there is no name to point at: setting both makes the label win
         and the dog's actual name lose, per the accName computation order. -->
    <div
      ref="box"
      class="modal-box max-w-3xl p-0"
      role="dialog"
      aria-modal="true"
      :aria-labelledby="dog ? 'dog-detail-name' : undefined"
      :aria-label="dog ? undefined : 'Dog details'"
    >
      <button
        ref="closeButton"
        type="button"
        class="btn btn-sm btn-circle bg-base-100/85 absolute top-3 right-3 z-20 border-none backdrop-blur-sm"
        aria-label="Close"
        @click="emit('close')"
      >
        ✕
      </button>

      <div v-if="loading" class="p-6">
        <div class="skeleton mb-4 h-64 w-full" />
        <div class="skeleton mb-2 h-6 w-1/3" />
        <div class="skeleton h-4 w-2/3" />
      </div>

      <!-- A dog leaving the feed is usually a happy ending, so say that rather than
           rendering an error. -->
      <div v-else-if="gone || !dog" class="card-body items-center gap-3 text-center">
        <span class="text-4xl">🏡</span>
        <h2 class="font-display text-2xl font-semibold">This dog is no longer listed</h2>
        <p class="max-w-prose text-sm opacity-70">
          They may well have found a home. The shelters below get new dogs constantly.
        </p>
        <button type="button" class="btn btn-primary" @click="emit('search-similar')">
          Find dogs like this
        </button>
      </div>

      <template v-else>
        <!-- object-contain, not cover: shelter photos come in every aspect ratio, and a
             fixed crop reliably cut the dog's head off. Letterboxing against the warm
             base reads as deliberate; a decapitated dog does not. -->
        <figure class="bg-base-300 relative flex h-72 w-full items-center justify-center sm:h-80">
          <img
            v-if="currentPhoto && !imageFailed"
            :src="currentPhoto"
            :alt="dog.name"
            class="max-h-full max-w-full object-contain"
            referrerpolicy="no-referrer"
            @error="imageFailed = true"
          />
          <span v-else class="grid h-full w-full place-items-center text-6xl" aria-hidden="true">🐶</span>
        </figure>

        <!-- Only when there is a real choice — one photo needs no picker. -->
        <div v-if="photos.length > 1" class="bg-base-300/50 flex gap-2 overflow-x-auto px-4 py-2">
          <button
            v-for="(photo, i) in photos"
            :key="photo"
            type="button"
            class="h-14 w-14 shrink-0 overflow-hidden rounded-lg transition-opacity"
            :class="i === photoIndex ? 'ring-primary ring-2' : 'opacity-70 hover:opacity-100'"
            :aria-label="`Photo ${i + 1} of ${photos.length} of ${dog.name}`"
            :aria-pressed="i === photoIndex"
            @click="showPhoto(i)"
          >
            <img
              :src="photo"
              alt=""
              class="h-full w-full object-cover"
              loading="lazy"
              referrerpolicy="no-referrer"
            />
          </button>
        </div>

        <div class="card-body gap-3">
          <div class="flex flex-wrap items-start justify-between gap-2">
            <div>
              <h2 id="dog-detail-name" class="font-display text-3xl font-semibold">{{ dog.name }}</h2>
              <p v-if="metaLine" class="text-sm opacity-70">{{ metaLine }}</p>
            </div>
            <button
              type="button"
              class="btn btn-sm gap-2"
              :class="favorite ? 'btn-primary' : 'btn-outline'"
              :aria-pressed="favorite"
              @click="emit('toggle-favorite')"
            >
              <svg class="h-4 w-4" viewBox="0 0 24 24" :fill="favorite ? 'currentColor' : 'none'"
                stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
                <path d="M21 8.25c0-2.485-2.099-4.5-4.688-4.5-1.935 0-3.597 1.126-4.312 2.733-.715-1.607-2.377-2.733-4.313-2.733C5.1 3.75 3 5.765 3 8.25c0 7.22 9 12 9 12s9-4.78 9-12Z" />
              </svg>
              {{ favorite ? 'Saved' : 'Save' }}
            </button>
          </div>

          <div class="flex flex-wrap gap-2">
            <span v-if="dog.ageGroup" class="badge badge-primary badge-soft">{{ dog.ageGroup }}</span>
            <span class="badge badge-soft">{{ dog.breed }}</span>
            <!-- 81% of adopters rank the fee the most important item on a profile, and it was
                 the one thing this screen never showed. -->
            <span v-if="dog.adoptionFee" class="badge badge-secondary badge-soft font-bold">
              Adoption fee {{ dog.adoptionFee }}
            </span>
          </div>

          <!--
            How the dog does with kids, dogs and cats — the other field adopters act on, and the
            one most likely to be blank. Known values are stated; blanks become a question to
            ask, never an implied "no".
          -->
          <div v-if="temperament.length || unrecorded.length" class="flex flex-col gap-1">
            <div v-if="temperament.length" class="flex flex-wrap gap-2">
              <span v-for="badge in temperament" :key="badge.text" class="badge" :class="badge.tone">
                {{ badge.text }}
              </span>
            </div>
            <p v-if="unrecorded.length" class="text-xs opacity-60 italic">
              This rescue hasn't recorded how {{ dog.name }} does with
              {{ joinList(unrecorded) }} — worth asking when you call.
            </p>
          </div>

          <p class="flex items-center gap-1.5 text-sm">
            <svg class="text-primary/80 h-4 w-4 shrink-0" viewBox="0 0 24 24" fill="none"
              stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"
              aria-hidden="true">
              <path d="M15 10.5a3 3 0 11-6 0 3 3 0 016 0z" />
              <path d="M19.5 10.5c0 7.142-7.5 11.25-7.5 11.25S4.5 17.642 4.5 10.5a7.5 7.5 0 1115 0z" />
            </svg>
            {{ dog.city }}, {{ dog.state }}
          </p>

          <p v-if="dog.description" class="max-w-prose text-sm leading-relaxed">{{ dog.description }}</p>
          <p v-else class="max-w-prose text-sm opacity-60 italic">
            {{ dog.name }}'s shelter hasn't written a bio yet — worth asking about
            temperament and history when you call.
          </p>

          <!-- The single most useful thing on this screen: who to call, and what to
               say. PetHarbor buries both, which is why visitors reported "no contact
               info" after clicking through. -->
          <div v-if="dog.contactInfo || dog.orgName" class="border-base-300 bg-base-200 rounded-box border p-3">
            <p class="text-xs font-bold tracking-wide uppercase opacity-60">Contact the shelter</p>
            <!-- The rescue that actually has the dog. "Source: RescueGroups" names the feed;
                 this names who picks up the phone. -->
            <p v-if="dog.orgName" class="mt-1 text-sm font-semibold">{{ dog.orgName }}</p>
            <p v-if="dog.contactInfo" class="mt-1 text-sm font-medium">{{ dog.contactInfo }}</p>
            <p v-if="dog.animalRef" class="mt-1 text-sm">
              Mention animal ID <strong>{{ dog.animalRef }}</strong> so they know which dog you mean.
            </p>
          </div>

          <!--
            Its own block, not a line inside the contact box. Nested there it only rendered for
            dogs that also published a phone number, so the dogs with the least information —
            exactly the ones whose reader most needs prompting — silently got nothing. Caught by
            an e2e check that had been passing only because the sampled dog happened to have a
            number.
          -->
          <p v-if="!dog.adoptionFee" class="max-w-prose text-sm opacity-70">
            This rescue hasn't listed an adoption fee — ask what it is and what it covers.
            Shelter and rescue fees usually run $50–$500 and include vaccinations, microchip and
            often spay or neuter.
          </p>

          <a
            class="btn btn-primary btn-block"
            :href="dog.listingUrl"
            target="_blank"
            rel="noopener noreferrer"
          >
            Start the adoption at {{ dog.source }} ↗
          </a>
          <p class="mx-auto max-w-prose text-center text-xs opacity-60">
            Listed by
            <a :href="dog.sourceUrl" target="_blank" rel="noopener noreferrer" class="link">{{ dog.source }}</a>
            — adoption fees, hours, and requirements are theirs, not ours.
          </p>
        </div>
      </template>
    </div>
  </div>
</template>
