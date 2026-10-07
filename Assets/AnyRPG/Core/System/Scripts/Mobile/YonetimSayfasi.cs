namespace AnyRPG {

    /// <summary>Yönetim panelinin sayfası. ELLE DÜZENLEME: Tools~/yonetim/panel.html değiştirilip
    /// python3 Tools~/yonetim/gom.py çalıştırılır.</summary>
    public static class YonetimSayfasi {

        public const string Html = @"<!doctype html>
<html lang=""tr"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<meta name=""robots"" content=""noindex"">
<title>Ötüken Yönetim</title>
<style>
:root{--bg:#12100d;--kart:#1d1a15;--kart2:#26221b;--cizgi:#3a3328;--yazi:#efe6d4;--soluk:#a89a80;--altin:#e0b44c;--yesil:#5fbf6a;--kirmizi:#e0604c;--mavi:#5aa7e0}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--yazi);font:15px/1.45 system-ui,-apple-system,Segoe UI,Roboto,sans-serif}
header{position:sticky;top:0;z-index:5;background:#0d0b09ee;border-bottom:1px solid var(--cizgi);backdrop-filter:blur(6px)}
.ust{display:flex;align-items:center;gap:10px;padding:10px 16px}
.ust h1{font-size:17px;margin:0;color:var(--altin);letter-spacing:.5px;flex:1}
.ust small{color:var(--soluk)}
nav{display:flex;gap:4px;overflow-x:auto;padding:0 12px 8px;scrollbar-width:none}
nav::-webkit-scrollbar{display:none}
nav button{flex:none;background:none;border:1px solid transparent;color:var(--soluk);padding:7px 12px;border-radius:18px;font-size:14px}
nav button.secili{background:var(--kart2);color:var(--altin);border-color:var(--cizgi)}
main{padding:14px 16px 60px;max-width:1100px;margin:0 auto}
.gizli{display:none!important}
.izgara{display:grid;grid-template-columns:repeat(auto-fill,minmax(150px,1fr));gap:10px}
.kutu{background:var(--kart);border:1px solid var(--cizgi);border-radius:12px;padding:12px}
.kutu .ad{color:var(--soluk);font-size:12px;text-transform:uppercase;letter-spacing:.4px}
.kutu .deger{font-size:22px;font-weight:600;margin-top:2px;word-break:break-word}
.kutu .deger.kucuk{font-size:14px;font-weight:500}
h2{font-size:15px;color:var(--altin);margin:20px 0 8px}
.liste{display:flex;flex-direction:column;gap:8px}
.satir{background:var(--kart);border:1px solid var(--cizgi);border-radius:12px;padding:10px 12px}
.satir .bas{display:flex;align-items:center;gap:8px;flex-wrap:wrap}
.satir .bas b{font-size:16px}
.satir .alt{color:var(--soluk);font-size:13px;margin-top:4px;display:flex;flex-wrap:wrap;gap:4px 14px}
.satir .alt span b{color:var(--yazi);font-weight:500}
.rozet{font-size:11px;padding:2px 7px;border-radius:9px;background:var(--kart2);border:1px solid var(--cizgi);color:var(--soluk)}
.rozet.y{color:var(--yesil);border-color:#2f5a33}.rozet.k{color:var(--kirmizi);border-color:#6a2f26}.rozet.m{color:var(--mavi);border-color:#2a4d6a}.rozet.a{color:var(--altin);border-color:#6a5426}
.durum{font-weight:600;color:var(--altin)}
.can{height:5px;background:#3a2620;border-radius:3px;overflow:hidden;width:80px;display:inline-block;vertical-align:middle}
.can i{display:block;height:100%;background:var(--yesil)}
button.d,.d{background:var(--kart2);color:var(--yazi);border:1px solid var(--cizgi);border-radius:9px;padding:7px 12px;font-size:14px;cursor:pointer}
button.d:active{transform:scale(.97)}
button.ana{background:var(--altin);color:#1b1408;border-color:var(--altin);font-weight:600}
button.tehlike{color:var(--kirmizi);border-color:#6a2f26}
.dugmeler{display:flex;gap:6px;margin-left:auto}
input,textarea,select{width:100%;background:#0d0b09;color:var(--yazi);border:1px solid var(--cizgi);border-radius:9px;padding:9px 11px;font:inherit}
textarea{min-height:70px;resize:vertical}
label{display:block;color:var(--soluk);font-size:13px;margin:10px 0 4px}
.arac{display:flex;gap:8px;align-items:center;margin-bottom:10px;flex-wrap:wrap}
.arac input{flex:1;min-width:160px}
pre{background:#0d0b09;border:1px solid var(--cizgi);border-radius:12px;padding:10px;font:12px/1.5 ui-monospace,Menlo,Consolas,monospace;white-space:pre-wrap;word-break:break-word;margin:0;max-height:70vh;overflow:auto}
pre .h{color:var(--kirmizi)}pre .u{color:var(--altin)}
.giris{max-width:340px;margin:18vh auto 0;padding:0 16px}
.giris h1{color:var(--altin);font-size:22px;text-align:center}
.not{color:var(--soluk);font-size:13px}
.hata{color:var(--kirmizi);font-size:14px;min-height:20px;margin-top:8px}
.tamam{color:var(--yesil)}
.cubuk{display:flex;align-items:center;gap:8px;margin:4px 0}
.cubuk .e{width:130px;color:var(--soluk);font-size:13px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.cubuk .b{flex:1;height:14px;background:var(--kart2);border-radius:7px;overflow:hidden}
.cubuk .b i{display:block;height:100%;background:linear-gradient(90deg,#b8862c,var(--altin))}
.cubuk .s{width:28px;text-align:right;font-size:13px}
.bos{color:var(--soluk);text-align:center;padding:24px}
#bildirim{position:fixed;left:50%;bottom:18px;transform:translateX(-50%);background:var(--kart2);border:1px solid var(--cizgi);padding:9px 16px;border-radius:20px;font-size:14px;opacity:0;transition:opacity .25s;pointer-events:none}
#bildirim.goster{opacity:1}
</style>
</head>
<body>

<div id=""girisEkrani"" class=""giris gizli"">
  <h1>ÖTÜKEN DESTANI<br><small style=""font-size:14px;color:var(--soluk)"">Sunucu Yönetimi</small></h1>
  <div class=""kutu"">
    <label for=""sifre"">Panel şifresi</label>
    <input id=""sifre"" type=""password"" autocomplete=""current-password"" autofocus>
    <div style=""margin-top:12px""><button class=""d ana"" style=""width:100%"" onclick=""girisYap()"">Gir</button></div>
    <div class=""hata"" id=""girisHata""></div>
  </div>
  <p class=""not"">İlk şifre sunucuda: <code>cat /var/lib/otuken/yonetim-sifresi.txt</code></p>
</div>

<div id=""uygulama"" class=""gizli"">
<header>
  <div class=""ust""><h1>ÖTÜKEN • Yönetim</h1><small id=""saat""></small><button class=""d"" onclick=""cikis()"">Çık</button></div>
  <nav id=""sekmeler"">
    <button data-s=""genel"" class=""secili"">Genel</button>
    <button data-s=""oyuncular"">Oyuncular <span id=""sayiRozet""></span></button>
    <button data-s=""hesaplar"">Hesaplar</button>
    <button data-s=""oturumlar"">Oturumlar</button>
    <button data-s=""kayitlar"">Kayıtlar</button>
    <button data-s=""gunluk"">Günlük</button>
    <button data-s=""ayarlar"">Ayarlar</button>
  </nav>
</header>
<main>

<section id=""s-genel"">
  <div class=""izgara"" id=""genelKutular""></div>
  <h2>Haritalarda kim var</h2>
  <div class=""kutu"" id=""haritaDagilim""></div>
  <h2>Ne yapıyorlar</h2>
  <div class=""kutu"" id=""durumDagilim""></div>
  <h2>Sunucu</h2>
  <div class=""izgara"" id=""sunucuKutular""></div>
  <h2>Herkese duyuru</h2>
  <div class=""kutu"">
    <textarea id=""duyuru"" maxlength=""300"" placeholder=""Çevrimiçi bütün oyunculara sohbet penceresinde sarı yazıyla gider""></textarea>
    <div style=""margin-top:8px;display:flex;justify-content:flex-end""><button class=""d ana"" onclick=""duyuruGonder()"">Duyuru gönder</button></div>
  </div>
</section>

<section id=""s-oyuncular"" class=""gizli"">
  <div class=""arac""><input id=""oyuncuAra"" placeholder=""Oyuncu, karakter, harita ara…"" oninput=""oyunculariCiz()""></div>
  <div class=""liste"" id=""oyuncuListe""></div>
</section>

<section id=""s-hesaplar"" class=""gizli"">
  <div class=""arac""><input id=""hesapAra"" placeholder=""Hesap veya karakter ara…"" oninput=""hesaplariCiz()"">
    <select id=""hesapSuz"" style=""width:auto"" onchange=""hesaplariCiz()""><option value="""">Hepsi</option><option value=""c"">Çevrimiçi</option><option value=""g"">Google bağlı</option><option value=""y"">Yasaklı</option></select></div>
  <div class=""not"" id=""hesapOzet"" style=""margin-bottom:8px""></div>
  <div class=""liste"" id=""hesapListe""></div>
</section>

<section id=""s-oturumlar"" class=""gizli"">
  <div class=""not"" style=""margin-bottom:8px"">Son 200 oturum (en yenisi üstte). Harita değiştirirken 30 saniyeye kadar kopukluk aynı oturum sayılır.</div>
  <div class=""liste"" id=""oturumListe""></div>
</section>

<section id=""s-kayitlar"" class=""gizli"">
  <div class=""arac"">
    <button class=""d kt"" data-t=""sohbet"">Sohbet</button>
    <button class=""d kt"" data-t=""sikayet"">Şikâyetler</button>
    <button class=""d kt"" data-t=""ticaret"">Ticaret</button>
    <button class=""d kt"" data-t=""odeme"">Ödemeler</button>
  </div>
  <div class=""arac""><input id=""kayitAra"" placeholder=""Süz…"" oninput=""kayitlariCiz()""></div>
  <pre id=""kayitMetin"">Bir kayıt seç.</pre>
</section>

<section id=""s-gunluk"" class=""gizli"">
  <div class=""arac""><input id=""gunlukAra"" placeholder=""Süz…"" oninput=""gunluguCiz()"">
    <label style=""margin:0;display:flex;gap:6px;align-items:center;white-space:nowrap""><input type=""checkbox"" id=""yalnizHata"" style=""width:auto"" onchange=""gunluguCiz()""> yalnız uyarı/hata</label></div>
  <pre id=""gunlukMetin""></pre>
</section>

<section id=""s-ayarlar"" class=""gizli"">
  <h2>Google Play Games girişi</h2>
  <div class=""kutu"">
    <div class=""satir"" style=""border:0;padding:0;background:none"">
      <div class=""alt"" id=""googleDurum""></div>
    </div>
    <label for=""anahtar"">Oyun sunucusu (web) istemcisinin gizli anahtarı</label>
    <input id=""anahtar"" type=""password"" autocomplete=""off"" spellcheck=""false"" placeholder=""GOCSPX-…"">
    <p class=""not"">Google Cloud Console → API'ler ve Hizmetler → Kimlik bilgileri → OAuth 2.0 istemci kimlikleri → <b>web uygulaması</b> türündeki istemci (Play Console'da “Oyun sunucusu” olarak eklediğin) → İstemci gizli anahtarı. Anahtar yalnızca bu sunucuda saklanır; oyuna ve GitHub'a gitmez.</p>
    <div style=""display:flex;gap:8px;flex-wrap:wrap"">
      <button class=""d ana"" onclick=""anahtarKaydet()"">Kaydet ve sına</button>
      <button class=""d"" onclick=""googleYukle(true)"">Yeniden sına</button>
      <button class=""d tehlike"" onclick=""anahtarSil()"">Anahtarı sil</button>
    </div>
    <div class=""hata"" id=""googleSonuc""></div>
  </div>

  <h2>Panel şifresi</h2>
  <div class=""kutu"">
    <label for=""eskiSifre"">Şimdiki şifre</label><input id=""eskiSifre"" type=""password"" autocomplete=""current-password"">
    <label for=""yeniSifre"">Yeni şifre (en az 10 karakter)</label><input id=""yeniSifre"" type=""password"" autocomplete=""new-password"">
    <label for=""yeniSifre2"">Yeni şifre (tekrar)</label><input id=""yeniSifre2"" type=""password"" autocomplete=""new-password"">
    <div style=""margin-top:10px""><button class=""d ana"" onclick=""sifreDegistir()"">Şifreyi değiştir</button></div>
    <div class=""hata"" id=""sifreSonuc""></div>
  </div>
</section>

</main>
</div>
<div id=""bildirim""></div>

<script>
const $=id=>document.getElementById(id);
const esc=s=>String(s==null?'':s).replace(/[&<>""']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','""':'&quot;',""'"":'&#39;'}[c]));
let sekme='genel',genel={},oyuncular=[],hesaplar=[],kayitSatirlari=[],kayitTur='',gunlukSatirlari=[],zamanlayici=null,sonYavas=0;

function sure(dk){dk=dk|0;if(dk<60)return dk+' dk';const s=Math.floor(dk/60),d=dk%60;if(s<24)return s+' sa '+(d?d+' dk':'');const g=Math.floor(s/24);return g+' gün '+(s%24)+' sa';}
function saniyeSure(sn){return sure(Math.floor(sn/60));}
function bildir(m){const b=$('bildirim');b.textContent=m;b.classList.add('goster');clearTimeout(b._t);b._t=setTimeout(()=>b.classList.remove('goster'),2500);}

async function api(yol,veri){
  const r=await fetch('/yonetim/'+yol,veri===undefined?{credentials:'same-origin'}:{method:'POST',credentials:'same-origin',headers:{'Content-Type':'application/json'},body:JSON.stringify(veri)});
  if(r.status===401&&yol!=='giris'){girisGoster();throw new Error('giriş gerekli');}
  let j=null;try{j=await r.json();}catch(e){}
  if(!r.ok)throw new Error((j&&j.hata)||('HTTP '+r.status));
  return j;
}

function girisGoster(){$('uygulama').classList.add('gizli');$('girisEkrani').classList.remove('gizli');clearInterval(zamanlayici);zamanlayici=null;setTimeout(()=>$('sifre').focus(),50);}
async function girisYap(){
  $('girisHata').textContent='';
  try{await api('giris',{sifre:$('sifre').value});$('sifre').value='';basla();}
  catch(e){$('girisHata').textContent=e.message;}
}
$('sifre').addEventListener('keydown',e=>{if(e.key==='Enter')girisYap();});
async function cikis(){try{await api('cikis',{});}catch(e){}girisGoster();}

function basla(){
  $('girisEkrani').classList.add('gizli');$('uygulama').classList.remove('gizli');
  yenile(true);clearInterval(zamanlayici);zamanlayici=setInterval(()=>yenile(false),3000);
}

document.querySelectorAll('#sekmeler button').forEach(b=>b.onclick=()=>{
  sekme=b.dataset.s;
  document.querySelectorAll('#sekmeler button').forEach(x=>x.classList.toggle('secili',x===b));
  document.querySelectorAll('main>section').forEach(s=>s.classList.toggle('gizli',s.id!=='s-'+sekme));
  yenile(true);
});
document.querySelectorAll('.kt').forEach(b=>b.onclick=()=>{kayitTur=b.dataset.t;document.querySelectorAll('.kt').forEach(x=>x.classList.toggle('ana',x===b));kayitYukle();});

async function yenile(hepsi){
  if(document.hidden)return;
  try{
    genel=await api('api/genel');genelCiz();
    oyuncular=await api('api/oyuncular');$('sayiRozet').textContent=oyuncular.length?'('+oyuncular.length+')':'';
    if(sekme==='genel')dagilimCiz();
    if(sekme==='oyuncular')oyunculariCiz();
    const yavas=hepsi||Date.now()-sonYavas>15000;
    if(yavas){
      sonYavas=Date.now();
      if(sekme==='hesaplar'){hesaplar=await api('api/hesaplar');hesaplariCiz();}
      if(sekme==='oturumlar'){oturumlariCiz(await api('api/oturumlar'));}
      if(sekme==='kayitlar'&&kayitTur)kayitYukle();
      if(sekme==='ayarlar'&&hepsi)googleYukle(false);
    }
    if(sekme==='gunluk'){gunlukSatirlari=await api('api/gunluk');gunluguCiz();}
  }catch(e){if(e.message!=='giriş gerekli')$('saat').textContent='bağlantı yok: '+e.message;}
}

function kutu(ad,deger,kucuk){return '<div class=""kutu""><div class=""ad"">'+esc(ad)+'</div><div class=""deger'+(kucuk?' kucuk':'')+'"">'+esc(deger)+'</div></div>';}
function genelCiz(){
  const g=genel;$('saat').textContent=g.saat||'';
  $('genelKutular').innerHTML=
    kutu('Şu an oyunda',g.cevrimici)+kutu('Bugünkü tepe',g.gunTepe)+kutu('Bugün oynayan',g.gunOyuncu)+
    kutu('Bugün ilk kez gelen',g.gunYeni)+kutu('Toplam hesap',g.hesap<0?'…':g.hesap)+kutu('Google bağlı hesap',g.googleHesap)+
    kutu('Etkinlik',g.etkinlik||'yok',true)+kutu('Bugünkü ödeme',g.odemeBugun)+kutu('Reklam ödülü',g.reklamOdulu+' (açılıştan beri)',true);
  $('sunucuKutular').innerHTML=
    kutu('Sürüm',g.surum,true)+kutu('Açık kalma',saniyeSure(g.calisma),true)+kutu('Oyun belleği',g.bellekMb+' MB',true)+
    kutu('Makine belleği',g.makineBellek||'-',true)+kutu('Yük (1/5/15 dk)',(g.yuk||'-').split(' ').slice(0,3).join(' '),true)+
    kutu('Yerdeki ganimet',g.yerdekiGanimet,true)+kutu('Google girişi',g.google,true);
}
function cubuklar(say){
  const d=Object.entries(say).sort((a,b)=>b[1]-a[1]);if(!d.length)return '<div class=""bos"">Şu an kimse yok.</div>';
  const en=d[0][1];return d.map(([e,s])=>'<div class=""cubuk""><span class=""e"">'+esc(e)+'</span><span class=""b""><i style=""width:'+(100*s/en)+'%""></i></span><span class=""s"">'+s+'</span></div>').join('');
}
function dagilimCiz(){
  const h={},d={};
  oyuncular.forEach(o=>{h[o.harita||'?']=(h[o.harita||'?']||0)+1;const k=o.durum.split(' →')[0];d[k]=(d[k]||0)+1;});
  $('haritaDagilim').innerHTML=cubuklar(h);$('durumDagilim').innerHTML=cubuklar(d);
}

function oyunculariCiz(){
  const a=$('oyuncuAra').value.toLocaleLowerCase('tr');
  const l=oyuncular.filter(o=>!a||(o.hesap+' '+o.karakter+' '+o.harita+' '+o.sinif+' '+o.durum).toLocaleLowerCase('tr').includes(a));
  if(!l.length){$('oyuncuListe').innerHTML='<div class=""bos"">'+(oyuncular.length?'Eşleşen yok.':'Şu an oyunda kimse yok.')+'</div>';return;}
  $('oyuncuListe').innerHTML=l.map(o=>
    '<div class=""satir""><div class=""bas""><b>'+esc(o.karakter)+'</b><span class=""rozet a"">Sv '+o.seviye+'</span><span class=""rozet"">'+esc(o.sinif||'-')+'</span>'+
    (o.google?'<span class=""rozet m"">Google</span>':'')+
    '<div class=""dugmeler""><button class=""d"" data-no=""'+o.hesapNo+'"" data-ad=""'+esc(o.karakter)+'"" onclick=""at(this)"">At</button><button class=""d tehlike"" data-no=""'+o.hesapNo+'"" data-ad=""'+esc(o.hesap)+'"" data-evet=""1"" onclick=""yasakla(this)"">Yasakla</button></div></div>'+
    '<div class=""alt""><span class=""durum"">'+esc(o.durum)+'</span><span>📍 <b>'+esc(o.harita)+'</b></span><span>Can <span class=""can""><i style=""width:'+o.can+'%""></i></span> '+o.can+'%</span></div>'+
    '<div class=""alt""><span>Bu oturum <b>'+sure(o.oturumDk)+'</b></span><span>Toplam <b>'+sure(o.toplamDk)+'</b></span><span>Kut <b>'+o.kut+'</b></span><span>Akçe <b>'+esc(o.para)+'</b></span><span>Hesap <b>'+esc(o.hesap)+'</b> #'+o.hesapNo+'</span><span>IP '+esc(o.ip)+'</span></div></div>').join('');
}

function hesaplariCiz(){
  const a=$('hesapAra').value.toLocaleLowerCase('tr'),s=$('hesapSuz').value;
  const l=hesaplar.filter(h=>(!a||(h.ad+' '+h.karakter).toLocaleLowerCase('tr').includes(a))&&(!s||(s==='c'&&h.cevrimici)||(s==='g'&&h.google)||(s==='y'&&h.yasak)));
  const top=hesaplar.reduce((t,h)=>t+h.dakika,0);
  $('hesapOzet').textContent=hesaplar.length+' hesap (en son görülen üstte, en çok 1000) · hepsinin toplam oyun süresi '+sure(top);
  $('hesapListe').innerHTML=l.length?l.slice(0,300).map(h=>
    '<div class=""satir""><div class=""bas""><b>'+esc(h.ad)+'</b><span class=""not"">#'+h.no+'</span>'+
    (h.cevrimici?'<span class=""rozet y"">oyunda</span>':'')+(h.google?'<span class=""rozet m"">Google</span>':'')+(h.yasak?'<span class=""rozet k"">yasaklı</span>':'')+
    '<div class=""dugmeler"">'+(h.yasak?'<button class=""d"" data-no=""'+h.no+'"" data-ad=""'+esc(h.ad)+'"" onclick=""yasakla(this)"">Yasağı kaldır</button>':'<button class=""d tehlike"" data-no=""'+h.no+'"" data-ad=""'+esc(h.ad)+'"" data-evet=""1"" onclick=""yasakla(this)"">Yasakla</button>')+'</div></div>'+
    '<div class=""alt""><span>Karakter <b>'+esc(h.karakter||'-')+'</b></span><span>Toplam <b>'+sure(h.dakika)+'</b></span><span>Oturum <b>'+h.oturum+'</b></span><span>İlk <b>'+esc(h.ilk||'-')+'</b></span><span>Son <b>'+esc(h.son||'-')+'</b></span></div></div>').join('')
    :'<div class=""bos"">Hesap yok.</div>';
}

function oturumlariCiz(l){
  $('oturumListe').innerHTML=l.length?l.map(o=>'<div class=""satir""><div class=""bas""><b>'+esc(o.karakter||o.ad)+'</b><span class=""not"">'+esc(o.ad)+'</span><span class=""rozet a"" style=""margin-left:auto"">'+sure(o.dk)+'</span></div><div class=""alt""><span>Giriş <b>'+esc(o.bas)+'</b></span><span>'+esc(o.haritalar)+'</span></div></div>').join(''):'<div class=""bos"">Henüz kapanmış oturum yok.</div>';
}

async function kayitYukle(){try{kayitSatirlari=await api('api/kayit?tur='+kayitTur);kayitlariCiz();}catch(e){$('kayitMetin').textContent=e.message;}}
function kayitlariCiz(){const a=$('kayitAra').value.toLocaleLowerCase('tr');const l=kayitSatirlari.filter(s=>!a||s.toLocaleLowerCase('tr').includes(a));$('kayitMetin').innerHTML=l.length?l.map(esc).join('\n'):'Kayıt yok.';}
function gunluguCiz(){
  const a=$('gunlukAra').value.toLocaleLowerCase('tr'),h=$('yalnizHata').checked;
  const l=gunlukSatirlari.filter(s=>(!a||s.toLocaleLowerCase('tr').includes(a))&&(!h||/\[(Warning|Error|Exception|Assert)\]/.test(s)));
  $('gunlukMetin').innerHTML=l.length?l.map(s=>{const e=esc(s);return /\[(Error|Exception|Assert)\]/.test(s)?'<span class=""h"">'+e+'</span>':/\[Warning\]/.test(s)?'<span class=""u"">'+e+'</span>':e;}).join('\n'):'Kayıt yok.';
}

async function at(b){const no=+b.dataset.no,ad=b.dataset.ad;if(!confirm(ad+' oyundan atılsın mı? (yeniden girebilir)'))return;try{await api('api/at',{hesap:no});bildir(ad+' atıldı');}catch(e){bildir(e.message);}}
async function yasakla(b){
  const no=+b.dataset.no,ad=b.dataset.ad,evet=b.dataset.evet==='1';
  if(!confirm(evet?ad+' yasaklansın mı? Oyundaysa atılır ve bir daha giremez.':ad+' hesabının yasağı kaldırılsın mı?'))return;
  try{await api(evet?'api/yasakla':'api/yasak-kaldir',{hesap:no});bildir(evet?ad+' yasaklandı':ad+' yasağı kaldırıldı');setTimeout(async()=>{if(sekme==='hesaplar'){hesaplar=await api('api/hesaplar');hesaplariCiz();}},2500);}catch(e){bildir(e.message);}
}
async function duyuruGonder(){
  const m=$('duyuru').value.trim();if(!m)return;
  if(!confirm('Çevrimiçi '+oyuncular.length+' oyuncuya gönderilsin mi?\n\n'+m))return;
  try{await api('api/duyuru',{metin:m});$('duyuru').value='';bildir('Duyuru gönderildi');}catch(e){bildir(e.message);}
}

function googleCiz(g){
  $('googleDurum').innerHTML=
    '<span>Oyunda web istemci kimliği: <b>'+(g.kurulu?'var ✓':'yok — oyun sürümüne eklenmeli')+'</b></span>'+
    (g.webIstemci?'<span style=""word-break:break-all"">'+esc(g.webIstemci)+'</span>':'')+
    '<span>Gizli anahtar: <b>'+(g.anahtarVar?'kayıtlı':'girilmedi')+'</b></span>'+
    '<span>Sınama: <b class=""'+(/DOĞRU/.test(g.sinama)?'tamam':'')+'"">'+esc(g.sinama)+'</b></span>';
}
async function googleYukle(sina){
  try{const g=sina?await api('api/google',{sina:true}):await api('api/google');
    googleCiz(g);
    if(g.sinama==='sınanıyor...')setTimeout(()=>googleYukle(false),2500);
  }catch(e){$('googleSonuc').textContent=e.message;}
}
async function anahtarKaydet(){
  const a=$('anahtar').value.trim();$('googleSonuc').textContent='';
  if(!a){$('googleSonuc').textContent='Anahtarı yapıştır.';return;}
  try{googleCiz(await api('api/google',{anahtar:a}));$('anahtar').value='';bildir('Kaydedildi, sınanıyor…');setTimeout(()=>googleYukle(false),3000);}
  catch(e){$('googleSonuc').textContent=e.message;}
}
async function anahtarSil(){
  if(!confirm('Gizli anahtar silinsin mi? Google ile giriş çalışmaz.'))return;
  try{googleCiz(await api('api/google',{anahtar:''}));bildir('Anahtar silindi');}catch(e){$('googleSonuc').textContent=e.message;}
}
async function sifreDegistir(){
  const s=$('sifreSonuc');s.className='hata';s.textContent='';
  if($('yeniSifre').value!==$('yeniSifre2').value){s.textContent='Yeni şifreler aynı değil.';return;}
  try{await api('api/sifre',{eski:$('eskiSifre').value,yeni:$('yeniSifre').value});
    ['eskiSifre','yeniSifre','yeniSifre2'].forEach(i=>$(i).value='');
    alert('Şifre değişti. Yeni şifreyle yeniden gir.');girisGoster();
  }catch(e){s.textContent=e.message;}
}

document.addEventListener('visibilitychange',()=>{if(!document.hidden&&zamanlayici)yenile(true);});
api('api/genel').then(()=>basla()).catch(()=>girisGoster());
</script>
</body>
</html>
";
    }
}
