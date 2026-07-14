// Random breed photos from the free, keyless dog.ceo API.
// Cached per breed path so a breed shows the same photo for the whole session.
const cache = new Map()

export async function fetchBreedImage(imagePath) {
  if (!imagePath) return null
  if (cache.has(imagePath)) return cache.get(imagePath)
  try {
    const res = await fetch(`https://dog.ceo/api/breed/${imagePath}/images/random`)
    if (!res.ok) return null
    const data = await res.json()
    const url = data.status === 'success' ? data.message : null
    if (url) cache.set(imagePath, url)
    return url
  } catch {
    return null // photos are decoration — never block the UI on them
  }
}
