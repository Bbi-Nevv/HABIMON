// Rebuild only Parent/HabitLogPanel. Run from the project root with Node.js.
const fs = require('fs');
const path = 'Assets/_Scenes/Parent.unity';
const source = fs.readFileSync(path, 'utf8').replace(/^\uFEFF/, '').replace(/\r\n/g, '\n');
const matches = [...source.matchAll(/^--- !u!(\d+) &(-?\d+)[^\n]*\n([\s\S]*?)(?=^--- !u!|$(?![\s\S]))/gm)];
const docs = new Map(matches.map(m => [m[2], m[0]]));
const original = new Map(docs);
const must = id => { if (!docs.has(String(id))) throw Error('Missing scene object ' + id); return docs.get(String(id)); };
const set = (text, field, value) => text.replace(new RegExp('^  ' + field + ':.*$', 'm'), '  ' + field + ': ' + value);
const ref = id => `{fileID: ${id}}`;
const rounded = '{fileID: 21300000, guid: 4fbf65d3c6fb430882b98a766e1de551, type: 3}';
const asset = (fileID, guid) => `{fileID: ${fileID}, guid: ${guid}, type: 3}`;
const rgb = hex => {
  const color = hex.replace('#', '');
  return `{r: ${parseInt(color.slice(0,2),16)/255}, g: ${parseInt(color.slice(2,4),16)/255}, b: ${parseInt(color.slice(4,6),16)/255}, a: ${color.length === 8 ? parseInt(color.slice(6,8),16)/255 : 1}}`;
};
const common = go => `  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: ${ref(go)}\n`;
function componentOf(go, signature) {
  const ids = [...must(go).matchAll(/component: \{fileID: (\d+)\}/g)].map(m => m[1]);
  return ids.find(id => must(id).includes(signature));
}
function goOf(transform) { return must(transform).match(/m_GameObject: \{fileID: (\d+)\}/)[1]; }
function childIds(transform) { return [...(must(transform).match(/m_Children:\n([\s\S]*?)  m_Father/)?.[1] || '').matchAll(/fileID: (\d+)/g)].map(m => m[1]); }
const buttonTemplate = must('1785463330');
const imageTemplate = must('1785463331');
const textTemplate = must(componentOf(goOf('1733978684'), 'TMPro.TextMeshProUGUI'));
const scrollTemplate = must('405831200');
if (!scrollTemplate.includes('ScrollRect')) throw Error('ScrollRect template changed');
const oldChildren = childIds('1731265594');
const removed = new Set();
function removeSubtree(id) {
  if (id === '1399576742') return;
  for (const child of childIds(id)) removeSubtree(child);
  const go = goOf(id);
  for (const m of must(go).matchAll(/component: \{fileID: (\d+)\}/g)) removed.add(m[1]);
  removed.add(go);
}
for (const id of oldChildren) removeSubtree(id);
for (const id of removed) docs.delete(id);
let serial = 2151000000;
const nodes = [];
const alloc = () => String(++serial);
function node(name, parent, x, y, w, h, options={}) {
  const item = {go:alloc(), rect:alloc(), name, parent, x, y, w, h, children:[], comps:[], options};
  if (parent && parent.children) parent.children.push(item.rect);
  nodes.push(item);
  return item;
}
function cloneComponent(template, item, changes={}) {
  const id = alloc();
  let text = template.replace(/^--- !u!(\d+) &\d+/, `--- !u!$1 &${id}`);
  text = set(text, 'm_GameObject', ref(item.go));
  for (const [key,value] of Object.entries(changes)) text = set(text,key,value);
  docs.set(id,text);
  item.comps.push(id);
  return id;
}
function canvasRenderer(item) {
  const id=alloc();item.comps.push(id);
  docs.set(id,`--- !u!222 &${id}\nCanvasRenderer:\n${common(item.go)}  m_CullTransparentMesh: 1\n`);
}
function image(item, color, sprite=rounded, type=1, raycast=0) {
  canvasRenderer(item);
  const id=cloneComponent(imageTemplate,item,{m_Color:rgb(color),m_Sprite:sprite,m_Type:type,m_RaycastTarget:raycast,m_PreserveAspect:0,m_PixelsPerUnitMultiplier:0.25});
  item.image=id;
  return item;
}
function box(name,parent,x,y,w,h,color,sprite=rounded,type=1) { return image(node(name,parent,x,y,w,h),color,sprite,type); }
function text(name,parent,x,y,w,h,value,size=26,color='2F5449',bold=false,align=1) {
  const item=node(name,parent,x,y,w,h);canvasRenderer(item);
  item.text=cloneComponent(textTemplate,item,{m_text:JSON.stringify(value),m_fontSize:size,m_fontSizeBase:size,m_fontColor:rgb(color),m_fontStyle:bold?1:0,m_HorizontalAlignment:align,m_VerticalAlignment:512,m_RaycastTarget:0,m_enableAutoSizing:0,m_TextWrappingMode:0,m_overflowMode:0,m_isRichText:1});
  const packed = (0xff000000 | parseInt(color.slice(4,6),16)<<16 | parseInt(color.slice(2,4),16)<<8 | parseInt(color.slice(0,2),16)) >>> 0;
  docs.set(item.text,must(item.text).replace(/  m_fontColor32:\n[\s\S]*?  m_fontColor:/,`  m_fontColor32:\n    serializedVersion: 2\n    rgba: ${packed}\n  m_fontColor:`));
  return item;
}
function button(name,parent,x,y,w,h,label,color='EDF5E4',sourceButton=null) {
  const item=box(name,parent,x,y,w,h,color);
  // Buttons need a raycast surface; decorative images remain non-interactive.
  docs.set(item.image,set(must(item.image),'m_RaycastTarget',1));
  const template=sourceButton?must(sourceButton):buttonTemplate;
  item.button=cloneComponent(template,item,{m_TargetGraphic:ref(item.image),m_Transition:1});
  let data=must(item.button).replace(/  m_Colors:\n[\s\S]*?  m_SpriteState:/, `  m_Colors:\n    m_NormalColor: {r: 1, g: 1, b: 1, a: 1}\n    m_HighlightedColor: {r: 0.96, g: 1, b: 0.94, a: 1}\n    m_PressedColor: {r: 0.84, g: 0.94, b: 0.83, a: 1}\n    m_SelectedColor: {r: 1, g: 1, b: 1, a: 1}\n    m_DisabledColor: {r: 1, g: 1, b: 1, a: 0.5}\n    m_ColorMultiplier: 1\n    m_FadeDuration: 0.12\n  m_SpriteState:`);
  data=data.replace(/  m_Navigation:\n[\s\S]*?  m_Transition:/,'  m_Navigation:\n    m_Mode: 0\n    m_WrapAround: 0\n    m_SelectOnUp: {fileID: 0}\n    m_SelectOnDown: {fileID: 0}\n    m_SelectOnLeft: {fileID: 0}\n    m_SelectOnRight: {fileID: 0}\n  m_Transition:');
  if (!sourceButton) data=data.replace(/  m_OnClick:\n[\s\S]*/, '  m_OnClick:\n    m_PersistentCalls:\n      m_Calls: []\n');
  docs.set(item.button,data);
  if(label)text('Label',item,0,0,w,h,label,26,'2F6A53',true,2);
  return item;
}
function card(name,parent,x,y,w,h,fill='FFFCF2',border='D6DFC2') {
  const item=node(name,parent,x,y,w,h);
  box('SoftShadow',item,0,7,w,h,'31513C16');
  box('Border',item,0,0,w,h,border);
  box('Surface',item,2,2,w-4,h-4,fill);
  return item;
}
function mask(item) {
  const id=alloc();item.comps.push(id);
  docs.set(id,`--- !u!114 &${id}\nMonoBehaviour:\n${common(item.go)}  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: 3312d7739989d2b4e91e6319e9a96d76, type: 3}\n  m_Name: \n  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.RectMask2D\n  m_Padding: {x: 0, y: 0, z: 0, w: 0}\n  m_Softness: {x: 0, y: 0}\n`);
}
function scroll(name,parent,x,y,w,h,horizontal) {
  const item=box(name,parent,x,y,w,h,'FFFFFF00','{fileID: 0}',0);docs.set(item.image,set(must(item.image),'m_RaycastTarget',1));
  mask(item);
  const content=node('Content',item,0,0,w,h);
  item.scroll=cloneComponent(scrollTemplate,item,{m_Content:ref(content.rect),m_Viewport:ref(item.rect),m_Horizontal:horizontal?1:0,m_Vertical:horizontal?0:1,m_MovementType:2,m_ScrollSensitivity:36,m_HorizontalScrollbar:'{fileID: 0}',m_VerticalScrollbar:'{fileID: 0}',m_HorizontalScrollbarVisibility:0,m_VerticalScrollbarVisibility:0});
  return {viewport:item,content};
}
const panel={rect:'1731265594',children:[]};
const frame=node('GardenReport_1080x1920',panel,0,0,1080,1920,{center:true});
const header=node('Header',frame,60,126,960,104);
text('Eyebrow',header,0,0,800,28,'CÃ™NG CON Lá»šN KHÃ”N',21,'557F65',true);
text('Title',header,0,34,820,68,'BÃ¡o cÃ¡o thÃ³i quen',52,'264D40',true);
const calendar=asset('7122867478828336978','b794b30c467840e4bad20288ae91ab26');
box('Calendar',header,846,10,104,104,'FFFFFF',calendar,0);
const hero=card('GardenEncouragement',frame,52,262,976,190,'EFF8DD','D3E4BB');
text('HeroTitle',hero,30,22,694,100,'Gieo thÃ³i quen nhá»,\nnuÃ´i nhá»¯ng Æ°á»›c mÆ¡.',39,'315D46',true);
text('HeroCaption',hero,32,128,690,36,'Má»—i ngÃ y má»™t chÃºt, con tiáº¿n bá»™ hÆ¡n.',25,'63836B');
const mascot=box('HabiMascot',hero,732,-20,220,220,'FFFFFF',asset('7888141139028744870','3471e2b8e538f8341ac75902f083c763'),0);
const week=card('WeekNavigator',frame,52,478,976,100);
const previous=button('PreviousWeek',week,16,14,72,72,'â€¹');
const next=button('NextWeek',week,888,14,72,72,'â€º');
text('WeekLabel',week,106,12,574,24,'TUáº¦N ÄANG XEM',18,'7F907C',true,2);
const period=text('Period',week,106,39,574,46,'19/10 â€“ 25/10/2026',31,'315D46',true,2);
const today=button('CurrentWeek',week,702,24,162,52,'Tuáº§n nÃ y','E8F3D7');
const stats=node('WeeklySummary',frame,52,604,976,146);
const stat1=card('Completed',stats,0,0,309,146);
const stat2=card('CompletionRate',stats,333,0,309,146,'EAF5DF','D3E4BB');
const stat3=card('Children',stats,667,0,309,146,'EFF6ED','D7E4D0');
const summary=text('CompletedValue',stat1,24,16,265,62,'â€” / â€”',44,'315D46',true);
text('CompletedCaption',stat1,24,91,265,32,'Nhiá»‡m vá»¥ Ä‘Ã£ xong',23,'72836C');
const rate=text('RateValue',stat2,24,16,265,62,'â€”',44,'3C7C54',true);
text('RateCaption',stat2,24,91,265,32,'Tá»· lá»‡ cáº£ tuáº§n',23,'72836C');
box('ProgressTrack',stat2,24,80,257,6,'D5E7C3');
const progress=box('ProgressFill',stat2,24,80,257,6,'72A85C');
docs.set(progress.image,set(set(set(must(progress.image),'m_Type',3),'m_FillMethod',0),'m_FillAmount',0));
const childCount=text('ChildrenValue',stat3,24,16,265,62,'â€”',44,'47746B',true);
text('ChildrenCaption',stat3,24,91,265,32,'BÃ© Ä‘ang theo dÃµi',23,'72836C');
const chart=card('WeeklyChartCard',frame,52,776,976,496);
text('ChartTitle',chart,30,22,740,48,'Nhá»‹p Ä‘iá»‡u trong tuáº§n',34,'315D46',true);
text('ChartHint',chart,32,74,888,32,'Cháº¡m vÃ o ngÃ y Ä‘á»ƒ xem thá»i gian hoÃ n thÃ nh',23,'7D8E78');
const chartArea=scroll('ChartViewport',chart,28,120,920,350,true);
const status=text('ChartStatus',chart,100,225,776,90,'Äang táº£i dá»¯ liá»‡u tuáº§n...',27,'6D8773',false,2);
const retry=button('Retry',chart,388,326,200,58,'Thá»­ láº¡i');
retry.options.inactive=true;
const details=card('SelectedDayCard',frame,52,1298,976,312);
text('DetailTitle',details,30,18,450,44,'Má»™t ngÃ y cá»§a con',32,'315D46',true);
const selected=text('SelectedDate',details,30,62,500,32,'CN Â· 25/10/2026',23,'7D8E78');
const detailCount=text('DetailCount',details,650,26,294,40,'Äang Ä‘á»“ng bá»™',23,'63836B',true,4);
const detailArea=scroll('DetailViewport',details,30,108,916,158,false);
text('ScrollHint',details,32,279,912,24,'Vuá»‘t Ä‘á»ƒ xem thÃªm',19,'8F9D87',false,2);
// Keep the controller with the panel so hidden reports do not retain live listeners.
panel.children.push('1399576742');
docs.set('1399576742',set(set(set(must('1399576742'),'m_Father',ref(panel.rect)),'m_LocalPosition','{x: 0, y: 0, z: 0}'),'m_LocalScale','{x: 1, y: 1, z: 1}'));
docs.set('682633265',must('682633265').replace('  - {fileID: 1399576742}\n',''));
let root=must(panel.rect).replace(/  m_Children:\n[\s\S]*?  m_Father:/,'  m_Children:\n'+panel.children.map(id=>'  - '+ref(id)+'\n').join('')+'  m_Father:');
docs.set(panel.rect,root);
let backdrop=must('1731265592');
for(const [field,value] of Object.entries({m_Color:rgb('FFFFFF'),m_Sprite:asset('21300000','71201edab1080aa4b9384bbb1ad2cb63'),m_Type:0}))backdrop=set(backdrop,field,value);
docs.set('1731265592',backdrop);
const controllerFields={summaryText:summary.text,detailText:0,periodLabelText:period.text,previousButton:previous.button,nextButton:next.button,todayButton:today.button,retryButton:retry.button,completionRateText:rate.text,childrenCountText:childCount.text,selectedDayText:selected.text,detailCountText:detailCount.text,statusText:status.text,weeklyProgressFill:progress.image,detailContentRoot:detailArea.content.rect};
let controller=must('1399576743').split('  summaryText:')[0];
for(const [field,id] of Object.entries(controllerFields))controller+=`  ${field}: ${ref(id)}\n`;
controller+=`  roundedSprite: ${rounded}\n  chartRoot: ${ref(chartArea.content.rect)}\n  chartBarPrefab: {fileID: 0}\n  animationDuration: 0.45\n  animationEase: 9\n  chartTextColor: ${rgb('597063')}\n  selectedDayColor: ${rgb('8AB56812')}\n  dayFilterButton: {fileID: 0}\n  monthFilterButton: {fileID: 0}\n  yearFilterButton: {fileID: 0}\n`;
docs.set('1399576743',controller);
// Parent's existing Taskbar remains responsible for navigation.
const motionId=alloc();
docs.set(motionId,`--- !u!114 &${motionId}\nMonoBehaviour:\n${common('1731265591')}  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: 8235244f28d249e4b99c936d13fd9b42, type: 3}\n  m_Name: \n  m_EditorClassIdentifier: Assembly-CSharp::HabitLogPanelMotion\n  layoutRoot: ${ref(frame.rect)}\n  animatedCards:\n${[header,hero,week,stats,chart,details].map(n=>'  - '+ref(n.rect)).join('\n')}\n  mascot: ${ref(mascot.rect)}\n  respectSafeArea: 1\n`);
let panelGo=must('1731265591');
// A second run replaces the previous motion component rather than accumulating one.
for(const m of [...panelGo.matchAll(/  - component: \{fileID: (\d+)\}\n/g)])if(original.get(m[1])?.includes('HabitLogPanelMotion')){panelGo=panelGo.replace(m[0],'');if(m[1]!==motionId)docs.delete(m[1]);}
panelGo=panelGo.replace(/  - component: \{fileID: 1731265592\}/,'  - component: {fileID: 1731265592}\n  - component: '+ref(motionId));
docs.set('1731265591',panelGo);
for(const item of nodes) {
  const components=[item.rect,...item.comps];
  docs.set(item.go,`--- !u!1 &${item.go}\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n${components.map(id=>'  - component: '+ref(id)).join('\n')}\n  m_Layer: 5\n  m_Name: ${item.name}\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: ${item.options.inactive?0:1}\n`);
  const anchor=item.options.center?'{x: 0.5, y: 0.5}':'{x: 0, y: 1}';
  docs.set(item.rect,`--- !u!224 &${item.rect}\nRectTransform:\n${common(item.go)}  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children:${item.children.length?'\n'+item.children.map(id=>'  - '+ref(id)).join('\n'):' []'}\n  m_Father: ${ref(item.parent.rect)}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n  m_AnchorMin: ${anchor}\n  m_AnchorMax: ${anchor}\n  m_AnchoredPosition: {x: ${item.x}, y: ${-item.y}}\n  m_SizeDelta: {x: ${item.w}, y: ${item.h}}\n  m_Pivot: ${anchor}\n`);
}
// Check references before touching the scene. Asset and built-in references carry a GUID.
for(const [id,data] of docs)for(const m of data.matchAll(/\{fileID: (-?\d+)\}/g))if(m[1]!=='0'&&!docs.has(m[1]))throw Error(`Dangling local reference ${m[1]} in ${id}`);
const headerYaml=source.slice(0,source.indexOf('--- !u!'));
const output=headerYaml+[...docs.values()].join('');
fs.mkdirSync('Temp/HabitLogGarden',{recursive:true});
if(!fs.existsSync('Temp/HabitLogGarden/Parent.before.unity'))fs.writeFileSync('Temp/HabitLogGarden/Parent.before.unity',source);
fs.writeFileSync(path,output,'utf8');
fs.writeFileSync('Temp/HabitLogGarden/scene-map.json',JSON.stringify({panel:panel.rect,frame:frame.rect,chart:chartArea.content.rect,details:detailArea.content.rect,controller:'1399576743',motion:motionId},null,2));
console.log(`Built garden report: ${nodes.length} UI objects; ${removed.size} obsolete scene records removed; all local references resolved.`);
