# Hala produkcyjna — grywalny prototyp

## Zakres

Oddzielna scena `Produkcja_Prototyp`, istniejąca postać z animacjami i dostawczak z projektu. Hala ma zbiornik, linię UHT, pasteryzator, fermentację, dwie pakowarki, regały i plac wysyłki. Sklep znajduje się około 145 metrów dalej, na końcu drogi testowej. Budynek i maszyny są funkcjonalnymi modelami zastępczymi, do późniejszego opracowania graficznego.

Pierwszy produkt do sprawdzenia: UHT. Nowy zapis testowy zaczyna z 24 litrami mleka w zbiorniku. Jogurt jest drugą ścieżką i wymaga 4 minut fermentacji. Nie dodano jeszcze przyjmowania kolejnych dostaw surowca w tej scenie. Nie przebudowano mapy gminy.

## Sterowanie i przebieg

1. Przy zbiorniku wybierz `1` (UHT) lub `2` (jogurt). `Q` rozpoczyna partię.
2. Podejdź do właściwej maszyny i przytrzymaj `Q`, żeby osobiście wykonać pracę. Dla UHT następna jest pakowarka kartonów. Dla jogurtu: pasteryzator, fermentacja i pakowarka kubków.
3. Przy pakowarce `Q` podnosi gotową skrzynkę. Jednorazowo można nieść 4 sztuki produktu.
4. Zanieś ją do regału magazynu i naciśnij `Q`, żeby odłożyć towar.
5. Naciśnij `Q` ponownie przy regale, żeby pobrać wybrany produkt do wysyłki.
6. Przy dostawczaku `Q` ładuje niesioną skrzynkę. `E` wsiada do pojazdu.
7. Jedź drogą do sklepu. `F` dostarcza zamówienia i sprzedaje nadwyżki przez istniejącą ekonomię.

Przy chodzeniu: WASD, Shift — bieg. Kliknięcie w grę przejmuje mysz do kamery; Escape ją zwalnia. W pojeździe `R` stawia go na kołach.

## Stan, zapis i zgodność

- Użyto istniejącego GameState, symulacji, komend i portfela. Nowe pola PlayerState: manualHandling, packingRack, carried, carriedFromStore. Simulation.Migrate uzupełnia brakujące tablice.
- W manualnym trybie produkt po pakowaniu czeka na odbiór, zamiast pojawiać się na regale. Przejście przez magazyn jest obowiązkowe przed wysyłką.
- Automatyczny załadunek LoadVan jest niedostępny wyłącznie w manualnym trybie. Dotychczasowe sceny mają manualHandling=false i zachowują stare zachowanie.
- Pojemność uwzględnia towar na pakowaniu i niesiony. Nieudana operacja nie usuwa skrzynki. Ponowne naciśnięcie nie tworzy dodatkowych towarów ani zapłaty.
- Scena używa osobnego slotu `produkcja_prototyp1`; nie wczytuje ani nie zastępuje `gra1`. Zapis obejmuje również towar niesiony i czekający przy pakowaniu. Pozycja postaci i pojazdu nadal nie jest zapisywana przez istniejący SaveSystem.
- Pierwszy demonstracyjny stan wyłącza automatycznych pracowników i daje surowiec tylko raz. Jest to próba pracy osobistej, jeszcze bez zarządzania załogą i dostawami.

## Weryfikacja

20 testów logiki: produkcja ręczna, przenoszenie, wymóg magazynu, pełny pojazd, sprzedaż bez podwójnej zapłaty, migracja pól i zgodność starego trybu. Kompilacja trzech zestawów kodu z lokalnymi bibliotekami Unity. Dodatkowy test JSON w menu `Mleczarka → Produkcja → Testy logistyki i zapisu`.

W Unity wykonano również 13/13 kontroli pełnej pętli w trybie gry: stanowiska, wymóg obecności, pakowanie, magazyn, zapis skrzynki, załadunek przy pojeździe, wsiadanie oraz sprzedaż bez podwójnej zapłaty. Test przestawia postać i samochód między miejscami, więc ręczna próba chodzenia i jazdy pozostaje potrzebna. Raporty: Screenshots/produkcja_testy.txt i Screenshots/produkcja_petla.txt. Obserwacja w grze potwierdziła uruchomienie hali i interfejsu bez błędów konsoli; przy kopiowaniu mapy źródłowej Unity zgłosiło wcześniejsze ostrzeżenia shaderów drzew.

Przy stanowisku dostępny jest też przycisk „Obsłuż stanowisko”. Escape zwalnia mysz, aby go kliknąć. Opisy nad stanowiskami pokazują się tylko z bliska.

Scenę otwiera `Mleczarka → Produkcja → Otworz hale testowa`. Generator tworzy oddzielną kopię i nie nadpisuje istniejącej sceny prototypowej. Nie uruchamiać ponownie generatora mapy.

## Dalszy kierunek

Po sprawdzeniu tej pętli: rzeczywisty model hali i maszyn, animacja niesienia i obsługi, przyjęcie mleka z mapy oraz przeniesienie wnętrza do zakładu na gminie. Zachować jeden stan magazynu i pieniędzy.

Wyścigi pozostają w planie jako szybkie mleczne bolidy: zbieranie mleka, produkty do ataku i obrony oraz zdobywanie przewagi. Teraz priorytet ma produkcja, magazyn i sprzedaż.
