import React, { useEffect, useRef } from 'react';
import * as THREE from 'three';

export type FurColor = 'grey' | 'amber' | 'black';

export type ShirtStyle = 'hawaiian' | 'plain' | 'tuxedo';
const SHIRT_INDEX: Record<ShirtStyle, number> = { hawaiian: 0, plain: 1, tuxedo: 2 };

interface KoalaCharacterProps {
  state: 'idle' | 'talking' | 'listening' | 'thinking' | 'excited' | 'singing';
  width: number;
  height: number;
  furColor?: FurColor;
  showShirt?: boolean;
  shirtStyle?: ShirtStyle;
  showSunglasses?: boolean;
}

const FUR_PALETTES: Record<FurColor, { fur: number; lightFur: number; darkFur: number }> = {
  grey:  { fur: 0x8a8a9a, lightFur: 0xd0d0e0, darkFur: 0x555566 },
  amber: { fur: 0x9a6a2a, lightFur: 0xd4a96a, darkFur: 0x6b4010 },
  black: { fur: 0x1a1a1a, lightFur: 0x4a4a4a, darkFur: 0x0a0a0a },
};

const KoalaCharacter: React.FC<KoalaCharacterProps> = ({
  state, width, height, furColor = 'grey',
  showShirt = false, shirtStyle = 'hawaiian', showSunglasses = false,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const koalaRef = useRef<THREE.Group | null>(null);
  const mouthRef = useRef<THREE.Mesh | null>(null);
  const eyeRefs = useRef<{ left: THREE.Mesh | null; right: THREE.Mesh | null }>({ left: null, right: null });
  const armRefs = useRef<{ left: THREE.Mesh | null; right: THREE.Mesh | null }>({ left: null, right: null });
  const furMatsRef = useRef<{ fur: THREE.MeshStandardMaterial; lightFur: THREE.MeshStandardMaterial } | null>(null);
  const shirtGroupRef = useRef<THREE.Group | null>(null);
  const sunglassGroupRef = useRef<THREE.Group | null>(null);
  const animationFrameRef = useRef<number>(0);
  const blinkIntervalRef = useRef<NodeJS.Timeout | null>(null);
  const stateRef = useRef(state);
  stateRef.current = state;

  // Update fur colors when furColor prop changes without reinitializing Three.js
  useEffect(() => {
    if (!furMatsRef.current) return;
    const p = FUR_PALETTES[furColor];
    furMatsRef.current.fur.color.setHex(p.fur);
    furMatsRef.current.lightFur.color.setHex(p.lightFur);
  }, [furColor]);

  useEffect(() => {
    if (!shirtGroupRef.current) return;
    shirtGroupRef.current.visible = showShirt;
  }, [showShirt]);

  useEffect(() => {
    if (!shirtGroupRef.current) return;
    shirtGroupRef.current.children.forEach((c, i) => { c.visible = i === SHIRT_INDEX[shirtStyle]; });
  }, [shirtStyle]);

  useEffect(() => {
    if (!sunglassGroupRef.current) return;
    sunglassGroupRef.current.visible = showSunglasses;
  }, [showSunglasses]);

  useEffect(() => {
    if (!containerRef.current) return;
    const container = containerRef.current;

    const scene = new THREE.Scene();
    scene.background = null;

    const camera = new THREE.PerspectiveCamera(50, width / height, 0.1, 1000);
    camera.position.set(0, -0.3, 7.5);
    camera.lookAt(0, -0.3, 0);

    const renderer = new THREE.WebGLRenderer({ alpha: true, antialias: true });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.setSize(width, height, false);
    renderer.domElement.style.position = 'absolute';
    renderer.domElement.style.top = '0';
    renderer.domElement.style.left = '0';
    renderer.domElement.style.width = width + 'px';
    renderer.domElement.style.height = height + 'px';
    renderer.domElement.style.pointerEvents = 'none';
    container.appendChild(renderer.domElement);

    scene.add(new THREE.AmbientLight(0xffffff, 0.6));
    const dirLight = new THREE.DirectionalLight(0xffffff, 0.8);
    dirLight.position.set(1, 2, 2);
    scene.add(dirLight);

    const { koala, furMat, lightFurMat, leftArm, rightArm, shirtGroup, sunglassGroup } = createKoala(FUR_PALETTES[furColor]);
    scene.add(koala);
    koalaRef.current = koala;
    furMatsRef.current = { fur: furMat, lightFur: lightFurMat };
    armRefs.current = { left: leftArm, right: rightArm };
    shirtGroupRef.current = shirtGroup;
    sunglassGroupRef.current = sunglassGroup;
    shirtGroup.visible = showShirt;
    shirtGroup.children.forEach((c, i) => { c.visible = i === SHIRT_INDEX[shirtStyle]; });
    sunglassGroup.visible = showSunglasses;

    koala.traverse((child) => {
      if (child instanceof THREE.Mesh) {
        if (child.name === 'mouth') mouthRef.current = child;
        if (child.name === 'eye-left') eyeRefs.current.left = child;
        if (child.name === 'eye-right') eyeRefs.current.right = child;
      }
    });

    blinkIntervalRef.current = setInterval(() => {
      const l = eyeRefs.current.left, r = eyeRefs.current.right;
      if (!l || !r) return;
      l.scale.y = r.scale.y = 0.1;
      setTimeout(() => { l.scale.y = r.scale.y = 1; }, 150);
    }, 4000 + Math.random() * 2000);

    let time = 0;
    const animate = () => {
      animationFrameRef.current = requestAnimationFrame(animate);
      time += 0.02;
      const k = koalaRef.current;
      if (!k) return;

      const s = stateRef.current;

      if (s === 'talking') {
        k.position.y = Math.sin(time * 6) * 0.12;
        k.rotation.y = Math.sin(time * 3) * 0.12;
        k.rotation.z = Math.sin(time * 4) * 0.04;
        if (armRefs.current.left)  armRefs.current.left.rotation.z  =  Math.sin(time * 8) * 0.3;
        if (armRefs.current.right) armRefs.current.right.rotation.z = -Math.sin(time * 8 + 1) * 0.3;
        if (mouthRef.current) mouthRef.current.scale.y = 1 + Math.abs(Math.sin(time * 18)) * 0.5;
      } else if (s === 'excited') {
        k.position.y = Math.abs(Math.sin(time * 8)) * 0.28;
        k.rotation.y = Math.sin(time * 5) * 0.22;
        k.rotation.z = Math.sin(time * 7) * 0.1;
        k.scale.set(1 + Math.abs(Math.sin(time * 8)) * 0.08, 1 + Math.abs(Math.sin(time * 8 + Math.PI)) * 0.08, 1);
        if (armRefs.current.left)  armRefs.current.left.rotation.z  =  Math.sin(time * 12) * 0.6;
        if (armRefs.current.right) armRefs.current.right.rotation.z = -Math.sin(time * 12 + 0.5) * 0.6;
        if (mouthRef.current) mouthRef.current.scale.y = 1 + Math.abs(Math.sin(time * 22)) * 0.7;
      } else if (s === 'singing') {
        k.position.y = Math.sin(time * 3) * 0.1;
        k.rotation.z = Math.sin(time * 2) * 0.12;
        k.rotation.y = Math.sin(time * 1.5) * 0.08;
        k.scale.set(1, 1, 1);
        if (armRefs.current.left)  armRefs.current.left.rotation.z  =  Math.sin(time * 4) * 0.5;
        if (armRefs.current.right) armRefs.current.right.rotation.z =  Math.sin(time * 4 + Math.PI) * 0.5;
        if (armRefs.current.left)  armRefs.current.left.rotation.x  =  Math.PI + Math.sin(time * 3) * 0.3;
        if (armRefs.current.right) armRefs.current.right.rotation.x =  Math.PI + Math.sin(time * 3 + 1) * 0.3;
        if (mouthRef.current) mouthRef.current.scale.y = 1 + Math.abs(Math.sin(time * 4)) * 0.8;
      } else {
        k.scale.set(1, 1, 1);
        if (armRefs.current.left)  { armRefs.current.left.rotation.z  = 0; armRefs.current.left.rotation.x  = Math.PI; }
        if (armRefs.current.right) { armRefs.current.right.rotation.z = 0; armRefs.current.right.rotation.x = Math.PI; }
        if (mouthRef.current) mouthRef.current.scale.y = 1;

        if (s === 'listening') {
          k.position.y = Math.sin(time) * 0.05;
          k.rotation.y = Math.sin(time * 2) * 0.1;
          k.rotation.z = 0;
        } else if (s === 'thinking') {
          k.position.y = Math.sin(time) * 0.03;
          k.rotation.y = Math.sin(time * 0.5) * 0.05;
          k.rotation.x = -0.22 + Math.sin(time) * 0.05;
          k.rotation.z = 0;
        } else {
          k.position.y = Math.sin(time) * 0.05;
          k.rotation.y = Math.sin(time * 0.5) * 0.05;
          k.rotation.x = 0;
          k.rotation.z = 0;
        }
      }

      renderer.render(scene, camera);
    };
    animate();

    return () => {
      if (blinkIntervalRef.current) clearInterval(blinkIntervalRef.current);
      cancelAnimationFrame(animationFrameRef.current);
      if (container.contains(renderer.domElement)) container.removeChild(renderer.domElement);
      renderer.dispose();
    };
  }, []);

  return (
    <div
      ref={containerRef}
      style={{ width: width + 'px', height: height + 'px', background: 'transparent', overflow: 'hidden', position: 'relative', pointerEvents: 'none' }}
    />
  );
};

function createKoala(palette: { fur: number; lightFur: number; darkFur: number }): {
  koala: THREE.Group;
  furMat: THREE.MeshStandardMaterial;
  lightFurMat: THREE.MeshStandardMaterial;
  leftArm: THREE.Mesh;
  rightArm: THREE.Mesh;
  shirtGroup: THREE.Group;
  sunglassGroup: THREE.Group;
} {
  const koala = new THREE.Group();

  const fur        = new THREE.MeshStandardMaterial({ color: palette.fur, roughness: 0.95 });
  const lightFur   = new THREE.MeshStandardMaterial({ color: palette.lightFur, roughness: 0.9 });
  const darkFur    = new THREE.MeshStandardMaterial({ color: palette.darkFur, roughness: 0.95 });
  const noseMat    = new THREE.MeshStandardMaterial({ color: 0x1a1a2a, roughness: 0.15, metalness: 0.2 });
  const black      = new THREE.MeshStandardMaterial({ color: 0x0a0a0a, roughness: 0.3 });
  const white      = new THREE.MeshStandardMaterial({ color: 0xf5f5f5, roughness: 0.5 });
  const pink       = new THREE.MeshStandardMaterial({ color: 0xe8a0a0, roughness: 0.7 });

  // ── HEAD ──────────────────────────────────────────────────────────────────
  // Koala heads are very wide, slightly flat front-to-back, with a rounded top
  const head = new THREE.Mesh(new THREE.SphereGeometry(1, 32, 32), fur);
  head.scale.set(1.4, 1.1, 1.0);
  koala.add(head);

  // ── EARS ──────────────────────────────────────────────────────────────────
  // Koala ears are large, round, and very fluffy — set wide on the sides
  for (const side of [-1, 1]) {
    // Outer ear — large fluffy sphere
    const ear = new THREE.Mesh(new THREE.SphereGeometry(0.52, 32, 32), fur);
    ear.position.set(side * 1.3, 0.72, 0.0);
    ear.scale.set(1.0, 1.0, 0.75);
    koala.add(ear);

    // Ear tuft — slightly lighter, gives fluffy texture
    const tuft = new THREE.Mesh(new THREE.SphereGeometry(0.38, 32, 32), lightFur);
    tuft.position.set(side * 1.38, 0.78, 0.18);
    tuft.scale.set(1.0, 0.95, 0.45);
    koala.add(tuft);

    // Inner ear — pink
    const inner = new THREE.Mesh(new THREE.SphereGeometry(0.22, 32, 32), pink);
    inner.position.set(side * 1.42, 0.72, 0.28);
    inner.scale.set(0.9, 0.85, 0.3);
    koala.add(inner);
  }

  // ── MUZZLE ────────────────────────────────────────────────────────────────
  // Koalas have a distinct protruding muzzle area — lighter fur
  const muzzle = new THREE.Mesh(new THREE.SphereGeometry(0.52, 32, 32), lightFur);
  muzzle.position.set(0, -0.22, 0.82);
  muzzle.scale.set(1.1, 0.85, 0.55);
  koala.add(muzzle);

  // ── NOSE ──────────────────────────────────────────────────────────────────
  // Large, wide, leathery — the most distinctive koala feature
  const nose = new THREE.Mesh(new THREE.SphereGeometry(0.3, 32, 32), noseMat);
  nose.position.set(0, -0.08, 1.02);
  nose.scale.set(1.55, 1.05, 0.65);
  koala.add(nose);

  // Nose highlight
  const noseHL = new THREE.Mesh(new THREE.SphereGeometry(0.08, 16, 16), new THREE.MeshStandardMaterial({ color: 0x444455, roughness: 0.1, metalness: 0.4 }));
  noseHL.position.set(-0.1, -0.04, 1.12);
  koala.add(noseHL);

  // ── EYES ──────────────────────────────────────────────────────────────────
  // Small, round, set wide apart and slightly above the muzzle
  for (const [side, name] of [[-1, 'eye-left'], [1, 'eye-right']] as [number, string][]) {
    const eye = new THREE.Mesh(new THREE.SphereGeometry(0.14, 32, 32), black);
    eye.name = name;
    eye.position.set(side * 0.52, 0.18, 0.9);
    koala.add(eye);
    // Iris
    const iris = new THREE.Mesh(new THREE.SphereGeometry(0.09, 16, 16), new THREE.MeshStandardMaterial({ color: 0x3a2a10, roughness: 0.3 }));
    iris.position.set(side * 0.52, 0.18, 0.96);
    koala.add(iris);
    // Highlight
    const hl = new THREE.Mesh(new THREE.SphereGeometry(0.04, 16, 16), white);
    hl.position.set(side * 0.50, 0.22, 1.02);
    koala.add(hl);
  }

  // ── MOUTH ─────────────────────────────────────────────────────────────────
  // Real curved mouth — two line segments forming a W/smile shape
  const mouthMat = new THREE.MeshStandardMaterial({ color: 0x2a1a1a, roughness: 0.8 });

  // Centre dip
  const mouthCentre = new THREE.Mesh(new THREE.SphereGeometry(0.04, 8, 8), mouthMat);
  mouthCentre.position.set(0, -0.36, 1.0);
  koala.add(mouthCentre);

  // Left curve
  const mouthL = new THREE.Mesh(new THREE.TorusGeometry(0.1, 0.025, 8, 16, Math.PI * 0.55), mouthMat);
  mouthL.name = 'mouth';
  mouthL.position.set(-0.08, -0.32, 0.98);
  mouthL.rotation.set(Math.PI * 0.85, 0.3, Math.PI * 0.1);
  koala.add(mouthL);

  // Right curve
  const mouthR = new THREE.Mesh(new THREE.TorusGeometry(0.1, 0.025, 8, 16, Math.PI * 0.55), mouthMat);
  mouthR.position.set(0.08, -0.32, 0.98);
  mouthR.rotation.set(Math.PI * 0.85, -0.3, -Math.PI * 0.1);
  koala.add(mouthR);

  // ── CHIN / THROAT ─────────────────────────────────────────────────────────
  const chin = new THREE.Mesh(new THREE.SphereGeometry(0.4, 32, 32), lightFur);
  chin.position.set(0, -0.55, 0.65);
  chin.scale.set(1.0, 0.65, 0.5);
  koala.add(chin);

  // ── BODY ──────────────────────────────────────────────────────────────────
  const body = new THREE.Mesh(new THREE.SphereGeometry(0.85, 32, 32), fur);
  body.position.set(0, -1.45, -0.05);
  body.scale.set(1.1, 1.15, 0.88);
  koala.add(body);

  const belly = new THREE.Mesh(new THREE.SphereGeometry(0.52, 32, 32), lightFur);
  belly.position.set(0, -1.35, 0.52);
  belly.scale.set(0.88, 1.1, 0.42);
  koala.add(belly);

  // ── ARMS ──────────────────────────────────────────────────────────────────
  let leftArm!: THREE.Mesh, rightArm!: THREE.Mesh;
  for (const side of [-1, 1]) {
    const arm = new THREE.Mesh(new THREE.CapsuleGeometry(0.17, 0.42, 16, 32), fur);
    arm.position.set(side * 0.9, -1.15, 0.28);
    arm.rotation.z = 0;
    arm.rotation.x = Math.PI;
    koala.add(arm);
    if (side === -1) leftArm = arm; else rightArm = arm;
    const paw = new THREE.Mesh(new THREE.SphereGeometry(0.2, 32, 32), lightFur);
    paw.position.set(0, -0.38, 0);
    arm.add(paw);
  }

  // ── SURFBOARD ─────────────────────────────────────────────────────────────
  const boardMat  = new THREE.MeshStandardMaterial({ color: 0xf5c800, roughness: 0.35, side: THREE.DoubleSide });
  const stripeMat = new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.4, side: THREE.DoubleSide });
  const finMat    = new THREE.MeshStandardMaterial({ color: 0xe0b000, roughness: 0.4 });

  // Surfboard outline: pointed nose (right), rounded tail (left), narrow waist
  const boardShape = new THREE.Shape();
  boardShape.moveTo(-1.1, 0);           // tail centre
  boardShape.bezierCurveTo(-1.1, 0.22, -0.7, 0.32, 0.0, 0.32);   // tail-left to waist
  boardShape.bezierCurveTo(0.6, 0.32, 1.0, 0.18, 1.15, 0);        // waist to nose
  boardShape.bezierCurveTo(1.0, -0.18, 0.6, -0.32, 0.0, -0.32);   // nose to waist
  boardShape.bezierCurveTo(-0.7, -0.32, -1.1, -0.22, -1.1, 0);    // waist to tail

  const boardGeo = new THREE.ExtrudeGeometry(boardShape, { depth: 0.07, bevelEnabled: true, bevelThickness: 0.025, bevelSize: 0.025, bevelSegments: 4 });
  const board = new THREE.Mesh(boardGeo, boardMat);
  board.rotation.x = -Math.PI / 2;
  board.rotation.z = 0.28;
  board.position.set(-0.05, -2.55, 0.42);
  koala.add(board);

  // Centre stripe (slightly raised)
  const stripeShape = new THREE.Shape();
  stripeShape.moveTo(-1.05, 0);
  stripeShape.bezierCurveTo(-1.05, 0.07, -0.5, 0.09, 0.0, 0.09);
  stripeShape.bezierCurveTo(0.55, 0.09, 1.0, 0.05, 1.1, 0);
  stripeShape.bezierCurveTo(1.0, -0.05, 0.55, -0.09, 0.0, -0.09);
  stripeShape.bezierCurveTo(-0.5, -0.09, -1.05, -0.07, -1.05, 0);
  const stripeGeo = new THREE.ExtrudeGeometry(stripeShape, { depth: 0.075, bevelEnabled: false });
  const stripe = new THREE.Mesh(stripeGeo, stripeMat);
  stripe.rotation.x = -Math.PI / 2;
  stripe.rotation.z = 0.28;
  stripe.position.set(-0.05, -2.545, 0.42);
  koala.add(stripe);

  // Fin — swept back, under the tail
  const finShape = new THREE.Shape();
  finShape.moveTo(0, 0);
  finShape.bezierCurveTo(0.05, 0.12, 0.18, 0.22, 0.22, 0.28);
  finShape.bezierCurveTo(0.15, 0.28, 0.05, 0.15, -0.02, 0);
  finShape.closePath();
  const fin = new THREE.Mesh(new THREE.ExtrudeGeometry(finShape, { depth: 0.04, bevelEnabled: false }), finMat);
  fin.position.set(0.95, -2.63, 0.18);
  fin.rotation.y = Math.PI * 0.6;
  koala.add(fin);

  // ── SHIRTS ────────────────────────────────────────────────────────────────
  const shirtGroup = new THREE.Group();
  shirtGroup.visible = false;
  koala.add(shirtGroup);

  function makeCanvasTex(draw: (ctx: CanvasRenderingContext2D, W: number) => void, size = 512): THREE.CanvasTexture {
    const c = document.createElement('canvas');
    c.width = c.height = size;
    draw(c.getContext('2d')!, size);
    const t = new THREE.CanvasTexture(c);
    t.needsUpdate = true;
    return t;
  }

  // ── Hawaiian: royal blue + detailed white hibiscus flowers ──
  const hawaiianTex = makeCanvasTex((ctx, W) => {
    // Royal blue base with subtle stripe texture
    ctx.fillStyle = '#1a3a8f';
    ctx.fillRect(0, 0, W, W);
    // Subtle vertical stripe shadow
    for (let x = 0; x < W; x += 32) {
      ctx.fillStyle = 'rgba(0,0,80,0.15)';
      ctx.fillRect(x, 0, 16, W);
    }

    function drawHibiscus(cx: number, cy: number, r: number, petalColor: string, centerColor: string) {
      // 5 petals
      for (let i = 0; i < 5; i++) {
        const a = (i / 5) * Math.PI * 2 - Math.PI / 2;
        const px = cx + Math.cos(a) * r * 0.7;
        const py = cy + Math.sin(a) * r * 0.7;
        ctx.beginPath();
        ctx.ellipse(px, py, r * 0.55, r * 0.35, a, 0, Math.PI * 2);
        ctx.fillStyle = petalColor;
        ctx.fill();
        // Petal vein
        ctx.beginPath();
        ctx.moveTo(cx, cy);
        ctx.lineTo(px + Math.cos(a) * r * 0.3, py + Math.sin(a) * r * 0.3);
        ctx.strokeStyle = 'rgba(255,255,255,0.3)';
        ctx.lineWidth = 1.5;
        ctx.stroke();
      }
      // Center stamen
      ctx.beginPath();
      ctx.arc(cx, cy, r * 0.18, 0, Math.PI * 2);
      ctx.fillStyle = centerColor;
      ctx.fill();
      // Stamen dots
      for (let i = 0; i < 6; i++) {
        const a = (i / 6) * Math.PI * 2;
        ctx.beginPath();
        ctx.arc(cx + Math.cos(a) * r * 0.12, cy + Math.sin(a) * r * 0.12, r * 0.04, 0, Math.PI * 2);
        ctx.fillStyle = '#ffe066';
        ctx.fill();
      }
    }

    function drawLeaf(x1: number, y1: number, x2: number, y2: number) {
      ctx.beginPath();
      const mx = (x1 + x2) / 2 + (y2 - y1) * 0.4;
      const my = (y1 + y2) / 2 - (x2 - x1) * 0.4;
      ctx.moveTo(x1, y1);
      ctx.quadraticCurveTo(mx, my, x2, y2);
      ctx.quadraticCurveTo(mx - (y2 - y1) * 0.8, my + (x2 - x1) * 0.8, x1, y1);
      ctx.fillStyle = '#1a7a3a';
      ctx.fill();
      ctx.beginPath();
      ctx.moveTo(x1, y1);
      ctx.lineTo(x2, y2);
      ctx.strokeStyle = '#0d5a28';
      ctx.lineWidth = 1.5;
      ctx.stroke();
    }

    // Leaves first (behind flowers)
    drawLeaf(60, 80, 110, 130); drawLeaf(300, 50, 360, 110);
    drawLeaf(150, 280, 200, 340); drawLeaf(380, 300, 440, 360);
    drawLeaf(30, 380, 80, 440); drawLeaf(240, 160, 290, 210);

    // Flowers — white with yellow center, scattered
    const flowers: [number, number, number][] = [
      [80, 60, 38], [320, 80, 42], [180, 200, 35], [420, 200, 40],
      [60, 340, 36], [280, 340, 44], [150, 460, 38], [440, 420, 36],
      [200, 80, 30], [380, 460, 32],
    ];
    for (const [x, y, r] of flowers) {
      drawHibiscus(x, y, r, 'rgba(255,255,255,0.92)', '#ffe066');
    }
    // Smaller accent flowers in pink
    const pinkFlowers: [number, number, number][] = [
      [480, 120, 24], [100, 200, 22], [350, 250, 26], [480, 380, 22],
    ];
    for (const [x, y, r] of pinkFlowers) {
      drawHibiscus(x, y, r, 'rgba(255,180,200,0.85)', '#ff6688');
    }
  });

  // ── Plain: clean navy polo with collar detail ──
  const plainTex = makeCanvasTex((ctx, W) => {
    // Gradient navy
    const grad = ctx.createLinearGradient(0, 0, W, W);
    grad.addColorStop(0, '#1a4a7a');
    grad.addColorStop(1, '#0d2d4a');
    ctx.fillStyle = grad;
    ctx.fillRect(0, 0, W, W);
    // Subtle fabric weave
    for (let y = 0; y < W; y += 4) {
      ctx.fillStyle = y % 8 === 0 ? 'rgba(255,255,255,0.03)' : 'rgba(0,0,0,0.03)';
      ctx.fillRect(0, y, W, 2);
    }
    // Collar V-neck
    ctx.beginPath();
    ctx.moveTo(W * 0.35, 0);
    ctx.lineTo(W * 0.5, W * 0.18);
    ctx.lineTo(W * 0.65, 0);
    ctx.strokeStyle = 'rgba(255,255,255,0.5)';
    ctx.lineWidth = 6;
    ctx.stroke();
    // Collar fill
    ctx.beginPath();
    ctx.moveTo(W * 0.35, 0); ctx.lineTo(W * 0.5, W * 0.18); ctx.lineTo(W * 0.65, 0);
    ctx.lineTo(W * 0.7, 0); ctx.lineTo(W * 0.5, W * 0.22); ctx.lineTo(W * 0.3, 0);
    ctx.fillStyle = 'rgba(255,255,255,0.12)';
    ctx.fill();
    // Placket line
    ctx.beginPath();
    ctx.moveTo(W * 0.5, W * 0.18);
    ctx.lineTo(W * 0.5, W * 0.6);
    ctx.strokeStyle = 'rgba(255,255,255,0.2)';
    ctx.lineWidth = 3;
    ctx.stroke();
    // 3 buttons
    for (let i = 0; i < 3; i++) {
      ctx.beginPath();
      ctx.arc(W * 0.5, W * (0.25 + i * 0.1), 5, 0, Math.PI * 2);
      ctx.fillStyle = 'rgba(255,255,255,0.6)';
      ctx.fill();
    }
  });

  // ── Tuxedo: proper black jacket with white dress shirt, lapels, bow tie ──
  const tuxTex = makeCanvasTex((ctx, W) => {
    // Black jacket
    ctx.fillStyle = '#0a0a0a';
    ctx.fillRect(0, 0, W, W);
    // Subtle satin sheen on jacket
    const sheen = ctx.createLinearGradient(0, 0, W, 0);
    sheen.addColorStop(0, 'rgba(255,255,255,0.0)');
    sheen.addColorStop(0.3, 'rgba(255,255,255,0.06)');
    sheen.addColorStop(0.5, 'rgba(255,255,255,0.0)');
    sheen.addColorStop(0.7, 'rgba(255,255,255,0.06)');
    sheen.addColorStop(1, 'rgba(255,255,255,0.0)');
    ctx.fillStyle = sheen;
    ctx.fillRect(0, 0, W, W);

    // White dress shirt front (center panel)
    ctx.beginPath();
    ctx.moveTo(W * 0.38, 0);
    ctx.lineTo(W * 0.38, W * 0.85);
    ctx.lineTo(W * 0.62, W * 0.85);
    ctx.lineTo(W * 0.62, 0);
    ctx.fillStyle = '#f0f0f0';
    ctx.fill();
    // Shirt pleats
    for (let i = 0; i < 4; i++) {
      ctx.beginPath();
      ctx.moveTo(W * 0.44 + i * W * 0.04, W * 0.15);
      ctx.lineTo(W * 0.44 + i * W * 0.04, W * 0.75);
      ctx.strokeStyle = 'rgba(180,180,180,0.5)';
      ctx.lineWidth = 1;
      ctx.stroke();
    }

    // Left lapel
    ctx.beginPath();
    ctx.moveTo(W * 0.38, 0);
    ctx.lineTo(W * 0.5, W * 0.22);
    ctx.lineTo(W * 0.38, W * 0.35);
    ctx.lineTo(W * 0.1, W * 0.5);
    ctx.lineTo(0, W * 0.4);
    ctx.lineTo(0, 0);
    ctx.fillStyle = '#111';
    ctx.fill();
    // Left lapel satin edge
    ctx.beginPath();
    ctx.moveTo(W * 0.38, 0);
    ctx.lineTo(W * 0.5, W * 0.22);
    ctx.lineTo(W * 0.38, W * 0.35);
    ctx.strokeStyle = 'rgba(255,255,255,0.15)';
    ctx.lineWidth = 2;
    ctx.stroke();

    // Right lapel
    ctx.beginPath();
    ctx.moveTo(W * 0.62, 0);
    ctx.lineTo(W * 0.5, W * 0.22);
    ctx.lineTo(W * 0.62, W * 0.35);
    ctx.lineTo(W * 0.9, W * 0.5);
    ctx.lineTo(W, W * 0.4);
    ctx.lineTo(W, 0);
    ctx.fillStyle = '#111';
    ctx.fill();
    ctx.beginPath();
    ctx.moveTo(W * 0.62, 0);
    ctx.lineTo(W * 0.5, W * 0.22);
    ctx.lineTo(W * 0.62, W * 0.35);
    ctx.strokeStyle = 'rgba(255,255,255,0.15)';
    ctx.lineWidth = 2;
    ctx.stroke();

    // Bow tie — white satin
    const bx = W * 0.5, by = W * 0.12;
    ctx.beginPath();
    ctx.moveTo(bx, by);
    ctx.bezierCurveTo(bx - W * 0.12, by - W * 0.06, bx - W * 0.14, by + W * 0.06, bx, by);
    ctx.bezierCurveTo(bx + W * 0.14, by - W * 0.06, bx + W * 0.12, by + W * 0.06, bx, by);
    ctx.fillStyle = '#f0f0f0';
    ctx.fill();
    ctx.strokeStyle = 'rgba(180,180,180,0.6)';
    ctx.lineWidth = 1;
    ctx.stroke();
    // Bow tie knot
    ctx.beginPath();
    ctx.ellipse(bx, by, W * 0.025, W * 0.03, 0, 0, Math.PI * 2);
    ctx.fillStyle = '#ddd';
    ctx.fill();

    // Shirt buttons
    for (let i = 0; i < 4; i++) {
      ctx.beginPath();
      ctx.arc(W * 0.5, W * (0.28 + i * 0.13), 4, 0, Math.PI * 2);
      ctx.fillStyle = '#ccc';
      ctx.fill();
      ctx.beginPath();
      ctx.arc(W * 0.5, W * (0.28 + i * 0.13), 4, 0, Math.PI * 2);
      ctx.strokeStyle = '#aaa';
      ctx.lineWidth = 1;
      ctx.stroke();
    }

    // Pocket square (left breast)
    ctx.beginPath();
    ctx.moveTo(W * 0.18, W * 0.38);
    ctx.lineTo(W * 0.28, W * 0.38);
    ctx.lineTo(W * 0.28, W * 0.46);
    ctx.lineTo(W * 0.18, W * 0.46);
    ctx.fillStyle = '#111';
    ctx.fill();
    ctx.beginPath();
    ctx.moveTo(W * 0.19, W * 0.38);
    ctx.lineTo(W * 0.23, W * 0.34);
    ctx.lineTo(W * 0.27, W * 0.38);
    ctx.fillStyle = '#f0f0f0';
    ctx.fill();
  });

  // Shirt geometry: upper torso only — use a sphere clipped to top half
  // Body center is at (0, -1.45). Upper torso = y from -0.85 to -1.45 (top half of body sphere)
  // We use a sphere scaled to match body but positioned slightly higher and scaled Y to cover only top
  function makeShirtMesh(tex: THREE.CanvasTexture): THREE.Group {
    const mat = new THREE.MeshStandardMaterial({ map: tex, roughness: 0.75 });
    const g = new THREE.Group();

    // Upper torso — wraps the top half of the body sphere (body at y=-1.45, scale 1.1×1.15×0.88)
    // Use a full sphere slightly larger than body, clipped visually by being positioned at upper body
    const torso = new THREE.Mesh(new THREE.SphereGeometry(0.88, 32, 16, 0, Math.PI * 2, 0, Math.PI * 0.55), mat);
    torso.scale.set(1.13, 1.18, 0.92);
    torso.position.set(0, -1.45, -0.05); // match body center exactly
    g.add(torso);

    // Sleeve caps — match arm positions exactly: (±0.9, -1.15, 0.28), rotated z=±PI/5, x=-PI/5
    for (const side of [-1, 1]) {
      const sleeve = new THREE.Mesh(new THREE.SphereGeometry(0.20, 16, 12, 0, Math.PI * 2, 0, Math.PI * 0.7), mat);
      sleeve.scale.set(1.0, 1.4, 1.0);
      sleeve.position.set(side * 0.9, -1.15, 0.28);
      sleeve.rotation.z = 0;
      sleeve.rotation.x = Math.PI;
      g.add(sleeve);
    }
    return g;
  }

  [hawaiianTex, plainTex, tuxTex].forEach((tex, i) => {
    const g = makeShirtMesh(tex);
    g.name = `shirt-${i}`;
    shirtGroup.add(g);
  });

  // ── AVIATOR SUNGLASSES ────────────────────────────────────────────────────
  const sunglassGroup = new THREE.Group();
  sunglassGroup.visible = false;
  koala.add(sunglassGroup);

  const goldMat  = new THREE.MeshStandardMaterial({ color: 0xd4a017, roughness: 0.15, metalness: 0.95 });
  const lensMat  = new THREE.MeshStandardMaterial({ color: 0x0a2010, roughness: 0.05, metalness: 0.2, transparent: true, opacity: 0.72 });

  // Classic aviator lens: wide teardrop — flat/slightly curved top, wide rounded bottom
  // Eyes at (±0.52, 0.18, 0.9). Make lenses large enough to dominate the face.
  function makeAviatorShape(): THREE.Shape {
    const s = new THREE.Shape();
    // W=0.38 wide, H=0.44 tall — flat top, wide rounded bottom
    s.moveTo(-0.19, 0.10);                                          // top-left
    s.bezierCurveTo(-0.19, 0.16,  0.19, 0.16,  0.19, 0.10);       // flat top arc
    s.bezierCurveTo( 0.22, 0.04,  0.22, -0.16,  0.10, -0.28);     // right side curve down
    s.bezierCurveTo( 0.04, -0.34,  -0.04, -0.34, -0.10, -0.28);   // wide rounded bottom
    s.bezierCurveTo(-0.22, -0.16, -0.22,  0.04, -0.19,  0.10);    // left side curve up
    return s;
  }

  // Eyes at ±0.52, 0.18, 0.9 — position lenses directly over eyes, slightly forward
  for (const side of [-1, 1]) {
    const lensGeo = new THREE.ShapeGeometry(makeAviatorShape(), 48);
    const lens = new THREE.Mesh(lensGeo, lensMat);
    lens.position.set(side * 0.52, 0.18, 1.04);
    sunglassGroup.add(lens);

    // Wire frame tube around lens outline
    const pts = makeAviatorShape().getPoints(64);
    const curve = new THREE.CatmullRomCurve3(pts.map(p => new THREE.Vector3(p.x, p.y, 0)), true);
    const tube = new THREE.TubeGeometry(curve, 80, 0.022, 8, true);
    const frame = new THREE.Mesh(tube, goldMat);
    frame.position.set(side * 0.52, 0.18, 1.04);
    sunglassGroup.add(frame);
  }

  // Nose bridge — short flat bar sitting between the two lenses at top
  const bridgeCurve = new THREE.CatmullRomCurve3([
    new THREE.Vector3(-0.33, 0.22, 1.04),
    new THREE.Vector3(-0.16, 0.20, 1.06),
    new THREE.Vector3( 0.00, 0.20, 1.07),
    new THREE.Vector3( 0.16, 0.20, 1.06),
    new THREE.Vector3( 0.33, 0.22, 1.04),
  ]);
  sunglassGroup.add(new THREE.Mesh(new THREE.TubeGeometry(bridgeCurve, 16, 0.018, 8, false), goldMat));

  // Temples — sweep from outer top corner of each lens back to the ears
  for (const side of [-1, 1]) {
    const ox = side * (0.52 + 0.19); // outer edge of lens
    const templeCurve = new THREE.CatmullRomCurve3([
      new THREE.Vector3(ox,          0.22, 1.04),
      new THREE.Vector3(side * 0.85, 0.22, 0.80),
      new THREE.Vector3(side * 1.15, 0.18, 0.45),
      new THREE.Vector3(side * 1.30, 0.08, 0.10),
    ]);
    sunglassGroup.add(new THREE.Mesh(new THREE.TubeGeometry(templeCurve, 20, 0.018, 8, false), goldMat));
  }

  return { koala, furMat: fur, lightFurMat: lightFur, leftArm, rightArm, shirtGroup, sunglassGroup };
}

export default KoalaCharacter;
