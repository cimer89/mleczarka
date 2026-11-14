# Instalacja gry Mleczarka (Windows, bez Unity)

Gotowa gra jest w zakładce **Releases** tego repozytorium:
https://github.com/cimer89/mleczarka/releases/latest

## Wymagania
- Windows 10 lub 11, 64-bitowy.
- Karta graficzna z obsługą DirectX 11 (każdy komputer z ostatnich ~10 lat).
- Ok. 2 GB wolnego miejsca.

## Instalacja krok po kroku
1. Wejdź na stronę **Releases** (link wyżej) i w sekcji **Assets** kliknij `Mleczarka_Windows.zip`.
2. Po pobraniu kliknij plik prawym przyciskiem → **Wyodrębnij wszystkie…** → wybierz folder, np. `Dokumenty\Gry` → **Wyodrębnij**.
   Nie uruchamiaj gry bezpośrednio z wnętrza ZIP-a — najpierw rozpakuj.
3. Otwórz rozpakowany folder `Windows` i kliknij dwa razy **`Mleczarka.exe`**.
4. Jeśli pojawi się niebieskie okno „System Windows ochronił ten komputer” (gra nie ma płatnego podpisu cyfrowego):
   kliknij **Więcej informacji** → **Uruchom mimo to**. Pojawia się tylko za pierwszym razem.
5. Opcjonalnie: prawy przycisk na `Mleczarka.exe` → **Pokaż więcej opcji** → **Wyślij do** → **Pulpit (utwórz skrót)**.

Nie przenoś samego pliku `Mleczarka.exe` — musi zostać w folderze razem z `Mleczarka_Data` i `UnityPlayer.dll`.

## Aktualizacja do nowej wersji
Pobierz nowszy `Mleczarka_Windows.zip` z Releases, rozpakuj i zastąp stary folder. Zapisy gry zostają (nie są w folderze gry).

## Zapisy gry
Zapis jest w folderze użytkownika Windows, nie w folderze gry:
`%USERPROFILE%\AppData\LocalLow\Mleczarka\Mleczarka\`
Żeby przenieść grę razem z postępem na inny komputer, skopiuj zawartość tego folderu w to samo miejsce na drugim komputerze
(wklej ścieżkę w pasek adresu Eksploratora plików).

## Sterowanie (najważniejsze)
- **WASD** — chodzenie i jazda, **Shift** — bieg, **Spacja** — skok / hamulec ręczny, **mysz** — kamera (kliknij, by złapać kursor).
- **E** — wsiądź / wysiądź, **R** — postaw pojazd na koła.
- **F** — akcja w miejscu (kup mleko, przelej, rozładuj, sprzedaj), **Q** (przytrzymaj) — praca przy sprzęcie lub maszynie.
- **TAB** — zarządzanie mleczarnią, **O** — zamówienia, **M** — mapa, **G** — garaż.
- Kariera: **B** — rower, **K** — rozwój i zakupy, **1–9** — produkt, **− / +** — wielkość partii.
- **Escape** — menu i pauza (tam też pełna lista sterowania).

## Odinstalowanie
Usuń folder z grą. Żeby usunąć też zapisy, usuń folder z sekcji „Zapisy gry”.

## Wersja do edycji (z Unity)
Jeśli chcesz zmieniać grę, a nie tylko w nią grać — instrukcja w `README.md` (sekcja „Jak uruchomić w Unity”).
