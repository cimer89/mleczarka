# Mleczarnia w głównej grze — integracja 11 listopada 2026

## Stan wykonania

Produkcja, ręczne przenoszenie do magazynu, załadunek i sprzedaż działają teraz w scenie `Assets/_Mleczarka/Scenes/Gmina_Podlasie.unity`, która pozostaje pierwszą sceną budowania. Nie trzeba otwierać osobnego prototypu. W Unity uruchomiono główną mapę.

Nowy zakład zastępuje dawną makietę `Miejsca/Mleczarnia/Hala`. Stara makieta została wyłączona, a nie skasowana. Nie zmieniano terenu, dróg, gospodarstw, położenia rynku ani pojazdów. Nowa bryła znajduje się przy istniejącym placu mleczarni, środek na X=-26, Z=16; poziom posadzki Y=19,44 został dopasowany do istniejącego terenu. Wejście z rampą jest od strony placu. Dotychczasowe silosy, stodoła i szyld w otoczeniu zostały zachowane.

## Wygląd i zasoby

- Nowa bryła mleczarni: kremowa elewacja, granatowy stalowy dach, miętowy cokół, okna, szyld, daszek i rampa z poręczami.
- Wnętrze: jasna posadzka z fugami, oznaczone strefy, rurociągi, oświetlenie LED, osobne linie UHT i jogurtu oraz regały z paletami.
- Istniejący własny model `Silos_Mleka.fbx` użyty także jako zbiornik procesowy.
- Posiadany Kenney Factory Kit 3.0: maszyny, przenośniki, panele i skrzynki. Posiadany Food Kit 2.0: kartony i kubki; dodatkowo zaimportowano modele sera do późniejszego wykorzystania. Zasoby skopiowano z lokalnych archiwów, bez ponownego pobierania i bez instalowania pakietów.
- Oryginalne FBX i tekstury zachowano; dodano materiały URP oraz prefaby produktów i budynku. Każda paczka zawiera License.txt i SOURCE.md z autorem i SHA256 archiwum. Obie lokalne licencje wskazują CC0, użycie komercyjne i brak obowiązkowej atrybucji.
- Prefab budynku: `Assets/_Mleczarka/Prefabs/Buildings/Mleczarnia_Produkcja.prefab`. Prefaby produktów i skrzynki: `Assets/_Mleczarka/Prefabs/Production`.
- Dach i górne części ścian są ukrywane podczas osobistej pracy wewnątrz. Kolizje ścian nadal działają; wejście jest przez drzwi i rampę.

Źródła autorów: https://kenney.nl/assets/factory-kit oraz https://kenney.nl/assets/food-kit. Archiwa: `C:\Users\pasti\Downloads\kenney_factory-kit_3.0.zip` i `C:\Users\pasti\Downloads\kenney_food-kit.zip`.

## Rozgrywka

1. Odbierz mleko z gospodarstw i rozładuj cysternę w mleczarni dotychczasowym F.
2. Wejdź do hali. Przy zbiorniku wybierz 1 (UHT) lub 2 (jogurt), Q rozpoczyna partię.
3. Przytrzymuj Q przy odpowiedniej maszynie. UHT: linia UHT → pakowanie. Jogurt: pasteryzacja → fermentacja → pakowanie.
4. Przy pakowarce podejmij skrzynkę Q, odłóż ją na regale magazynu Q i pobierz do wysyłki Q.
5. Przy istniejącym dostawczaku Q ładuje skrzynkę. E — wsiądź, jedź na rynek w Zabielsku i F — dostarcz zamówienia / sprzedaj nadwyżki.
6. O nadal otwiera zamówienia, M mapę. TAB w mleczarni otwiera zarządzanie: podgląd partii, ludzie, zatrudnianie i rozbudowa. Stare przyciski uruchamiania i ręcznego przyspieszania partii w panelu nie omijają już stanowisk.

W panelu TAB można opcjonalnie włączyć pomoc istniejącej załogi. Domyślnie praca w głównej hali jest osobista. Liczba pracowników, ich dane i środki nie zostały skasowane. Pomoc załogi nie omija fizycznego przeniesienia gotowego towaru przez magazyn do pojazdu.

## Zapis i zgodność

- Zachowano główny slot `gra1`. Nie przenoszono stanu demonstracyjnego `produkcja_prototyp1` i nie dodano startowego mleka ani pieniędzy.
- Dane pakowania, niesionej skrzynki i pomocy załogi są częścią PlayerState. Starszy zapis otrzymuje brakujące pola z wartościami domyślnymi.
- `manualHandling` steruje ręcznym obiegiem towaru; `productionStaffAssist` opcjonalnie dopuszcza pracę zatrudnionych ludzi. Pozostałe sceny bez modułu zachowują dotychczasowe reguły.
- Kopie przed zmianą: `Backups/MainFactory_2026-11-11/Gmina_Podlasie.unity`, `mleczarka_gra1.json` oraz podkatalog `Scripts`.
- Istniejący system nadal nie zapisuje pozycji postaci ani pojazdu. Nie dodano dedykowanej animacji niesienia skrzynki.

## Weryfikacja

- Wszystkie cztery zestawy kodu skompilowane z lokalnymi bibliotekami Unity, następnie poprawna kompilacja w edytorze.
- 22/22 kontrole logiki, w tym brak duplikowania towaru i zapłaty, pojemność, zgodność starego trybu oraz opcjonalna pomoc załogi.
- 14/14 kontroli całej pętli w Play Mode na Gmina_Podlasie: zdarzenie Q, praca przy stanowiskach, pakowanie, magazyn, zapis skrzynki, załadunek przy dostawczaku, wsiadanie i sprzedaż na istniejącym rynku. Test przestawia obiekty między miejscami; nie zastępuje ręcznego przejazdu całej trasy.
- Stan sprzed testu odtworzono; testowe transakcje nie zostały w głównym zapisie.
- Zapisane podglądy: `Screenshots/Mleczarnia_Main_Exterior.png` i `Screenshots/Mleczarnia_Main_Interior.png` (wnętrze pokazano z odsłoniętym dachem).
- Raporty: `Screenshots/mleczarnia_integracja.txt`, `Screenshots/mleczarnia_kontrola.txt`, `Screenshots/produkcja_petla.txt`.
- W trakcie ładowania głównej mapy występują wcześniejsze ostrzeżenia o shaderach drzew Terrain. Nie dotyczą nowych modeli maszyn. Po uruchomieniu gry i teście pętli konsola nie pokazywała błędów.

## Dla Claude po wznowieniu

Nie uruchamiać generatora Podlasia nad obecną sceną — mógłby odtworzyć dawną makietę hali i usunąć ręczną integrację. Jeśli generator będzie zmieniany, najpierw włączyć nowy prefab zakładu oraz konfigurację ProductionFloor do jego kodu. Nie dublować GameController, magazynu ani portfela. Priorytetem jest dalsze dopracowanie pracy w zakładzie i dostaw; wyścigi z mlecznymi atakami pozostają osobnym późniejszym etapem.
