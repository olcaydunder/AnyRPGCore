#!/bin/bash
# Ötüken Destanı oyun sunucusu kurulumu (Ubuntu 22.04 / 24.04, x86). Çalışan bir sunucuya tek satırla kurulur,
# aynı sunucudaki başka oyunlara (ör. Zootopia Mobile: TCP 8080, UDP 7777-7799) dokunmaz:
#
#   curl -fsSL https://raw.githubusercontent.com/olcaydunder/AnyRPGCore/master/Tools~/ag/kur.sh | sudo bash
#
# Ne yapar:
#  - "otuken" adlı yetkisiz kullanıcı, /opt/otuken (oyun) ve /var/lib/otuken (hesaplar, karakterler)
#  - /usr/local/bin/otuken-guncelle: GitHub'daki "sunucu" sürümüne bakar (surum.txt); yeni sürüm varsa
#    OtukenSunucu.zip'i indirir, kurar, oyunu yeniden başlatır. 5 dakikada bir çalışır (systemd zamanlayıcı).
#  - otuken.service: oyunu başsız sunucu kipinde çalıştırır (paketteki baslat.sh; UDP 7770), çökerse yeniden açar
#  - güvenlik duvarı (ufw) açıksa UDP 7770'e (oyun) ve TCP 8081'e (yönetim paneli) izin verir
#  - disk koruması: oyunun çıktısı /var/log/syslog'a yazılmaz, journald 1 GB ile sınırlı, syslog 500 MB'da döndürülür
# Şifre, anahtar, jeton içermez; sunucu yalnız herkese açık sürüm dosyalarını indirir. Yeniden çalıştırmak zararsızdır.
# Kısa durum: otuken-durum     Günlük: journalctl -u otuken -n 100
set -e
export DEBIAN_FRONTEND=noninteractive
PORT=7770

apt-get update -y -qq
apt-get install -y -qq unzip curl ca-certificates
# olağan Linux oyunu (yedek) için sanal ekran ve kitaplıklar; ayrılmış sunucu derlemesi bunlara ihtiyaç duymaz
apt-get install -y -qq xvfb libgl1 libglu1-mesa libgl1-mesa-dri libxcursor1 libxrandr2 libxi6 || true
apt-get install -y -qq libglib2.0-0t64 || apt-get install -y -qq libglib2.0-0 || true

id otuken >/dev/null 2>&1 || useradd --system --create-home --home-dir /var/lib/otuken --shell /usr/sbin/nologin otuken
mkdir -p /opt/otuken /var/lib/otuken
chown otuken: /opt/otuken /var/lib/otuken

cat > /usr/local/bin/otuken-guncelle <<'BETIK'
#!/bin/bash
# yeni oyun sunucusu sürümü varsa indirir, kurar, yeniden başlatır
set -uo pipefail
DEPO="https://github.com/olcaydunder/AnyRPGCore/releases/download/sunucu"
KLASOR=/opt/otuken
mkdir -p "$KLASOR"
yeni=$(curl -fsSL --retry 3 "$DEPO/surum.txt" | tr -d '[:space:]') || exit 0
[ -n "$yeni" ] || exit 0
eski=$(cat "$KLASOR/surum.txt" 2>/dev/null || true)
if [ "$yeni" = "$eski" ] && [ -d "$KLASOR/oyun" ]; then
  exit 0
fi
gecici=$(mktemp -d)
if ! curl -fsSL --retry 3 -o "$gecici/sunucu.zip" "$DEPO/OtukenSunucu.zip"; then
  rm -rf "$gecici"; exit 0
fi
if ! unzip -q "$gecici/sunucu.zip" -d "$gecici/oyun"; then
  rm -rf "$gecici"; exit 0
fi
# paketin içindeki sürüm (varsa) yayımdakiyle aynı olmalı; değilse paket yarım/eski indirilmiştir, sonra yine denenir
ic=$(tr -d '[:space:]' < "$gecici/oyun/surum.txt" 2>/dev/null || true)
if [ -n "$ic" ] && [ "$ic" != "$yeni" ]; then
  logger -t otuken "indirilen paket $ic, beklenen $yeni; sonra yeniden denenecek"
  rm -rf "$gecici"; exit 0
