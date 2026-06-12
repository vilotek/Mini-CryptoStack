# Cryptodashboard


Każdy zarejestrowany użytkownik otrzymuje **$10 000 wirtualnych USD** i może handlować kryptowalutami po **rzeczywistych cenach** pobieranych z CoinGecko. Aplikacja zlicza wartość portfela, P&L i wystawia globalny ranking inwestorów.

---

## Spis treści

1. [Funkcjonalności](#funkcjonalności)
2. [Stack technologiczny](#stack-technologiczny)
3. [Wymagania](#wymagania)
4. [Uruchomienie krok po kroku](#uruchomienie-krok-po-kroku)
5. [Konto admina](#konto-admina)
6. [Struktura projektu](#struktura-projektu)
7. [Konfiguracja](#konfiguracja)
8. [Dokumentacja dodatkowa](#dokumentacja-dodatkowa)

---

## Funkcjonalności

### Dla każdego (publiczne):
- **Rynek** — lista śledzonych kryptowalut z aktualną ceną i zmianą 24h
- **Szczegół coina** — wykres historii cen (Chart.js) z dynamicznym ładowaniem przez AJAX
- **Ranking** — top inwestorów posortowany wg wartości portfela
- **Czat** — czat ogólny (odczyt bez logowania, pisanie po zalogowaniu)

### Dla zalogowanego użytkownika:
- **Rejestracja / logowanie** (ASP.NET Core Identity)
- **Handel** — kupno za USD i sprzedaż dowolnej ilości (transakcje atomowe w bazie)
- **Mój portfel** — saldo, posiadane coiny, średnia cena zakupu, P&L per coin i total
- **Wysyłanie wiadomości na czat**

### Dla administratora:
- **CRUD coinów** z możliwością uploadu obrazka logo (PNG/JPG/GIF/WebP, max 2 MB)
- **Zarządzanie użytkownikami** — reset portfela (czyści transakcje, holdingi, przywraca startowe $10k)
- **Ustawienia strony** — edycja nazwy aplikacji, tagline'a i tekstu stopki bez ruszania kodu
- **Ręczne odświeżanie cen** (oprócz automatu w tle)

### W tle (background):
- Automatyczne pobieranie cen z CoinGecko co **10 minut** (konfigurowalne)

---

## Stack technologiczny

| Warstwa | Technologia |
|---|---|
| Backend | **ASP.NET Core 8 MVC** |
| ORM | **Entity Framework Core 8** |
| Baza danych | **SQLite** (plik `crypto.db`) |
| Auth | **ASP.NET Core Identity** (role: `Admin`, `User`) |
| Frontend CSS | **Bootstrap 5.3** + własne `@media` queries |
| JavaScript | **Chart.js 4** (CDN), Vanilla JS, AJAX (fetch API) |
| Cache | **IMemoryCache** (dla ustawień strony) |
| Background | **BackgroundService** + `PeriodicTimer` |
| Zewnętrzne API | **CoinGecko** (darmowe, bez klucza) |

---

## Wymagania

- **.NET SDK 8.0** ([pobierz](https://dotnet.microsoft.com/download/dotnet/8.0))
- Połączenie z internetem (do pobierania cen z CoinGecko)
- Dowolne IDE: Visual Studio 2022 17.8+, VS Code lub Rider

---

## Uruchomienie krok po kroku

### 1. Sklonuj repozytorium

```bash
git clone https://github.com/<twoj-login>/CryptoDashboard.git
cd CryptoDashboard
```

### 2. Zainstaluj narzędzie EF (jeśli nie masz)

Sprawdź czy masz `dotnet-ef`:
```bash
dotnet ef --version
```

Jeśli zwraca błąd, zainstaluj globalnie (tylko raz na system):
```bash
dotnet tool install --global dotnet-ef
```

### 3. Przywróć pakiety NuGet

```bash
dotnet restore
```

### 4. Wygeneruj migrację bazy

```bash
dotnet ef migrations add InitialCreate
```

To tworzy folder `Migrations/` z plikami opisującymi strukturę bazy. Robisz **tylko raz**.

> **Uwaga:** Jeśli aktualizujesz starszą wersję aplikacji i masz już bazę `crypto.db`, usuń ją (lub plik `Migrations/`) zanim wygenerujesz nową migrację.

### 5. Uruchom aplikację

```bash
dotnet run
```

Aplikacja:
- automatycznie zaaplikuje migrację (utworzy plik `crypto.db`)
- zaseeduje 6 początkowych coinów (BTC, ETH, SOL, ADA, DOGE, XRP)
- utworzy konto admina
- po ~5 sekundach pobierze pierwsze ceny z CoinGecko

W konsoli zobaczysz adres aplikacji (zwykle `https://localhost:7XXX`).

### 6. Otwórz w przeglądarce

Wejdź na URL z konsoli. **Poczekaj kilkanaście sekund** żeby pierwsze ceny się pobrały, potem odśwież.

---

## Konto admina

Po pierwszym uruchomieniu jest gotowe konto administratora (seedowane):

- **Email:** `admin@dashboard.local`
- **Hasło:** `Admin123!`

Po zalogowaniu w nawigacji pojawi się żółty link „Admin" prowadzący do panelu.

> Hasło zaszyte w `DbInitializer.cs`. W realnym wdrożeniu warto przenieść do User Secrets albo zmiennych środowiskowych.

---

## Struktura projektu

```
CryptoDashboard/
├── Controllers/              # MVC controllers
│   ├── HomeController.cs     # Lista coinów
│   ├── CoinController.cs     # Detal + Buy/Sell + ChartData (JSON)
│   ├── PortfolioController.cs
│   ├── RankingController.cs
│   ├── ChatController.cs
│   └── AdminController.cs    # [Authorize(Roles="Admin")]
├── Models/                   # Encje
│   ├── ApplicationUser.cs    # IdentityUser + VirtualBalance + StartingBalance
│   ├── Coin.cs
│   ├── PricePoint.cs         # historia cen
│   ├── Holding.cs            # ile czego user posiada
│   ├── Transaction.cs        # historia transakcji
│   ├── ChatMessage.cs
│   ├── SiteSetting.cs        # edytowalne teksty
│   └── Enums/TransactionType.cs
├── Data/
│   ├── AppDbContext.cs       # IdentityDbContext + DbSet-y + konfiguracje
│   └── DbInitializer.cs      # seed ról, admina, coinów, ustawień
├── Services/
│   ├── CoinGeckoService.cs   # HttpClient + zapis PricePoint
│   ├── PriceUpdateService.cs # BackgroundService z PeriodicTimer
│   ├── TradingService.cs     # BuyAsync / SellAsync (transakcyjne)
│   ├── PortfolioService.cs   # snapshot portfela + ranking
│   └── SiteSettingsService.cs # ustawienia z cache
├── Views/
│   ├── Shared/_Layout.cshtml # wspólny layout (nav, footer)
│   ├── Home/, Coin/, Portfolio/, Ranking/, Chat/, Admin/
│   ├── _ViewImports.cshtml
│   └── _ViewStart.cshtml
├── wwwroot/
│   ├── css/site.css          # własne style + @media queries
│   └── uploads/              # uploadowane obrazki coinów
├── Program.cs                # bootstrap + DI + pipeline
├── appsettings.json          # connection string + config gry
├── DOCUMENTATION.md          # dokumentacja techniczna
├── USER_MANUAL.md            # instrukcja użytkownika
└── README.md                 # ten plik
```

---

## Konfiguracja

W `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=crypto.db"
  },
  "Game": {
    "StartingBalanceUsd": 10000,
    "PriceRefreshMinutes": 10
  },
  "CoinGecko": {
    "BaseUrl": "https://api.coingecko.com/api/v3/"
  }
}
```

| Klucz | Opis |
|---|---|
| `Game:StartingBalanceUsd` | Saldo dla nowego usera i reset usera przez admina |
| `Game:PriceRefreshMinutes` | Co ile minut `BackgroundService` pobiera ceny |
| `CoinGecko:BaseUrl` | API CoinGecko (zostaw domyślne) |

---