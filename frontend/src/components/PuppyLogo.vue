<template>
  <!-- Wizard puppy — hat & sparkles use theme colors so it matches every DaisyUI theme -->
  <svg viewBox="0 0 140 130" xmlns="http://www.w3.org/2000/svg" aria-label="PuppyFinder wizard puppy logo">
    <!-- sparkles -->
    <g fill="var(--color-accent)" opacity="0.9">
      <path d="M22 52l2.2 5.6 5.6 2.2-5.6 2.2-2.2 5.6-2.2-5.6-5.6-2.2 5.6-2.2z" />
      <path d="M116 44l1.8 4.5 4.5 1.8-4.5 1.8-1.8 4.5-1.8-4.5-4.5-1.8 4.5-1.8z" />
      <path d="M108 84l1.4 3.5 3.5 1.4-3.5 1.4-1.4 3.5-1.4-3.5-3.5-1.4 3.5-1.4z" />
    </g>

    <!-- ears -->
    <ellipse cx="38" cy="86" rx="12" ry="22" fill="#a97c50" transform="rotate(18 38 86)" />
    <ellipse cx="102" cy="86" rx="12" ry="22" fill="#a97c50" transform="rotate(-18 102 86)" />

    <!-- head -->
    <circle cx="70" cy="88" r="34" fill="#e8c99b" />

    <!-- muzzle -->
    <ellipse cx="70" cy="102" rx="16" ry="12" fill="#f8ecd7" />

    <!-- eyes -->
    <circle cx="58" cy="86" r="4.2" fill="#3d2b1f" />
    <circle cx="82" cy="86" r="4.2" fill="#3d2b1f" />
    <circle cx="59.5" cy="84.5" r="1.4" fill="#fff" />
    <circle cx="83.5" cy="84.5" r="1.4" fill="#fff" />

    <!-- nose + mouth -->
    <path d="M65 98 h10 l-5 6 z" fill="#3d2b1f" rx="2" />
    <path d="M70 104 v4 M70 108 q-4 4 -8 1 M70 108 q4 4 8 1" stroke="#3d2b1f" stroke-width="1.8" fill="none" stroke-linecap="round" />

    <!-- wizard hat -->
    <g transform="rotate(-8 70 48)">
      <path d="M70 6 L92 56 L48 56 Z" fill="var(--color-primary)" />
      <ellipse cx="70" cy="56" rx="30" ry="7" fill="var(--color-primary)" />
      <ellipse cx="70" cy="56" rx="30" ry="7" fill="black" opacity="0.15" />
      <!-- hat band -->
      <path d="M56 44 Q70 50 84 44 L86 50 Q70 56 54 50 Z" fill="var(--color-secondary)" />
      <!-- hat star -->
      <path d="M70 20l2.6 6.4 6.4 2.6-6.4 2.6-2.6 6.4-2.6-6.4-6.4-2.6 6.4-2.6z" fill="var(--color-accent)" />
      <!-- hat tip glow -->
      <circle cx="70" cy="7" r="4" fill="var(--color-accent)" />
    </g>
  </svg>
</template>
