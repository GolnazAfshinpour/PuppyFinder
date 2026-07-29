<template>
  <!-- Friendly pup with a collar in brand colors — the honest-guide identity
       (the old wizard hat said "fantasy toy"; the product is a trust tool). -->
  <svg viewBox="20 50 100 74" xmlns="http://www.w3.org/2000/svg" aria-label="PuppyFinder logo">
    <!-- ears -->
    <ellipse cx="38" cy="86" rx="12" ry="22" fill="#a97c50" transform="rotate(18 38 86)" />
    <ellipse cx="102" cy="86" rx="12" ry="22" fill="#a97c50" transform="rotate(-18 102 86)" />

    <!-- head -->
    <circle cx="70" cy="88" r="34" fill="#e8c99b" />

    <!-- muzzle -->
    <ellipse cx="70" cy="102" rx="16" ry="12" fill="#f8ecd7" />

    <!-- eyes -->
    <circle cx="58" cy="84" r="4.2" fill="#3d2b1f" />
    <circle cx="82" cy="84" r="4.2" fill="#3d2b1f" />
    <circle cx="59.5" cy="82.5" r="1.4" fill="#fff" />
    <circle cx="83.5" cy="82.5" r="1.4" fill="#fff" />

    <!-- brow spots -->
    <circle cx="56" cy="74" r="2.4" fill="#a97c50" opacity="0.8" />
    <circle cx="84" cy="74" r="2.4" fill="#a97c50" opacity="0.8" />

    <!-- nose + mouth -->
    <path d="M65 96 h10 l-5 6 z" fill="#3d2b1f" />
    <path d="M70 102 v4 M70 106 q-4 4 -8 1 M70 106 q4 4 8 1" stroke="#3d2b1f" stroke-width="1.8" fill="none" stroke-linecap="round" />

    <!-- collar + tag -->
    <path d="M44 111 Q70 126 96 111 L94 118 Q70 131 46 118 Z" fill="var(--color-primary)" />
    <circle cx="70" cy="121" r="4.5" fill="var(--color-secondary)" />
    <circle cx="70" cy="121" r="1.6" fill="var(--color-secondary-content, #3a2a10)" opacity="0.55" />
  </svg>
</template>
