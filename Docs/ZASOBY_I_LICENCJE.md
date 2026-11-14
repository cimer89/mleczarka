# Zasoby 3D: źródła, licencje i plan importu

Zasady:
- Bierzemy tylko darmowe wersje (budżet 0 zł) z jasną licencją.
- Każdy zasób trafia do `Art/ThirdParty/<Autor>/<Paczka>` w oryginalnej postaci i dostaje wpis w tej tabeli.
- **Kierunek stylu:** stylizowane, teksturowane modele z serii MegaKit (Quaternius 2025), projektowane jako jedna rodzina (postacie, ubrania, animacje, natura, wieś). Pojazdy i maszyny dobieramy do tej palety.

## Pobrane 2026-11-11 (wszystkie wersje darmowe, 0 zł)

**Stan 2026-11-11 wieczór:** paczki są zaimportowane do projektu Unity (`Assets/_Mleczarka/Art/ThirdParty`), każda z plikiem licencji. Pominięto tylko tekstury stroju Ranger (na razie nieużywane). Oryginalne archiwa leżą w `C:\Users\pasti\Downloads`. Mniejsze są już też w `Game Assets/08_Modele_3D/<paczka>`. Pięć dużych (Quaternius: UBC, Outfits, Nature, Village) trafi do projektu Unity, rozpakowane wybiórczo, gdy projekt będzie gotowy (przesyłanie plików ponad ok. 100 MB w jednym kawałku się nie udało).

| Paczka | Plik | Co faktycznie zawiera darmowa wersja | Licencja (plik w paczce) | Import do Unity |
|---|---|---|---|---|
| Universal Base Characters (Quaternius) | `Universal Base Characters[Standard].zip` 129 MB | 2 ciała „Superhero” (M/K) z teksturami jasnej i ciemnej skóry; fryzury Buzzed, BuzzedFemale, Buns, Long, SimpleParted, broda, 2 typy brwi (wersje „rigged to head bone”) | CC0 (`License_Standard.txt`) | FBX (Unity), rig Humanoid, URP Lit, mapa gładkości z roughness |
| Modular Character Outfits – Fantasy (Quaternius) | `…Fantasy[Standard].zip` 294 MB | Stroje **Peasant** (chłopski) i **Ranger** (M/K), osobne części: tułów, ręce, nogi, stopy, kaptur, naramiennik; tekstury ciał „Regular” | CC0 | FBX (Unity), Humanoid; części jako sloty kreatora |
| Universal Animation Library (Quaternius) | 16 MB | Animacje w jednym FBX (UAL1_Standard) + wersja z root motion | CC0 | Humanoid, podział na klipy, Animator |
| Universal Animation Library 2 (Quaternius) | 19 MB | Animacje UAL2_Standard (+ root motion), manekin żeński | CC0 | jw.; sprawdzić klipy prac rolniczych |
| Stylized Nature MegaKit (Quaternius) | 104 MB | 68 modeli: CommonTree ×5, Pine ×5, TwistedTree ×5, DeadTree ×5, krzewy, paprocie, kwiaty, koniczyna, grzyby, trawy, kamienie, ścieżki z kamieni | CC0 | FBX (Unity); liście z alpha clip, własny shader wiatru (Shader Graph) |
| Medieval Village MegaKit (Quaternius) | 161 MB | 176 modułów: ściany (tynk, cegła), okna, **okiennice**, drzwi, dachy (dachówka, drewno), schody, płoty, rekwizyty | CC0 | FBX; z modułów złożymy prefaby podlaskich domów |
| Car Kit 3.0 (Kenney) | 4,8 MB | Ciągnik (3 warianty), ciężarówka, ciężarówka z platformą, bus, dostawczak, auta, 5 kartów wyścigowych, osobne koła | CC0 | FBX; koła jako osobne obiekty (pod WheelCollider) |
| Factory Kit 3.0 (Kenney, dawniej Conveyor Kit) | 4,5 MB | Taśmociągi, rury, maszyny, roboty, skrzynki | CC0 | FBX; przemalowanie z fioletu na paletę mleczarni |
| Food Kit (Kenney) | 4,6 MB | 200+ produktów, w tym karton (mleko), ser, plaster sera | CC0 | FBX; produkty na taśmie i w sklepie |

### Ocena spójności stylu
- **Quaternius: postacie, natura, wieś.** Jedna rodzina: teksturowane, stylizowane, miękkie światło („Ghibli”). Główny kierunek gry.
- **Kenney: pojazdy, hala, jedzenie.** Inny styl: kanciaste „zabawki” bez tekstur szczegółów. Obok postaci Quaternius będą odstawać. Na teraz traktujemy je jako zastępcze pojazdy do testów fizyki i rozgrywki. Docelowe pojazdy (cysterna, ciągnik, kart, dostawczak) trzeba znaleźć w podobnym stylu (Sketchfab CC0/CC-BY, Poly Pizza) albo dopracować w Blenderze.
- **Ograniczenia wersji darmowych:** brak ciał „Regular” i „Teen” oraz shaderów koloru skóry i oczu (są tylko w Source). Kolory skóry, włosów i ubrań zrobimy własnym shaderem w URP (maska koloru plus tint).

## Kandydaci — etap 2 (zweryfikowane 2026-11-11)

