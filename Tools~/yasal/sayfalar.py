#!/usr/bin/env python3
"""zootopiayazilim.com'a yüklenecek yasal sayfalar: gizlilik, kullanım şartları, oyun kuralları, hesap silme.
Kullanım: python3 sayfalar.py <çıkış klasörü> [oyun adı] [e-posta]"""
import sys, html, datetime
from pathlib import Path

CIKIS = Path(sys.argv[1] if len(sys.argv) > 1 else "yasal_sayfalar")
OYUN = sys.argv[2] if len(sys.argv) > 2 else "Ötüken Destanı"
EPOSTA = sys.argv[3] if len(sys.argv) > 3 else "destek@zootopiayazilim.com"
GELISTIRICI = "Olcay Yasin Dünder (Zootopia Yazılım)"
SITE = "https://zootopiayazilim.com"
TARIH = datetime.date.today().strftime("%d.%m.%Y")

CSS = """
:root{--bg:#faf7f1;--fg:#1f1b16;--soluk:#6b6257;--vurgu:#8a5a12;--cizgi:#e4dccd;--kutu:#fffdf9}
@media (prefers-color-scheme:dark){:root{--bg:#16130f;--fg:#efe7da;--soluk:#a89d8f;--vurgu:#e0b25c;--cizgi:#3a332a;--kutu:#1e1a15}}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--fg);font:16px/1.65 system-ui,-apple-system,"Segoe UI",Roboto,sans-serif}
main{max-width:860px;margin:0 auto;padding:32px 18px 64px}h1{font-size:1.9rem;line-height:1.25;margin:0 0 6px}
h2{font-size:1.25rem;margin:34px 0 8px;padding-top:8px;border-top:1px solid var(--cizgi)}h3{font-size:1.05rem;margin:22px 0 6px}
.ust{color:var(--soluk);font-size:.95rem;margin-bottom:22px}a{color:var(--vurgu)}nav{background:var(--kutu);border:1px solid var(--cizgi);border-radius:10px;padding:12px 16px;margin:18px 0}
nav a{margin-right:14px;white-space:nowrap}table{border-collapse:collapse;width:100%;margin:10px 0;font-size:.95rem}
td,th{border:1px solid var(--cizgi);padding:8px 10px;text-align:left;vertical-align:top}th{background:var(--kutu)}
.not{background:var(--kutu);border-left:4px solid var(--vurgu);padding:10px 14px;border-radius:6px;margin:14px 0}
footer{margin-top:40px;color:var(--soluk);font-size:.9rem;border-top:1px solid var(--cizgi);padding-top:14px}
ol li,ul li{margin:4px 0}.tablo{overflow-x:auto;-webkit-overflow-scrolling:touch;margin:10px 0}.tablo table{min-width:520px;margin:0}
"""

def sayfa(dosya, baslik, govde_tr, govde_en, aciklama):
    diger = [("gizlilik", "Gizlilik Politikası"), ("kullanim-sartlari", "Kullanım Şartları"),
             ("oyun-kurallari", "Oyun Kuralları"), ("hesap-silme", "Hesap Silme")]
    nav = " ".join(f'<a href="{SITE}/{d}">{html.escape(a)}</a>' for d, a in diger)
    metin = f"""<!doctype html>
<html lang="tr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>{html.escape(baslik)} · {html.escape(OYUN)}</title><meta name="description" content="{html.escape(aciklama)}">
<style>{CSS}</style></head><body><main>
<h1>{html.escape(baslik)}</h1>
<div class="ust">{html.escape(OYUN)} · {html.escape(GELISTIRICI)} · Son güncelleme: {TARIH} · <a href="#english">English</a></div>
<nav>{nav}</nav>
{govde_tr}
<h2 id="english">English</h2>
{govde_en}
<footer>{html.escape(OYUN)} · {html.escape(GELISTIRICI)} · İletişim / Contact: <a href="mailto:{EPOSTA}">{EPOSTA}</a> · <a href="{SITE}">{SITE.replace('https://','')}</a></footer>
</main></body></html>
"""
    metin = metin.replace("<table>", '<div class="tablo"><table>').replace("</table>", "</table></div>")
    CIKIS.mkdir(parents=True, exist_ok=True)
    (CIKIS / dosya).write_text(metin, encoding="utf-8")

