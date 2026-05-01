import React, { useEffect, useRef } from 'react';
import * as THREE from 'three';

interface KoalaCharacterProps {
  state: 'idle' | 'talking' | 'listening' | 'thinking';
  onChatClick: () => void;
  chatOpen: boolean;
  width: number;
  height: number;
}

const KoalaCharacter: React.FC<KoalaCharacterProps> = ({ state, onChatClick, chatOpen, width, height }) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const koalaRef = useRef<THREE.Group | null>(null);
  const mouthRef = useRef<THREE.Mesh | null>(null);
  const eyeRefs = useRef<{ left: THREE.Mesh | null; right: THREE.Mesh | null }>({ left: null, right: null });
  const animationFrameRef = useRef<number>(0);
  const blinkIntervalRef = useRef<NodeJS.Timeout | null>(null);
  const stateRef = useRef(state);
  const draggingRef = useRef(false);
  stateRef.current = state;

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
    container.appendChild(renderer.domElement);

    scene.add(new THREE.AmbientLight(0xffffff, 0.6));
    const dirLight = new THREE.DirectionalLight(0xffffff, 0.8);
    dirLight.position.set(1, 2, 2);
    scene.add(dirLight);

    const koala = createKoala();
    scene.add(koala);
    koalaRef.current = koala;

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
      if (koalaRef.current) {
        koalaRef.current.position.y = Math.sin(time) * 0.05;
        koalaRef.current.rotation.y = Math.sin(time * 0.5) * 0.05;
        if (stateRef.current === 'talking' && mouthRef.current) {
          mouthRef.current.scale.y = 1 + Math.sin(time * 15) * 0.3;
        } else if (mouthRef.current) {
          mouthRef.current.scale.y = 1;
        }
        if (stateRef.current === 'listening') koalaRef.current.rotation.y = Math.sin(time * 2) * 0.1;
        if (stateRef.current === 'thinking') koalaRef.current.rotation.x = -0.22 + Math.sin(time) * 0.05;      }
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

  const handleMouseDown = (e: React.MouseEvent) => {
    let moved = false;
    let lastX = e.screenX, lastY = e.screenY;
    draggingRef.current = true;
    window.electron.startMove();

    const onMove = (me: MouseEvent) => {
      const dx = me.screenX - lastX, dy = me.screenY - lastY;
      if (Math.abs(dx) > 2 || Math.abs(dy) > 2) moved = true;
      window.electron.moveWindow(dx, dy);
      lastX = me.screenX; lastY = me.screenY;
    };
    const onUp = () => {
      draggingRef.current = false;
      window.removeEventListener('mousemove', onMove);
      window.removeEventListener('mouseup', onUp);
      if (!moved) onChatClick();
    };
    window.addEventListener('mousemove', onMove);
    window.addEventListener('mouseup', onUp);
  };

  return (
    <div
      ref={containerRef}
      onMouseDown={handleMouseDown}
      style={{ width: width + 'px', height: height + 'px', cursor: 'grab', background: 'transparent', overflow: 'hidden', position: 'relative' }}
      title="Click to chat · Drag to move"
    >
      {!chatOpen && (
        <div style={{
          position: 'absolute', bottom: '8px', left: '50%', transform: 'translateX(-50%)',
          color: 'rgba(255,255,255,0.7)', fontSize: '12px', pointerEvents: 'none', textAlign: 'center',
        }}>
          Click to chat with Rémi 🐨
        </div>
      )}
    </div>
  );
};

function createKoala(): THREE.Group {
  const koala = new THREE.Group();

  const fur        = new THREE.MeshStandardMaterial({ color: 0x8a8a9a, roughness: 0.95 });
  const lightFur   = new THREE.MeshStandardMaterial({ color: 0xd0d0e0, roughness: 0.9 });
  const darkFur    = new THREE.MeshStandardMaterial({ color: 0x555566, roughness: 0.95 });
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
  for (const side of [-1, 1]) {
    const arm = new THREE.Mesh(new THREE.CapsuleGeometry(0.17, 0.42, 16, 32), fur);
    arm.position.set(side * 0.9, -1.15, 0.28);
    arm.rotation.z = side * Math.PI / 5;
    arm.rotation.x = -Math.PI / 5;
    koala.add(arm);
    const paw = new THREE.Mesh(new THREE.SphereGeometry(0.2, 32, 32), lightFur);
    paw.position.set(side * 1.1, -1.4, 0.42);
    koala.add(paw);
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

  return koala;
}

export default KoalaCharacter;
