// Visual pose only: does not steer the simulated velocity or position.
export function bankingAngle(turnRate,speed,length) {
  const angle=-turnRate*Math.max(0,speed)*.36/Math.max(.3,length+.2);
  return Number.isFinite(angle) && angle!==0 ? Math.max(-.32,Math.min(.32,angle)) : 0;
}
