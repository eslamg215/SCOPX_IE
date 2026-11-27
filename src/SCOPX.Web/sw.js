const CACHE='scopx-demo-shell-v3';
const FILES=['./','./index.html','./styles.css','./app.js','./manifest.webmanifest','./assets/icon.svg'];
const CACHE_PREFIX='scopx-demo-shell-';
const isAppAsset=url=>FILES.some(path=>new URL(path,self.registration.scope).pathname===url.pathname);
self.addEventListener('install',event=>event.waitUntil(caches.open(CACHE).then(cache=>cache.addAll(FILES))));
self.addEventListener('activate',event=>event.waitUntil(caches.keys().then(keys=>Promise.all(keys.filter(key=>key.startsWith(CACHE_PREFIX)&&key!==CACHE).map(key=>caches.delete(key))))));
self.addEventListener('fetch',event=>{
  const request=event.request;
  if(request.method!=='GET')return;
  const url=new URL(request.url);
  if(url.origin!==self.location.origin)return;
  const cacheable=isAppAsset(url);
  if(!cacheable&&request.mode!=='navigate')return;
  event.respondWith(caches.match(request).then(cached=>cached||fetch(request).then(response=>{
    if(response.ok&&cacheable){const copy=response.clone();caches.open(CACHE).then(cache=>cache.put(request,copy));}
    return response;
  }).catch(()=>{
    if(request.mode==='navigate')return caches.match('./index.html');
    return caches.match(request);
  })));
});
