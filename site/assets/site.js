import * as THREE from "./vendor/three.module.js";

const reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

/* ---------- hero: a warm particle orb + drifting field ---------- */
function hero3d(){
  const mount = document.getElementById("hero3d");
  if(!mount) return;
  const scene = new THREE.Scene();
  const cam = new THREE.PerspectiveCamera(55, 1, 0.1, 100);
  cam.position.z = 6.2;
  const renderer = new THREE.WebGLRenderer({ antialias:true, alpha:true });
  renderer.setClearColor(0x000000, 0);
  mount.appendChild(renderer.domElement);

  const gold = new THREE.Color("#F5B23B");
  const orange = new THREE.Color("#E4571E");

  // orb: points on a sphere, colored gold->orange by latitude
  const N = 2600, R = 2.35;
  const pos = new Float32Array(N*3), col = new Float32Array(N*3);
  for(let i=0;i<N;i++){
    const y = 1 - (i/(N-1))*2;
    const r = Math.sqrt(1-y*y);
    const phi = i * Math.PI * (3 - Math.sqrt(5));
    const x = Math.cos(phi)*r, z = Math.sin(phi)*r;
    pos[i*3]=x*R; pos[i*3+1]=y*R; pos[i*3+2]=z*R;
    const c = gold.clone().lerp(orange, (y+1)/2);
    col[i*3]=c.r; col[i*3+1]=c.g; col[i*3+2]=c.b;
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute("position", new THREE.BufferAttribute(pos,3));
  g.setAttribute("color", new THREE.BufferAttribute(col,3));
  const m = new THREE.PointsMaterial({ size:0.045, vertexColors:true, transparent:true,
    opacity:0.95, blending:THREE.AdditiveBlending, depthWrite:false });
  const orb = new THREE.Points(g, m);
  scene.add(orb);

  // inner wire shell for structure
  const shell = new THREE.Mesh(
    new THREE.IcosahedronGeometry(1.7, 1),
    new THREE.MeshBasicMaterial({ color:0xE4571E, wireframe:true, transparent:true, opacity:0.12 }));
  scene.add(shell);

  // ambient field
  const F = 900;
  const fp = new Float32Array(F*3);
  for(let i=0;i<F;i++){ fp[i*3]=(Math.random()-.5)*22; fp[i*3+1]=(Math.random()-.5)*14; fp[i*3+2]=(Math.random()-.5)*10-2; }
  const fg = new THREE.BufferGeometry();
  fg.setAttribute("position", new THREE.BufferAttribute(fp,3));
  const field = new THREE.Points(fg, new THREE.PointsMaterial({ size:0.03, color:0xF5B23B,
    transparent:true, opacity:0.5, blending:THREE.AdditiveBlending, depthWrite:false }));
  scene.add(field);

  let mx=0, my=0;
  window.addEventListener("pointermove", (e)=>{ mx=(e.clientX/innerWidth-.5); my=(e.clientY/innerHeight-.5); });

  function size(){
    const w = mount.clientWidth, h = mount.clientHeight || mount.offsetHeight || 600;
    renderer.setPixelRatio(Math.min(devicePixelRatio,2));
    renderer.setSize(w,h,false); cam.aspect=w/h; cam.updateProjectionMatrix();
  }
  size(); window.addEventListener("resize", size);

  let t=0;
  function loop(){
    t += reduced ? 0 : 0.0045;
    orb.rotation.y = t; orb.rotation.x = Math.sin(t*0.6)*0.18;
    shell.rotation.y = -t*0.7; shell.rotation.x = t*0.3;
    field.rotation.y = t*0.25;
    const s = 1 + Math.sin(t*1.4)*0.03; orb.scale.setScalar(s);
    cam.position.x += (mx*1.4 - cam.position.x)*0.05;
    cam.position.y += (-my*1.0 - cam.position.y)*0.05;
    cam.lookAt(0,0,0);
    renderer.render(scene,cam);
    requestAnimationFrame(loop);
  }
  loop();
}

/* ---------- scroll reveal ---------- */
function reveals(){
  const io = new IntersectionObserver((es)=>{ for(const e of es) if(e.isIntersecting){ e.target.classList.add("in"); io.unobserve(e.target); } }, { threshold:0.12 });
  document.querySelectorAll(".reveal, .fade-up").forEach(el=>io.observe(el));
}

/* ---------- hero orb scales + fades as you scroll past it ---------- */
function heroScroll(){
  const el = document.getElementById("hero3d");
  const hero = document.querySelector(".applehero");
  if(!el || !hero) return;
  const on = ()=>{
    const p = Math.min(Math.max(window.scrollY / hero.offsetHeight, 0), 1);
    el.style.transform = `scale(${1 + p*0.6}) translateY(${p*30}px)`;
    el.style.opacity = String(1 - p*0.9);
  };
  on(); window.addEventListener("scroll", on, {passive:true});
}

/* ---------- pinned scrub: cross-fade panels through a tall sticky block ---------- */
function pinScrub(){
  const pin = document.querySelector(".pin");
  if(!pin) return;
  const panels = [...pin.querySelectorAll(".pin-panel")];
  const on = ()=>{
    const rect = pin.getBoundingClientRect();
    const total = pin.offsetHeight - window.innerHeight;
    const p = Math.min(Math.max(-rect.top / total, 0), 0.999);
    const idx = Math.floor(p * panels.length);
    panels.forEach((el,i)=>el.classList.toggle("active", i===idx));
  };
  on(); window.addEventListener("scroll", on, {passive:true});
}

/* ---------- animated counters ---------- */
function counters(){
  const io = new IntersectionObserver((es)=>{
    for(const e of es){ if(!e.isIntersecting) continue; const el=e.target;
      const to = parseFloat(el.dataset.count), suf = el.dataset.suffix||"", dec = el.dataset.dec?parseInt(el.dataset.dec):0;
      const dur = 1400, t0 = performance.now();
      function step(now){ const p=Math.min((now-t0)/dur,1); const v=(to*(1-Math.pow(1-p,3)));
        el.textContent = v.toFixed(dec)+suf; if(p<1) requestAnimationFrame(step); }
      requestAnimationFrame(step); io.unobserve(el);
    }
  }, { threshold:0.5 });
  document.querySelectorAll("[data-count]").forEach(el=>io.observe(el));
}

/* ---------- chatbot (local, canned) ---------- */
function chatbot(){
  const btn=document.getElementById("botBtn"), panel=document.getElementById("botPanel"),
        msgs=document.getElementById("botMsgs"), input=document.getElementById("botIn"), send=document.getElementById("botSend");
  if(!btn) return;
  btn.addEventListener("click", ()=>panel.classList.toggle("open"));
  const canned = {
    "price":"Pricing is not live yet. Pro Shifter, Great Shifter, and Enterprise tiers are planned once the managed backend ships.",
    "key":"SideShift uses your own Anthropic or OpenRouter key, stored encrypted on your machine. No server, no account.",
    "verify":"Verify runs an independent critic on a highlighted claim, scores confidence, and cites honestly. Turn on Web-grounded Verify for real live sources.",
    "download":"Grab the mac or Windows build from the Download button up top. It floats over any app.",
    "default":"I am a demo assistant on this page. Ask about Verify, your API key, pricing, or downloads."
  };
  function reply(q){ q=q.toLowerCase(); for(const k in canned) if(k!=="default"&&q.includes(k)) return canned[k]; return canned.default; }
  function add(text, who){ const d=document.createElement("div"); d.className="bot-msg "+(who==="me"?"bot-me":"bot-ai"); d.textContent=text; msgs.appendChild(d); msgs.scrollTop=msgs.scrollHeight; }
  function submit(){ const v=input.value.trim(); if(!v) return; add(v,"me"); input.value=""; setTimeout(()=>add(reply(v),"ai"),300); }
  send.addEventListener("click",submit);
  input.addEventListener("keydown",(e)=>{ if(e.key==="Enter") submit(); });
}

/* ---------- mobile nav toggle ---------- */
function nav(){ const b=document.getElementById("burger"), l=document.querySelector(".nav-links"); if(!b) return;
  b.addEventListener("click",()=>{ l.style.display = l.style.display==="flex"?"none":"flex"; }); }

/* ---------- contact globe: wire sphere + point dots ---------- */
function globe(){
  const mount = document.getElementById("globe");
  if(!mount) return;
  const scene = new THREE.Scene();
  const cam = new THREE.PerspectiveCamera(45,1,0.1,100); cam.position.z=3.2;
  const renderer = new THREE.WebGLRenderer({antialias:true,alpha:true});
  renderer.setClearColor(0x000000,0); mount.appendChild(renderer.domElement);

  const wire = new THREE.Mesh(new THREE.SphereGeometry(1.25,26,26),
    new THREE.MeshBasicMaterial({color:0xE4571E,wireframe:true,transparent:true,opacity:0.16}));
  scene.add(wire);
  // point dots on the sphere
  const N=700, pos=new Float32Array(N*3);
  for(let i=0;i<N;i++){ const y=1-(i/(N-1))*2, r=Math.sqrt(1-y*y), phi=i*Math.PI*(3-Math.sqrt(5));
    pos[i*3]=Math.cos(phi)*r*1.27; pos[i*3+1]=y*1.27; pos[i*3+2]=Math.sin(phi)*r*1.27; }
  const g=new THREE.BufferGeometry(); g.setAttribute("position",new THREE.BufferAttribute(pos,3));
  const dots=new THREE.Points(g,new THREE.PointsMaterial({size:0.03,color:0xF5B23B,transparent:true,opacity:0.85,blending:THREE.AdditiveBlending,depthWrite:false}));
  scene.add(dots);
  function size(){ const s=mount.clientWidth; renderer.setPixelRatio(Math.min(devicePixelRatio,2)); renderer.setSize(s,s,false); }
  size(); window.addEventListener("resize",size);
  let t=0; (function loop(){ t+=reduced?0:0.004; wire.rotation.y=t; dots.rotation.y=t; wire.rotation.x=dots.rotation.x=0.3; renderer.render(scene,cam); requestAnimationFrame(loop); })();
}

hero3d(); globe(); reveals(); heroScroll(); pinScrub(); counters(); chatbot(); nav();
