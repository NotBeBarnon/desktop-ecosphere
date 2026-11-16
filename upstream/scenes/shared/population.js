export function populationName(value) {
  return value === 'sparse' || value === 'lively' ? value : 'standard';
}

export function riverCount(value) {
  return { sparse: 12, standard: 24, lively: 36 }[populationName(value)];
}

export function reefCounts(value) {
  const name = populationName(value);
  return {
    clownfish: 3,
    chromis: name === 'sparse' ? 5 : name === 'lively' ? 14 : 9,
    anthias: name === 'sparse' ? 4 : name === 'lively' ? 11 : 7,
    shrimp: 2,
  };
}
