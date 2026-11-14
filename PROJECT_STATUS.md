# STAN NA 2026-11-14, 17:20 — działy, tabliczki, kariera „od kuchni” (Claude)

**Działy produkcyjne (skrzydło maszyn):** 6 działów — 1 Przyjęcie i laboratorium, 2 Mleko płynne, 3 Fermentacja, 4 Masło i sery, 5 Rozlew i pakowanie, 6 Chłodnia. Przeszklone ścianki z drzwiami od korytarza, pełne ścianki między działami (górą rury), otwór w ściance na taśmę zbiorczą, kolor posadzki działu. Chłodnia obrócona drzwiami do korytarza. Obsługa maszyny tylko z pola obsługi (nie przez szybę). Pracownicy chodzą przez drzwi działów.

**Tabliczki:** napisy to obrazki (Art/Signs, czcionka DejaVu) na płytach z ramą — tablice działów nad drzwiami, tabliczki maszyn na linkach nad stanowiskami, tabliczki stanowisk w starej hali zamiast unoszących się napisów (stare napisy wyłączone, nie usunięte), szyld „MLECZARNIA” na fasadzie, „SKRZYDŁO PRODUKCYJNE” na ścianie skrzydła.

**Kariera „od kuchni do mleczarni” (nowy tryb, menu → Nowa gra):**
- Start: 60 zł, rower z koszykiem, bańka 10 l, letnia kuchnia przy wsi (dom gracza, wybrany automatycznie wolny teren 140 m od rynku; trawa usunięta tylko spod szopy).
- Pętla: F u gospodarza — mleko do bańki (na litry) → F w domu — do lodówki → Q przy sprzęcie: twaróg, mleko w butelkach, jogurt (kuchnia), zbieranie śmietanki (lodówka), śmietana i kefir (stół), masło (maselnica), wirówka ręczna, ser (prasa) → F przy domu — do koszyka → F na rynku — sprzedaż na straganie (rynek się nasyca, potem odbija).
- Rozwój (K): maselnica, grzybek, duży garnek, przyczepka, wirówka ręczna, druga lodówka, prasa, stragan, dostawczak, cysterna, hala, skrzydło maszyn. Pojazdy, umowy, hala i skrzydło zablokowane do czasu zakupu (komunikat z ceną). Cysterna przed halą rozładowuje się do lodówek w domu, dostawczak ładuje się ze spiżarki.
- Rower: B przy rowerze; postać siedzi (klip Driving_Loop), koła i korba się kręcą, bańka na bagażniku, słoiki w koszyku.
- Modele z Blendera (Blender/Kariera_Kuchnia.blend → Art/Career): szopa, kuchnia węglowa z garnkiem, lodówka retro, stół, półka ze słoikami, maselnica, wirówka ręczna, prasa, bańka, rower.
- Testy: kariera 33/33 (pętla, blokady, migracja), symulacja 34/34, hala 64/64. Test w grze (menu Kariera → 3, bez zapisu gracza): kupno mleka, twaróg, koszyk, rower, sprzedaż 2 szt. za 27,02 zł.
- Zapisy w starych trybach bez zmian (kariera nieaktywna).

# STAN NA 2026-11-14, 16:30 — noszenie skrzynek, wersja .exe (Claude)

- Noszenie: warstwa „Noszenie” w kontrolerach rolnika i pracownika (maska: ręce, klip UAL2 Walk_Carry_Loop, menu „Produkcja → 13”). Skrzynka zmniejszona do ok. 45 × 30 cm, leży na dłoniach przed klatką piersiową. Sprawdzone zdjęciami z boku i z przodu (menu „Produkcja → 14”).
- Zbudowana wersja Windows: `Builds/Windows/Mleczarka.exe` (821 MB, 14,5 min, 0 błędów). Folder Builds nie trafia do repozytorium.
- Czeka na decyzję: podział skrzydła na działy („tak, działy”) i tryb „Kariera: od kuchni” („tak, kariera”).

# STAN NA 2026-11-14, 13:20 — widok we wnętrzu hali (Claude)

- `FactoryShellVisibility`: zamiast chowania wszystkich ścian i dachu po wejściu chowają się tylko bryły, które zasłaniają gracza przed kamerą (linia kamera → głowa, pierś, stopy). Reszta ścian, okna, dźwigary i dach zostają. Dotyczy hali Codexa i skrzydła maszyn. Sprawdzone w trybie gry.
- Podgląd skrzydła (menu 12) na czas zdjęć z góry chowa ściany i dach sam.

