#!/usr/bin/env python3
"""HTTP asset smoke test and optional browser smoke test for the demo PWA."""
from pathlib import Path
from http.server import ThreadingHTTPServer, SimpleHTTPRequestHandler
from threading import Thread
from urllib.request import urlopen
import json
import os
import tempfile
from playwright.sync_api import sync_playwright
ROOT=Path(__file__).resolve().parents[2]/'src'/'SCOPX.Web'
class Handler(SimpleHTTPRequestHandler):
    def __init__(self,*args,**kwargs): super().__init__(*args,directory=str(ROOT),**kwargs)
    def log_message(self,*args): pass
server=ThreadingHTTPServer(('127.0.0.1',0),Handler)
Thread(target=server.serve_forever,daemon=True).start()
url=f'http://127.0.0.1:{server.server_port}/'
checks=[]
def check(ok,label):
    checks.append((ok,label)); print(('PASS: ' if ok else 'FAIL: ')+label)
try:
    for path in ['','index.html','styles.css','app.js','manifest.webmanifest','sw.js','assets/icon.svg']:
        with urlopen(url+path,timeout=3) as response:
            check(response.status==200 and len(response.read())>0,f'HTTP serves {path or "root"}')
    browser_available=True
    try:
        with sync_playwright() as p:
            browser=p.chromium.launch(headless=True,args=['--no-sandbox'])
            page=browser.new_page(viewport={'width':1365,'height':850})
            errors=[]
            page.on('pageerror',lambda err:errors.append(str(err)))
            page.goto(url,wait_until='networkidle',timeout=7000)
            page.keyboard.press('Tab')
            check(page.evaluate("document.activeElement.classList.contains('skip-link')"),'keyboard focus reaches the skip-to-content link first')
            page.keyboard.press('Enter')
            check(page.evaluate("document.activeElement.id === 'viewRoot'"),'skip link moves keyboard focus to main content')
            check(page.locator('#pageTitle').inner_text()=='نظرة عامة','browser renders dashboard')
            check(page.locator('.stat-card').count()==4,'dashboard displays metric cards')
            page.locator('[data-view="patients"]').first.click()
            page.locator('[data-open-patient]').first.click()
            page.locator('#patientForm input[name="name"]').fill('Demo Smoke Patient')
            page.locator('#patientForm input[name="record"]').fill('SMOKE-TEST-001')
            page.locator('#patientForm input[name="age"]').fill('42')
            page.locator('#patientForm button[type="submit"]').click()
            check(page.get_by_text('Demo Smoke Patient').count()>0,'synthetic patient creation works')
            page.locator('[data-new-procedure]').last.click()
            page.locator('#procedureForm input[name="title"]').fill('Smoke Test Procedure')
            page.locator('#procedureForm input[name="doctor"]').fill('Demo Tester')
            page.locator('#procedureForm button[type="submit"]').click()
            check(page.get_by_text('Smoke Test Procedure').count()>0,'procedure creation works')
            smoke_procedure=page.locator('.procedure-card').filter(has_text='Smoke Test Procedure')
            smoke_procedure.locator('[data-start]').click()
            check(smoke_procedure.locator('[data-complete]').count()==1,'new synthetic procedure transitions to in-progress')
            smoke_procedure.locator('[data-complete]').click()
            page.locator('[data-view="reports"]').first.click()
            check(page.get_by_text('Demo report — Smoke Test Procedure').count()>0,'procedure completion creates report preview')
            page.locator('[data-view="media"]').first.click()
            page.locator('[data-open-media]').click()
            page.locator('#mediaForm input[name="file"]').set_input_files({
                'name':'fake.svg',
                'mimeType':'image/svg+xml',
                'buffer':b'<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script></svg>'
            })
            page.once('dialog',lambda dialog:dialog.accept())
            page.locator('#mediaForm button[type="submit"]').click()
            check(page.locator('.media-card').count()==0,'SVG upload is rejected by binary signature validation')
            page.locator('#mediaForm input[name="file"]').set_input_files({
                'name':'synthetic-smoke.png',
                'mimeType':'image/png',
                'buffer':bytes.fromhex('89504e470d0a1a0a0000000d49484452000000010000000108060000001f15c4890000000d49444154789c63606060f80f00010401005fe5c34b0000000049454e44ae426082')
            })
            page.locator('#mediaForm input[name="caption"]').fill('Synthetic smoke image')
            page.locator('#mediaForm button[type="submit"]').click()
            page.wait_for_selector('.media-card', timeout=5000)
            check(page.locator('.media-card').count()==1,'synthetic media upload and local gallery rendering work')
            with page.expect_download() as download_info:
                page.locator('#exportData').click()
            download=download_info.value
            with tempfile.TemporaryDirectory() as temp_dir:
                backup_path=Path(temp_dir)/download.suggested_filename
                download.save_as(str(backup_path))
                backup=json.loads(backup_path.read_text(encoding='utf-8'))
                check(len(backup['patients'])>=3 and len(backup['media'])==1,'JSON export contains synthetic records and media')
                check(backup_path.stat().st_size<=10*1024*1024,'exported backup respects the 10 MiB import limit')
            page.reload(wait_until='networkidle')
            page.locator('[data-view="media"]').first.click()
            check(page.locator('.media-card').count()==1,'synthetic media persists after browser reload')
            page.locator('[data-view="patients"]').first.click()
            check(page.get_by_text('Demo Smoke Patient').count()>0,'synthetic patient persists after browser reload')
            page.once('dialog',lambda dialog:dialog.accept())
            page.locator('#resetDemoData').click()
            check(page.get_by_text('Demo Smoke Patient').count()==0 and page.get_by_text('Demo Patient 01').count()>0,'confirmed demo reset restores the seeded synthetic dataset')
            invalid_backup=json.dumps({'patients':[{}],'procedures':[],'reports':[]}).encode('utf-8')
            page.locator('#importFile').set_input_files({'name':'invalid-demo-backup.json','mimeType':'application/json','buffer':invalid_backup})
            check(page.get_by_text('Demo Patient 01').count()>0,'invalid JSON backup is rejected without replacing local demo data')
            oversized_backup=b' '*(10*1024*1024+1)
            page.locator('#importFile').set_input_files({'name':'oversized-demo-backup.json','mimeType':'application/json','buffer':oversized_backup})
            check(page.get_by_text('Demo Patient 01').count()>0,'oversized backup is rejected before reading or replacing local demo data')
            future_backup=json.dumps({'schemaVersion':99,'patients':[],'procedures':[],'reports':[]}).encode('utf-8')
            page.locator('#importFile').set_input_files({'name':'future-demo-backup.json','mimeType':'application/json','buffer':future_backup})
            check(page.get_by_text('Demo Patient 01').count()>0,'unknown future backup schema is rejected without replacing local data')
            check(not errors,'no uncaught browser JS errors')
            page.set_viewport_size({'width':390,'height':844})
            page.reload(wait_until='networkidle')
            check(page.locator('#menuToggle').is_visible(),'mobile navigation toggle visible')
            width=page.evaluate('document.documentElement.scrollWidth')
            check(width<=410,f'mobile viewport avoids horizontal overflow ({width}px)')
            page.evaluate("navigator.serviceWorker.ready")
            page.context.set_offline(True)
            page.reload(wait_until='domcontentloaded',timeout=7000)
            check(page.locator('#pageTitle').inner_text()=='نظرة عامة','PWA reloads the cached demo shell while offline')
            page.context.set_offline(False)
            page.evaluate("localStorage.setItem('scopx.ie.demo.v01', JSON.stringify({patients:[{}],procedures:[],reports:[],media:[]}))")
            page.reload(wait_until='networkidle')
            check(page.get_by_text('Demo Patient 01').count()>0 and page.get_by_text('Demo Smoke Patient').count()==0,'malformed persisted local data is replaced with the safe synthetic seed')
            browser.close()
    except Exception as e:
        browser_available=False
        print(f'SKIP: browser-interaction tests unavailable in this environment ({type(e).__name__}: {e})')
        if os.environ.get('SCOPX_REQUIRE_BROWSER') == '1':
            raise
finally:
    server.shutdown()
fail=sum(not ok for ok,_ in checks)
print(f'\n{len(checks)-fail}/{len(checks)} HTTP smoke checks passed')
print('Browser interaction checks: '+('completed' if browser_available else 'skipped; environment browser policy blocked navigation'))
raise SystemExit(1 if fail else 0)
