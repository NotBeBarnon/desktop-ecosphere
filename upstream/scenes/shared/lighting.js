export function lightingValue(value) {
  return Number.isFinite(value) ? Math.max(0.05, Math.min(1.5, value)) : 1;
}