# STAN NA 2026-11-14, 13:05 — podświetlenia maszyn (Claude)

- `PlantGuide`: maszyny, przy których trzeba teraz coś zrobić, mają świecące pole obsługi, pulsującą strzałkę z napisem (TUTAJ / NAPRAW / UMYJ / ODBIERZ / ODŁÓŻ / START) i światło w kolorze zadania. U góry ekranu pasek „Co dalej” z czynnością, produktem i odległością.
- Kolejność: awaria → czynność przy partii (najdalej zaawansowana pierwsza) → mycie brudnej maszyny, na którą czeka partia → odbiór skrzynek / chłodnia → próbka w laboratorium → start partii przy przyjęciu. Gdy maszyny pracują same — „pracuje sama, jeszcze ok. N s”.
- Przy panelu przyjęcia wszystkie maszyny trasy wybranego produktu mają numery etapów 1, 2, 3…; panel maszyny pokazuje trasę partii z bieżącym etapem.
- Sprawdzone w trybie gry (podgląd skrzydła). Uwaga: odświeżanie Unity w trakcie Play psuje sesję gry (domena skryptów) — przed odświeżeniem zatrzymywać Play.

# STAN NA 2026-11-14, 12:50 — wersja bez pracowników i bez limitów czasu (Claude)

- **Bez pracowników (domyślnie):** `PlantState.workersOff = true`. Halę obsługuje tylko gracz; pracownicy z zapisu zostają, ale nie chodzą, nie pracują i nie dostają pensji. Zatrudnianie i przypisywanie zablokowane. Przełącznik w menu Escape („Pracownicy w hali: wyłączeni / włączeni”).
- **Bez ograniczeń czasowych (domyślnie):** `GameState.noTimeLimits = true`. Zamówienia nie przepadają, misja nie ma limitu i nie kończy się przegraną z czasu; licznik czasu zniknął z HUD. Migracja przywraca zamówienia i misję przegrane wcześniej z braku czasu.
- Przytrzymanie Q nie zasypuje już ekranu dymkami z procentami (postęp jest w panelu maszyny).
- Testy: hala 64/64 (nowe: wersja bez pracowników, bez pensji, brak limitów czasu, migracja), symulacja 34/34. Podgląd skrzydła bez pracowników: UHT i masło przeszły cały ciąg „rękami gracza”.

# STAN NA 2026-11-14, popołudnie — skrzydło maszyn (Claude)

**Zrobione i sprawdzone w Unity (tryb gry):**
- Skrzydło 24 × 26 m od zachodu hali Codexa, ta sama posadzka (19,44 m), przejście 4 m w ścianie hali (oryginalna ściana wyłączona, nie usunięta). Teren pod skrzydłem niższy od posadzki, w obrysie nie było żadnych obiektów ani drzew. Kopia sceny: `Backups/PlantWing_2026-11-14_*`.
- 17 własnych modeli z Blendera (`Art/Machines`), materiały URP przez „1. Przygotuj materiały”. Silos, 16 rurociągów, taśma zbiorcza, paleta odbioru, chłodnia, laboratorium, stacja CIP, zbiornik śmietanki.
- `PlantFloor` + `PlantMachineView`: nowa hala włącza się sama (`plant.enabled`). Q / przytrzymanie Q, C (CIP), X (przegląd / naprawa, w laboratorium odrzucenie mleka), P (pracownik), 1–8 i −/+ przy przyjęciu. Panel maszyny: kroki, postęp, temperatura, brud, stan, pracownik, opis.
- Animacje: wirówka, mieszadła, beczka maślnicy, tłoki homogenizatora, prasa, harfa, talerz i głowice rozlewaczek, ramię pakowarki, wskazówki, lampki stanu, krążki w dojrzewalni, poziom śmietanki, probówka, drzwi chłodni, para i bąbelki. Przepływ mleka w rurach przy zmianie maszyny, opakowania na taśmie, skrzynki na palecie i w chłodni.
- Pracownicy widoczni (model rolnika, inny kolor), chodzą korytarzem, przy pracy animacja „Interact”. Gracz też ma stan „Praca”.
- Stara linia Codexa działa dalej (jej partie dokańczają się także przy nowej hali).
- Testy: hala 54/54, symulacja 34/34, produkcja 25/25. Podgląd: „Produkcja → 12. Podgląd skrzydła” (zapis wstrzymany): UHT ×4 i masło ×2 przeszły cały ciąg z pracownikami, jogurt i twaróg w toku.

