/**
 * Derives the console's palette from the single brand colour the admin sets.
 *
 * One value re-skins both clients (`AppConfiguration.PrimaryColor`), so the
 * shades around it are computed rather than configured — an admin choosing a
 * colour should not also have to choose a hover state and a tint.
 */
export function applyBrandColor(primary: string, document: Document): void {
  const hex = normalise(primary);
  const [r, g, b] = channels(hex);

  const style = document.documentElement.style;

  style.setProperty('--brand', hex);
  style.setProperty('--brand-rgb', `${r} ${g} ${b}`);
  style.setProperty('--brand-ink', shade(hex, -0.18));
  style.setProperty('--brand-hover', shade(hex, -0.08));
  style.setProperty('--brand-tint', `rgba(${r}, ${g}, ${b}, 0.1)`);
  style.setProperty('--brand-tint-strong', `rgba(${r}, ${g}, ${b}, 0.18)`);

  // Black or white on a filled button, by luminance rather than by taste: a
  // brand colour light enough to need dark text is a real possibility, and
  // white-on-yellow is unreadable.
  style.setProperty('--on-brand', luminance(r, g, b) > 0.6 ? '#1a1a1a' : '#ffffff');
}

function normalise(raw: string): string {
  let hex = (raw || '').trim().replace('#', '');

  if (hex.length === 3) {
    hex = hex
      .split('')
      .map((c) => c + c)
      .join('');
  }

  return /^[0-9a-fA-F]{6}$/.test(hex) ? `#${hex.toUpperCase()}` : '#2B6B4A';
}

function channels(hex: string): [number, number, number] {
  const value = parseInt(hex.slice(1), 16);
  return [(value >> 16) & 255, (value >> 8) & 255, value & 255];
}

/** Positive lightens towards white, negative darkens towards black. */
function shade(hex: string, amount: number): string {
  const [r, g, b] = channels(hex);
  const target = amount < 0 ? 0 : 255;
  const weight = Math.abs(amount);

  const mix = (channel: number) => Math.round(channel + (target - channel) * weight);

  return `#${[mix(r), mix(g), mix(b)]
    .map((c) => c.toString(16).padStart(2, '0'))
    .join('')}`;
}

/** Relative luminance, sRGB-weighted. */
function luminance(r: number, g: number, b: number): number {
  return (0.299 * r + 0.587 * g + 0.114 * b) / 255;
}
