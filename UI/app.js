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
 $('soundStop').disabled=false;
 $('hotkeyValue').textContent=s.hotkey;
 $('hotkey').textContent=s.assigningHotkey?'Press any key…':'Assign Hotkey';
 $('hotkey').setAttribute('aria-pressed',Boolean(s.assigningHotkey));
 $('micHotkeyValue').textContent=s.micHotkey??'Not assigned';
 $('micHotkey').textContent=s.assigningMicHotkey?'Press any key…':'Assign Hotkey';
 $('micHotkey').setAttribute('aria-pressed',Boolean(s.assigningMicHotkey));
 renderSoundboard(s);
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
function renderSoundboard(s){
 if(document.activeElement!==$('soundMaster'))$('soundMaster').value=s.soundVolume??100;
 $('soundMasterValue').value=`${s.soundVolume??100}%`;
 const clips=s.soundClips??[];
 const signature=JSON.stringify(clips);
 if(signature!==soundLibrarySignature){
  soundLibrarySignature=signature;
  const list=$('soundClips');list.replaceChildren();
  if(!clips.length){const empty=document.createElement('p');empty.className='soundboard-empty';empty.textContent='Your sounds go here. Add a clip to get started.';list.append(empty);}
  for(const clip of clips){
   const card=document.createElement('section');card.className='sound-clip';card.dataset.clip=clip.id;
   const name=document.createElement('input');name.type='text';name.value=clip.name;name.maxLength=60;name.setAttribute('aria-label','Sound name');name.addEventListener('change',()=>send('soundRename',{id:clip.id,name:name.value}));
   const row=document.createElement('div');row.className='sound-clip-actions';
   const play=document.createElement('button');play.className='primary';play.textContent='▶ Play';play.setAttribute('aria-label',`Play ${clip.name}`);play.addEventListener('click',()=>send('soundPlay',{id:clip.id}));
   const remove=document.createElement('button');remove.className='secondary';remove.textContent='Remove';remove.setAttribute('aria-label',`Remove ${clip.name}`);remove.addEventListener('click',()=>{if(window.confirm(`Remove "${clip.name}" from your soundboard?`))send('soundRemove',{id:clip.id});});
   row.append(play,remove);
   const label=document.createElement('label');label.textContent=`Clip volume: ${clip.volume}%`;
   const volume=document.createElement('input');volume.type='range';volume.min=0;volume.max=100;volume.value=clip.volume;volume.setAttribute('aria-label',`${clip.name} volume`);volume.addEventListener('input',()=>label.textContent=`Clip volume: ${volume.value}%`);volume.addEventListener('change',()=>send('soundVolume',{id:clip.id,value:Number(volume.value)}));
   card.append(name,row,label,volume);list.append(card);
  }
 }
 for(const card of $('soundClips').querySelectorAll('[data-clip]'))card.classList.toggle('playing',(s.soundPlaying??[]).includes(card.dataset.clip));
}
function setSoundboardOpen(open){
 const panel=$('soundboardPanel');panel.inert=!open;panel.setAttribute('aria-hidden',!open);
 document.querySelector('main').inert=open;
 $('soundboardBackdrop').hidden=!open;document.body.classList.toggle('soundboard-open',open);
 $('soundboardOpen').setAttribute('aria-expanded',open);
 if(open){send('cancelHotkey');$('soundboardClose').focus();}else $('soundboardOpen').focus();
}
$('soundboardOpen').addEventListener('click',()=>setSoundboardOpen(true));
$('soundboardClose').addEventListener('click',()=>setSoundboardOpen(false));
$('soundboardBackdrop').addEventListener('click',()=>setSoundboardOpen(false));
$('soundImport').addEventListener('click',()=>send('soundImport'));
$('soundStop').addEventListener('click',()=>send('soundStop'));
$('soundMaster').addEventListener('input',()=>$('soundMasterValue').value=`${$('soundMaster').value}%`);
$('soundMaster').addEventListener('change',()=>send('soundMaster',{value:Number($('soundMaster').value)}));
document.addEventListener('keydown',e=>{
 if(!document.body.classList.contains('soundboard-open'))return;
 if(e.key==='Escape'){e.preventDefault();setSoundboardOpen(false);}
 if(e.key==='Tab'){
  const controls=[...$('soundboardPanel').querySelectorAll('button,input')].filter(el=>!el.disabled);
  const first=controls[0],last=controls.at(-1);
  if(e.shiftKey&&document.activeElement===first){e.preventDefault();last.focus();}
  else if(!e.shiftKey&&document.activeElement===last){e.preventDefault();first.focus();}
 }
});
if(bridge){bridge.addEventListener('message',e=>{if(e.data.type==='state')render(e.data);});send('ready');}
else{$('status').textContent='Design preview — open ProgramMic to connect audio';for(const el of document.querySelectorAll('button:not([data-local]),select,input'))el.disabled=true;}
