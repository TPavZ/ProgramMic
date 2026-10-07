'use strict';
const $=id=>document.getElementById(id);
const bridge=window.chrome?.webview;
const send=(command,extra={})=>bridge?.postMessage({command,...extra});
let previousActive=false;
function updateActivation(active){
 const button=$('toggle');
 const label=active?'Deactivate':'Activate';
 if(button.dataset.label!==label){button.textContent=label;button.dataset.label=label;}
 button.setAttribute('aria-pressed',active);
 if(active&&!previousActive&&!window.matchMedia('(prefers-reduced-motion: reduce)').matches){
  button.animate([{boxShadow:'0 0 0 0 #86efac00'},{boxShadow:'0 0 28px 7px #86efac99',offset:.35},{boxShadow:'0 0 0 14px #86efac00'}],{duration:1000,easing:'ease-out'});
  for(let i=0;i<10;i++){
   const spark=document.createElement('span');spark.className='activation-spark';spark.textContent='✦';spark.setAttribute('aria-hidden','true');
   spark.style.left=`${8+i*9}%`;spark.style.top=i%2?'70%':'25%';button.append(spark);
   const rise=i%2?24:-32;
   const animation=spark.animate([{opacity:0,transform:'translate(-50%,-50%) scale(.2)'},{opacity:1,transform:'translate(-50%,-50%) scale(1.3)',offset:.25},{opacity:0,transform:`translate(calc(-50% + ${i%2?12:-12}px),calc(-50% + ${rise}px)) scale(.3)`}],{duration:950,delay:i*25,easing:'ease-out',fill:'both'});
   animation.onfinish=()=>spark.remove();
  }
 }
 previousActive=active;
}
function options(id,items,selected){const el=$(id);const signature=JSON.stringify(items);if(el.dataset.options!==signature){el.replaceChildren(...items.map(x=>new Option(x.label,x.value)));el.dataset.options=signature;if(!items.length)el.add(new Option('No devices available',-1));}el.value=String(selected);}
function render(s){
 options('process',s.processes,s.process);options('microphone',s.microphones,s.microphone);options('output',s.outputs,s.output);
 for(const [id,value,label] of [['programVolume',s.programVolume,'programValue'],['micVolume',s.micVolume,'micValue'],['masterVolume',s.masterVolume,'masterValue']]){if(document.activeElement!==$(id))$(id).value=value;$(label).value=`${value}%`;}
 updateActivation(Boolean(s.programEnabled));
 $('muteMic').textContent=s.micMuted?'Unmute microphone':'Mute microphone';$('muteMic').setAttribute('aria-pressed',s.micMuted);
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