**Znane braki:** ręczne przejście gracza przez cały ciąg (bez pracowników) nie było jeszcze nagrane; poza laboratorium nie ma wizualnej cysterny przy rozładunku; jazda testowa Zabielsk → Zarzecze nadal utyka; ekonomia (ujemne saldo zapisu) do przeglądu.

# STAN NA 2026-11-14 (Claude)

**Kolejność prac (decyzja użytkownika):** produkcja i jej animacja → ekonomia (garaż płatny, ceny) → kariera i odblokowania → spółdzielnie. Wyścigi później. Mapy i grafiki nie przebudowywać.

## Zrobione dziś
- **Menu startowe i pauza** (`Runtime/UI/StartMenuUI.cs`, dodaje się samo do sceny z GameController): Kontynuuj/Graj, Nowa gra od początku (misja 40 min albo tryb swobodny; stary zapis kopiowany do `mleczarka_gra1_kopia_<data>.json` i scena ładowana od nowa), Sterowanie, Wyjście. Escape otwiera pauzę, gdy żaden panel nie jest otwarty. Sprawdzone w trybie gry.
- **Produkcja jako proces** (`ProductionFloor/Runtime/ProcessAnimator.cs`, tylko odczyt stanu symulacji): mleko płynie rurami (zbiornik → UHT → pakowarka; zbiornik → pasteryzacja → fermentacja → pakowarka; dorobione zejścia rur i odcinek do pasteryzatora), pracująca maszyna drga, miga zieloną lampką i paruje, czekająca na pracę świeci na pomarańczowo, fermentatory bąbelkują i mają wskaźnik postępu, zbiornik przyjęć pokazuje poziom mleka, kartony i kubki jadą taśmą 0,6 m/s do skrzynki napełnianej w trakcie pakowania; rolnik obraca się do maszyny, przy której pracuje. Podgląd: menu **Mleczarka → Produkcja → 8. Podgląd animacji procesu (Play)** (zapis gry wstrzymany na czas podglądu), klatki w `Screenshots/proces_klatki`.
- **Wersja .exe**: menu **Mleczarka → Zbuduj grę dla Windows (.exe)** → `Builds/Windows/Mleczarka.exe` (jeszcze nie budowana).
- Migracja zapisu: starsze zapisy dostają brakujące gospodarstwa (12 zamiast 6).

## Wyniki testów (2026-11-14)
- Symulacja 34/34, produkcja (logistyka i zapis) 25/25, kompilacja bez błędów.
- Jazda testowa Mleczarnia → Olszanka: dojechała (1,32 km w 83 s). Jazda Zabielsk → Zarzecze: **utknęła na starcie** (start w środku rynku między straganami) — do poprawy w teście.

## Znane problemy do następnego etapu
- W obecnym zapisie gracza saldo jest ujemne (−29,80 zł, dzień 5) — ekonomia wymaga przeglądu (koszty pensji vs. przychody w trybie ręcznej produkcji).
- Garaż nie pobiera jeszcze pieniędzy, części nie są powiązane z karierą.
- Brak animacji postaci przy pracy (rolnik tylko obraca się do maszyny) i animacji niesienia skrzynki.
- Repozytorium git jest w środowisku Claude; GitHub czeka na puste repozytorium utworzone przez użytkownika.

---

# INTEGRACJA PRODUKCJI Z GŁÓWNĄ GRĄ — 2026-11-11

Produkcja działa w Gmina_Podlasie: nowa hala z maszynami Kenney CC0, ręczna praca, magazyn, załadunek i sprzedaż na rynku. Zachowano gra1 oraz dane pracowników; TAB zawiera zarządzanie i opcjonalną pomoc załogi. Wyniki: 22 testy logiki, 14 kontroli pętli w głównej scenie. Szczegóły i kopie bezpieczeństwa: Docs/PRODUKCJA_GLOWNA_GRA.md. Nie uruchamiać ponownie generatora mapy bez uwzględnienia nowego zakładu.

---

# PROJECT_STATUS — Mleczarka (Unity)

_Aktualizacja: 2026-11-11 (noc)_

Projekt: `C:\Users\pasti\Mleczarka` · Unity 6.3 LTS (6000.3.28f1) · URP · Input System 1.20 · Cinemachine 3.1 · MCP for Unity 10.3 (zainstalowany w projekcie, ale Claude jeszcze go nie widzi, więc praca idzie przez edytor i pliki).

