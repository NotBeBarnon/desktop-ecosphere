import assert from 'node:assert/strict';
import { lightingValue } from './lighting.js';
assert.equal(lightingValue(1),1);
assert.equal(lightingValue(.35),.35);
assert.equal(lightingValue(-1),.05);
assert.equal(lightingValue(2),1.5);
for(const invalid of [NaN,Infinity,-Infinity,null,undefined,'0.5']) assert.equal(lightingValue(invalid),1);
console.log('PASS lighting API bounds and invalid inputs');