O = html.escape(OYUN)
E = f'<a href="mailto:{EPOSTA}">{EPOSTA}</a>'

# ---------------------------------------------------------------- gizlilik
gizlilik_tr = f"""
<p>Bu politika, <b>{O}</b> mobil oyununu ("Oyun") oynarken hangi kişisel verilerin işlendiğini, neden işlendiğini, ne kadar saklandığını ve haklarını açıklar. 6698 sayılı Kişisel Verilerin Korunması Kanunu (KVKK) kapsamında aydınlatma metni yerine de geçer.</p>
<h2>1. Veri sorumlusu</h2>
<p>{html.escape(GELISTIRICI)}, İstanbul, Türkiye. İletişim: {E}</p>
<h2>2. Oyunu çevrimdışı (tek oyunculu) oynarsan</h2>
<p>Karakterin, eşyaların ve ayarların yalnız telefonunda saklanır; bize gönderilmez. Uygulamayı silmek ya da verisini temizlemek bu kayıtları siler.</p>
<h2>3. İşlenen veriler</h2>
<table><tr><th>Veri</th><th>Ne zaman</th><th>Amaç</th><th>Hukuki sebep (KVKK m.5)</th></tr>
<tr><td><b>Çevrimiçi hesap:</b> kullanıcı adı, şifre (sunucuda yalnız tuzlanmış özeti — PBKDF2 — saklanır, şifrenin kendisi saklanmaz), hesap numarası</td><td>Çevrimiçi oyuna kaydolunca</td><td>Hesabını oluşturmak, giriş yapmak</td><td>Sözleşmenin kurulması ve ifası</td></tr>
<tr><td><b>Oyun verileri:</b> karakter adı, sınıfı, seviyesi, eşyalar, oyun parası, Kut bakiyesi, görev ve günlük ilerleme, konum (oyun haritası içinde)</td><td>Çevrimiçi oynarken</td><td>Oyunu sunmak, ilerlemeni saklamak</td><td>Sözleşmenin ifası</td></tr>
<tr><td><b>Sohbet yazıları, pazar adları, şikâyetler</b></td><td>Sohbette yazınca, pazar açınca, şikâyet edince</td><td>Oyuncuları korumak, kural ihlallerini incelemek (küfür süzgeci, şikâyet incelemesi)</td><td>Meşru menfaat; hukuki yükümlülük</td></tr>
<tr><td><b>Takas, pazar satışı ve satın alma kayıtları:</b> karakter adları, eşyalar, tutarlar, Google Play sipariş numarası ve ürün</td><td>Takas, satış ya da Kut satın alınca</td><td>Dolandırıcılığı önlemek, anlaşmazlıkları çözmek, satın alınanı teslim etmek, muhasebe</td><td>Sözleşmenin ifası; hukuki yükümlülük; meşru menfaat</td></tr>
<tr><td><b>Teknik veriler:</b> IP adresi (bağlantı sırasında), oyun sürümü</td><td>Sunucuya bağlanınca</td><td>Bağlantıyı kurmak, saldırıları önlemek</td><td>Meşru menfaat</td></tr>
<tr><td><b>Hata raporları:</b> cihaz modeli, Android sürümü, ekran boyutu, grafik ayarları, oyun sürümü, hata metni ve son oyun kayıtları; "Sorun Bildir" ile yazdığın not ve o anki oyun ekranının görüntüsü</td><td>Oyun hata verince ya da sen bildirince (Ayarlar'dan kapatılabilir)</td><td>Hataları düzeltmek</td><td>Meşru menfaat</td></tr>
</table>
<p>Oyun <b>reklam göstermez</b>, reklam ya da analiz (izleme) kitaplığı içermez, rehberine, konumuna (GPS), kamerana, mikrofonuna, fotoğraflarına erişmez. Ad-soyad, e-posta, telefon numarası istemez.</p>
<p><b>Bildirimler:</b> izin verirsen günlük armağan hatırlatması telefonunda yerel bildirim olarak kurulur; bunun için sunucuya bilgi gönderilmez. İzni telefonunun ayarlarından ya da oyundaki Menü &gt; Oyun &gt; Bildirimler'den kapatabilirsin.</p>
<h2>4. Ödemeler</h2>
<p>Kut satın almaları <b>Google Play</b> üzerinden yapılır. Kart ve ödeme bilgilerini Google işler; bize yalnız sipariş numarası, ürün kimliği ve satın alma belirteci (doğrulama için) gelir. Google'ın gizlilik politikası: <a href="https://policies.google.com/privacy">policies.google.com/privacy</a></p>
<h2>5. Verilerin aktarıldığı yerler</h2>
<ul>
<li><b>Oyun sunucusu:</b> Hostinger (VPS hizmet sağlayıcısı). Sunucu yurt dışında bulunabilir.</li>
<li><b>Hata raporları:</b> ntfy.sh (bildirim iletimi) ve GitHub (geliştirme aracı, ABD).</li>
<li><b>Ödeme:</b> Google (Google Play Faturalandırma).</li>
</ul>
<p>Yurt dışına aktarım KVKK m.9'a uygun olarak (uygun güvenceler ya da gerektiğinde açık rızan ile) ve yalnız yukarıdaki amaçlarla yapılır. Verilerin satılmaz, reklam için kullanılmaz.</p>
<h2>6. Saklama süreleri</h2>
<table><tr><th>Veri</th><th>Süre</th></tr>
<tr><td>Hesap ve oyun verileri</td><td>Hesabın silinene kadar</td></tr>
<tr><td>Sohbet kayıtları</td><td>30 gün</td></tr>
<tr><td>Şikâyetler</td><td>1 yıl</td></tr>
<tr><td>Takas ve pazar kayıtları</td><td>2 yıl</td></tr>
<tr><td>Satın alma kayıtları</td><td>Yasal saklama süresi boyunca (en çok 10 yıl)</td></tr>
<tr><td>Hata raporları, sunucu teknik kayıtları</td><td>1 yıl / 30 gün</td></tr>
</table>
<p>Süre dolunca veriler silinir ya da anonim hâle getirilir.</p>
<h2>7. Hesabını ve verilerini silme</h2>
<p>Oyunda <b>Menü &gt; Hesap &gt; Hesabımı sil</b> ile hesabını ve bütün karakter verilerini hemen silebilirsin. Oyuna giremiyorsan: <a href="{SITE}/hesap-silme">{SITE.replace('https://','')}/hesap-silme</a></p>
<h2>8. Çocuklar</h2>
<p>Oyun 13 yaşından küçük çocuklara yönelik değildir. 18 yaşından küçüksen satın alma yapmadan önce ebeveyninin iznini al. 13 yaşından küçük bir çocuğun hesap açtığını fark edersek hesabı sileriz; bize {E} adresinden bildirebilirsin.</p>
<h2>9. Güvenlik</h2>
<p>Şifreler sunucuda tuzlanmış özet olarak saklanır; sunucuya erişim sınırlıdır. Hiçbir sistem tam güvenli değildir: başka sitelerde kullandığın şifreyi oyunda kullanma.</p>
<h2>10. Hakların (KVKK m.11)</h2>
<p>Verilerinin işlenip işlenmediğini öğrenme, bilgi isteme, amacını öğrenme, aktarıldığı kişileri bilme, düzeltilmesini, silinmesini isteme, itiraz etme ve zararın giderilmesini isteme haklarına sahipsin. Başvurunu {E} adresine, oyundaki karakter adınla birlikte gönderebilirsin; en geç 30 gün içinde yanıtlarız. Kişisel Verileri Koruma Kurulu'na şikâyet hakkın saklıdır.</p>
<h2>11. Değişiklikler</h2>
<p>Bu politikayı güncelleyebiliriz; önemli değişikliklerde oyunda yeniden onayını isteriz. Güncel sürüm her zaman bu sayfadadır.</p>
"""
gizlilik_en = f"""
<p>This policy explains what personal data <b>{O}</b> ("the Game") processes, why, for how long, and your rights. Data controller: {html.escape(GELISTIRICI)}, Istanbul, Türkiye — {E}.</p>
<h3>Offline play</h3><p>Single-player saves stay only on your device and are never sent to us.</p>
<h3>Data we process (online play)</h3>
<ul>
<li><b>Account:</b> username and password (stored only as a salted PBKDF2 hash), account number — to create your account and sign you in.</li>
<li><b>Game data:</b> character name, class, level, items, in-game currency, Kut balance, quest and daily progress — to provide the game.</li>
<li><b>Chat messages, shop titles, reports</b> — to protect players and investigate rule violations (profanity filter, report review).</li>
<li><b>Trade, shop sale and purchase records</b> (character names, items, amounts, Google Play order ID and product) — fraud prevention, dispute resolution, delivering purchases, accounting.</li>
<li><b>Technical data:</b> IP address while connected, game version — to run the connection and prevent abuse.</li>
<li><b>Error reports:</b> device model, Android version, screen size, graphics settings, game version, error text and recent game log lines; if you use "Report a problem", your note and a screenshot of the game — to fix bugs (can be turned off in Settings).</li>
</ul>
<p>The Game shows <b>no ads</b>, contains no advertising or analytics/tracking SDKs, and does not access your contacts, location, camera, microphone or photos. We never ask for your real name, email or phone number.</p>
<p><b>Notifications:</b> if you allow them, daily gift reminders are scheduled locally on your phone; no data is sent to our server for this.</p>
<h3>Payments</h3><p>Kut purchases are processed by <b>Google Play</b>. We only receive the order ID, product ID and purchase token for verification.</p>
<h3>Processors and transfers</h3><p>Game server: Hostinger (may be located outside Türkiye). Error reports: ntfy.sh and GitHub (USA). Payments: Google. Transfers follow Article 9 of the Turkish Personal Data Protection Law (KVKK). Your data is never sold or used for advertising.</p>
<h3>Retention</h3><p>Account and game data: until you delete your account. Chat logs: 30 days. Reports: 1 year. Trade and shop records: 2 years. Purchase records: statutory period (up to 10 years). Error reports: 1 year; server technical logs: 30 days.</p>
<h3>Deleting your account</h3><p>In the Game: <b>Menu &gt; Account &gt; Delete my account</b> deletes your account and all character data immediately. If you cannot sign in, see <a href="{SITE}/hesap-silme">{SITE.replace('https://','')}/hesap-silme</a>.</p>
<h3>Children</h3><p>The Game is not directed at children under 13. If you are under 18, ask a parent before buying anything.</p>
<h3>Your rights</h3><p>You may request access, correction, deletion, objection and other rights under KVKK Article 11 (and the GDPR where applicable) by emailing {E} with your character name. We reply within 30 days.</p>
"""
sayfa("gizlilik.html", "Gizlilik Politikası", gizlilik_tr, gizlilik_en, f"{OYUN} gizlilik politikası ve KVKK aydınlatma metni")

