# Migracja: Mleczarka 7+ (HTML/Three.js) → Mleczarka (Unity 6 LTS, URP)

Stan na 2026-11-11.

Wersja HTML jest **zamrożona jako prototyp**. Kopię zabezpieczono w `Game Assets/00_Archiwum/Mleczarka7_HTML_2026-11-11`: gra w jednym pliku, źródła, pełna historia git i sumy SHA256. Tag ostatniego stanu: `html-prototyp-final-2026-11-11`. Niczego nie usunięto.

## 1. Co jest w prototypie (audyt)

Prototyp ma ok. 4,3 tys. linii JS. Logika jest oddzielona od grafiki, co bardzo ułatwia przeniesienie.

| Obszar | Pliki HTML | Stan | Wartość dla Unity |
|---|---|---|---|
| Symulacja ekonomii mleczarni | `engine/game.js` (479), `config.js`, `data.js` | działa, testy | **Wysoka.** Czysta maszyna stanów, gracze jako tablica `players[pid]`, zdarzenia `S.events`. Przenosimy do C# 1:1 jako moduł bez Unity. |
| Umowy z gospodarstwami, licytacja, rywal AI | `game.js` (`bid`, `offerInfo`, `rivalTick`) | działa | Wysoka. Podstawa przyszłego rynku multiplayer (gracze zamiast rywala AI). |
| Zamówienia klientów z terminami | `game.js` (`genOrder`, `claimOrder`, `deliver`) | działa | Wysoka: zlecenia transportowe w karierze. |
| Produkcja: receptury, etapy pracy/czasu, partie | `data.js` (RECIPES, STATIONS), `game.js` | działa (UHT, jogurt); masło i ser są w danych, bez stanowisk | Wysoka. Rdzeń modułu Produkcja i minigier. |
| Pracownicy (praca, chodzenie, wydajność, pensje, „Pomóż!”) | `game.js` | działa | Średnia: zasady zostają, prezentacja do przebudowy. |
| Rozbudowy (cysterna, bus, zbiornik, magazyn, maszyny) | `config.js` UPGRADES | działa | Wysoka: zaczątek drzewka rozwoju firmy. |
| Pompowanie mleka (odbiór i rozładunek) | `game.js` `pumpTick` | działa | Wysoka: minigra przy cysternie. |
| Jazda po mapie (droga, pobocze, cofanie) | `drive.js`, `engine/roads.js` | działa (arcade 2D) | Niska. W Unity jazda dostaje prawdziwą fizykę (WheelCollider). Zostaje graf dróg dla AI i nawigacji. |
| Wyścigi kartingowe | `kart/sim.js` (265), `kart/tracks.js` | działa, 18 testów | Wysoka. Zasady przenosimy: drift z 3 stopniami turbo, przeszkadzajki (placek, ziemniak, gęś, turbo z kiszonki, parasol), bańki mleka, AI z pasami. Fizykę kartu stroimy na nowo w Unity. |
| Misja 1 (15 min), tryb swobodny, samouczek, ciekawostki | `game.js`, `ui/tutorial.js`, `data.js` FACTS | działa | Średnia: przejdą do systemu zadań i encyklopedii. |
| Zapis gry | `storage.js` (localStorage JSON, wersja `v:1`, autozapis co 15 s) | działa | Format do zmiany na zapis wersjonowany z migracjami. Zasada („zapis = migawka stanu symulacji”) zostaje. |
| Dźwięk | `audio/soundManager.js`, `ui/sound.js` | działa | Zasoby przenosimy. Logikę (szyny muzyka/efekty/tło, losowe odgłosy wsi) odtwarzamy w Unity Audio Mixer. |
| Efekty | `fx/pfx.js`, `render/flipbook.js` | działa | Arkusze ParticleFX przenosimy jako tekstury Flipbook dla Particle System lub VFX Graph. |
| Grafika 3D (karty, otoczenie, budynki) | `render/*` | **przestarzała** | Nie przenosimy kodu. Zostają tylko proporcje, paleta i spis elementów świata. |
| UI (HTML/CSS) | `ui/ui.js`, `styles.css` | działa | Nie przenosimy kodu. Zostają układ ekranów i ikonografia; nowe UI w UI Toolkit. |

