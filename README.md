# NiTorrent

Windows-РєР»РёРµРЅС‚ РЅР° WinUI Рё .NET 11. РЎРѕСЃС‚РѕСЏРЅРёРµ РґРѕРєСѓРјРµРЅС‚Р°С†РёРё СЃРІРµСЂРµРЅРѕ СЃ СЂР°Р±РѕС‡РёРј РґРµСЂРµРІРѕРј 27 РЅРѕСЏР±СЂСЏ 2026 РіРѕРґР°.

- [РќР°С‡Р°Р»Рѕ СЂР°Р±РѕС‚С‹ Рё РїРѕСЂСЏРґРѕРє С‡С‚РµРЅРёСЏ](docs/analysis/README.md).
- [РўРµРєСѓС‰Р°СЏ РєР°СЂС‚Р° РєРѕРґР°](docs/analysis/CURRENT_CODE.md).
- [РњРѕРґСѓР»СЊРЅС‹Р№ РјРѕРЅРѕР»РёС‚: СЂРµР°Р»РёР·Р°С†РёСЏ, РїСЂРѕРІРµСЂРєРё Рё РѕРіСЂР°РЅРёС‡РµРЅРёСЏ](docs/analysis/MODULAR_MONOLITH_IMPLEMENTED_2026-11-27.md).
- [РЎРѕРіР»Р°СЃРѕРІР°РЅРЅС‹Рµ С‚СЂРµР±РѕРІР°РЅРёСЏ](docs/analysis/MODULAR_MONOLITH_REQUIREMENTS.md).
- [РђРєС‚СѓР°Р»СЊРЅРѕСЃС‚СЊ РІСЃРµС… Markdown-РґРѕРєСѓРјРµРЅС‚РѕРІ](docs/DOCUMENTATION_STATUS.md).
- [РћС‚РєСЂС‹С‚С‹Рµ РІРѕРїСЂРѕСЃС‹](docs/analysis/QUESTIONS.md) Рё [РїРѕРЅСЏС‚РёСЏ РїСЂРѕРµРєС‚Р°](GLOSSARY.md).

РСЃС…РѕРґРЅРёРєРё РЅР°С…РѕРґСЏС‚СЃСЏ РІ `src/NiTorrent.App` Рё `src/Modules/{Torrents,Desktop,Updates}`, РІСЃРµ РїСЏС‚СЊ С‚РµСЃС‚РѕРІС‹С… РїСЂРѕРµРєС‚РѕРІ вЂ” РІ `tests`. РђРєС‚РёРІРЅС‹ Lifecycle Рё Architecture; С‚СЂРё С‚РµСЃС‚РѕРІС‹С… РїСЂРѕРµРєС‚Р° РїСЂРµР¶РЅРµР№ РјРѕРґРµР»Рё СЃРѕС…СЂР°РЅРµРЅС‹ Р±РµР· РІРєР»СЋС‡РµРЅРёСЏ РІ СЃР±РѕСЂРєСѓ СЂРµС€РµРЅРёСЏ.

Р”Р»СЏ managed x64-РїСЂРѕРІРµСЂРєРё РЅР° Windows СЃ SDK РёР· [global.json](global.json) Рё Windows SDK:

```powershell
dotnet build NiTorrent.slnx -p:Platform=x64 -p:WindowsPackageType=None -p:PublishAot=false -p:PublishTrimmed=false -p:PublishReadyToRun=false -v minimal
dotnet test NiTorrent.slnx --no-build --no-restore -p:Platform=x64 -p:WindowsPackageType=None -p:PublishAot=false -p:PublishTrimmed=false -p:PublishReadyToRun=false --logger trx --results-directory artifacts/verification -v minimal
```

Р”Р»СЏ Release РґРѕР±Р°РІРёС‚СЊ `-c Release` РІ РѕР±Рµ РєРѕРјР°РЅРґС‹. РџРѕСЃР»РµРґРЅРёРµ СЂРµР·СѓР»СЊС‚Р°С‚С‹ вЂ” 73 lifecycle + 13 Р°СЂС…РёС‚РµРєС‚СѓСЂРЅС‹С… С‚РµСЃС‚РѕРІ; СЌС‚Рѕ Р·Р°С„РёРєСЃРёСЂРѕРІР°РЅРЅС‹Р№ РїСЂРѕРіРѕРЅ, Р° РЅРµ РіР°СЂР°РЅС‚РёСЏ СЃРѕСЃС‚РѕСЏРЅРёСЏ РїРѕСЃР»Рµ Р±СѓРґСѓС‰РёС… РёР·РјРµРЅРµРЅРёР№. AOT/MSIX Рё x86/ARM64 СЌС‚РёРјРё РїСЂРѕРІРµСЂРєР°РјРё РЅРµ РїРѕРґС‚РІРµСЂР¶РґРµРЅС‹. РџРѕРґСЂРѕР±РЅС‹Рµ РєРѕРјР°РЅРґС‹ Рё Р¶СѓСЂРЅР°Р»С‹ РЅР°С…РѕРґСЏС‚СЃСЏ РІ РѕС‚С‡С‘С‚Рµ СЂРµР°Р»РёР·Р°С†РёРё.
