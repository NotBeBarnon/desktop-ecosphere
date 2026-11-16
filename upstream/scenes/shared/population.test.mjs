import assert from 'node:assert/strict';
import { register } from 'node:module';
import { populationName, riverCount, reefCounts } from './population.js';
register('../riverscape/tests/three-loader.mjs', import.meta.url);
const THREE = await import('three');
const { createFishSchool } = await import('../riverscape/src/fish.js');
const { ReefSimulation, FIXED_STEP } = await import('../reefscape/src/simulation.js');
for (const [profile, river, reef] of [['sparse',12,12],['standard',24,19],['lively',36,28]]) {
  const school=createFishSchool(new THREE.Scene(), { count:riverCount(profile) });
  assert.equal(school.fish.length,river);
  assert.equal(school.getTelemetry().count,river);
  for(let i=0;i<60*30;i++) school.update(FIXED_STEP,i*FIXED_STEP,null);
  assert.ok(school.fish.every(f=>[f.position.x,f.position.y,f.position.z].every(Number.isFinite)));
  school.dispose();
  const sim=new ReefSimulation(36719,{population:profile});
  assert.equal(sim.fish.length,reef);
  assert.equal(sim.shrimp.length,2);
  assert.equal(sim.fish.filter(f=>f.kind==='clown').length,3);
  assert.deepEqual(sim.diagnostics().population,reefCounts(profile));
  for(let i=0;i<60*30;i++) sim.step(FIXED_STEP);
  assert.equal(sim.diagnostics().finite,true);
  console.log(`PASS ${profile}: river ${river}, reef ${reef}, finite after 30s`);
}
for(const invalid of [undefined,null,'','unknown','999']) {
  assert.equal(populationName(invalid),'standard');
  assert.equal(riverCount(invalid),24);
  assert.deepEqual(reefCounts(invalid),reefCounts('standard'));
}
console.log('PASS invalid profiles fall back to standard');