# ---------------------------------------------------------------- kullanım şartları
sartlar_tr = f"""
<p>Bu şartlar, <b>{O}</b> ("Oyun") ile {html.escape(GELISTIRICI)} ("biz") arasındaki kullanım sözleşmesidir. Oyunu indirip "Kabul Ediyorum"a dokunarak bu şartları, <a href="{SITE}/oyun-kurallari">Oyun Kuralları</a>'nı kabul etmiş ve <a href="{SITE}/gizlilik">Gizlilik Politikası</a>'nı okumuş olursun.</p>
<h2>1. Oyunu kullanma izni</h2>
<p>Oyunu kişisel ve ticari olmayan amaçla oynaman için sana devredilemez, münhasır olmayan bir izin veriyoruz. Oyunun kodu, hikâyesi, görselleri ve markası bize ya da lisans verenlerimize aittir. Oyunu kopyalamak, değiştirmek, tersine mühendislik yapmak, sunucuya yetkisiz erişmek yasaktır.</p>
<h2>2. Hesap</h2>
<ul><li>Çevrimiçi oyun için kullanıcı adı ve şifreyle hesap açarsın. Şifrenin güvenliğinden sen sorumlusun; hesabını başkasına veremez, satamazsın.</li>
<li>13 yaşından küçükler hesap açamaz. 18 yaşından küçüksen ebeveyninin iznini almalısın.</li>
<li>Karakter adın Oyun Kuralları'na uygun olmalıdır.</li></ul>
<h2>3. Sanal eşyalar, oyun parası ve Kut</h2>
<ul><li>Oyundaki eşyalar, akçe (oyun parası) ve <b>Kut</b> yalnız oyunda kullanılan sanal içeriklerdir; sana mülkiyet hakkı değil, Oyun'da kullanma izni verir. Gerçek para değeri yoktur, gerçek paraya ya da başka bir şeye çevrilemez.</li>
<li><b>Kut</b> Google Play üzerinden satın alınır ve satın alındığı karaktere yüklenir. Kut ve Kut ile alınan eşyalar hesaba bağlıdır: takas edilemez, pazarda satılamaz.</li>
<li>Rastgele ödüllü (sandık, çark) satış yoktur. Demirci gibi şansa bağlı işlemlerde başarı oranları oyunda açıkça yazar.</li>
<li><b>Gerçek para karşılığı</b> eşya, akçe, Kut ya da hesap alıp satmak yasaktır ve hesabın kapatılmasına yol açabilir.</li></ul>
<h2>4. Satın almalar ve iade</h2>
<p>Ödemeler Google Play tarafından alınır; fiyatlar Google Play'de gösterilir. Kut dijital içerik olup satın alma onaylandığı anda hesabına yüklenir; Mesafeli Sözleşmeler Yönetmeliği uyarınca anında ifa edilen dijital içeriklerde cayma hakkı, onayınla sona erer. İade istekleri Google Play'in iade kurallarına göre Google'a yapılır. Yasal haklarının saklı olduğu durumlar dışında kullanılmış Kut iade edilmez. Hesabın kurallar ihlali yüzünden kapatılırsa kalan Kut iade edilmez.</p>
<h2>5. Davranış kuralları</h2>
<p><a href="{SITE}/oyun-kurallari">Oyun Kuralları</a> bu şartların parçasıdır. Kurallara aykırı davranışta uyarı, susturma, ticaret yasağı, geçici ya da kalıcı hesap kapatma uygulayabiliriz.</p>
<h2>6. Oyuncu içeriği</h2>
<p>Sohbet yazıları, pazar adları ve karakter adların senin sorumluluğundadır. Bu içerikleri Oyun'u sunmak için saklama ve gösterme iznini bize verirsin. Uygunsuz içeriği silebilir, süzgeçten geçirebiliriz. Uygunsuz içerik ve oyuncuları oyundaki <b>Şikâyet Et</b> ve <b>Engelle</b> ile bildirebilirsin.</p>
<h2>7. Hizmetin sürekliliği</h2>
<p>Oyunu, sunucuyu ve içerikleri güncelleyebilir, değiştirebilir; bakım için geçici olarak durdurabiliriz. Çevrimiçi hizmeti sona erdirmeye karar verirsek en az 30 gün önceden oyunda duyururuz.</p>
<h2>8. Sorumluluğun sınırı</h2>
<p>Oyun "olduğu gibi" sunulur. Yasanın izin verdiği ölçüde, internet kesintisi, cihaz sorunları, başka oyuncuların davranışları ve takaslardaki kayıplardan sorumlu değiliz. Tüketici olarak kanundan doğan hakların saklıdır.</p>
<h2>9. Hesabın sona ermesi</h2>
<p>Hesabını istediğin zaman <b>Menü &gt; Hesap &gt; Hesabımı sil</b> ile silebilirsin. Kuralları ağır ya da tekrar tekrar ihlal edersen hesabını kapatabiliriz.</p>
<h2>10. Uygulanacak hukuk</h2>
<p>Bu şartlar Türkiye Cumhuriyeti hukukuna tabidir. Uyuşmazlıklarda tüketici hakem heyetleri ve tüketici mahkemeleri, diğer hâllerde İstanbul mahkemeleri ve icra daireleri yetkilidir.</p>
<h2>11. Değişiklikler ve iletişim</h2>
<p>Şartları güncelleyebiliriz; önemli değişikliklerde oyunda yeniden onayını isteriz. Sorular için: {E}</p>
"""
sartlar_en = f"""
<p>These Terms are an agreement between you and {html.escape(GELISTIRICI)} for <b>{O}</b>. By tapping "I Agree" you accept these Terms and the <a href="{SITE}/oyun-kurallari">Game Rules</a> and acknowledge the <a href="{SITE}/gizlilik">Privacy Policy</a>.</p>
<ul>
<li><b>License:</b> a personal, non-commercial, non-transferable license to play. No copying, modification, reverse engineering or unauthorized server access.</li>
<li><b>Account:</b> you are responsible for your password; accounts may not be shared or sold. Users under 13 may not create accounts; under 18 need parental permission.</li>
<li><b>Virtual items, in-game currency and Kut:</b> a limited license to use in the Game only; no real-world value and cannot be exchanged for money. Kut and items bought with Kut are account-bound (not tradable, not sellable). No randomized paid rewards (loot boxes); odds of chance-based actions (e.g. the Blacksmith) are shown in the Game. <b>Real-money trading</b> of items, currency, Kut or accounts is prohibited.</li>
<li><b>Purchases and refunds:</b> payments are handled by Google Play. Kut is digital content delivered immediately; refunds follow Google Play's refund policy, without prejudice to your statutory rights.</li>
<li><b>Conduct:</b> the Game Rules are part of these Terms; violations may lead to warnings, mutes, trade bans, or temporary/permanent account closure.</li>
<li><b>User content:</b> you are responsible for chat, shop titles and character names; you grant us permission to store and display them to run the Game. Use in-game <b>Report</b> and <b>Block</b> for inappropriate content or users.</li>
<li><b>Service:</b> we may update, change or pause the Game; if we end online service we will announce it in the Game at least 30 days in advance.</li>
<li><b>Liability:</b> the Game is provided "as is" to the extent permitted by law; your consumer rights are not affected.</li>
<li><b>Termination:</b> delete your account anytime via Menu &gt; Account &gt; Delete my account.</li>
<li><b>Law:</b> Turkish law; consumer arbitration committees and consumer courts, otherwise Istanbul courts. Contact: {E}</li>
</ul>
"""
sayfa("kullanim-sartlari.html", "Kullanım Şartları", sartlar_tr, sartlar_en, f"{OYUN} kullanım şartları")

