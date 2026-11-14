# Architektura projektu Unity „Mleczarka”

Cel: jedna spójna gra z modułami rozwijanymi niezależnie. Logika jest gotowa na multiplayer, ale na tym etapie nie ma infrastruktury sieciowej.

## Zasady
1. **Symulacja oddzielona od prezentacji.** Reguły gry (ekonomia, produkcja, spółdzielnie, umiejętności, zadania) to czysty C# bez `UnityEngine`. Każdy moduł ma osobny `.asmdef` z `noEngineReferences: true`. Te moduły da się testować bez sceny i uruchomić na serwerze.
2. **Zmiany stanu tylko przez komendy.** Komenda to obiekt z `PlayerId`, typem i danymi, np. `BidForContract`, `StartBatch`, `VoteOnProposal`. Symulacja waliduje komendę i publikuje zdarzenie. Dziś komendy wykonuje lokalny `LocalCommandBus`, a w multiplayer ten sam strumień przejdzie przez sieć do autorytatywnego hosta lub serwera.
3. **Stan to serializowalna migawka.** Ten sam format służy do zapisu gry, wczytania i (w przyszłości) synchronizacji. Ma numer wersji i migracje.
4. **Deterministyczny takt.** Symulacja liczy w stałych krokach. Czas pochodzi z `GameClock`, a losowość z `IRandom` z zapisanym ziarnem.
5. **Dane w ScriptableObject.** Produkty, receptury, maszyny, pojazdy, części ubrań i umiejętności są danymi, nie kodem. Nowe ubranie czy maszynę dodaje się bez programowania.
6. **Prezentacja reaguje na zdarzenia.** MonoBehaviours tylko czytają stan i słuchają zdarzeń (animacje, dźwięk, UI). Nie zmieniają reguł.

## Moduły i zestawy (assembly)

| Moduł | Symulacja (bez Unity) | Prezentacja (Unity) |
|---|---|---|
| Rdzeń | `Mleczarka.Core`: Id, GameClock, komendy, zdarzenia, IRandom, migawka | `Mleczarka.Runtime`: bootstrap, sceny, serwisy |
| Ekonomia | `Mleczarka.Economy`: konta, ceny, transakcje, rynek umów | |
| Produkcja | `Mleczarka.Production`: receptury, partie, stanowiska, magazyn | `Mleczarka.Production.View`: maszyny, minigry |
| Mleczarnia (zarządzanie) | `Mleczarka.Business`: firma, budynki, pracownicy, logistyka, koszty | `…Business.View`: budowanie, panel firmy |
| Pojazdy | `Mleczarka.Vehicles.Model`: ładunek, pojemność, zużycie | `Mleczarka.Vehicles`: fizyka kół, zawieszenie, kamera, wejście |
| Wyścigi | `Mleczarka.Racing.Rules`: punkty, przeszkadzajki, okrążenia | `Mleczarka.Racing`: tory, AI, efekty |
| Postacie | `Mleczarka.Characters.Model`: wygląd (lista części i kolorów) | `Mleczarka.Characters`: składanie modelu, Animator, kreator |
| Otwarty świat | `Mleczarka.World.Model`: miejsca, drogi, gospodarstwa | `Mleczarka.World`: sceny, streaming, interakcje |
| Umiejętności RPG | `Mleczarka.Skills`: doświadczenie, poziomy, bonusy | |
| Zadania i minigry | `Mleczarka.Quests`: zlecenia, cele, nagrody | `Mleczarka.Minigames` |
| Spółdzielnie | `Mleczarka.Coop`: członkowie, udziały, budżet, wnioski, głosowania, podział zysku | `…Coop.View` |
| Multiplayer | `Mleczarka.Net.Abstractions`: `ICommandTransport`, `IStateReplicator` (implementacja lokalna) | tu dopiero później konkretna sieć |
| UI i zapis | `Mleczarka.Save`: zapis, migracje | `Mleczarka.UI` (UI Toolkit) |

Zależności idą tylko w dół, do rdzenia. Moduły symulacji nie znają się nawzajem bez potrzeby, a współpracują przez zdarzenia i interfejsy.

## Spółdzielnia (model logiki)
- **Członek:** gracz, wkład (pieniądze i/lub dostarczone mleko), liczba udziałów.
- **Wspólny budżet:** wpłaty członków, przychody zakładu i koszty.
- **Wniosek:** np. inwestycja „nowa linia serów, 12 000 zł”. Głosowanie trwa N dni gry i ma kworum.
- **Reguła głosu do wyboru w statucie:** „1 członek = 1 głos” (klasyczna zasada spółdzielcza) albo głosy ważone udziałami.
- **Podział nadwyżki:** proporcjonalnie do obrotu członka ze spółdzielnią (np. ilość dostarczonego mleka), po odpisie na fundusz zasobowy.
- **Firmy prywatne:** ten sam model `Business`, tylko z jednym właścicielem i bez głosowań.

## Multiplayer: decyzja później
Do porównania, gdy przyjdzie etap 8:
- Netcode for GameObjects z Unity Relay/Lobby (darmowy próg UGS, potem płatne);
- Photon Fusion (darmowe 20 CCU);
- FishNet lub Mirror (darmowe, hostuje gracz albo własny serwer).

Trwały świat spółdzielni potrzebuje serwera i bazy danych, więc zanim cokolwiek wybierzemy, trzeba ustalić koszty. Do tego czasu architektura komend i migawek pozwala grać lokalnie bez przebudowy.

## Struktura folderów
```
Assets/_Mleczarka/
  Scripts/<Moduł>/            kod + .asmdef każdego modułu
  Data/                       ScriptableObjects (produkty, receptury, maszyny, części postaci)
  Art/ThirdParty/<Autor>/<Paczka>/   oryginalne importy (niezmienione)
  Art/Materials, Art/Shaders  wspólne materiały i shadery (spójny styl)
  Prefabs/Characters|Vehicles|Buildings|Props|Nature|FX
  Scenes/Bootstrap, Scenes/Demo_Gospodarstwo, Scenes/Race_Okiennice
  Audio/Music|SFX|Ambience
  Settings/URP (PC, Mobile), Settings/Input
Docs/  MIGRACJA.md, ARCHITEKTURA.md, ZASOBY_I_LICENCJE.md
PROJECT_STATUS.md, CHANGELOG.md
```
