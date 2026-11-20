const $ = id => document.getElementById(id);
let latestReport = null;
const fmt = (value, fallback = 'Unknown') => value === null || value === undefined || value === '' ? fallback : String(value);
const bytes = n => Number.isFinite(n) && n > 0 ? (n / (1024 ** 3)).toFixed(1) + ' GiB' : 'Unknown';
// System.Text.Json serializes C# HealthLevel enums as integers by default.
const levelName = level => ({0:'Green',1:'Yellow',2:'Red',3:'Unknown'})[level] ?? (typeof level === 'string' ? level : 'Unknown');
const pct = n => Number.isFinite(n) ? Math.max(0, Math.min(100, n)) : 0;
const safe = s => String(s ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
function facts(target, entries) {
  $(target).replaceChildren();
  for (const [key, value] of entries) {
    const row = document.createElement('div'), dt = document.createElement('dt'), dd = document.createElement('dd');
    dt.textContent = key; dd.textContent = fmt(value); row.append(dt, dd); $(target).append(row);
  }
}
function card(name, icon, value, detail, fill, color='') {
  return '<article class="panel metric"><div class="metric-top"><span><span class="metric-icon">'+icon+'</span> &nbsp;'+safe(name)+'</span></div><div class="metric-value">'+safe(value)+'</div><div class="metric-sub">'+safe(detail)+'</div><div class="track"><div class="fill '+color+'" style="width:'+pct(fill)+'%"></div></div></article>';
}
function render(report, file) {
  latestReport = report;
  const sys=report.System??{}, health=report.Health??{}, network=report.Network??{}, battery=report.Battery??{};
  const disk=(report.Disks??[]).find(d=>d.Name==='/')??(report.Disks??[])[0];
  const total=disk?.TotalBytes??0, free=disk?.FreeBytes??0, freePct=total>0?free/total*100:null;
  const score=Number(health.OverallScore??0);
  $('overall').textContent=Number.isFinite(score)?score:'—';
  $('ring').style.setProperty('--score-angle',pct(score)*3.6+'deg');
  $('overall-label').textContent=({Green:'Healthy',Yellow:'Needs attention',Red:'Attention required',Unknown:'Unknown'})[levelName(health.OverallLevel)];
  $('machine').textContent=fmt(report.MachineName)+' · '+fmt(sys.CpuModel)+' · '+fmt(sys.OsDescription);
  const low=freePct!==null&&freePct<10;
  $('warning').classList.toggle('hidden',!low);
  if(low)$('warning').textContent='⚠ Low disk space — '+freePct.toFixed(1)+'% free ('+bytes(free)+' available)';
  const storageVolumes = report.Disks ?? [];
  const rootVolume = storageVolumes.find(d => d.Name === '/') ?? null;
  const storageTarget = rootVolume ?? (storageVolumes.length === 1 ? storageVolumes[0] : null);
  const storageTotal = storageTarget?.TotalBytes;
  const storageFree = storageTarget?.FreeBytes;
  const validStorage = Number.isFinite(storageTotal) && storageTotal > 0 && Number.isFinite(storageFree) && storageFree >= 0 && storageFree <= storageTotal;
  const usedBytes = validStorage ? storageTotal - storageFree : null;
  const usedPercent = validStorage ? usedBytes / storageTotal * 100 : null;
  $('storage-total').textContent = validStorage ? bytes(storageTotal) : 'N/A';
  $('storage-free').textContent = validStorage ? bytes(storageFree) : 'N/A';
  $('storage-used').textContent = validStorage ? (usedBytes === 0 ? '0 GiB' : bytes(usedBytes)) : 'N/A';
  $('storage-used-pct').textContent = validStorage ? usedPercent.toFixed(1) + '%' : 'N/A';
  $('storage-fill').style.width = validStorage ? pct(usedPercent) + '%' : '0%';
  $('storage-available-fill').style.width = validStorage ? pct(100 - usedPercent) + '%' : '0%';
  $('storage-bar-used').textContent = validStorage ? usedPercent.toFixed(1) + '% used' : 'Used: N/A';
  $('storage-bar-available').textContent = validStorage ? (100 - usedPercent).toFixed(1) + '% available' : 'Available: N/A';
  $('storage-progress').setAttribute('aria-valuenow', validStorage ? usedPercent.toFixed(1) : '0');
  $('storage-progress').setAttribute('aria-valuetext', validStorage ? usedPercent.toFixed(1) + '% used, ' + (100 - usedPercent).toFixed(1) + '% available' : 'Storage information unavailable');
  const volumeContainer = $('storage-volumes');
  volumeContainer.replaceChildren();
  if (!storageVolumes.length) {
    volumeContainer.textContent = 'No logical volumes reported.';
  } else {
    for (const volume of storageVolumes) {
      const dl = document.createElement('dl');
      dl.className = 'storage-entry';
      for (const [label, value] of [
        ['Volume / mount', volume.Name],
        ['Capacity', Number.isFinite(volume.TotalBytes) && volume.TotalBytes > 0 ? bytes(volume.TotalBytes) : 'Unknown'],
        ['Available', Number.isFinite(volume.FreeBytes) && volume.FreeBytes >= 0 ? (volume.FreeBytes === 0 ? '0 GiB' : bytes(volume.FreeBytes)) : 'Unknown']
      ]) {
        const row = document.createElement('div'), dt = document.createElement('dt'), dd = document.createElement('dd');
        dt.textContent = label; dd.textContent = fmt(value); row.append(dt, dd); dl.append(row);
      }
      volumeContainer.append(dl);
    }
  }
  const physicalDisks = report.Storage?.Disks ?? [];
  const physicalContainer = $('storage-physical');
  physicalContainer.replaceChildren();
  if (!physicalDisks.length) {
    physicalContainer.textContent = 'No physical disk information reported.';
  } else {
    for (const diskInfo of physicalDisks) {
      const dl = document.createElement('dl');
      dl.className = 'storage-entry';
      for (const [label, value] of [['Device', diskInfo.Name ?? diskInfo.Model], ['Model', diskInfo.Model], ['TRIM support', diskInfo.TrimSupport]]) {
        const row = document.createElement('div'), dt = document.createElement('dt'), dd = document.createElement('dd');
        dt.textContent = label; dd.textContent = fmt(value); row.append(dt, dd); dl.append(row);
      }
      physicalContainer.append(dl);
    }
  }
  const networkItem = (health.Items ?? []).find(i => i.Name === 'Network');
  const networkLevel = networkItem ? levelName(networkItem.Level) : 'Unknown';
  $('network-health').textContent = networkLevel;
  $('network-score').textContent = Number.isFinite(networkItem?.Score) && networkLevel !== 'Unknown' ? networkItem.Score + '/100' : 'N/A';
  $('network-health-message').textContent = fmt(networkItem?.Message, 'No network health explanation in latest report.');
  facts('network-facts',[
    ['Primary adapter', network.PrimaryAdapterName],
    ['Adapter type', network.PrimaryAdapterType],
    ['Local IPv4 address', network.IPv4],
    ['Default gateway', network.Gateway],
    ['Wi-Fi network (SSID)', network.Wifi?.Ssid || (network.Wifi?.Notes?.includes('SSID restricted by macOS') ? 'Restricted by macOS' : 'Unknown')]
  ]);
  const batteryPresent = battery.IsPresent === true;
  const charge = batteryPresent && Number.isFinite(battery.Percentage) && battery.Percentage >= 0 && battery.Percentage <= 100 ? battery.Percentage : null;
  const charging = !batteryPresent ? 'N/A' : battery.IsCharging === true ? 'Charging' : battery.IsCharging === false ? 'Not charging' : 'Unknown';
  const design = batteryPresent && Number.isFinite(battery.DesignCapacitymAh) && battery.DesignCapacitymAh > 0 ? battery.DesignCapacitymAh : null;
  const full = batteryPresent && Number.isFinite(battery.FullChargeCapacitymAh) && battery.FullChargeCapacitymAh > 0 ? battery.FullChargeCapacitymAh : null;
  const batteryHealth = design !== null && full !== null ? full / design * 100 : null;
  $('battery-charge').textContent = charge === null ? 'N/A' : charge + '%';
  $('battery-charging').textContent = charging;
  $('battery-condition').textContent = batteryPresent ? fmt(battery.Condition, 'Unknown') : 'N/A';
  $('battery-cycles').textContent = batteryPresent && Number.isInteger(battery.CycleCount) && battery.CycleCount >= 0 ? String(battery.CycleCount) : 'N/A';
  $('battery-fill').style.width = charge === null ? '0%' : pct(charge) + '%';
  $('battery-charge-label').textContent = charge === null ? 'N/A' : charge + '%';
  const healthLabel = batteryHealth === null ? 'N/A' : batteryHealth.toFixed(1) + '%';
  $('battery-health-label').textContent = healthLabel;
  $('battery-health-fill').style.width = batteryHealth === null ? '0%' : pct(batteryHealth) + '%';
  $('battery-health-progress').setAttribute('aria-valuenow', batteryHealth === null ? '0' : String(pct(batteryHealth)));
  $('battery-health-progress').setAttribute('aria-valuetext', batteryHealth === null ? 'Estimated battery health unavailable' : healthLabel + ' estimated battery health');
  $('battery-progress').setAttribute('aria-valuenow', charge === null ? '0' : String(charge));
  $('battery-progress').setAttribute('aria-valuetext', charge === null ? 'Battery charge unavailable' : charge + '% charged');
  $('battery-status-note').textContent = batteryPresent ? 'Battery detected.' : fmt(battery.Notes, 'No battery detected or battery data unavailable.');
  facts('battery-facts', [
    ['Design capacity', design === null ? 'N/A' : design + ' mAh'],
    ['Full-charge capacity', full === null ? 'N/A' : full + ' mAh'],
    ['Estimated battery health', batteryHealth === null ? 'N/A' : batteryHealth.toFixed(1) + '%'],
    ['Data source', fmt(battery.Source, 'Unknown')]
  ]);
  facts('memory-facts',[['Installed physical RAM',bytes(sys.RamBytes)],['Live measurement','macOS memory_pressure free percentage'],['Memory health',fmt((health.Items??[]).find(i=>i.Name==='RAM')?.Message)]]);
  facts('cpu-facts',[['Processor',sys.CpuModel],['Logical CPU count',sys.CpuCores],['Architecture',sys.Architecture]]);
  facts('system-facts',[['Operating system',sys.OsDescription],['Processor',sys.CpuModel],['CPU cores',sys.CpuCores],['Memory',bytes(sys.RamBytes)],['Architecture',sys.Architecture],['.NET version',sys.DotNetVersion]]);
  const items=health.Items??[], find=n=>items.find(i=>i.Name===n);
  const cpu=find('CPU'), ram=find('RAM'), storage=find('Disk'), net=find('Network'), batt=find('Battery');
  const physical=(report.Storage?.Disks??[])[0];
  $('metrics').innerHTML=[
    card('CPU','▣',cpu?.Score!==undefined?cpu.Score+'/100':'Unknown',cpu?.Message??'No measurement',cpu?.Score??0),
    card('RAM','▤',ram?.Score!==undefined?ram.Score+'/100':'Unknown',ram?.Message??'No measurement',ram?.Score??0),
    card('Storage','▥',freePct===null?'Unknown':freePct.toFixed(1)+'% free',bytes(free)+' available · '+bytes(total)+' total',freePct??0,low?'bad':'good'),
    card('Battery','▰',battery.Percentage===null||battery.Percentage===undefined?'Unknown':battery.Percentage+'%',fmt(battery.Condition)+' · '+fmt(battery.CycleCount)+' cycles',battery.Percentage??0,'good'),
    card('Network','◉',levelName(net?.Level)==='Green'?'Connected':levelName(net?.Level),net?.Message??'No measurement',net?.Score??0,levelName(net?.Level)==='Green'?'good':'')
  ].join('');
  $('health-bars').innerHTML=items.map(i=>'<div class="health-row"><span>'+safe(i.Name)+'</span><div class="track"><div class="fill '+(levelName(i.Level)==='Red'?'bad':levelName(i.Level)==='Green'?'good':'')+'" style="width:'+(levelName(i.Level)==='Unknown'?0:pct(i.Score))+'%"></div></div><span class="score">'+(levelName(i.Level)==='Unknown'?'N/A':safe(i.Score)+'/100')+'</span></div>').join('');
  facts('detail-facts',[['Wireless adapter',network.PrimaryAdapterName],['Adapter type',network.PrimaryAdapterType],['IPv4 address',network.IPv4],['Gateway',network.Gateway],['Internet ping',network.Pings?.InternetPing],['Wi-Fi SSID',network.Wifi?.Ssid],['SSD model',physical?.Name??physical?.Model],['SSD TRIM',physical?.TrimSupport],['Battery cycles',battery.CycleCount]]);
  $('timestamp').textContent='Scan recorded: '+fmt(report.GeneratedAt);
  $('report-file').textContent='Source: '+fmt(file);
  $('download').disabled=false;
}
async function load() {
  $('refresh').disabled=true;
  try {
    const response=await fetch('/api/report/latest',{cache:'no-store'});
    const data=await response.json();
    if(!response.ok)throw new Error(data.error??data.detail??'Unable to load report');
    render(data.report,data.file);
    $('error').classList.add('hidden');
  } catch(error) {
    $('error').textContent=error.message+' Run the diagnostic CLI from the repository root, then reload this page.';
    $('error').classList.remove('hidden');
  } finally { $('refresh').disabled=false; }
}
$('refresh').addEventListener('click',load);
$('download').addEventListener('click',()=>{
  if(!latestReport)return;
  const blob=new Blob([JSON.stringify(latestReport,null,2)],{type:'application/json'});
  const url=URL.createObjectURL(blob);
  const a=document.createElement('a');a.href=url;a.download='pc_diagnostic_report.json';a.click();
  setTimeout(()=>URL.revokeObjectURL(url),1000);
});
load();
const liveSeries = { cpu: [], memory: [] };
let liveBusy = false;
function appendSample(series, value) {
  series.push(Number.isFinite(value) ? pct(value) : null);
  if (series.length > 30) series.shift();
}
function drawSeries(id, series) {
  // Gaps are preserved when a measurement is unavailable.
  const paths = [];
  let segment = [];
  series.forEach((value, i) => {
    if (value === null) {
      if (segment.length) paths.push(segment);
      segment = [];
    } else {
      const x = series.length === 1 ? 500 : i * 500 / (series.length - 1);
      segment.push(x.toFixed(1) + ',' + (140 - value * 1.4).toFixed(1));
    }
  });
  if (segment.length) paths.push(segment);
  const svg = $(id);
  // Multiple polylines allow missing samples to be shown as gaps.
  svg.replaceChildren(...paths.map(points => {
    const line = document.createElementNS('http://www.w3.org/2000/svg', 'polyline');
    line.setAttribute('points', points.join(' '));
    line.setAttribute('fill', 'none');
    line.setAttribute('stroke', id === 'cpu-line' ? '#ae86ff' : '#43dba1');
    line.setAttribute('stroke-width', '3');
    line.setAttribute('stroke-linejoin', 'round');
    line.setAttribute('stroke-linecap', 'round');
    return line;
  }));
}
async function sampleLive() {
  if (liveBusy || document.hidden) return;
  liveBusy = true;
  try {
    const response = await fetch('/api/live', { cache: 'no-store' });
    if (!response.ok) throw new Error('HTTP ' + response.status);
    const data = await response.json();
    if (!data.supported) {
      $('live-status').textContent = data.reason ?? 'Live monitoring unavailable';
      return;
    }
    appendSample(liveSeries.cpu, data.cpuUsagePercent);
    const validCpu = liveSeries.cpu.filter(Number.isFinite);
    $('cpu-current').textContent = Number.isFinite(data.cpuUsagePercent) ? data.cpuUsagePercent.toFixed(1) + '%' : 'N/A';
    $('cpu-average').textContent = validCpu.length ? (validCpu.reduce((sum, n) => sum + n, 0) / validCpu.length).toFixed(1) + '%' : 'N/A';
    $('cpu-peak').textContent = validCpu.length ? Math.max(...validCpu).toFixed(1) + '%' : 'N/A';
    $('cpu-count').textContent = String(validCpu.length);
    appendSample(liveSeries.memory, data.macMemoryPressureFreePercent);
    const validMemory = liveSeries.memory.filter(Number.isFinite);
    $('memory-current').textContent = Number.isFinite(data.macMemoryPressureFreePercent) ? data.macMemoryPressureFreePercent.toFixed(0) + '%' : 'N/A';
    $('memory-average').textContent = validMemory.length ? (validMemory.reduce((sum, n) => sum + n, 0) / validMemory.length).toFixed(1) + '%' : 'N/A';
    $('memory-lowest').textContent = validMemory.length ? Math.min(...validMemory).toFixed(0) + '%' : 'N/A';
    $('memory-count').textContent = String(validMemory.length);
    $('live-cpu').textContent = Number.isFinite(data.cpuUsagePercent) ? data.cpuUsagePercent.toFixed(1) + '%' : 'N/A';
    $('live-memory').textContent = Number.isFinite(data.macMemoryPressureFreePercent) ? data.macMemoryPressureFreePercent + '%' : 'N/A';
    drawSeries('cpu-line', liveSeries.cpu);
    drawSeries('memory-line', liveSeries.memory);
    $('live-status').textContent = 'Live · last updated ' + new Date(data.sampledAtUtc).toLocaleTimeString();
  } catch (error) {
    $('live-status').textContent = 'Live data unavailable (' + error.message + ')';
  } finally {
    liveBusy = false;
  }
}
sampleLive();
setInterval(sampleLive, 10000);
document.addEventListener('visibilitychange', () => { if (!document.hidden) sampleLive(); });

let scanBusy = false;
async function runFullScan() {
  if (scanBusy || document.hidden) return;
  scanBusy = true;
  $('scan-now').disabled = true;
  $('scan-status').textContent = 'Running a full diagnostic scan…';
  try {
    const response = await fetch('/api/scan', { method: 'POST', cache: 'no-store' });
    const data = await response.json();
    if (!response.ok) throw new Error(data.error ?? data.detail ?? 'Scan failed');
    await load();
    $('scan-status').textContent = 'Full scan complete · ' + new Date().toLocaleTimeString();
  } catch (error) {
    $('scan-status').textContent = 'Full scan error: ' + error.message;
  } finally {
    scanBusy = false;
    $('scan-now').disabled = false;
  }
}
$('scan-now').addEventListener('click', runFullScan);
// Do not scan while the tab is hidden. Full scans write local reports.
setInterval(() => { if (!document.hidden) runFullScan(); }, 5 * 60 * 1000);

let processData = [];
let processBusy = false;
function renderProcesses() {
  const sort = $('process-sort').value;
  const rows = [...processData].sort((a,b) => sort === 'cpu' ? b.cpuPercent - a.cpuPercent || b.memoryBytes - a.memoryBytes : b.memoryBytes - a.memoryBytes || b.cpuPercent - a.cpuPercent).slice(0,15);
  const tbody = $('process-rows');
  tbody.replaceChildren();
  if (!rows.length) {
    const tr = document.createElement('tr'), td = document.createElement('td');
    td.colSpan = 4; td.textContent = 'No accessible process samples available.'; tr.append(td); tbody.append(tr); return;
  }
  for (const p of rows) {
    const tr = document.createElement('tr');
    for (const value of [p.name, p.pid, Number.isFinite(p.cpuPercent) ? p.cpuPercent.toFixed(1)+'%' : 'N/A', Number.isFinite(p.memoryBytes) ? (p.memoryBytes / (1024*1024)).toFixed(1)+' MiB' : 'N/A']) {
      const td = document.createElement('td'); td.textContent = String(value); tr.append(td);
    }
    tbody.append(tr);
  }
}
async function sampleProcesses() {
  if (processBusy || document.hidden) return;
  processBusy = true;
  try {
    const response = await fetch('/api/processes', {cache:'no-store'});
    if (!response.ok) throw new Error('HTTP ' + response.status);
    const data = await response.json();
    processData = Array.isArray(data.processes) ? data.processes : [];
    renderProcesses();
    $('process-status').textContent = 'Updated ' + new Date(data.sampledAtUtc).toLocaleTimeString();
  } catch (error) {
    $('process-status').textContent = 'Process data unavailable (' + error.message + ')';
  } finally { processBusy = false; }
}
$('process-sort').addEventListener('change', renderProcesses);
sampleProcesses();
setInterval(sampleProcesses, 10000);
document.addEventListener('visibilitychange', () => { if (!document.hidden) sampleProcesses(); });