# ---------------------------------------------------------------- oyun kuralları
kurallar_tr = f"""
<p>Bozkır herkesindir. Bu kurallar, <b>{O}</b>'nda herkesin güvenle ve keyifle oynaması içindir.</p>
<h2>1. Saygı</h2>
<ul><li>Küfür, hakaret, aşağılama, nefret söylemi (din, dil, ırk, köken, cinsiyet, cinsel yönelim, engellilik), tehdit ve taciz yasaktır.</li>
<li>Cinsel içerik, şiddeti öven ya da yasa dışı içerik paylaşılamaz.</li>
<li>Başkalarının kişisel bilgilerini (adres, telefon, fotoğraf) paylaşmak yasaktır. Kendi kişisel bilgini de paylaşma.</li></ul>
<h2>2. Adlar ve pazar başlıkları</h2>
<ul><li>Karakter adları ve pazar başlıkları küfürlü, hakaretli, cinsel ya da siyasi olamaz.</li>
<li>Yetkili, geliştirici, Google ya da bir kurum gibi görünen adlar (Admin, GM, Destek vb.) kullanılamaz.</li>
<li>Bağlantı (internet adresi) ve reklam yazılamaz.</li></ul>
<h2>3. Ticaret</h2>
<ul><li>Takas ve pazar kendi sorumluluğundadır; takas ikinizin onayıyla olur ve <b>geri alınamaz</b>. Karşı tarafın koyduğu eşyayı ve parayı onaylamadan önce dikkatle incele.</li>
<li>Dolandırıcılık (yanıltma, sahte vaat, eşya değiştirerek kandırma) yasaktır.</li>
<li><b>Gerçek para karşılığı</b> eşya, akçe, Kut ya da hesap alıp satmak, bunun reklamını yapmak yasaktır.</li></ul>
<h2>4. Adil oyun</h2>
<ul><li>Hile programı, otomatik tıklama/bot (oyunun kendi Oto Av özelliği dışında), oyunu değiştirmek, sunucuya saldırmak yasaktır.</li>
<li>Bulduğun hataları kendi yararına kullanma; {E} adresine ya da oyundaki <b>Sorun Bildir</b> ile bildir.</li>
<li>Başkasının hesabını kullanmak ya da hesabını başkasına vermek yasaktır.</li></ul>
<h2>5. Spam</h2>
<p>Aynı yazıyı tekrar tekrar göndermek, sohbeti doldurmak, reklam yapmak yasaktır.</p>
<h2>6. Bildirme ve engelleme</h2>
<p>Kural dışı bir davranış görürsen oyunda <b>Ticaret &gt; Oyuncular &gt; Şikâyet Et</b> ile bildir. Bir oyuncuyu <b>Engelle</b>rsen yazılarını görmezsin, takas teklifleri gelmez. Şikâyetler incelenir; şikâyet edenin kimliği karşı tarafa söylenmez. Sohbet otomatik süzgeçten geçer.</p>
<h2>7. Yaptırımlar</h2>
<table><tr><th>İhlal</th><th>Olası yaptırım</th></tr>
<tr><td>Hafif (tek seferlik kaba söz, spam)</td><td>Uyarı, sohbet susturma (1-24 saat)</td></tr>
<tr><td>Orta (tekrarlanan hakaret, uygunsuz ad, dolandırıcılık girişimi)</td><td>Ad değiştirme, ticaret yasağı, 1-7 gün hesap askıya alma</td></tr>
<tr><td>Ağır (nefret söylemi, tehdit, hile, gerçek parayla ticaret, dolandırıcılık)</td><td>Kalıcı hesap kapatma; haksız kazanılan eşya ve paranın silinmesi</td></tr></table>
<p>Yaptırıma itiraz için {E} adresine karakter adınla yaz.</p>
"""
kurallar_en = f"""
<ul>
<li><b>Respect:</b> no profanity, insults, hate speech, threats or harassment; no sexual or illegal content; never share anyone's personal information.</li>
<li><b>Names and shop titles:</b> no offensive, sexual or political names; no impersonation of staff, developers, Google or organizations (Admin, GM, Support...); no links or ads.</li>
<li><b>Trading:</b> trades require both players' confirmation and cannot be undone — check carefully. Scams are prohibited. <b>Real-money trading</b> of items, currency, Kut or accounts is prohibited.</li>
<li><b>Fair play:</b> no cheats, macros or bots (other than the Game's own Auto Hunt), no exploiting bugs (report them), no account sharing.</li>
<li><b>Spam:</b> no repeated messages, flooding or advertising.</li>
<li><b>Report and block:</b> use Trade &gt; Players &gt; Report / Block in the Game. Reports are reviewed; reporters stay anonymous. Chat is automatically filtered.</li>
<li><b>Sanctions:</b> warnings and mutes; name changes, trade bans and 1-7 day suspensions; permanent bans for hate speech, threats, cheating, real-money trading and fraud. Appeals: {E}</li>
</ul>
"""
sayfa("oyun-kurallari.html", "Oyun Kuralları", kurallar_tr, kurallar_en, f"{OYUN} topluluk ve oyun kuralları")

