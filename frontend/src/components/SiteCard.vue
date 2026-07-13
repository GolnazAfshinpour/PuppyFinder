<script setup>
defineProps({
  site: { type: Object, required: true },
})
</script>

<template>
  <li class="card">
    <div class="card-body">
      <div class="card-title">
        <h2>{{ site.name }}</h2>
        <span class="tag" :class="{ buy: site.kind !== 'Adopt' }">{{ site.kind }}</span>
      </div>
      <p class="description">{{ site.description }}</p>
      <dl class="facts">
        <div class="fact">
          <dt>Vetting</dt>
          <dd>{{ site.vetting }}</dd>
        </div>
        <div class="fact">
          <dt>Price</dt>
          <dd>{{ site.priceNote }}</dd>
        </div>
        <div class="fact">
          <dt>Getting your dog</dt>
          <dd>{{ site.delivery }}</dd>
        </div>
        <div class="fact">
          <dt>Best for</dt>
          <dd>{{ site.bestFor }}</dd>
        </div>
      </dl>
      <a class="cta" :href="site.linkUrl" target="_blank" rel="noopener noreferrer">
        {{ site.linkLabel }} ↗
      </a>
    </div>
  </li>
</template>

<style scoped>
.card {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  box-shadow: var(--shadow);
  transition: box-shadow 0.25s, transform 0.25s;
  display: flex;
}

.card:hover {
  box-shadow: var(--shadow-hover);
  transform: translateY(-3px);
}

.card-body {
  padding: 1.4rem 1.5rem 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 0.7rem;
  flex: 1;
}

.card-title {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
}

.card-title h2 {
  font-size: 1.15rem;
  margin: 0;
}

.tag {
  font-size: 0.72rem;
  font-weight: 600;
  padding: 0.2rem 0.65rem;
  border-radius: 999px;
  background: #e8f5ee;
  color: #1e7e4d;
  white-space: nowrap;
}

.tag.buy {
  background: var(--accent-soft);
  color: var(--accent);
}

.description {
  margin: 0;
  font-size: 0.9rem;
  color: var(--text);
}

.facts {
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
  border-top: 1px dashed var(--border);
  padding-top: 0.7rem;
  flex: 1;
}

.fact {
  display: flex;
  gap: 0.5rem;
  font-size: 0.8rem;
}

.fact dt {
  flex: 0 0 7.5rem;
  color: var(--text-muted);
  font-weight: 600;
}

.fact dd {
  margin: 0;
  color: var(--text);
}

.cta {
  display: block;
  text-align: center;
  padding: 0.6rem 1rem;
  border-radius: 999px;
  background: var(--accent);
  color: #fff;
  font-size: 0.9rem;
  font-weight: 600;
  text-decoration: none;
  transition: filter 0.2s;
}

.cta:hover {
  filter: brightness(1.1);
}
</style>
