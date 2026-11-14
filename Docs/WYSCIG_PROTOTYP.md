# Pierwszy wyścig — prototyp

Nowy priorytet użytkownika: mapa z kontraktami i odbiorem mleka, rzeczywiste wyścigi oraz własny zakład z pracą przy produkcji. Nie zastępować tego samymi panelami ekonomii. Spółdzielnia pozostaje dalszym etapem.

## Ten etap

- Osobna scena Wyscig_Prototyp: zamknięty owalny tor testowy, istniejący model pojazdu skopiowany z otwartej sceny, rywal z innym lakierem, kamera, odliczanie, dwa okrążenia, 40 punktów kontrolnych, wynik i restart.
- WASD/strzałki: jazda, Spacja: hamulec, R: restart, Backspace: postaw na kołach. Gamepad: triggery, lewy drążek i przycisk południowy.
- Menu Mleczarka → Wyscigi → Przygotuj osobna scene testowa; potem Otworz scene testowa i Play. Budowa wymaga otwartej sceny z istniejącą cysterną, a nie nowego projektu. Generator nie przebudowuje ani nie zapisuje mapy źródłowej.
- AI jest pierwszą wersją kierowania po punktach, do strojenia po teście fizyki. Pozycja w HUD wynika z liczby zaliczonych punktów; między punktami jest przybliżona.
- W tej wersji wyścig nie przyznaje pieniędzy ani nie zmienia zapisu kariery. Nie ma jeszcze wyboru pojazdu, menu zawodów, trwałych wyników ani integracji z kontraktami. Tor ma zastępczą nawierzchnię i bariery, używa obecnych modeli pojazdów.
- 19 testów reguł wykonano poza Unity. Kompilacja kodu z lokalnymi bibliotekami Unity jest osobnym sprawdzeniem; test jazdy gracza i rywala trzeba wykonać w Play Mode.

## Podział pracy

Codex: ten izolowany prototyp wyścigu i jego testy. Claude po wznowieniu: sprawdzenie przerwanych zmian bez rozbudowy mapy, kontrakty i odbiór mleka w świecie oraz grywalny zakład. Docelowy pierwszy produkt: gracz wychodzi z pojazdu, przyjmuje mleko, obsługuje pasteryzację/UHT, pakowanie i wysyłkę. Wykorzystać istniejącą symulację i animacje; czynności w świecie powinny wysyłać komendy zamiast tworzyć drugi magazyn lub drugi portfel. Potem połączyć przychód ze sprzedaży i nagrody wyścigowe z rozwojem pojazdu i zakładu. Dopiero później rozbudowana kariera i spółdzielnie.

## Dalsze kryteria odbioru

1. Gracz i AI kończą dwa okrążenia, a restart nie zostawia starego wyniku ani prędkości.
2. Nie można zakończyć wyścigu jadąc prosto na metę lub wstecz.
3. Jazda na obecnej mapie i istniejący zapis pozostają bez zmian.
4. Nagroda w integracji z karierą jest przyznana raz za identyfikator zawodów; restart i wczytanie nie duplikują środków.
5. Przed przeniesieniem zawodów na drogi gminy sprawdzić szerokość, kolizje i zachowanie cysterny. Nie przebudowywać mapy automatycznie.