# ---------------------------------------------------------------- hesap silme
silme_tr = f"""
<p>Bu sayfa <b>{O}</b> (geliştirici: {html.escape(GELISTIRICI)}) çevrimiçi hesabını ve verilerini nasıl sileceğini anlatır.</p>
<h2>Oyunun içinden (hemen)</h2>
<ol><li>Oyunu aç ve çevrimiçi oyuna gir.</li><li><b>Menü &gt; Hesap &gt; Hesabımı sil</b>'e dokun.</li>
<li>Şifreni yaz, onaylamak için <b>SİL</b> yaz ve "Hesabımı ve verilerimi kalıcı olarak sil"e dokun.</li></ol>
<p>Hesabın ve bütün karakterlerin birkaç saniye içinde silinir.</p>
<h2>Oyuna giremiyorsan (e-postayla)</h2>
<p>{E} adresine konu satırına <b>"Hesap silme"</b> yazarak; <b>kullanıcı adını</b>, <b>karakter adlarını</b> ve hesabın sana ait olduğunu gösteren bilgileri (ör. son giriş tarihi, karakter seviyesi, varsa Google Play sipariş numarası) gönder. Hesabın doğrulandıktan sonra en geç <b>30 gün</b> içinde silinir ve sana bildirilir.</p>
<h2>Silinen veriler</h2>
<ul><li>Kullanıcı adı ve şifre özeti</li><li>Bütün karakterler, eşyalar, oyun parası, Kut bakiyesi, ilerleme, posta ve arkadaş listesi</li></ul>
<h2>Bir süre saklanan veriler</h2>
<ul><li>Takas ve pazar kayıtları: dolandırıcılık incelemesi için 2 yıl</li>
<li>Satın alma kayıtları (sipariş numarası, ürün): yasal saklama süresi boyunca (en çok 10 yıl)</li>
<li>Şikâyet kayıtları: 1 yıl; sohbet kayıtları: 30 gün</li></ul>
<p>Satın alınmış Kut silmeyle iade edilmez; iade için Google Play'e başvurabilirsin. Tek oyunculu oyunun kayıtları yalnız telefonundadır: oyundaki karakter seçme ekranından ya da telefonunun Ayarlar &gt; Uygulamalar bölümünden uygulamanın verisini temizleyerek silebilirsin.</p>
"""
silme_en = f"""
<p>How to delete your <b>{O}</b> online account (developer: {html.escape(GELISTIRICI)}).</p>
<h3>In the Game (immediate)</h3><ol><li>Open the Game and sign in to online play.</li><li>Go to <b>Menu &gt; Account &gt; Delete my account</b>.</li><li>Enter your password, type <b>SİL</b> to confirm and tap the delete button.</li></ol>
<h3>If you cannot sign in (email)</h3><p>Email {E} with the subject <b>"Account deletion"</b>, your <b>username</b>, <b>character names</b> and details proving ownership (e.g. last sign-in date, character level, Google Play order number if any). Your account is deleted within <b>30 days</b> after verification.</p>
<h3>Deleted</h3><p>Username and password hash; all characters, items, in-game currency, Kut balance, progress, mail and friends list.</p>
<h3>Retained for a limited time</h3><p>Trade and shop records: 2 years (fraud investigation). Purchase records (order number, product): statutory period, up to 10 years. Reports: 1 year; chat logs: 30 days.</p>
<p>Purchased Kut is not refunded by deletion; contact Google Play for refunds. Offline saves stay only on your device; clear them via the Game or your phone's app settings.</p>
"""
sayfa("hesap-silme.html", "Hesap ve Veri Silme", silme_tr, silme_en, f"{OYUN} hesap ve veri silme")
print("yazıldı:", ", ".join(sorted(p.name for p in CIKIS.glob("*.html"))))
