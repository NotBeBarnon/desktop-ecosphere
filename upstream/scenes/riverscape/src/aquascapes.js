import * as THREE from 'three';
import { GeometryBatch, groundHeight, randomGenerator, noise, vec } from './math.js';
import { blade, stem, foliageMaterial, foliageDepth } from './foliage.js';

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
  // Opposite leaf pairs, progressively smaller crowns, varied heights and curved tips.
  const plant=(x,z,height,red,carpet=false)=>{
    const root=vec(x,groundHeight(x,z),z),lean=between(-.18,.18);
    const color=new THREE.Color(red?'#8c4550':carpet?'#638b39':'#75904b');
    color.offsetHSL(between(-.025,.025),between(-.06,.06),between(-.035,.035));
    if(!carpet) stem(batch,[root,root.clone().add(vec(lean,height*.5,0)),root.clone().add(vec(lean*.6,height,0))],.012,color,root,.22);
    const tiers=carpet?1:Math.max(3,Math.round(height*3.6));
    for(let j=0;j<tiers;j++) for(let k=0;k<(carpet?4:2);k++){
      const t=(j+.5)/tiers,a=j*2.399+k*Math.PI*(carpet?.5:1)+between(-.3,.3);
      const length=(carpet?.18:.46)*(1-.35*t)*between(.8,1.25);
      const base=root.clone().add(vec(lean*t,height*t,0));
      const dir=vec(Math.cos(a),0,Math.sin(a));
      blade(batch,[base,base.clone().addScaledVector(dir,length*.3).add(vec(0,length*.4,0)),base.clone().addScaledVector(dir,length*.85).add(vec(0,length*.45,0)),base.clone().addScaledVector(dir,length).add(vec(0,length*.22,0))],length*(carpet?.24:(red?.16:.22)),color,root,.3,{rows:10,cols:4,thin:.65,twist:a+Math.PI*.5+between(-.18,.18)});
    }
  };
  const density=settings.quality === "eco" ? .55 : 1;
  // Rounded stands with gaps and a sloping canopy, rather than straight colour bands.
  const beds = valley ? [[-7,-4,1.7,.9,.65,false],[6.8,-4.4,1.8,.8,.9,false]] : [
    [-8,-4.3,1.8,1.1,4.7,false],[-5,-4.4,1.3,1,5.1,true],[-1.6,-4.8,1.6,.8,3.9,false],
    [2,-4.4,1.5,1,4.6,true],[6,-4.5,2,1,4.3,false],[8.8,-3.8,.9,1,3.7,false],
    [-6,-2.1,1.3,.8,1.7,false],[4.1,-2,1.7,.7,1.8,true]];
  for(const [cx,cz,sx,sz,height,red] of beds) {
    const count=Math.round((valley?30:55)*density);
    thickets.push({minX:cx-sx,maxX:cx+sx,minZ:cz-sz,maxZ:cz+sz,maxY:Math.max(1.8,groundHeight(cx,cz)+height)});
    for(let i=0;i<count;i++) {
      const a=between(0,Math.PI*2),r=Math.sqrt(rand());
      const x=cx+Math.cos(a)*sx*r,z=cz+Math.sin(a)*sz*r;
      if(rocks.some(([a,b,rx,,rz])=>Math.hypot((x-a)/rx,(z-b)/rz)<1.03)) continue;
      plant(x,z,height*(1-.38*r*r)*between(.8,1.12),red);
    }
  }
  for(let i=0;i<Math.round((valley?150:500)*density);i++) {
    const x=between(-10,10),z=between(-1.5,3.6);
    // Keep a winding sand corridor clear in both compositions.
    if(Math.abs(x-Math.sin(z)*1.4)<(valley?4:1.5) || noise(x*1.5,0,z*1.5)<.38) continue;
    plant(x,z,between(.08,.25),false,true);
  }
  const mesh=new THREE.Mesh(batch.geometry(),foliageMaterial());mesh.castShadow=mesh.receiveShadow=true;
  mesh.customDepthMaterial=foliageDepth({animated:settings.animatedShadows});scene.add(mesh);
  return {obstacles,landmarks,plants:{mesh,thickets,stats:{vertices:batch.positions.length/3,triangles:batch.indices.length/3}}};
}
