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
    appendSample(liveSeries.memory, data.macMemoryPressureFreePercent);
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
