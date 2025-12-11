\# Firebird Metadata Sync Tool



Aplikacja konsolowa .NET 8.0 służąca do zarządzania metadanymi bazy Firebird 5.0. 

Narzędzie umożliwia generowanie skryptów SQL z bazy, budowanie nowej bazy ze skryptów oraz aktualizację istniejącej struktury.





Wymagania

\* .NET 8.0 SDK

\* Firebird 5.0 Server (działający na localhost, port 3050)





Funkcjonalności

1\. Budowanie bazy (`build-db`) - tworzy nową bazę danych z plików SQL.



2\. Eksport skryptów (`export-scripts`) - generuje pliki .sql dla domen, tabel i procedur istniejących w bazie danych.



3\. Aktualizacja bazy (`update-db`) - bezpiecznie aktualizuje istniejącą bazę (pomija istniejące tabele, aktualizuje procedury).







Przykłady użycia

&nbsp;	1. Eksport skryptów

DbMetaTool export-scripts --connection-string "User=SYSDBA;Password=masterkey;Database=C:\\Dane\\baza.fdb;DataSource=localhost;Dialect=3;Charset=UTF8;" --output-dir "C:\\Projekty\\Output"



&nbsp;	2. Budowanie nowej bazy

DbMetaTool build-db --db-dir "C:\\Dane" --scripts-dir "C:\\Projekty\\Output"



&nbsp;	3. Aktualizacja bazy

DbMetaTool update-db --connection-string "User=SYSDBA;Password=masterkey;Database=C:\\Dane\\baza.fdb;DataSource=localhost;Dialect=3;Charset=UTF8;" --scripts-dir "C:\\Projekty\\Output"





