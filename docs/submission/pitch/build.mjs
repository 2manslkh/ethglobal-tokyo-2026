import fs from 'node:fs/promises';
import path from 'node:path';
import {pathToFileURL} from 'node:url';

const root = process.env.TAGTAG_ROOT || process.cwd();
const runtime = process.env.RUNTIME_NODE_MODULES;
const skill = process.env.SKILL_DIR;
const work = process.env.PITCH_BUILD_DIR || '/private/tmp/tagtag-pitch-build';
if (!runtime || !skill) throw new Error('Set RUNTIME_NODE_MODULES and SKILL_DIR.');
const {FontLibrary} = await import(pathToFileURL(path.join(runtime, '@oai/artifact-tool/node_modules/skia-canvas/lib/index.js')));
const fonts = path.join(root,'Assets/Resources/Tagtag/Fonts');
FontLibrary.use('Shadows Into Light', [path.join(fonts,'ShadowsIntoLight.ttf')]);
FontLibrary.use('Instrument Sans', [path.join(fonts,'InstrumentRegular.ttf'),path.join(fonts,'InstrumentSemibold.ttf')]);
const {Presentation,PresentationFile} = await import(pathToFileURL(path.join(runtime,'@oai/artifact-tool/dist/artifact_tool.mjs')));
const {finalizePresentation} = await import(pathToFileURL(path.join(skill,'container_tools/artifact_tool_utils.mjs')));
await fs.mkdir(path.join(work,'output'),{recursive:true});
await fs.mkdir(path.join(work,'previews'),{recursive:true});
const p=Presentation.create({slideSize:{width:1280,height:720}});
const C={paper:'#FFFEFA',warm:'#F5F3EC',ink:'#262623',muted:'#62625B',yellow:'#FFE54D'};
const notes=(await fs.readFile(path.join(root,'docs/submission/pitch/VIDEO_SCRIPT.md'),'utf8')).split(/^## (?=\d)/m).slice(1,10);
const slides=[];
function slide(name,warm=false){const s=p.slides.add();s.background.fill=warm?C.warm:C.paper;slides.push(s);s.speakerNotes.textFrame.setText(notes[slides.length-1]||'');return s;}
function text(s,value,x,y,w,h,size=32,display=false,color=C.ink){const n=s.shapes.add({geometry:'textbox',name:value.slice(0,55),position:{left:x,top:y,width:w,height:h},fill:'none',line:{fill:'none',width:0}});n.text=value;n.text.style={typeface:display?'Shadows Into Light':'Instrument Sans',fontSize:size,color,autoFit:'none',wrap:'word',insets:{left:0,right:0,top:0,bottom:0}};return n;}
async function image(s,file,x,y,w,h){s.images.add({blob:new Uint8Array(await fs.readFile(path.join(root,file))),contentType:'image/png',alt:path.basename(file),fit:'contain',position:{left:x,top:y,width:w,height:h}});}
function folio(s,n){text(s,String(n).padStart(2,'0'),1190,662,45,28,18,false,C.muted);}
const A='Assets/Resources/Tagtag/';
let s=slide('tagtag');
text(s,'tagtag',86,100,620,208,158,true);
text(s,'An AR collecting game\nfor communities',92,342,660,115,42);
text(s,'Find your places. Collect your moments.',94,530,670,58,35,true);
await image(s,A+'Presets/taggi-1.png',780,126,410,460);
text(s,'ETHGlobal Tokyo 2026',94,650,450,30,20,false,C.muted);folio(s,1);

s=slide('Two inspirations',true);
text(s,'Two inspirations',80,52,1060,94,68,true);
text(s,'POAP',85,197,500,80,58,true);
text(s,'Memories worth keeping',85,285,530,52,34);
text(s,'~7.6 million',85,378,520,76,54);
text(s,'badges minted',87,459,430,38,26,false,C.muted);
text(s,'46,210+ issuers',85,521,550,48,34);
text(s,'Pokémon GO',705,197,500,80,58,true);
text(s,'Discovery worth\ngoing out for',705,285,450,110,36);
await image(s,A+'Presets/taggi-6.png',929,420,233,233);
text(s,'Historical totals reported August 2026',85,631,770,32,19,false,C.muted);
s.speakerNotes.textFrame.setText(notes[1]+'\nSources: https://poap.xyz/ and https://thecoinomist.com/news/poap-to-shut-down-after-minting-7-6m-event-badges/ (3 August 2026). 46,210 counts issuers, not distinct event designs. Pokémon GO: https://pokemongo.com/en. These products inspired tagtag; no affiliation is claimed.');folio(s,2);

s=slide('A place has a story');
await image(s,'docs/submission/screenshots/05-confirm-spot-simulator.png',84,56,290,592);
text(s,'A place has\na story',483,124,700,210,82,true);
text(s,'Leave a little discovery\nfor the next person.',488,405,690,116,42);
text(s,'App preview from iOS Simulator',81,669,600,26,18,false,C.muted);folio(s,3);

s=slide('The collecting loop',true);
text(s,'The collecting loop',80,48,1100,90,68,true);
await image(s,A+'Presets/taggi-3.png',123,191,226,235);
await image(s,A+'Navigation/explore.png',524,181,235,245);
await image(s,A+'Navigation/stick-book.png',927,191,225,235);
text(s,'1. Leave a sticker',79,466,355,65,42,true);
text(s,'Art, a place, a private note.',81,552,330,74,27);
text(s,'2. Find its place',479,466,355,65,42,true);
text(s,'Follow the clue into AR.',481,552,330,74,27);
text(s,'3. Keep the memory',879,466,355,65,42,true);
text(s,'Collect a copy. Reveal the note.',881,552,324,74,27);folio(s,4);

s=slide('A game communities can make their own');
text(s,'A game communities\ncan make their own',80,83,770,180,65,true);
text(s,'A conference’s gathering places',84,331,730,60,34);
text(s,'A neighbourhood’s art walk',84,412,730,60,34);
text(s,'Shared discoveries give people\na reason to meet and return.',84,536,705,100,30);
await image(s,'docs/submission/screenshots/02-sticker-book-simulated.png',901,58,268,580);
text(s,'Proposed uses',84,662,500,28,18,false,C.muted);
text(s,'UI preview / sample data',901,654,275,34,17,false,C.muted);folio(s,5);

s=slide('DEMO',true);
text(s,'DEMO',425,202,750,204,150,true);
await image(s,A+'Navigation/stick.png',90,333,310,310);
text(s,'A little discovery, in the real world',443,448,760,65,35,true);folio(s,6);

s=slide('How it works');
text(s,'How it works',79,50,1100,90,68,true);
text(s,'Unity + ARKit',84,213,750,60,44,true);
text(s,'Place and recover stickers in AR',85,279,760,49,30);
text(s,'MapKit',84,374,750,60,44,true);
text(s,'Nearby discovery on Apple’s maps',85,440,760,49,30);
text(s,'Firebase + Cloud Run',84,535,760,60,44,true);
text(s,'Stickers, collections and private notes',85,600,760,49,30);
await image(s,A+'Presets/taggi-8.png',902,161,283,298);
text(s,'On chain souvenirs',879,514,337,55,28,true);
text(s,'Already implemented',879,581,332,73,23,false,C.muted);folio(s,7);

s=slide('The first community pilot',true);
text(s,'The first\ncommunity pilot',81,76,720,188,73,true);
text(s,'A small trail of meaningful places.\nOriginal stickers from the community.\nReal people trying the full journey.',86,342,760,167,34);
text(s,'Learn: do people finish, and return?',86,577,850,68,36,true);
await image(s,A+'Presets/taggi-5.png',930,290,270,300);folio(s,8);

s=slide('Find your places. Collect your moments.');
text(s,'Find your places.\nCollect your moments.',82,89,840,208,78,true);
text(s,'Let’s create the first\ncommunity trail together.',87,397,740,130,38);
await image(s,A+'Presets/taggi-9.png',918,333,291,314);
text(s,'tagtag',87,605,340,73,52,true);folio(s,9);

await fs.writeFile(path.join(work,'presentation.json'),JSON.stringify(p.toProto()));
const candidate=path.join(work,'candidate.pptx');
await (await PresentationFile.exportPptx(p)).save(candidate);
for(let i=0;i<slides.length;i++){
  const b=await p.export({slide:slides[i],format:'png',scale:1.5});
  await fs.writeFile(path.join(work,'previews',`slide-${String(i+1).padStart(2,'0')}.png`),new Uint8Array(await b.arrayBuffer()));
}
const result=await finalizePresentation({workspaceDir:work,candidatePath:candidate,finalPath:path.join(work,'output/tagtag-pitch.pptx'),pythonExecutable:process.env.RUNTIME_PYTHON,integrityValidatorPath:path.join(skill,'container_tools/inspect_presentation_package_integrity.py'),layoutValidatorPath:path.join(skill,'container_tools/inspect_presentation_layout_geometry.py'),layoutArgs:['--expected-slide-size-emu','12192000,6858000','--validate-heading-fit'],requiredNativeTableOwnerSlides:[],requiredNativeChartOwnerSlides:[],fontPolicy:{basis:'design',families:['Shadows Into Light','Instrument Sans']},verifyArtifactToolImport:true,receiptPath:path.join(work,'validation.json')});
console.log(JSON.stringify(result));
