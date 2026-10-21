import { test, expect } from "@playwright/test";
import { readFileSync, readdirSync } from "node:fs";
import { resolve, join, relative } from "node:path";

test.skip(!process.env.VIEWER_WEB_ROOT, "Front-door journeys require a published Blazor wwwroot.");
test.setTimeout(60000);
const root = resolve(import.meta.dirname, "../fixtures/pbi-assure-coverage");
function files(dir = root) {
  return readdirSync(dir, {withFileTypes:true}).flatMap(e => e.isDirectory() ? files(join(dir,e.name)) :
    [{path:relative(root,join(dir,e.name)).replaceAll("\\","/"), text:readFileSync(join(dir,e.name),"utf8")}]);
}
async function start(page) {
  await page.goto("/");
  await expect(page.locator('[data-pbiassure-app-ready]')).toBeVisible({timeout:30000});
  // Observe the actual wire payload and acknowledgement, not an alternative adapter.
  await page.evaluate(() => {
    window.deliveries = [];
    const open = window.open;
    window.open = function(...args) {
      const child = open.apply(this,args);
      if(child) {
        window.lastViewer = child;
        const post = child.postMessage.bind(child);
        child.postMessage = (data, origin) => {
          if(window.deliveryMode === 'unsupported' && data?.snapshot) data.snapshot.ContractVersion = '99.0';
          if(data?.type === 'pbi-assure-viewer-snapshot') window.deliveries.push(structuredClone(data));
          post(data,origin);
        };
      }
      return child;
    };
  });
}
async function choose(page, retained = false) {
  if(retained) {
    await page.evaluate(data => {
      window.currentFiles = data;
      window.enumerations = 0;
      const directory = (name, prefix="") => ({ name,kind:"directory",async *entries() {
        window.enumerations++;
        const children = new Set();
        for(const f of window.currentFiles.filter(f=>f.path.startsWith(prefix))) {
          const rest = f.path.slice(prefix.length), [part] = rest.split("/");
          if(children.has(part))continue; children.add(part);
          yield [part,rest.includes("/") ? directory(part,prefix+part+"/") :
            {kind:"file",getFile:async()=>new File([f.text],part)}];
        }
      }});
      window.showDirectoryPicker = async options => {
        if(options.mode!=="read")throw Error("Unexpected write access");
        return directory("Retained project");
      };
    }, files());
    await page.getByRole('button',{name:'Choose project folder',exact:true}).click();
  } else {
    await page.evaluate(()=>delete window.showDirectoryPicker);
    const picker=page.waitForEvent('filechooser');
    await page.getByRole('button',{name:'Choose project folder',exact:true}).click();
    await (await picker).setFiles(root);
  }
  await expect(page.getByRole('button',{name:'Run analysis',exact:true})).toBeVisible();
}
async function analyse(page) {
  await page.getByRole('button',{name:'Run analysis',exact:true}).click();
  await expect(page.getByRole('button',{name:'Open investigation',exact:true})).toBeEnabled({timeout:30000});
}
async function investigate(page, name = 'Open investigation') {
  const next=page.waitForEvent('popup');
  await page.getByRole('button',{name,exact:true}).click();
  const viewer=await next;
  await expect(viewer.locator('#workspace-heading')).toBeVisible({timeout:30000});
  await expect(page.locator('#investigation-status')).toContainText('Investigation opened');
  return viewer;
}