| # | Paczka | Autor | Co daje Mleczarce | Wersja / rozmiar | Licencja | Status |
|---|---|---|---|---|---|---|
| 1 | [Universal Base Characters](https://quaternius.com/packs/universalbasecharacters.html) | Quaternius | 6 bazowych sylwetek (zwykła, nastolatek, „superbohater”; K/M), humanoidalny rig, 20 fryzur. Baza kreatora postaci. | Standard 122 MB | CC0 | do pobrania* |
| 2 | [Modular Character Outfits – Fantasy](https://quaternius.com/packs/modularcharacteroutfitsfantasy.html) | Quaternius | 12 strojów z 62 części, 3 warianty kolorów, zgodne z UBC; m.in. chłopskie (tag „peasant”) | Standard 263 MB | CC0 | do pobrania*; sprawdzić, ile strojów pasuje na wieś |
| 3 | [Universal Animation Library](https://quaternius.itch.io/universal-animation-library) | Quaternius | 120+ animacji: chód w 8 kierunkach, bieg, pchanie, siadanie, emocje | Standard 13 MB | CC0 | do pobrania |
| 4 | [Universal Animation Library 2](https://quaternius.itch.io/universal-animation-library-2) | Quaternius | 130+ animacji, w tym **prace rolnicze** | Standard 9,7 MB | CC0 | sprawdzić, czy Standard jest darmowy* |
| 5 | [Stylized Nature MegaKit](https://quaternius.itch.io/stylized-nature-megakit) | Quaternius | 116 modeli: 40 drzew, 35 roślin i kwiatów, kamienie, trawa, krzewy | Standard 99 MB (Pro i Source płatne) | CC0 | do pobrania |
| 6 | [Medieval Village MegaKit](https://quaternius.com/packs/medievalvillagemegakit.html) | Quaternius | 300+ modułów: ściany, dachy, okna, drzwi. Do drewnianych zabudowań wsi. | Standard (rozmiar do sprawdzenia) | CC0 | do oceny stylu |
| 7 | [Car Kit 3.0](https://kenney.nl/assets/car-kit) | Kenney | 40+ pojazdów: auta, ciężarówki, busy, od wersji 3.0 karty wyścigowe | 3,4 MB | CC0 | do pobrania |
| 8 | [Conveyor Kit](https://kenney.nl/assets/conveyor-kit) | Kenney | Taśmociągi i elementy linii produkcyjnej (hala mleczarni) | 60 plików | CC0 | do pobrania |
| 9 | [Food Kit](https://kenney.nl/assets/food-kit) | Kenney | Produkty spożywcze (sprawdzić nabiał) | 200+ plików | CC0 | do oceny |
| 10 | [Mixamo](https://www.mixamo.com/) | Adobe | Animacje jazdy (siedzenie za kierownicą), pracy, noszenia | — | darmowe z kontem Adobe, bez opłat licencyjnych w grach (nie wolno udostępniać samych plików) | wymaga Twojego logowania |
| 11 | [Starter Assets – Third Person](https://assetstore.unity.com/) | Unity | Kontroler postaci z Input System i Cinemachine (baza ruchu) | — | Unity Companion License (darmowe) | wymaga konta Unity w Asset Store |

\* Na stronach itch.io te paczki mają „Name your own price”. Przy niektórych plikach strona podaje minimalną kwotę, dotyczącą raczej wersji Source. Przed pobraniem sprawdzę w przeglądarce, że wersja Standard jest darmowa. **Nie zapłacę za nic bez Twojej zgody.**

## Już w bibliotece GameAssets (do przeniesienia)
| Zasób | Autor | Licencja |
|---|---|---|
| Krowa, koń, osioł (animowane) | Quaternius przez Poly Pizza | CC0 |
| Nature Kit | Kenney | CC0 |
| Impact Sounds, Interface Sounds | Kenney | CC0 |
| 5 HDRI nieba | Poly Haven | CC0 |
| 12 arkuszy efektów | ParticleFX Studio | użytek komercyjny, bez podpisu |
| Muzyka i odgłosy wsi | ElevenLabs (plan użytkownika) | wg warunków ElevenLabs dla planu |

## Do znalezienia (luki)
- **Cysterna na mleko i ciągnik w stylu cartoon.** Car Kit ma ciężarówkę z platformą. Cysternę złożę z niej i zbiornika (Blender) albo poszukam modelu CC0/CC-BY na Sketchfabie lub w Poly Pizza.
- **Maszyny mleczarskie** (zbiorniki stalowe, pasteryzator, pakowarka). Prawdopodobnie złożę je w Blenderze z modułów Conveyor Kit, bo darmowych stylizowanych brak.
- **Współczesne ubrania** (dżinsy, kalosze, ogrodniczki, czapka z daszkiem). Seria Quaternius ma głównie stroje fantasy, więc trzeba poszukać zgodnych z rigiem UBC albo dorobić warianty w Blenderze.


## FactoryKit i FoodKit — integracja głównej hali (2026-11-11)

Zaimportowano posiadane archiwa Kenney Factory Kit 3.0 i Food Kit 2.0 do Assets/_Mleczarka/Art/ThirdParty/Kenney/FactoryKit oraz FoodKit. Lokalne License.txt potwierdzają CC0 i komercyjne wykorzystanie bez obowiązkowej atrybucji. Każdy katalog zawiera SOURCE.md z autorem, datą i SHA256 oryginalnego archiwum. Materiały URP i prefaby opisuje Docs/PRODUKCJA_GLOWNA_GRA.md. Oryginalnych modeli i tekstur nie zmieniano.