fi
chmod +x "$gecici"/oyun/*.x86_64 "$gecici"/oyun/baslat.sh 2>/dev/null
systemctl stop otuken || true
rm -rf "$KLASOR/oyun.eski"
[ -d "$KLASOR/oyun" ] && mv "$KLASOR/oyun" "$KLASOR/oyun.eski"
mv "$gecici/oyun" "$KLASOR/oyun"
echo "$yeni" > "$KLASOR/surum.txt"
chown -R otuken: "$KLASOR"
rm -rf "$gecici"
systemctl start otuken
logger -t otuken "oyun sunucusu $yeni kuruldu"
BETIK
chmod 755 /usr/local/bin/otuken-guncelle

# kısa durum: sürüm, çalışıyor mu, son sunucu satırları ("otuken-durum" yazınca)
cat > /usr/local/bin/otuken-durum <<'BETIK'
#!/bin/bash
echo "Kurulu sürüm : $(cat /opt/otuken/surum.txt 2>/dev/null || echo yok)"
echo "Yayımdaki    : $(curl -fsSL https://github.com/olcaydunder/AnyRPGCore/releases/download/sunucu/surum.txt 2>/dev/null | tr -d '[:space:]')"
echo "Oyun sunucusu: $(systemctl is-active otuken 2>/dev/null)   Güncelleyici: $(systemctl is-active otuken-guncelle.timer 2>/dev/null)"
echo "UDP 7770     : $(ss -lun 2>/dev/null | grep -q ':7770 ' && echo dinleniyor || echo DİNLENMİYOR)"
echo "Yönetim      : http://$(curl -fsS4 --max-time 3 https://api.ipify.org 2>/dev/null || hostname -I | cut -d' ' -f1):8081/yonetim ($(ss -ltn 2>/dev/null | grep -q ':8081 ' && echo açık || echo KAPALI))"
[ -f /var/lib/otuken/yonetim-sifresi.txt ] && echo "Panel ilk şifresi: $(cat /var/lib/otuken/yonetim-sifresi.txt)"
echo "Disk         : $(df -h / | awk 'NR==2{print $5" dolu, "$4" boş"}')   syslog: $(du -ch /var/log/syslog* 2>/dev/null | tail -n1 | cut -f1)   journal: $(journalctl --disk-usage 2>/dev/null | grep -oE '[0-9.]+[KMGT]' | head -n1)"
echo "--- son sunucu satırları"
journalctl -u otuken --no-pager -n 400 2>/dev/null | grep -E "\[Sunucu\]|Exception|signal|Killed|oom" | tail -n 8
journalctl -t otuken --no-pager -n 3 2>/dev/null
BETIK
chmod 755 /usr/local/bin/otuken-durum

eski_birim=$(cat /etc/systemd/system/otuken.service 2>/dev/null || true)
cat > /etc/systemd/system/otuken.service <<BIRIM
[Unit]
Description=Otuken Destani oyun sunucusu
After=network-online.target
Wants=network-online.target
ConditionPathExists=/opt/otuken/oyun/baslat.sh

[Service]
User=otuken
Environment=HOME=/var/lib/otuken
Environment=PORT=$PORT
WorkingDirectory=/opt/otuken/oyun
ExecStart=/opt/otuken/oyun/baslat.sh
Restart=always
RestartSec=10
LimitNOFILE=65536
# aynı makinedeki öteki oyunu sıkıştırmasın
MemoryMax=4G
Nice=5
# günlük: yalnız journald'a (boyutu sınırlı), /var/log/syslog'a yazılmaz (rsyslog süzgeci aşağıda);
# her saniye tekrarlanan bir hata olursa 30 sn'de 1000 satırdan fazlası atılır
SyslogIdentifier=otuken-oyun
LogRateLimitIntervalSec=30s
LogRateLimitBurst=1000

[Install]
WantedBy=multi-user.target
BIRIM

# --- disk koruması: oyunun çıktısı diski doldurmasın
# 1) rsyslog: oyun satırları /var/log/syslog'a kopyalanmaz (journalctl -u otuken ile okunur; güncelleyicinin "otuken" notları kalır)
if [ -d /etc/rsyslog.d ]; then
  cat > /etc/rsyslog.d/10-otuken.conf <<'RSYS'
# Ötüken Destanı oyun sunucusu: satırları journald'da kalır, syslog dosyalarına yazılmaz
if ($programname == "otuken-oyun" or $programname == "baslat.sh" or $programname == "OtukenSunucu.x86_64") then {
  stop
}
RSYS
  systemctl restart rsyslog 2>/dev/null || true
fi
# 2) journald: en çok 1 GB, diskte en az 4 GB boş kalsın
mkdir -p /etc/systemd/journald.conf.d
cat > /etc/systemd/journald.conf.d/otuken.conf <<'JRN'
[Journal]
SystemMaxUse=1G
SystemKeepFree=4G
SystemMaxFileSize=128M
JRN
systemctl restart systemd-journald 2>/dev/null || true
journalctl --vacuum-size=1G >/dev/null 2>&1 || true
# 3) logrotate: syslog ve diğerleri 500 MB'ı geçince beklemeden döndürülsün; denetim saatte bir
if [ -f /etc/logrotate.d/rsyslog ] && ! grep -q "maxsize" /etc/logrotate.d/rsyslog; then
  sed -i -E 's/^([[:space:]]*)(daily|weekly|monthly)[[:space:]]*$/&\n\1maxsize 500M/' /etc/logrotate.d/rsyslog
fi
if systemctl cat logrotate.timer >/dev/null 2>&1; then
  mkdir -p /etc/systemd/system/logrotate.timer.d
  cat > /etc/systemd/system/logrotate.timer.d/otuken.conf <<'LRT'
[Timer]
OnCalendar=
OnCalendar=hourly
AccuracySec=1m
LRT
fi

cat > /etc/systemd/system/otuken-guncelle.service <<'BIRIM'
[Unit]
Description=Otuken oyun sunucusu guncelleyici
After=network-online.target

[Service]
Type=oneshot
ExecStart=/usr/local/bin/otuken-guncelle
BIRIM

cat > /etc/systemd/system/otuken-guncelle.timer <<'BIRIM'
[Unit]
Description=Otuken oyun sunucusu guncelleyici (5 dakikada bir)

[Timer]
OnBootSec=1min
OnUnitActiveSec=5min

[Install]
WantedBy=timers.target
BIRIM

# güvenlik duvarı: yalnız oyunun portu eklenir (öteki kurallar olduğu gibi kalır)
if command -v ufw >/dev/null 2>&1 && ufw status 2>/dev/null | grep -q "Status: active"; then
  ufw allow $PORT/udp
  ufw allow 8081/tcp
fi

systemctl daemon-reload
systemctl restart logrotate.timer 2>/dev/null || true
systemctl enable otuken.service
systemctl enable --now otuken-guncelle.timer
/usr/local/bin/otuken-guncelle || true
# hizmet tanımı değiştiyse (ör. günlük sınırları) yeni ayarlar için oyun yeniden başlatılır (oyuncular birkaç saniye kopar)
if [ -n "$eski_birim" ] && [ "$eski_birim" != "$(cat /etc/systemd/system/otuken.service)" ] && systemctl is-active --quiet otuken; then
  systemctl restart otuken || true
fi
systemctl is-active --quiet otuken || systemctl start otuken || true
# sunucunun açılmasını bekle (en çok 2 dk)
for i in $(seq 1 60); do
  ss -lun 2>/dev/null | grep -q ":$PORT " && break
  sleep 2
done
echo ""
echo "Ötüken Destanı sunucusu kuruldu."
/usr/local/bin/otuken-durum