test('published first-use journey, focused entry points, exports and return',async({page,context},info)=>{
  const external=[]; context.on('request',r=>{if(!r.url().startsWith('http://127.0.0.1:4176/'))external.push(r.url())});
  await start(page);
  await expect(page.locator('.brand')).toHaveText('PBI Project Lens');
  await expect(page).toHaveTitle('Analyse — PBI Project Lens');
  await expect(page.locator('body')).not.toContainText('PBI Assure');
  await expect(page.locator('.privacy')).toContainText('Local & read-only');
  await page.screenshot({path:info.outputPath('front-door-1366.png')});
  await choose(page); await analyse(page);
  await expect(page.locator('.download-options')).not.toHaveAttribute('open');
  await expect(page.getByRole('button',{name:'Open report',exact:true})).toHaveCount(0);
  await page.setViewportSize({width:1440,height:900});
  await page.screenshot({path:info.outputPath('completion-1440.png')});
  const viewer=await investigate(page);
  await expect(viewer.locator('.brand')).toHaveText('PBI Project Lens');
  await expect(viewer).toHaveTitle('PBI Project Lens · Investigation workspace');
  await viewer.setViewportSize({width:1366,height:768});
  await viewer.screenshot({path:info.outputPath('viewer-brand-1366.png')});
  await expect(viewer.locator('#workspace-heading')).toHaveText('Overview');
  const payload=await page.evaluate(()=>window.deliveries[0]);
  expect(payload.snapshot.ContractVersion).toBe('1.0');
  expect(new URL(viewer.url()).searchParams.size).toBe(1); // asset version only
  expect(await viewer.evaluate(()=>[localStorage.getItem('analysis'),sessionStorage.length])).toEqual([null,0]);
  await page.evaluate(()=>{window.returnFocusCount=0;window.focus=()=>window.returnFocusCount++});
  await viewer.getByRole('link',{name:'Return to project',exact:true}).last().click();
  expect(await page.evaluate(()=>window.returnFocusCount)).toBeGreaterThan(0);
  await expect(page.getByRole('button',{name:'Choose folder again to analyse',exact:true})).toBeVisible();
  const unused=await investigate(page,'Review apparently unused in Model Objects');
  await expect(unused.locator('#usage-filter')).toHaveValue('ApparentlyUnused');
  await expect(unused.locator('[data-object]')).toHaveCount(payload.snapshot.Objects.filter(o=>o.UsageState==='ApparentlyUnused'&&!o.IsSystemGenerated).length);
  const scope=await investigate(page,'Review analysis scope');
  await expect(scope.locator('#workspace-heading')).toHaveText('Analysis scope');
  await page.locator('.download-options > summary').click();
  const download=async(name)=>{
    const pending=page.waitForEvent('download');await page.getByRole('button',{name,exact:true}).click();
    const file=await pending; return readFileSync(await file.path(),'utf8');
  };
  const fullHtml=await download('Download report');
  const unusedHtml=await download('Download apparently unused review');
  expect(fullHtml.toLowerCase().startsWith('<!doctype html>')).toBe(true);
  expect(unusedHtml.includes('Apparently unused')).toBe(true);
  for(const html of [fullHtml,unusedHtml]) {
    expect(html.includes('PBI Project Lens')).toBe(true);
    expect(html.includes('PBI Assure')).toBe(false);
  }
  await page.getByRole('button',{name:'Export data',exact:true}).click();
  const catalogue=await download('Download CSV');
  expect(catalogue.split('\n')[0]).toContain('UserFacing');
  await page.getByRole('radio',{name:'Usage mapping',exact:true}).check();
  const mapping=await download('Download CSV');
  expect(mapping.split('\n')[0]).toContain('Report');
  expect(mapping).not.toBe(catalogue);
  await page.locator('.legacy-output > summary').click();
  expect((await download('Download semantic usage CSV')).split('\n')[0]).toContain('SemanticUsage');
  expect(external).toEqual([]);
});

test('retained handle reruns create new snapshots without changing an open viewer',async({page})=>{
  await start(page); await choose(page,true); await analyse(page);
  const original=await investigate(page);
  const before=await original.locator('.overview-workspace').innerText();
  const enumerations=await page.evaluate(()=>window.enumerations);
  await page.evaluate(()=>{
    window.currentFiles=window.currentFiles.map(f=>f.path.endsWith('/definition/tables/Fact.tmdl')?
      {...f,text:f.text+'\n\tmeasure SliceFourNewMeasure = 123\n'}:f);
  });
  await page.getByRole('button',{name:'Analyse again',exact:true}).click();
  await expect(page.getByRole('button',{name:'Open investigation',exact:true})).toBeEnabled();
  const updated=await investigate(page);
  expect(await page.evaluate(()=>window.enumerations)).toBeGreaterThan(enumerations);
  const snapshots=await page.evaluate(()=>window.deliveries.map(d=>d.snapshot));
  expect(snapshots[1].Objects.some(o=>o.Name==='SliceFourNewMeasure')).toBe(true);
  expect(snapshots[0].Objects.some(o=>o.Name==='SliceFourNewMeasure')).toBe(false);
  expect(snapshots[1].ScannedAtUtc).not.toBe(snapshots[0].ScannedAtUtc);
  expect(await original.locator('.overview-workspace').innerText()).toBe(before);
  await expect(updated.locator('#workspace-heading')).toHaveText('Overview');
});

test('popup blocking, cancelled reselection and failed delivery retain the result for retry',async({page})=>{
  await start(page); await choose(page); await analyse(page);
  await page.evaluate(()=>{window.savedOpen=window.open;window.open=()=>null});
  await page.getByRole('button',{name:'Open investigation',exact:true}).click();
  await expect(page.locator('#investigation-status')).toContainText('blocked');
  await page.evaluate(()=>{window.open=window.savedOpen;window.pbiAssureProjectPicker.choose=async()=>{throw Error('[PBIASSURE:CANCELLED]')}});
  await page.getByRole('button',{name:'Choose folder again to analyse',exact:true}).click();
  await expect(page.locator('#project-message')).toHaveText('Project selection was cancelled.');
  await expect(page.getByRole('button',{name:'Open investigation',exact:true})).toBeEnabled();
  await page.route('**/investigation/index.html?*',route=>route.fulfill({status:404,body:'Viewer unavailable'}));
  await page.clock.install();
  const next=page.waitForEvent('popup');await page.getByRole('button',{name:'Open investigation',exact:true}).click();
  const broken=await next;
  await page.clock.fastForward(21000);
  await expect(page.locator('#investigation-status')).toContainText('did not confirm');
  await broken.close(); await page.unroute('**/investigation/index.html?*');
  await investigate(page);
});

