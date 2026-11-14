# CHANGELOG — Mleczarka (Unity)

## [Nieopublikowane]
### 2026-11-11 (etap 5: symulacja w świecie)
- Nowa scena **Swiat_Gmina** budowana z menu: mapa z wersji HTML ×2 (drogi, 6 gospodarstw, mleczarnia, rynek, Mleczpol, pola, las, wzgórza na krawędziach). Teren 380 m z wyrównanymi placami i drogami malowanymi na terenie.
- Blender: silos mleka, szyld mleczarni, stragan targowy (`Art/World`).
- `WorldPlace` (strefy), `GameController` (symulacja w scenie, akcja F, synchronizacja pojazdu i pojemności z częściami, zdarzenia, zapis co 15 s, odtwarzanie konfiguracji pojazdów z zapisu).
- `HudUI` + `UiKit`: pasek stanu, pojazd i pompa, podpowiedzi, komunikaty, ciekawostki, panel mleczarni (TAB) i zamówień (O). Garaż nie dubluje już podpowiedzi.
- `FarmerController.EnterOrExit()` (pod sterowanie dotykowe i testy); kamera nie krąży, gdy otwarty jest panel.
- Komenda `SelectWorkerCmd` (wybór pracownika z panelu).
- Test **Pętla gry** (tryb gry): 9/9.
- Poprawka: odtwarzanie pojazdów z zapisu nie wywraca się przy dwóch obiektach o tej samej nazwie.
### 2026-11-11 (etap 4: rdzeń symulacji mleczarni)
- Nowy zestaw `Mleczarka.Core` (czysty C#, bez UnityEngine): port `engine/game.js`, `config.js`, `data.js` z prototypu 1:1 — pompowanie mleka (odbiór/rozładunek), produkcja z recepturami UHT i jogurtu (etapy pracy i czasu, sloty stanowisk), pracownicy (chodzenie, wydajność, automatyczne zadania, „Pomóż!”), zamówienia z terminami, dostawczak, rynek, umowy i licytacja, rywal Mleczpol, rozbudowy, zatrudnianie, pensje, misja 1 z gwiazdkami, ciekawostki.
- Wszystkie zmiany stanu przez komendy z id gracza (`ICommand`, `LocalCommandBus`) — gotowe pod autorytatywny serwer.
- Stan (`GameState`) jako serializowalne dane; deterministyczny generator losowy zapisany w stanie.
- `SaveSystem`: zapis JSON z wersją i miejscem na migracje, zapis atomowy (plik tymczasowy + kopia .bak); zapisuje też konfiguracje pojazdów.
- Testy (menu **Mleczarka → Testy → Symulacja mleczarni**): 34/34 — przejście misji od odbioru do dostawy, umowy, pensje, rywal, zapis/odczyt i determinizm.
- Zmiana względem prototypu: rywal nie jeździ po grafie dróg, tylko liczy czas przejazdu (25 s).
### 2026-11-11 (tekstury i oświetlenie)
- **Niebo HDRI** z Poly Haven (CC0, z biblioteki Game Assets): dzień, popołudnie (złota godzina), zmierzch. Słońce ustawiane automatycznie zgodnie z położeniem słońca na panoramie (odczytanym z pliku HDR). Menu **Mleczarka → Oświetlenie**.
- Światło otoczenia i odbicia z nieba, sonda odbić nad zagrodą (lakier, chrom, szyby), mgła w kolorze horyzontu, postprocess (ACES, bloom, chłodniejsze cienie i cieplejsze światła).
- URP: na PC cienie 4 kaskady 4096 px do 110 m, miękkie, MSAA 4×, SSAO; na telefonach 2 kaskady 2048 px do 55 m, bez SSAO.
- **Teren** (Unity Terrain 240×240 m): płasko w zagrodzie i przy drodze, łagodne pagórki dalej; drzewa i trawy stawiane na wysokości terenu.
- **Własne stylizowane tekstury** (wygenerowane, bez obcych licencji): trawa z kwiatkami, łąka, ziemia z kamykami (+ mapy normalnych); polna droga, ścieżka do domu i plac przy dostawczaku malowane na terenie.
- **Naklejki „MLECZARNIA — świeże mleko z Podlasia”** z kropelką mleka: na bokach wszystkich zbiorników (dopasowane do krzywizny) i na drzwiach chłodni.
- Poprawka: mapy warstw terenu nie zapisywały się (asset tworzony po malowaniu).
### 2026-11-11 (dostawczak i garaż)
- Blender: modułowy dostawczak — podwozie, kabina zwykła i sportowa, chłodnia z łatami krowy, skrzynia otwarta z 9 bańkami mleka, skrzynia wyścigowa ze skrzydłem, zderzaki, wydechy. Eksport do `Art/Vehicles/Dostawczak`.
- Poprawka: punkty dachu i spoilera w kabinach cysterny miały w FBX nazwy z sufiksem (`.001`), więc spoiler trafiał na punkt podwozia. Eksport z poprawnymi nazwami.
- `VehicleFactory` (zastępuje `CisternFactory`): katalog 27 części obu pojazdów, podwozia cysterny i dostawczaka.
- Koła: komplet kół skaluje się do promienia koła fizycznego, więc te same koła pasują do cysterny i dostawczaka.
- `VehiclePartInstance`: znacznik zamontowanej części zapisany w scenie; zmiana części w trybie gry usuwa starą (wcześniej zostawała).
- Parametry pojazdu liczone przy starcie gry (wcześniej w trybie gry były zerowe).
- **Garaż (UI Toolkit)**: klawisz G przy pojeździe; zmiana każdej części strzałkami (z poziomem i ceną), 12 kolorów dla 3 warstw lakieru, parametry i wartość części na żywo; kamera krąży wokół pojazdu. Sprawdzone w trybie gry.
- Scena: dostawczak mleczarni (chłodnia), dostawczak z bańkami i dostawczak wyścigowy zamiast modelu Kenney.
### 2026-11-11 (części tuningowe i wyścigowe cysterny)
- Blender: 13 nowych części: kabina sportowa (z własnymi punktami dachu i spoilera), zbiorniki 4000, 9000 i wyścigowy 5000 l, koła standardowe, sportowe i terenowe, belka świetlna, spoiler, wydech podwójny, zderzak sportowy i dyfuzor.
- Katalog 17 części z poziomami (zwykła, tuning, wyścigowa), cenami i wpływem na parametry (`Data/Vehicles/Czesci`).
- `VehicleAssembler`: punkty montażu szukane najpierw na zamontowanych częściach (dach kabiny), wymiana kompletu kół, lakier nakładany przy każdym włączeniu (MaterialPropertyBlock nie zapisuje się w scenie).
- Scena demo: trzy cysterny obok drogi — wiejska, tuningowa (zielona, 9000 l, koła terenowe) i wyścigowa (żółta, 90 km/h). Sprawdzone w trybie gry.
### 2026-11-11 (pierwsza cysterna z Blendera)
- Blender 5.3: zainstalowany oficjalny dodatek MCP (Blender Lab, v1.0.3, GPL-3.0), włączony dostęp online dla rozszerzeń (wymóg dodatku), serwer startuje automatycznie.
- Modułowa cysterna wymodelowana w Blenderze (`Blender/Pojazdy_Mleczarnia.blend`): podwozie z kołami i punktami `Slot_…`, kabina zwykła, zbiornik 6000 l, zderzaki, wydech pionowy. Eksport FBX do `Art/Vehicles/Cysterna`.
- Materiały pojazdów (lakier główny, dodatkowy i akcent, chrom, stal, guma, szyby, świecące światła) w URP.
- `CisternFactory`: dane części i podwozia jako ScriptableObject (`Data/Vehicles`) i złożenie cysterny w scenie przez `VehicleAssembler`; zawieszenie dobierane do masy pojazdu.
- Scena demo: cysterna na polnej drodze (4600 kg, 62 km/h, 6000 l); w trybie gry stoi stabilnie. Dostawczak Kenney zostaje jako model zastępczy.
### 2026-11-11 (system personalizacji pojazdów — kod)
- Nowy zestaw `Mleczarka.Vehicles.Model` (bez UnityEngine): typy pojazdów (Cysterna, Dostawczak), sloty części, poziomy części (zwykła, tuning, wyścigowa), `VehicleLoadout` (wybrane części i 3 kolory lakieru, do zapisu i sieci), `VehicleStats` i kalkulator parametrów.
- `VehiclePartDefinition` i `VehicleDefinition` (ScriptableObject): części i podwozia jako dane.
- `VehicleAssembler`: składa pojazd z podwozia i części w punktach `Slot_…`, nakłada lakier przez MaterialPropertyBlock, przekazuje moc, prędkość, masę i przyczepność do fizyki. Kompiluje się bez błędów; czeka na modele z Blendera.
### 2026-11-11 (popołudnie)
- Poprawka: rolnik chodził tyłem. Model Quaternius patrzy w -Z, więc siedzi teraz w obiekcie gracza „Rolnik” obrócony o 180° (sterowanie, kolizja i kamera są na obiekcie gracza).
- Ciągnik usunięty ze sceny (w grze nie ma jazdy ciągnikiem). W jego miejscu dostawczak mleczarni na tej samej fizyce kół (model zastępczy Kenney „delivery”).
### 2026-11-11 (wieczór)
- Utworzony projekt Unity 6.3 LTS (URP, Input System, Cinemachine, MCP for Unity z rejestru OpenUPM).
- Zaimportowane paczki Quaternius i Kenney Car Kit do `Art/ThirdParty` (przesłane jako 15 paczek `.unitypackage` i zaimportowane menu **Mleczarka → 0**).
- `ThirdPartyImportRules`: rig Humanoid dla ciał i animacji, Generic dla części ubrań i włosów, materiały nazwane jak w pliku źródłowym, tekstury 2D (poprawka: część tekstur dostała kształt Cube), limity dla telefonów.
- `QuaterniusMaterials` (menu **1**): 33 materiały URP Lit (kolor, normalne, gładkość z roughness/ORM, AO, alpha clip dla liści), podpięte do modeli; klipy UAL nazwane i zapętlone.
- `DemoFarmBuilder` (menu **2**): scena *Demo_Gospodarstwo* (zagroda, dom z modułów, las, łąka, rolnik, ciągnik, kamera, światło, postprocess). Korekta obrotu modułów wsi (FBX „leżały”).
- `SkinnedPartBinder`: podpinanie ubrań i włosów pod szkielet ciała; z ciała „Superhero” zostaje tylko głowa (ciało prześwitywało przez strój).
- `FarmerController`: ruch względem kamery, sprint, skok, obrót kamery Cinemachine, wsiadanie do pojazdu.
- `WheeledVehicle`: pojazd na WheelColliderach (napęd, hamulec, skręt malejący z prędkością, koła wizualne z fizyki).
- `SceneShots` (menu **3**) i `Diagnostics` (menu **9**): zrzuty kontrolne do `Screenshots/` i raport materiałów.

### 2026-11-11
- Zabezpieczona wersja HTML (prototyp Mleczarka 7+): gra w jednym pliku, źródła, pełna historia git, sumy kontrolne, tag `html-prototyp-final-2026-11-11`.
- Dodane dokumenty: `Docs/MIGRACJA.md` (audyt mechanik, zasoby do przeniesienia, lista do ponownej implementacji), `Docs/ARCHITEKTURA.md` (moduły, komendy, migawki stanu, model spółdzielni), `Docs/ZASOBY_I_LICENCJE.md` (pierwsze darmowe paczki z licencjami).
- Pobrane darmowe paczki CC0: Quaternius (Universal Base Characters, Modular Outfits Fantasy, Universal Animation Library 1 i 2, Stylized Nature MegaKit, Medieval Village MegaKit) i Kenney (Car Kit 3.0, Factory Kit 3.0, Food Kit). Rejestr z faktyczną zawartością i oceną spójności stylu.
