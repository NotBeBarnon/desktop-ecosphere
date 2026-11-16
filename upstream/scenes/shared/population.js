export function populationName(value) {
  return value === 'sparse' || value === 'lively' ? value : 'standard';
}

export function customCount(value,min,max,fallback) {
  const number=typeof value==='string' && /^\d{1,2}$/.test(value)?Number(value):value;
  return Number.isInteger(number)?Math.max(min,Math.min(max,number)):fallback;
}
export function riverCount(value,custom) {
  return customCount(custom,1,48,{ sparse: 12, standard: 24, lively: 36 }[populationName(value)]);
}

export function reefCounts(value,{chromis,anthias}={}) {
  const name = populationName(value);
  return {
    clownfish: 3,
    chromis: customCount(chromis,2,18,name === 'sparse' ? 5 : name === 'lively' ? 14 : 9),
    anthias: customCount(anthias,2,14,name === 'sparse' ? 4 : name === 'lively' ? 11 : 7),
    shrimp: 2,
  };
}
