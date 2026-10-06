'use strict';
const $=id=>document.getElementById(id);
const bridge=window.chrome?.webview;
const send=(command,extra={})=>bridge?.postMessage({command,...extra});
function options(id,items,selected){const el=$(id);const signature=JSON.stringify(items);if(el.dataset.options!==signature){el.replaceChildren(...items.map(x=>new Option(x.label,x.value)));el.dataset.options=signature;if(!items.length)el.add(new Option('No devices available',-1));}el.value=String(selected);}
function render(s){
 options('process',s.processes,s.process);options('microphone',s.microphones,s.microphone);options('output',s.outputs,s.output);
 for(const [id,value,label] of [['programVolume',s.programVolume,'programValue'],['micVolume',s.micVolume,'micValue'],['masterVolume',s.masterVolume,'masterValue']]){if(document.activeElement!==$(id))$(id).value=value;$(label).value=`${value}%`;}
 $('toggle').textContent=s.programEnabled?'Turn program audio off':'Turn program audio on';$('toggle').setAttribute('aria-pressed',s.programEnabled);
 $('muteMic').textContent=s.micMuted?'Unmute microphone':'Mute microphone';$('muteMic').setAttribute('aria-pressed',s.micMuted);
 $('status').textContent=s.status.replace(/^●\s*/, '');$('dot').classList.toggle('live',s.running);
 for(const el of document.querySelectorAll('button,select,input'))el.disabled=s.busy;
 if(![...$('hotkey').options].some(o=>o.text===s.hotkey))$('hotkey').add(new Option(s.hotkey,''));
 $('hotkey').value=[...$('hotkey').options].find(o=>o.text===s.hotkey)?.value??'';
}
for(let i=1;i<=24;i++)$('hotkey').add(new Option(`F${i}`,111+i));
for(const id of ['process','microphone','output'])$(id).addEventListener('change',()=>send('select',{target:id,value:Number($(id).value)}));
for(const [id,target,label] of [['programVolume','program','programValue'],['micVolume','microphone','micValue'],['masterVolume','output','masterValue']]){ $(id).addEventListener('input',()=>$(label).value=`${$(id).value}%`);$(id).addEventListener('change',()=>send('volume',{target,value:Number($(id).value)})); }
for(const command of ['toggle','refresh','muteMic'])$(command).addEventListener('click',()=>send(command));
$('hotkey').addEventListener('change',()=>send('hotkey',{value:Number($('hotkey').value)}));
if(bridge){bridge.addEventListener('message',e=>{if(e.data.type==='state')render(e.data);});send('ready');}
else{$('status').textContent='Design preview — open ProgramMic to connect audio';for(const el of document.querySelectorAll('button,select,input'))el.disabled=true;}