## Etap 1: audyt, zabezpieczenie, projekt — ✅ zakończony
| Zadanie | Stan |
|---|---|
| Kopia wersji HTML (gra, źródła, historia git, SHA256) | ✅ `Game Assets/00_Archiwum/Mleczarka7_HTML_2026-11-11` |
| Audyt i plan migracji | ✅ `Docs/MIGRACJA.md` |
| Architektura modułów | ✅ opis w `Docs/ARCHITEKTURA.md`; w kodzie na razie tylko zestawy `Mleczarka.Runtime` i `Mleczarka.Editor` (moduły symulacji jeszcze nie istnieją) |
| Unity 6.3 LTS z modułami Windows, Android, iOS | ✅ zainstalowane |
| Projekt URP z Input System i Cinemachine | ✅ |
| Unity MCP widoczny dla Claude | ❌ pakiet działa w edytorze (port 6401), ale serwer nie pojawia się w sesji Claude |

## Etap 2: zasoby — ✅ pierwsza partia w projekcie
| Zadanie | Stan |
|---|---|
| Import paczek Quaternius (natura, wieś, postacie, stroje, animacje UAL 1 i 2) i Kenney Car Kit | ✅ `Assets/_Mleczarka/Art/ThirdParty/<Autor>/<Paczka>` + pliki licencji |
| Zasady importu (rig Humanoid dla ciał i animacji, tekstury normalnych map, limity 1024 px i ASTC na telefony) | ✅ `ThirdPartyImportRules.cs` |
| 33 materiały URP Lit z mapami normalnymi, gładkością i AO (mapy gładkości wyliczone z roughness/ORM) | ✅ `Art/Materials/Quaternius` |
| Klipy UAL: nazwy, zapętlenie, animacje w miejscu | ✅ |
| Prefaby | ⚠️ jeszcze nie; scena składa obiekty bezpośrednio z modeli. Prefaby domów, płotów i postaci są następne. |

## Etap 3 (zadanie 6): pierwsza scena — ✅ działa w trybie gry
Scena `Assets/_Mleczarka/Scenes/Demo_Gospodarstwo.unity` budowana w całości menu **Mleczarka → 2. Zbuduj scenę demo**.

Sprawdzone zrzutami z edytora i w trybie gry, konsola bez błędów:
- zagroda z domem złożonym z 40 modułów Medieval Village (szachulec, okiennice, dachówka, komin), płot z bramą, wóz, skrzynie;
- las i łąka: 72 drzewa, krzewy, kwiaty, 260 kęp trawy, kamienie, grzyby, polna droga i kamienna ścieżka;
- rolnik złożony z modułów: głowa z UBC, strój chłopski (Peasant), włosy i broda; Animator z UAL (bezczynność, chód, trucht, sprint, skok);
- sterowanie Input System: WASD lub gamepad, Shift = sprint, Spacja = skok, mysz obraca kamerę Cinemachine; postać zderza się z domem (sprawdzone);
- dostawczak mleczarni na WheelColliderach (napęd tył, skręt przód, zawieszenie), wsiadanie klawiszem E; fizykę jazdy sprawdził użytkownik (na ciągniku, ten sam kod); rolnik chodzi przodem (poprawione);
- światło dzienne, niebo, mgła, postprocess (ACES, bloom, korekcja kolorów, winieta).

## Pojazdy mleczarni — w toku
- ✅ System personalizacji (dane części, sloty, lakier, parametry).
- ✅ Cysterna wiejska z Blendera, złożona z części w scenie; stoi stabilnie w trybie gry. Jazdy cysterną jeszcze nie testowano.
- ✅ Części tuningowe i wyścigowe cysterny (17 w katalogu), trzy przykładowe konfiguracje w scenie.
- ✅ Dostawczak z Blendera (chłodnia, skrzynia z bańkami, wersja wyścigowa); katalog 27 części.
- ✅ Garaż: G przy pojeździe, części, lakier, parametry na żywo (sprawdzone w trybie gry).
- ⏳ Strojenie jazdy (różnice między wersjami zwykłą i wyścigową są dziś tylko w liczbach), zapis konfiguracji w zapisie gry, ceny powiązane z ekonomią, odblokowywanie części w karierze.

