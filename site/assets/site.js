import * as THREE from "./vendor/three.module.js";

const reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

/* ---------- hero: a warm particle orb + drifting field ---------- */
function hero3d(){
  const mount = document.getElementById("hero3d");
  if(!mount) return;
  const scene = new THREE.Scene();
  const cam = new THREE.PerspectiveCamera(55, 1, 0.1, 100);
  cam.position.z = 6.2;
  let renderer;
  try { renderer = new THREE.WebGLRenderer({ antialias:true, alpha:true }); }
  catch(e){ mount.style.display = "none"; return; }   // no WebGL -> skip 3D, site keeps working
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

  // live recolor hook for the accent sampler
  window.ssSetAccent = (goldHex, orangeHex)=>{
    const gc=new THREE.Color(goldHex), oc=new THREE.Color(orangeHex);
    const ca=g.attributes.color;
    for(let i=0;i<N;i++){ const t=(pos[i*3+1]/R+1)/2; const c=gc.clone().lerp(oc,t); ca.setXYZ(i,c.r,c.g,c.b); }
    ca.needsUpdate=true;
    shell.material.color.set(orangeHex); field.material.color.set(goldHex);
  };

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
    el.style.transform = `translate(${p*110}px, ${p*24}px) scale(${1 + p*0.5})`;
    el.style.opacity = String(1 - p*0.85);
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

/* ---------- live accent sampler (recolors the whole site + the 3D orb) ---------- */
const ACCENTS = {
  amber:  {gold:"#F5B23B", amber:"#E8912B", orange:"#E4571E"},
  gold:   {gold:"#FFD666", amber:"#F0B429", orange:"#DE9400"},
  coral:  {gold:"#FFB199", amber:"#F0704E", orange:"#E23B2E"},
  cyan:   {gold:"#7FE3E0", amber:"#2FBEBC", orange:"#0F9E9B"},
  blue:   {gold:"#9FC4FF", amber:"#5B8DEF", orange:"#2E6FDF"},
  violet: {gold:"#C9A8FF", amber:"#9B6BF0", orange:"#7C3AED"},
  pink:   {gold:"#FFA8CE", amber:"#F063A0", orange:"#E23B82"}
};
function accentSampler(){
  const root = document.documentElement;
  const saved = localStorage.getItem("ss-accent") || "amber";
  const apply = (name)=>{
    const a = ACCENTS[name] || ACCENTS.amber;
    root.style.setProperty("--gold", a.gold);
    root.style.setProperty("--amber", a.amber);
    root.style.setProperty("--orange", a.orange);
    root.style.setProperty("--accent", a.orange);
    if(window.ssSetAccent) window.ssSetAccent(a.gold, a.orange);
    localStorage.setItem("ss-accent", name);
  };
  const bar = document.createElement("div"); bar.className = "accentbar";
  const lbl = document.createElement("span"); lbl.className = "lbl"; lbl.textContent = "Accent"; bar.appendChild(lbl);
  Object.keys(ACCENTS).forEach((name)=>{
    const b = document.createElement("button"); b.style.background = ACCENTS[name].orange; b.title = name;
    if(name === saved) b.classList.add("on");
    b.addEventListener("click", ()=>{ apply(name); [...bar.querySelectorAll("button")].forEach(x=>x.classList.remove("on")); b.classList.add("on"); });
    bar.appendChild(b);
  });
  document.body.appendChild(bar);
  apply(saved);
}

/* ---------- interactive Verify demo ---------- */
function verifyDemo(){
  const w = document.getElementById("vwidget");
  if(!w) return;
  const CLAIMS = [
    { src:'"Honey never spoils. 3,000-year-old tomb honey is still edible."', conf:72,
      verdict:'Broadly true, but "never" is overstated and the tomb detail needs a live check.',
      tags:[["ESTABLISHED","#3FA35B","Honey resists spoilage. Low water, acidic."],
            ["UNCERTAIN","#E8912B","\"Never.\" Can ferment if moisture enters."],
            ["NEEDS LIVE CHECK","#4C86C6","The tomb-honey story. Verify the source."]] },
    { src:'"The Great Wall of China is visible from space with the naked eye."', conf:11,
      verdict:'A popular myth. It is not visible unaided from orbit.',
      tags:[["LIKELY FALSE","#E23B3B","Too narrow to resolve with the naked eye."],
            ["ESTABLISHED","#3FA35B","Visible only with magnification, low orbit."]] },
    { src:'"Humans only use 10% of their brain."', conf:6,
      verdict:'A persistent myth. Nearly all of the brain is active.',
      tags:[["LIKELY FALSE","#E23B3B","Imaging shows widespread activity."],
            ["ESTABLISHED","#3FA35B","No large permanently idle region exists."]] }
  ];
  const g = id => document.getElementById(id);
  let timers = [];
  function run(c){
    timers.forEach(clearTimeout); timers = [];
    g("vsrc").textContent = c.src;
    g("vverdict").textContent = "";
    g("vclaims").innerHTML = "";
    g("vfill").style.width = "0%";
    // confidence count + bar
    const col = c.conf>=70?"#3FA35B":c.conf>=40?"#E8912B":"#E23B3B";
    timers.push(setTimeout(()=>{ g("vfill").style.width = c.conf+"%"; g("vfill").style.background = col; }, 60));
    const t0 = performance.now(); let raf;
    (function tick(now){ const p=Math.min((now-t0)/900,1); const v=Math.round(c.conf*(1-Math.pow(1-p,3)));
      g("vconf").textContent = v+"/100"; g("vconf").style.color = col; if(p<1) raf=requestAnimationFrame(tick); })(performance.now());
    timers.push(setTimeout(()=>{ g("vverdict").textContent = c.verdict; }, 420));
    c.tags.forEach((t,i)=>timers.push(setTimeout(()=>{
      const d=document.createElement("div"); d.className="aw-claim";
      d.style.cssText="opacity:0;transform:translateY(8px);transition:.32s cubic-bezier(.16,.8,.24,1)";
      d.innerHTML='<span class="tag" style="background:'+t[1]+'">'+t[0]+'</span><span>'+t[2]+'</span>';
      g("vclaims").appendChild(d); requestAnimationFrame(()=>{ d.style.opacity=1; d.style.transform="none"; });
    }, 720 + i*230)));
  }
  const chips = [...document.querySelectorAll(".claim-chip")];
  let touched = false;
  chips.forEach(b=>b.addEventListener("click", ()=>{ touched = true; chips.forEach(x=>x.classList.remove("on")); b.classList.add("on"); run(CLAIMS[+b.dataset.i]); }));
  if(chips[0]){ chips[0].classList.add("on"); }
  // auto-run the default once, when it scrolls in, unless the user already picked
  const io = new IntersectionObserver((es)=>{ es.forEach(e=>{ if(e.isIntersecting){ if(!touched) run(CLAIMS[0]); io.disconnect(); } }); }, {threshold:.4});
  io.observe(w);
}

/* ---------- cursor-tilt on app-shot cards ---------- */
function tilts(){
  if(matchMedia("(pointer:coarse)").matches) return;
  document.querySelectorAll(".shot").forEach(card=>{
    card.addEventListener("pointermove", e=>{ const r=card.getBoundingClientRect();
      const x=(e.clientX-r.left)/r.width-.5, y=(e.clientY-r.top)/r.height-.5;
      card.style.transform = `perspective(1000px) rotateX(${(-y*6).toFixed(2)}deg) rotateY(${(x*8).toFixed(2)}deg)`; });
    card.addEventListener("pointerleave", ()=>{ card.style.transform=""; });
  });
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
  let renderer;
  try { renderer = new THREE.WebGLRenderer({antialias:true,alpha:true}); }
  catch(e){ mount.style.display="none"; return; }
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

// Isolate every feature: a failure in one (e.g. WebGL) must never break the others.
for(const fn of [hero3d, globe, reveals, heroScroll, pinScrub, counters, chatbot, nav, accentSampler, verifyDemo, tilts]){
  try { fn(); } catch(e){ console.warn("[site] init failed:", fn.name, e && e.message); }
}
