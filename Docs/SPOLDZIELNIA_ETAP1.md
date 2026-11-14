# Etap niezależny od pracy Claude: lokalna spółdzielnia

Przygotowano 11 listopada 2026. Claude zachowuje przerwaną pracę nad mapą; plików scen, modeli i generatorów nie zmieniono.

## Wykonane

- Osobny moduł Mleczarka.Coop bez zależności od Unity, sieci i istniejącego Core.
- Członkostwo, wpłaty, wspólny budżet, wnioski inwestycyjne, jeden głos na członka, kworum i rozliczanie inwestycji.
- Kwoty w groszach (long), czas jako takt symulacji. Przy wpłacie portfel i budżet zmieniają się razem, dopiero po walidacji.
- Skład uprawnionych do głosowania utrwalany przy utworzeniu wniosku. Nowy członek nie zmienia trwającego głosowania. Remis, brak kworum i brak budżetu odrzucają wniosek.
- Powtórne rozliczenie zamkniętego wniosku nie pobiera pieniędzy.
- 20 kontroli wykonanych przez kompilację C# i uruchomienie CooperativeChecks.Run(): 20 OK, 0 błędów. Nie są to jeszcze testy uruchomione w Unity.

## Pliki do projektu Unity

- Coop/ → Assets/_Mleczarka/Scripts/Coop/
- Tests/CooperativeChecks.cs i CooperativeEditorChecks.cs → Assets/_Mleczarka/Scripts/Editor/ (prefiks Cooperative jest nowy, nie zastępować istniejących plików).
- README.md → Docs/SPOLDZIELNIA_ETAP1.md

Unity utworzy pliki .meta podczas importu. Menu Mleczarka → Testy → Spoldzielnia uruchamia te same kontrole w edytorze. Nie uruchamiać generatorów mapy.

## Kontrakt integracji dla Claude

1. Adapter komend powinien zweryfikować tożsamość gracza i pobrać jego portfel. Obecny Core używa double w złotych, nowy moduł long w groszach: określić jedno jawne, sprawdzane zaokrąglenie na granicy. Nie utrzymywać dwóch niezależnych portfeli. Wpłata otrzymuje portfel przez ref; adapter musi zapisać wynik do PlayerState wyłącznie po sukcesie.
2. Czas pochodzi z symulacji, nie zegara systemowego. Wszystkie komendy wykonywać szeregowo. Metody nie są zabezpieczeniem przed nieautoryzowanym klientem sieciowym.
3. Dopisać CooperativeState do kolejnej wersji SaveFile i wykonać migrację v1, która inicjalizuje pustą spółdzielnię bez zmiany istniejących pieniędzy. Sprawdzić JSON round-trip w Unity. Aktualny moduł nie jest jeszcze podłączony do zapisu i nie zmienia jego formatu.
4. Investment ze statusem Accepted reprezentuje zakupioną inwestycję. Świat ma odtwarzać efekt na podstawie id, idempotentnie, również po wczytaniu. Nie przyznawać maszyn ponownie przy każdym odczycie.
5. Przed podłączeniem UI dodać testy migracji, serializacji i adaptera portfela. Konstruktor sprawdza wersję i podstawowy stan; pełna walidacja niezaufanych lub uszkodzonych zapisów jest zadaniem warstwy ładowania.

## Pozostały zakres

Ten etap nie implementuje podziału nadwyżki, wypłat/zwrotów, wykluczania członków, zmiany statutu, multiplayera, panelu UI ani efektów inwestycji w świecie. Kworum jest liczbą osób, decyzja wymaga większości oddanych głosów. To jawne reguły pierwszego prototypu, do potwierdzenia przy integracji.

Następny mały etap: bezpieczny zapis/migracja i adapter transakcji. Osobny etap ekonomii: obecne GarageUI.Cycle zmienia część bez pobierania ceny; zakup musi walidować pieniądze, odblokowanie oraz pojemność względem przewożonego ładunku, a zakup i montaż muszą wykonać się razem.

## Uruchomienie kontroli poza Unity

Run-CooperativeChecks.ps1 może być niedostępny przy lokalnej blokadzie skryptów PowerShell. Nie zmieniano ustawień systemu. Sprawdzony sposób w sesji PowerShell:

```powershell
Add-Type -Path @('Development/Coop/Cooperative.cs', 'Development/Tests/CooperativeChecks.cs')
[CooperativeChecks]::Run()
```