test('running and analysis failure are honest',async({page})=>{
  await start(page); await choose(page);
  await page.evaluate(()=>{window.pbiAssureProjectPicker.read=()=>new Promise((_,reject)=>window.failRead=()=>reject(Error('private/raw exception')))});
  await page.getByRole('button',{name:'Run analysis',exact:true}).click();
  await expect(page.locator('#analysis-status')).toHaveText('Analysing your project…');
  await expect(page.getByRole('button',{name:'Change project',exact:true})).toBeDisabled();
  await page.evaluate(()=>window.failRead());
  await expect(page.locator('#project-message')).toContainText('No investigation was generated');
  await expect(page.locator('#project-message')).not.toContainText('private/raw');
  await expect(page.getByRole('button',{name:'Open investigation',exact:true})).toHaveCount(0);
  await expect(page.getByRole('button',{name:'Run analysis',exact:true})).toBeEnabled();
});

test('acknowledgement waits for acceptance; rejected, closed and broken viewers can be reopened',async({page,context})=>{
  await start(page); await choose(page); await analyse(page);
  let release, requested;
  const gate=new Promise(r=>release=r), request=new Promise(r=>requested=r);
  await context.route('**/investigation/controller.js?*',async route=>{requested();await gate;await route.continue()});
  const next=page.waitForEvent('popup');
  await page.getByRole('button',{name:'Open investigation',exact:true}).click();
  const viewer=await next; await request;
  await expect(page.locator('#investigation-status')).toHaveText('Opening investigation…');
  await page.evaluate(()=>{
    const id=window.deliveries.at(-1).deliveryId;
    for(const event of [
      {origin:location.origin,source:window},
      {origin:'https://invalid.example',source:window.lastViewer},
      {origin:location.origin,source:window.lastViewer,wrongId:true},
    ])window.dispatchEvent(new MessageEvent('message',{...event,data:{type:'pbi-assure-viewer-snapshot-ack',deliveryId:event.wrongId?'old-delivery':id,accepted:true}}));
  });
  await expect(page.locator('#investigation-status')).toHaveText('Opening investigation…');
  release();
  await expect(viewer.locator('#workspace-heading')).toHaveText('Overview');
  await expect(page.locator('#investigation-status')).toContainText('Investigation opened');
  await context.unroute('**/investigation/controller.js?*');
  await page.evaluate(()=>window.deliveryMode='unsupported');
  const badNext=page.waitForEvent('popup');
  await page.getByRole('button',{name:'Open investigation',exact:true}).click();
  const rejected=await badNext;
  await expect(rejected.locator('#app')).toContainText('Unsupported viewer snapshot version');
  await expect(page.locator('#investigation-status')).toContainText('could not accept');
  await page.evaluate(()=>window.deliveryMode=null);
  await context.route('**/investigation/index.html?*',route=>route.fulfill({body:'Loading test viewer'}));
  const closing=page.waitForEvent('popup');
  await page.getByRole('button',{name:'Open investigation',exact:true}).click();
  await (await closing).close();
  await expect(page.locator('#investigation-status')).toContainText('closed before accepting');
  await context.unroute('**/investigation/index.html?*');
  await context.route('**/investigation/controller.js?*',route=>route.abort());
  const missing=page.waitForEvent('popup');
  await page.getByRole('button',{name:'Open investigation',exact:true}).click();
  const broken=await missing;
  await expect(broken.locator('#app')).toContainText('The investigation could not be loaded');
  await expect(broken.locator('#app')).not.toContainText('Failed to fetch');
  await expect(page.locator('#investigation-status')).toContainText('could not accept');
  await context.unroute('**/investigation/controller.js?*');
  await investigate(page);
});

test('fallback reselection runs a new analysis and cancellation preserves export choices',async({page})=>{
  await start(page); await choose(page); await analyse(page);
  await page.locator('.download-options > summary').click();
  await page.getByRole('button',{name:'Export data',exact:true}).click();
  await page.getByRole('radio',{name:'Usage mapping',exact:true}).check();
  await page.evaluate(()=>{window.savedChoose=window.pbiAssureProjectPicker.choose;window.pbiAssureProjectPicker.choose=async()=>{throw Error('[PBIASSURE:CANCELLED]')}});
  await page.getByRole('button',{name:'Choose folder again to analyse',exact:true}).click();
  await expect(page.locator('#project-message')).toHaveText('Project selection was cancelled.');
  await expect(page.getByRole('radio',{name:'Usage mapping',exact:true})).toBeChecked();
  await investigate(page);
  await page.evaluate(()=>window.pbiAssureProjectPicker.choose=window.savedChoose);
  await page.bringToFront();
  const next=page.waitForEvent('filechooser');
  await page.getByRole('button',{name:'Choose folder again to analyse',exact:true}).click();
  await (await next).setFiles(root);
  await expect(page.getByRole('button',{name:'Open investigation',exact:true})).toBeEnabled();
  await investigate(page);
  expect(await page.evaluate(()=>window.deliveries[0].snapshot.ScannedAtUtc!==window.deliveries[1].snapshot.ScannedAtUtc)).toBe(true);
});