## Oprawa (tekstury i światło) — ✅ pierwsza wersja
- Niebo HDRI w 3 porach dnia, słońce zgodne z niebem, odbicia, SSAO, miękkie cienie, mgła.
- Teren z pagórkami i stylizowanymi teksturami; droga i ścieżki; logo mleczarni na pojazdach.
- Do dalszej pracy: płynny cykl dnia i nocy w grze (dziś przełączanie w edytorze), trawa rysowana instancjami na terenie, wiatr w liściach (shader), światła pojazdów nocą.

## Etap 4: rdzeń symulacji — ✅
- `Mleczarka.Core` (bez Unity): cała logika mleczarni z prototypu, komendy, deterministyczny stan, zapis JSON z wersją. Testy 34/34.

## Etap 5: symulacja w świecie — ✅ pętla gry działa
Scena `Assets/_Mleczarka/Scenes/Swiat_Gmina.unity` (menu **Mleczarka → 4. Zbuduj świat: Gmina**), pierwsza w ustawieniach budowania.
- Mapa z wersji HTML w skali ×2 (ok. 380 × 380 m): 19 odcinków polnych dróg, 6 gospodarstw (dom, zbiornik na mleko, pastwisko z płotem, wóz), mleczarnia z silosem i szyldem, rynek ze straganami i domami, hala Mleczpolu, pola, ok. 340 drzew, wzgórza zamykające mapę.
- Modele z Blendera (własne): silos mleka, szyld mleczarni, stragan.
- Strefy (`WorldPlace`) podają symulacji, gdzie jest gracz; `GameController` łączy symulację ze sceną: F = akcja w miejscu (umowa, pompowanie, rozładunek, załadunek, dostawa i sprzedaż), pojemność cysterny i dostawczaka wynika z zamontowanych części.
- Interfejs (`HudUI`, UI Toolkit): pieniądze, dzień i godzina, reputacja, misja z czasem, stan pojazdu i pompy, podpowiedź akcji, komunikaty, ciekawostki; panel mleczarni (TAB): produkcja, partie w toku, „Pomóż!”, pracownicy, zatrudnianie, rozbudowy; lista zamówień (O).
- Zapis gry co 15 s i przy wyjściu, razem z konfiguracją pojazdów.
- Sprawdzone: test **Mleczarka → Testy → Pętla gry** w trybie gry 9/9 (zbieranie mleka, umowa, rozładunek, zamówienie, produkcja z przesuwaniem pracowników, załadunek, dostawa, sprzedaż nadwyżek, zapis i odczyt); panel mleczarni na zrzucie z gry; konsola bez błędów.
- Nie sprawdzone ręcznie: przejazd całej trasy za kierownicą (test przestawia pojazdy między miejscami), czytelność podpowiedzi przy zmianie miejsc w czasie jazdy.
- Uproszczenia: hala mleczarni i domy rynku to powiększony dom z modułów wsi (docelowo własne budynki z Blendera); rywal nie jeździ po mapie.

## Znane braki i ryzyka
- **Pojazdy z Kenney Car Kit odstają stylem** (kanciaste zabawki). To tylko zastępcze modele do testów fizyki; docelowe trzeba znaleźć albo dorobić.
- Wersje darmowe nie mają ciał „Regular”/„Teen”, więc rolnik ma głowę z ciała „Superhero”. Kolory skóry, włosów i ubrań wymagają własnego shadera (kreator postaci).
- Brak sterowania dotykowego (Android/iOS) i profili jakości URP dla telefonów.
- Budowanie na iOS wymaga Maca z Xcode.

## Następne kroki
0. Etap 6: wyścigi (tor, okrążenia, rywale AI, cysterny wyścigowe), potem etap 7 (kreator postaci i kariera) i etap 8 (spółdzielnie, abstrakcje sieci).
1. **Pojazdy (decyzja 2026-11-11):** dwa typy, cysterna i dostawczak, oba z personalizacją: od zwykłych po wyścigowe (np. wyścigowa cysterna). Plan: modułowe pojazdy (podwozie, kabina, zabudowa, koła i felgi, zderzaki, dodatki, lakier i naklejki) jako dane w ScriptableObject; modele do zrobienia w Blenderze, bo darmowych stylizowanych cystern brak.
2. Prefaby: dom (2 warianty), płot, rolnik; dane części postaci w ScriptableObject (początek kreatora).
3. Teren z warstwami i trawą; lepsze niebo (HDRI Poly Haven z biblioteki).
