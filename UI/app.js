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
 for(const el of document.querySelectorAll('button,select,input'))el.disabled=s.busy;
 $('hotkeyValue').textContent=s.hotkey;
 $('hotkey').textContent=s.assigningHotkey?'Press any key…':'Assign Hotkey';
 $('hotkey').setAttribute('aria-pressed',Boolean(s.assigningHotkey));
 $('micHotkeyValue').textContent=s.micHotkey??'Not assigned';
 $('micHotkey').textContent=s.assigningMicHotkey?'Press any key…':'Assign Hotkey';
 $('micHotkey').setAttribute('aria-pressed',Boolean(s.assigningMicHotkey));
}
for(const id of ['process','microphone','output'])$(id).addEventListener('change',()=>send('select',{target:id,value:Number($(id).value)}));
for(const [id,target,label] of [['programVolume','program','programValue'],['micVolume','microphone','micValue'],['masterVolume','output','masterValue']]){ $(id).addEventListener('input',()=>$(label).value=`${$(id).value}%`);$(id).addEventListener('change',()=>send('volume',{target,value:Number($(id).value)})); }
for(const command of ['toggle','refresh','muteMic'])$(command).addEventListener('click',()=>send(command));
document.addEventListener('pointerdown',e=>{
 if(!e.target.closest('#hotkey,#micHotkey'))send('cancelHotkey');
},true);
$('hotkey').addEventListener('click',()=>send($('hotkey').getAttribute('aria-pressed')==='true'?'cancelHotkey':'assignHotkey'));
$('micHotkey').addEventListener('click',()=>send($('micHotkey').getAttribute('aria-pressed')==='true'?'cancelHotkey':'assignMicHotkey'));
if(bridge){bridge.addEventListener('message',e=>{if(e.data.type==='state')render(e.data);});send('ready');}
else{$('status').textContent='Design preview — open ProgramMic to connect audio';for(const el of document.querySelectorAll('button,select,input'))el.disabled=true;}
