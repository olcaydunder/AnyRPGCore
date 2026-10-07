#!/usr/bin/env python3
"""Yönetim paneli sayfasını (panel.html) oyun koduna gömer: YonetimSayfasi.cs

    python3 Tools~/yonetim/gom.py
"""
import os

KOK = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
KAYNAK = os.path.join(KOK, "Tools~", "yonetim", "panel.html")
HEDEF = os.path.join(KOK, "Assets", "AnyRPG", "Core", "System", "Scripts", "Mobile", "YonetimSayfasi.cs")

html = open(KAYNAK, encoding="utf-8").read().replace("\r\n", "\n")
govde = html.replace('"', '""')
cs = (
    "namespace AnyRPG {\n\n"
    "    /// <summary>Yönetim panelinin sayfası. ELLE DÜZENLEME: Tools~/yonetim/panel.html değiştirilip\n"
    "    /// python3 Tools~/yonetim/gom.py çalıştırılır.</summary>\n"
    "    public static class YonetimSayfasi {\n\n"
    "        public const string Html = @\"" + govde + "\";\n"
    "    }\n"
    "}\n"
)
open(HEDEF, "w", encoding="utf-8", newline="\n").write(cs)
print("yazıldı:", HEDEF, len(html), "bayt")