### Mechaniki do zachowania (nie usuwamy bez zgody)
- Ekonomia: ceny produktów (UHT 4,50 zł, jogurt 12 zł, masło 30 zł, ser 42 zł), pensje 10 zł/dzień, zatrudnienie 60 zł, koszty rozbudów, cena skupu mleka.
- Umowy: opłata 15 zł, przejęcie ×2, przebicie o min. 0,20 zł/l, ochrona 150 s, cena maks. 3 zł/l.
- Zamówienia: co 70 s, max 6 aktywnych, 3 przyjęte, termin 300–480 s, nagroda ×1,6.
- Doba gry 600 s, start o 6:00.
- Wyścig: wszystkie stałe `K` (prędkości, drift, turbo, trafienia, utrata mleka 30%, pojemność 20 l), 3 tory (Okiennice, Narew, Biebrza), 3 rywali (Mleczpol, Muuu-Trans, Krówka Express).
- Ciekawostki edukacyjne o mleku (10 tekstów).

## 2. Zasoby do bezpośredniego przeniesienia

| Zasób | Lokalizacja | Do Unity | Licencja |
|---|---|---|---|
| 32 dźwięki i muzyka (MP3) | `assets/audio`, `Game Assets/05_Dzwieki` | `Assets/_Mleczarka/Audio` (import bez zmian; muzyka jako Streaming) | Hobo Fighting: biblioteka użytkownika; ElevenLabs: plan użytkownika; Kenney CC0 |
| Zwierzęta animowane: krowa, koń, osioł (GLB) | `Game Assets/08_Modele_3D/Quaternius_zwierzeta` | Import FBX/GLB (glTFast), Animator | CC0 |
| Kenney Nature Kit (GLB) | `Game Assets/08_Modele_3D/Kenney_Nature_Kit` | Rekwizyty zastępcze. Docelowo Stylized Nature MegaKit dla spójności. | CC0 |
| HDRI Poly Haven (5 nieb) | `Game Assets/09_HDRI_PolyHaven` | Skybox + Lighting Environment (URP) | CC0 |
| Arkusze efektów ParticleFX (12) | `Game Assets/06_Efekty_czasteczkowe/ParticleFX_Studio` | Texture Sheet Animation w Particle System | komercyjna bez podpisu |
| Ilustracje-wycinanki (3) | `Game Assets/04_Interfejs_i_tla/Wycinanki_Podlasie` | Karty torów i ekrany ładowania | wygenerowane na koncie użytkownika (ChatGPT) |

**Nie przenosimy** modeli z kodu (karty, chałupy, cerkiew, drzewa z prymitywów). Zastąpią je pobrane modele 3D. Kod HTML zostaje w archiwum jako dokumentacja zachowania.

## 3. Lista elementów do ponownej implementacji (w Unity)

1. Rdzeń symulacji w C#, bez zależności od Unity (`Mleczarka.Core`): czas gry, zdarzenia, komendy, migawka stanu.
2. Ekonomia i rynek: umowy, licytacja, ceny, zamówienia.
3. Produkcja: receptury i etapy, stanowiska, partie, magazyn (z danych ScriptableObject).
4. Pracownicy: zadania, wydajność, pensje.
5. Pojazdy: fizyka z zawieszeniem i kołami, cysterna z pompą, dostawczak, kamera Cinemachine.
6. Wyścigi: zasady z `kart/sim.js`, nowe tory 3D, AI po splajnie.
7. Zapis: JSON wersjonowany z migracjami. Ten sam format posłuży do synchronizacji sieciowej.
8. Audio: Mixer (Music/SFX/Ambience), losowe odgłosy wsi, zmiana muzyki między widokami.
9. UI: HUD jazdy, ekran mleczarni, ekonomia, kreator postaci.
10. Samouczek i ciekawostki: system zadań z treściami z `data.js`.
