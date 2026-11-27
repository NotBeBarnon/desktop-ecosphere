import * as THREE from 'three';
import { GeometryBatch, groundHeight, randomGenerator, noise, vec } from './math.js';
import { blade, foliageMaterial, foliageDepth } from './foliage.js';

import { limnophila, hygrophila } from './stemplants.js';

export const aquascape = ['garden', 'valley'].includes(new URLSearchParams(location.search).get('layout'))
  ? new URLSearchParams(location.search).get('layout') : 'river';

// Photo-inspired planted terraces and asymmetric stone groups share the live water,
// foliage and fish pipeline. Their collision volumes are generated with the stones.
export async function createAquascape(scene, settings) {
  const rand = randomGenerator(aquascape === 'garden' ? 2711 : 2712);
  const between = (a,b) => a + (b-a)*rand();
  const valley = aquascape === 'valley';
  const obstacles = [], landmarks = [], thickets = [];
  const sand = new THREE.MeshStandardMaterial({color: valley ? 0xd6c2a2 : 0x655b45, roughness: .93});
  const ground = new THREE.PlaneGeometry(24,18,100,70); ground.rotateX(-Math.PI/2);
  const gp = ground.attributes.position;
  for(let i=0;i<gp.count;i++) gp.setY(i,groundHeight(gp.getX(i),gp.getZ(i)));
  ground.computeVertexNormals();
  const floor = new THREE.Mesh(ground,sand);floor.receiveShadow=true;scene.add(floor);
  const loader=new THREE.TextureLoader();
  const [map,normalMap,sandMap]=await Promise.all(['rock_boulder_dry_diff.jpg','rock_boulder_dry_nor_gl.jpg','sand_01_diff.jpg'].map(name=>loader.loadAsync('assets/'+name)));
  map.colorSpace=sandMap.colorSpace=THREE.SRGBColorSpace;
  for(const t of [map,normalMap,sandMap]) {t.wrapS=t.wrapT=THREE.RepeatWrapping;t.repeat.set(2,2);}
  sand.map=sandMap;sand.needsUpdate=true;sandMap.repeat.set(10,6);
  const rockMaterial = new THREE.MeshStandardMaterial({color:0xa3aaac,map,normalMap,normalScale:new THREE.Vector2(.75,.75),roughness:.83});
  rockMaterial.onBeforeCompile = shader => {
    shader.vertexShader = 'varying vec3 stonePoint;\n'+shader.vertexShader.replace('#include <begin_vertex>','#include <begin_vertex>\nstonePoint=position;');
    shader.fragmentShader = 'varying vec3 stonePoint;\n'+shader.fragmentShader.replace('#include <color_fragment>',`#include <color_fragment>
      float strata=stonePoint.y*9.0+stonePoint.x*3.0+sin(stonePoint.z*6.0+sin(stonePoint.y*4.0))*1.2;
      float fracture=1.0-smoothstep(.012,.045,abs(sin(strata)));
      diffuseColor.rgb *= .91+.09*sin(strata*.6);
      diffuseColor.rgb=mix(diffuseColor.rgb,vec3(.38,.40,.37),fracture*.32);
    `);
  };
  const rocks = valley ? [[-3.9,-.8,2.0,3.8,1.5],[-6,-.1,1.3,2.0,1.2],[-1.9,-1.1,1,1.8,1],[4.8,-1.5,1.5,2.6,1.2],[6.6,-.5,.9,1.3,.9]]
    : [[-4,-1.5,1.2,1.5,1],[-2.6,-1,.7,.8,.7],[4.5,-2,1,1.3,.8]];
  for(const [x,z,rx,ry,rz] of rocks) {
    const g=new THREE.SphereGeometry(1,100,72),p=g.attributes.position;
    for(let i=0;i<p.count;i++) {
      const a=p.getX(i),b=p.getY(i),c=p.getZ(i);
      // Continuous multiscale erosion: no unrelated displacement at triangle corners.
      const n=noise(a*2.8+17,b*2.8+9,c*2.8)*.22+noise(a*9,b*9,c*9)*.065+noise(a*26,b*26,c*26)*.018;
      const groove=Math.pow(Math.abs(Math.sin(b*12+a*4+noise(a*4,b*4,c*4)*2)),12)*.035;
      const f=.89+n-groove;
      const edge=1-.08*Math.abs(Math.sin(b*8+a*3));
      p.setXYZ(i,Math.sign(a)*Math.pow(Math.abs(a),.88)*f*(.92+.08*b),b*f,c*f*edge);
    }
    g.computeVertexNormals();
    const m=new THREE.Mesh(g,rockMaterial);m.scale.set(rx,ry,rz);
    const y=groundHeight(x,z)+ry*.62;m.position.set(x,y,z);m.rotation.z=-.17;m.castShadow=m.receiveShadow=true;scene.add(m);
    // Stack spheres up the rock, rather than one oversized sphere blocking the valley.
    for(let h=.3;h<ry*1.55;h+=.65) obstacles.push({center:vec(x,y-ry*.6+h,z),radius:Math.max(rx,rz)*Math.sqrt(Math.max(.15,1-Math.pow((h-ry*.6)/ry,2)))});
    landmarks.push({kind:'rock',point:vec(x,y+ry+.2,z),obstacle:obstacles.length-1});
  }
  const batch=new GeometryBatch();
  // Reuse the botanical growth builders: branching axes, irregular internodes,
  // shaded basal leaf loss and smaller apical leaves, all attached to the same strand.
  let specimen = 0;
  const plant=(x,z,height,red,carpet=false)=>{
    const root=vec(x,groundHeight(x,z),z);
    if (!carpet) {
      const grow = !red && specimen++ % 3 !== 0 ? limnophila : hygrophila;
      grow(batch, {root,height,red,natural:true,exposure:between(.9,1.2),canopy:height*1.05,
        compliance:between(.2,.36),branches:height>2?1:0,
        path:{leanAngle:between(0,Math.PI*2),lean:between(.025,.12),push:between(.025,.065),
          wander:between(.025,.10),waves:between(.6,1.5),phase:between(0,Math.PI*2),arch:0}});
      return;
    }
    // Low rosettes grow from one crown. Narrow arching blades replace the flat crosses.
    const count=Math.floor(between(4,8));
    const color=new THREE.Color('#45672b').offsetHSL(between(-.02,.02),0,between(-.02,.045));
    for(let k=0;k<count;k++) {
      const a=k*2.399+between(-.25,.25),dir=vec(Math.cos(a),0,Math.sin(a));
      const length=between(.2,.48),rise=between(.18,.38);
      blade(batch,[root,root.clone().addScaledVector(dir,length*.15).add(vec(0,rise*.75,0)),
        root.clone().addScaledVector(dir,length*.65).add(vec(0,rise,0)),
        root.clone().addScaledVector(dir,length).add(vec(0,rise*.4,0))],
        between(.014,.027),color,root,.28,{rows:8,cols:2,thin:.78,twist:a+Math.PI*.5,browning:k===0?.12:0});
    }
  };
  const density=settings.quality === "eco" ? .55 : 1;
  // Rounded stands with gaps and a sloping canopy, rather than straight colour bands.
  const beds = valley ? [[-7,-4,1.7,.9,.65,false],[6.8,-4.4,1.8,.8,.9,false]] : [
    [-8,-4.3,1.8,1.1,4.7,false],[-5,-4.4,1.3,1,5.1,true],[-1.6,-4.8,1.6,.8,3.9,false],
    [2,-4.4,1.5,1,4.6,true],[6,-4.5,2,1,4.3,false],[8.8,-3.8,.9,1,3.7,false],
    [-6,-2.1,1.3,.8,1.7,false],[4.1,-2,1.7,.7,1.8,true]];
  for(const [cx,cz,sx,sz,height,red] of beds) {
    const count=Math.round((valley?20:25)*density);
    thickets.push({minX:cx-sx,maxX:cx+sx,minZ:cz-sz,maxZ:cz+sz,maxY:Math.max(1.8,groundHeight(cx,cz)+height)});
    for(let i=0;i<count;i++) {
      const a=between(0,Math.PI*2),r=Math.sqrt(rand());
      const x=cx+Math.cos(a)*sx*r,z=cz+Math.sin(a)*sz*r;
      if(rocks.some(([a,b,rx,,rz])=>Math.hypot((x-a)/rx,(z-b)/rz)<1.03)) continue;
      plant(x,z,height*(1-.28*r*r)*between(.64,1.16),red);
    }
  }
  if (!valley) for (const cx of [-8.3,-2.5,2.7,7.5]) {
    for(let i=0;i<Math.round(14*density);i++) {
      const x=cx+between(-.9,.9),z=between(-2.6,-1.3);
      if(rocks.some(([a,b,rx,,rz])=>Math.hypot((x-a)/rx,(z-b)/rz)<1.12)) continue;
      plant(x,z,between(.6,1.45),cx===2.7);
    }
  }
  for(let i=0;i<Math.round((valley?150:500)*density);i++) {
    const x=between(-10,10),z=between(-1.5,3.6);
    // Keep a winding sand corridor clear in both compositions.
    if(Math.abs(x-Math.sin(z)*1.4)<(valley?4:1.5) || noise(x*1.5,0,z*1.5)<.38) continue;
    plant(x,z,between(.08,.25),false,true);
  }
  const mesh=new THREE.Mesh(batch.geometry(),foliageMaterial({natural:true}));mesh.castShadow=mesh.receiveShadow=true;
  mesh.customDepthMaterial=foliageDepth({animated:settings.animatedShadows});scene.add(mesh);
  return {obstacles,landmarks,plants:{mesh,thickets,stats:{vertices:batch.positions.length/3,triangles:batch.indices.length/3}}};
}
