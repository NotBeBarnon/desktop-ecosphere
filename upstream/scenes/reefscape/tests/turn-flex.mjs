import assert from 'node:assert/strict';
import { register } from 'node:module';
register('../../riverscape/tests/three-loader.mjs', import.meta.url);
const { ReefSimulation,FIXED_STEP }=await import('../src/simulation.js');
const THREE=await import('three');
for(const kind of ['clown','chromis','anthias']) {
  const sim=new ReefSimulation(),fish=sim.fish.find(f=>f.kind===kind);
  fish.yaw=0;fish.turning=0;fish.bend=0;fish.speed=.35;fish.steer.set(0,0,0);
  let maximum=0;
  for(let i=0;i<45;i++) { sim.swim(fish,new THREE.Vector3(0,0,-.6),FIXED_STEP); maximum=Math.max(maximum,Math.abs(fish.bend)); assert.ok(Number.isFinite(fish.bend)&&Math.abs(fish.bend)<=.720001); }
  assert.ok(maximum>.25,`${kind} must flex visibly through a slow turn (${maximum})`);
  for(let i=0;i<300;i++) sim.swim(fish,new THREE.Vector3(),FIXED_STEP);
  assert.ok(Math.abs(fish.bend)<.01,`${kind} must relax after the turn`);
  console.log(`PASS ${kind}: bounded turn flex ${maximum.toFixed(3)}, relaxes to straight`);
}
