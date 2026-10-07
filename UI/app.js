'use strict';
const $=id=>document.getElementById(id);
const bridge=window.chrome?.webview;
const send=(command,extra={})=>bridge?.postMessage({command,...extra});
function options(id,items,selected){const el=$(id);const signature=JSON.stringify(items);if(el.dataset.options!==signature){el.replaceChildren(...items.map(x=>new Option(x.label,x.value)));el.dataset.options=signature;if(!items.length)el.add(new Option('No devices available',-1));}el.value=String(selected);}
function render(s){
 options('process',s.processes,s.process);options('microphone',s.microphones,s.microphone);options('output',s.outputs,s.output);
 for(const [id,value,label] of [['programVolume',s.programVolume,'programValue'],['micVolume',s.micVolume,'micValue'],['masterVolume',s.masterVolume,'masterValue']]){if(document.activeElement!==$(id))$(id).value=value;$(label).value=`${value}%`;}
 $('toggle').textContent=s.programEnabled?'Deactivate':'Activate';$('toggle').setAttribute('aria-pressed',s.programEnabled);
 const muteButton=$('muteMic');
 if(muteButton.dataset.muted!==String(s.micMuted)){
  muteButton.innerHTML='<svg class="mic-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="9" y="2" width="6" height="12" rx="3"/><path d="M5 10v2a7 7 0 0 0 14 0v-2M12 19v3M8 22h8"/>'+(s.micMuted?'<path class="mic-slash" d="M3 3l18 18"/>':'')+'</svg><span>'+(s.micMuted?'Unmute':'Mute')+'</span>';
  muteButton.dataset.muted=String(s.micMuted);
 }
 muteButton.setAttribute('aria-pressed',s.micMuted);
 muteButton.setAttribute('aria-label',s.micMuted?'Unmute microphone':'Mute microphone');
 $('status').textContent=s.programEnabled?'Routing Active':'Routing Inactive';$('dot').classList.toggle('live',s.programEnabled);
 for(const el of document.querySelectorAll('button:not([data-local]),select,input'))el.disabled=s.busy;
 $('hotkeyValue').textContent=s.hotkey;
 $('hotkey').textContent=s.assigningHotkey?'Press any key…':'Assign Hotkey';
 $('hotkey').setAttribute('aria-pressed',Boolean(s.assigningHotkey));
 $('micHotkeyValue').textContent=s.micHotkey??'Not assigned';
 $('micHotkey').textContent=s.assigningMicHotkey?'Press any key…':'Assign Hotkey';
 $('micHotkey').setAttribute('aria-pressed',Boolean(s.assigningMicHotkey));
 renderSoundboard(s);
 updateSoundUpload(s.busy);
 previewPlaying=Boolean(s.soundPreviewPlaying);$('uploadPreviewPlay').textContent=previewPlaying?'■ Stop':'▶ Preview';
 syncPreviewPlayhead(s);
}
for(const id of ['process','microphone','output'])$(id).addEventListener('change',()=>send('select',{target:id,value:Number($(id).value)}));
for(const [id,target,label] of [['programVolume','program','programValue'],['micVolume','microphone','micValue'],['masterVolume','output','masterValue']]){ $(id).addEventListener('input',()=>$(label).value=`${$(id).value}%`);$(id).addEventListener('change',()=>send('volume',{target,value:Number($(id).value)})); }
for(const command of ['toggle','refresh','muteMic'])$(command).addEventListener('click',()=>send(command));
document.addEventListener('pointerdown',e=>{
 if(!e.target.closest('#hotkey,#micHotkey'))send('cancelHotkey');
},true);
$('hotkey').addEventListener('click',()=>send($('hotkey').getAttribute('aria-pressed')==='true'?'cancelHotkey':'assignHotkey'));
$('micHotkey').addEventListener('click',()=>send($('micHotkey').getAttribute('aria-pressed')==='true'?'cancelHotkey':'assignMicHotkey'));
let soundLibrarySignature='';
let selectedPadId=null;
function renderSoundboard(s){
 if(document.activeElement!==$('soundMaster'))$('soundMaster').value=s.soundVolume??100;
 $('soundMasterValue').value=`${s.soundVolume??100}%`;
 const clips=s.soundClips??[];
 const signature=JSON.stringify(clips);
 if(signature!==soundLibrarySignature){
  soundLibrarySignature=signature;
  const list=$('soundClips');list.replaceChildren();
  const grid=document.createElement('div');grid.className='pads-grid';list.append(grid);
  const padCount=Math.max(15,Math.ceil((Math.max(-1,...clips.map(c=>c.pad))+2)/3)*3);
  for(let pad=0;pad<Math.min(108,padCount);pad++){
   const clip=clips.find(c=>c.pad===pad);
   const card=document.createElement('div');card.className='pad-cell';if(clip)card.dataset.clip=clip.id;
   const play=document.createElement('button');play.className=clip?'sound-pad assigned':'sound-pad empty';
   if(clip)play.style.setProperty('--pad-color',clip.color);
   const symbol=document.createElement('span');symbol.className='pad-symbol';symbol.textContent=clip?'▶':'+';
   const title=document.createElement('span');title.className='pad-name';title.textContent=clip?clip.name:'Assign Sound';
   if(!clip)play.append(symbol);play.append(title);play.setAttribute('aria-label',clip?`Play ${clip.name}`:`Assign sound to pad ${pad+1}`);
   play.addEventListener('click',()=>clip?send('soundPlay',{id:clip.id}):openSoundUpload({pad},play));card.append(play);
   if(clip){const edit=document.createElement('button');edit.className='pad-edit';edit.textContent='⋯';edit.setAttribute('aria-label',`Edit pad ${pad+1}: ${clip.name}`);edit.addEventListener('click',()=>{selectedPadId=selectedPadId===clip.id?null:clip.id;soundLibrarySignature='';renderSoundboard(s);});card.append(edit);}
   grid.append(card);
  }
  const clip=clips.find(c=>c.id===selectedPadId);
  if(clip){
   const card=document.createElement('section');card.className='sound-clip pad-editor';
   const heading=document.createElement('h3');heading.textContent=`Pad ${String(clip.pad+1).padStart(2,'0')} Settings`;
   const name=document.createElement('input');name.type='text';name.value=clip.name;name.maxLength=60;name.setAttribute('aria-label','Sound name');name.addEventListener('change',()=>send('soundRename',{id:clip.id,name:name.value}));
   const row=document.createElement('div');row.className='sound-clip-actions';
   const replace=document.createElement('button');replace.className='secondary';replace.textContent='Change Sound';replace.addEventListener('click',()=>openSoundUpload({id:clip.id,name:clip.name,volume:clip.volume},replace));
   const remove=document.createElement('button');remove.className='secondary';remove.textContent='Remove';remove.setAttribute('aria-label',`Remove ${clip.name}`);remove.addEventListener('click',()=>{if(window.confirm(`Remove "${clip.name}" from your soundboard?`))send('soundRemove',{id:clip.id});});
   row.append(replace,remove);
   const label=document.createElement('label');label.textContent=`Clip volume: ${clip.volume}%`;
   const volume=document.createElement('input');volume.type='range';volume.min=0;volume.max=100;volume.value=clip.volume;volume.setAttribute('aria-label',`${clip.name} volume`);volume.addEventListener('input',()=>label.textContent=`Clip volume: ${volume.value}%`);volume.addEventListener('change',()=>send('soundVolume',{id:clip.id,value:Number(volume.value)}));
   card.append(heading,name,row,label,volume);list.append(card);
  }
 }
 for(const card of $('soundClips').querySelectorAll('[data-clip]'))card.classList.toggle('playing',(s.soundPlaying??[]).includes(card.dataset.clip));
}
function setSoundboardOpen(open){
 const panel=$('soundboardPanel');panel.inert=!open;panel.setAttribute('aria-hidden',!open);

 document.body.classList.toggle('soundboard-open',open);send('soundPanel',{open});
 $('soundboardOpen').setAttribute('aria-expanded',open);
 if(open){send('cancelHotkey');$('soundboardClose').focus();}else $('soundboardOpen').focus();
}
$('soundboardOpen').addEventListener('click',()=>setSoundboardOpen(!document.body.classList.contains('soundboard-open')));
$('soundboardClose').addEventListener('click',()=>setSoundboardOpen(false));
$('soundMaster').addEventListener('input',()=>$('soundMasterValue').value=`${$('soundMaster').value}%`);
$('soundMaster').addEventListener('change',()=>send('soundMaster',{value:Number($('soundMaster').value)}));
document.addEventListener('keydown',e=>{
 if($('soundUpload').open)return;
 if(!document.body.classList.contains('soundboard-open'))return;
 if(e.key==='Escape'){e.preventDefault();setSoundboardOpen(false);}
});
let uploadTarget=null,uploadHasFile=false,uploadBusy=false,uploadOpener=null;
let uploadDuration=0,previewPlaying=false;
let playheadPosition=-1,playheadEnd=0,playheadUpdated=0,playheadFrame=0;
function paintPreviewPlayhead(){
 playheadFrame=0;
 const visible=playheadPosition>=0&&uploadDuration>0&&$('soundUpload').open&&uploadHasFile;
 $('wavePlayhead').style.display=visible?'':'none';
 $('previewPosition').hidden=!visible;
 if(!visible)return;
 const position=Math.min(playheadEnd,playheadPosition+(previewPlaying?(performance.now()-playheadUpdated)/1000:0));
 const x=position/uploadDuration*400;
 $('wavePlayhead').setAttribute('x1',x);$('wavePlayhead').setAttribute('x2',x);
 $('previewPosition').value=`${position.toFixed(2)}s`;
 if(previewPlaying)playheadFrame=requestAnimationFrame(paintPreviewPlayhead);
}
function syncPreviewPlayhead(s){
 playheadPosition=s.soundPreviewPosition??-1;playheadEnd=s.soundPreviewEnd??0;playheadUpdated=performance.now();
 if(playheadFrame)cancelAnimationFrame(playheadFrame);
 paintPreviewPlayhead();
}
function trimSelection(){return {start:Number($('uploadStart').value),end:Number($('uploadEnd').value)};}
function updateTrim(){
 const {start,end}=trimSelection();
 $('uploadLength').textContent=`${(end-start).toFixed(2)}s selected`;
 $('uploadLength').classList.toggle('over-limit',end-start>35.0000001);
 $('uploadTrimError').hidden=end-start<=35.0000001;
 const x=uploadDuration?start/uploadDuration*400:0,w=uploadDuration?(end-start)/uploadDuration*400:400;
 $('trimHighlight').setAttribute('x',x);$('trimHighlight').setAttribute('width',w);
 $('trimStartLine').setAttribute('x1',x);$('trimStartLine').setAttribute('x2',x);
 $('trimEndLine').setAttribute('x1',x+w);$('trimEndLine').setAttribute('x2',x+w);
 for(const [id,value] of [['trimStartHandle',start],['trimEndHandle',end]]){
  const handle=$(id);handle.style.left=`${uploadDuration?value/uploadDuration*100:0}%`;
  handle.setAttribute('aria-valuemin',0);handle.setAttribute('aria-valuemax',uploadDuration);
  handle.setAttribute('aria-valuenow',value);handle.setAttribute('aria-valuetext',`${value.toFixed(2)} seconds`);
 }
}
function updateSoundUpload(busy=uploadBusy){
 uploadBusy=busy;
 const {start,end}=trimSelection();
 $('uploadSave').disabled=busy||!uploadHasFile||!$('uploadName').value.trim()||end<=start||end-start>35.0000001;
 $('uploadPreviewPlay').disabled=busy||!uploadHasFile;
 $('uploadBrowse').disabled=busy;
 $('uploadCancel').disabled=busy;$('uploadClose').disabled=busy;
 $('uploadSave').textContent=busy?'Please wait…':uploadTarget?.id?'Save Sound':'Add Sound';
}
function openSoundUpload(target,opener){
 uploadTarget=target;uploadHasFile=false;uploadOpener=opener;
 $('uploadPreview').hidden=true;uploadDuration=0;previewPlaying=false;
 playheadPosition=-1;paintPreviewPlayhead();
 $('uploadFile').textContent='Choose a file';$('uploadName').value=target.name??'';
 $('uploadVolume').value=target.volume??100;$('uploadVolumeValue').value=`${$('uploadVolume').value}%`;
 updateSoundUpload(false);send('soundCancel');$('soundUpload').showModal();$('uploadBrowse').focus();
}
function closeSoundUpload(){
 send('soundPreviewStop');
 playheadPosition=-1;paintPreviewPlayhead();
 $('soundUpload').close();uploadTarget=null;uploadHasFile=false;
 if(uploadOpener?.isConnected)uploadOpener.focus();else $('soundboardClose').focus();
}
for(const id of ['uploadClose','uploadCancel'])$(id).addEventListener('click',()=>{if(!uploadBusy){send('soundCancel');closeSoundUpload();}});
$('soundUpload').addEventListener('cancel',e=>{e.preventDefault();if(!uploadBusy){send('soundCancel');closeSoundUpload();}});
$('uploadBrowse').addEventListener('click',()=>{updateSoundUpload(true);send('soundBrowse');});
$('uploadName').addEventListener('input',()=>updateSoundUpload());
$('uploadVolume').addEventListener('input',()=>$('uploadVolumeValue').value=`${$('uploadVolume').value}%`);
function moveTrimHandle(id,value){
 const gap=Math.min(.01,uploadDuration),{start,end}=trimSelection();
 $(id).value=id==='uploadStart'?Math.max(0,Math.min(value,end-gap)):Math.min(uploadDuration,Math.max(value,start+gap));
 updateTrim();updateSoundUpload();
}
for(const [handleId,id] of [['trimStartHandle','uploadStart'],['trimEndHandle','uploadEnd']]){
 const handle=$(handleId);let dragging=false;
 const move=e=>{const bounds=$('waveTrack').getBoundingClientRect();if(bounds.width)moveTrimHandle(id,(e.clientX-bounds.left)/bounds.width*uploadDuration);};
 handle.addEventListener('pointerdown',e=>{if(e.button!==0||uploadBusy)return;e.preventDefault();handle.focus();dragging=true;handle.setPointerCapture(e.pointerId);send('soundPreviewStop');});
 handle.addEventListener('pointermove',e=>{if(dragging)move(e);});
 for(const event of ['pointerup','pointercancel','lostpointercapture'])handle.addEventListener(event,()=>{dragging=false;});
 handle.addEventListener('keydown',e=>{
  if(uploadBusy)return;const step=e.shiftKey ? .1 : .01;let value=Number($(id).value);
  if(e.key==='ArrowLeft'||e.key==='ArrowDown')value-=step;
  else if(e.key==='ArrowRight'||e.key==='ArrowUp')value+=step;
  else if(e.key==='Home')value=0;
  else if(e.key==='End')value=uploadDuration;
  else return;
  e.preventDefault();send('soundPreviewStop');moveTrimHandle(id,value);
 });
}
$('uploadPreviewPlay').addEventListener('click',()=>send(previewPlaying?'soundPreviewStop':'soundPreview',{...trimSelection(),value:Number($('uploadVolume').value)}));
$('uploadForm').addEventListener('submit',e=>{e.preventDefault();if($('uploadSave').disabled)return;updateSoundUpload(true);send('soundImport',{...uploadTarget,...trimSelection(),name:$('uploadName').value.trim(),value:Number($('uploadVolume').value)});});
if(bridge){bridge.addEventListener('message',e=>{
 if(e.data.type==='state')render(e.data);
 if(e.data.type==='soundFile'&&$('soundUpload').open){
  uploadHasFile=true;uploadDuration=e.data.duration;$('uploadFile').textContent=e.data.file;
  if(!$('uploadName').value.trim())$('uploadName').value=e.data.name.slice(0,60);
  for(const id of ['uploadStart','uploadEnd'])$(id).max=uploadDuration;
  $('uploadStart').value=0;$('uploadEnd').value=uploadDuration;
  const peaks=e.data.peaks,max=Math.max(.01,...peaks);
  $('wavePeaks').setAttribute('d',peaks.map((p,i)=>{const h=Math.max(1,p/max*32),x=(i+.5)*400/peaks.length;return `M${x} ${40-h}V${40+h}`;}).join(''));
  $('uploadPreview').hidden=false;updateTrim();updateSoundUpload(false);
 }
 if(e.data.type==='soundSaved')closeSoundUpload();
});send('ready');}
else{$('status').textContent='Design preview — open ProgramMic to connect audio';for(const el of document.querySelectorAll('button:not([data-local]),select,input'))el.disabled=true;}
