import assert from 'node:assert/strict';
import { bankingAngle } from './motion.js';
assert.equal(bankingAngle(0,1,1),0);
assert.equal(bankingAngle(2,0,1),0);
assert.ok(bankingAngle(1,.6,1)<0);
assert.equal(bankingAngle(-1,.6,1),-bankingAngle(1,.6,1));
assert.ok(Math.abs(bankingAngle(1,.6,1))>Math.abs(bankingAngle(1,.1,1)));
assert.ok(Math.abs(bankingAngle(20,5,.2))<=.32);
console.log('PASS bank direction, symmetry, rest and bounded speed response');
