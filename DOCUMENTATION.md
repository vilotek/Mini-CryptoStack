# Dokumentacja techniczna — CryptoDashboard

Dokument opisuje architekturę rozwiązania, strukturę bazy danych oraz najważniejsze zaimplementowane funkcjonalności.

---

## 1. Architektura

Aplikacja jest klasyczną aplikacją **ASP.NET Core 8 MVC** z bazą **SQLite** i **Entity Framework Core 8** jako ORM. Renderowanie po stronie serwera (Razor Views) z punktową interaktywnością po stronie klienta (Chart.js + AJAX).

### Warstwy logiczne

```
┌─────────────────────────────────────────────────┐
│  Przeglądarka                                   │
│  Razor Views + Bootstrap + Chart.js + fetch()   │
└──────────────────┬──────────────────────────────┘
                   │ HTTP
┌──────────────────▼──────────────────────────────┐
│  Controllers (MVC + Identity Razor Pages)       │
│  Home, Coin, Portfolio, Ranking, Chat, Admin    │
└──────────────────┬──────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────┐
│  Services (logika domenowa)                     │
│  Trading, Portfolio, CoinGecko, SiteSettings    │
└──────────────────┬──────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────┐
│  Data Access (EF Core)                          │
│  AppDbContext (IdentityDbContext<ApplicationUser>) │
└──────────────────┬──────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────┐
│  SQLite (plik crypto.db)                        │
└─────────────────────────────────────────────────┘

   ┌──────────────────────────────────────────┐
   │  PriceUpdateService (BackgroundService)  │
   │  → wywołuje CoinGeckoService co 10 min   │
   └──────────────────────────────────────────┘

   ┌──────────────────────────────────────────┐
   │  CoinGecko API (zewnętrzne, HTTPS)       │
   └──────────────────────────────────────────┘
```


### Wzorce użyte w kodzie

| Wzorzec | Gdzie |
|---|---|
| Repository (uproszczony) | bezpośrednie użycie `DbContext` w serwisach |
| Service Layer | klasy w `Services/` — `TradingService`, `PortfolioService`, ... |
| Background Worker | `PriceUpdateService : BackgroundService` |
| DTO | rekordy w `PortfolioService` (`PortfolioSnapshot`, `HoldingValue`) |
| Dependency Injection | wbudowane w ASP.NET Core, konfiguracja w `Program.cs` |
| Unit of Work | `await using var tx = await ctx.Database.BeginTransactionAsync()` w `TradingService` |

---

## 2. Schemat bazy danych

### Tabele aplikacji (poza Identity)

```
┌─────────────────────┐                ┌───────────────────┐
│ AspNetUsers         │ 1            * │ Holdings          │
│ (ApplicationUser)   │◄───────────────│                   │
├─────────────────────┤                ├───────────────────┤
│ Id (string, PK)     │                │ Id (int, PK)      │
│ Email               │                │ UserId (FK)       │
│ UserName            │                │ CoinId (FK)       │
│ VirtualBalance      │                │ Quantity          │
│ StartingBalance     │                │ AvgBuyPrice       │
│ RegisteredAt        │                └───────┬───────────┘
└─────────┬───────────┘                        │ *
          │ 1                                  │
          │                                    │ 1
          │ *                          ┌───────▼───────────┐
   ┌──────▼──────────┐                 │ Coins             │
   │ Transactions    │                 ├───────────────────┤
   ├─────────────────┤  *           1  │ Id (int, PK)      │
   │ Id (long, PK)   │◄────────────────│ Symbol (unique)   │
   │ UserId (FK)     │                 │ Name              │
   │ CoinId (FK)     │                 │ CoinGeckoId       │
   │ Type (enum)     │                 │ ImageUrl          │
   │ Quantity        │                 │ AddedAt           │
   │ PricePerUnit    │                 └───────┬───────────┘
   │ TotalUsd        │                         │ 1
   │ Timestamp       │                         │
   └─────────────────┘                         │ *
                                       ┌───────▼───────────┐
   ┌──────────────────┐                │ PricePoints       │
   │ ChatMessages     │                ├───────────────────┤
   ├──────────────────┤                │ Id (long, PK)     │
   │ Id (long, PK)    │                │ CoinId (FK)       │
   │ UserId (FK)      │                │ Price             │
   │ Content (≤500)   │                │ Change24h         │
   │ CreatedAt        │                │ Timestamp         │
   └──────────────────┘                └───────────────────┘

   ┌──────────────────┐
   │ SiteSettings     │
   ├──────────────────┤
   │ Id (int, PK)     │
   │ Key (unique)     │
   │ Value (≤2000)    │
   │ UpdatedAt        │
   └──────────────────┘
```

### Tabele Identity (stworzone automatycznie)

`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`, `AspNetRoleClaims`.

### Kluczowe indeksy

| Tabela | Indeks | Cel |
|---|---|---|
| Coins | `Symbol` (unique) | szybkie wyszukiwanie po symbolu |
| Coins | `CoinGeckoId` (unique) | id z zewnętrznego API |
| PricePoints | `(CoinId, Timestamp)` | wykres, ostatnia cena |
| Holdings | `(UserId, CoinId)` unique | jeden holding per user/coin |
| Transactions | `(UserId, Timestamp)` | historia user'a |
| ChatMessages | `CreatedAt` | listing chronologiczny |
| SiteSettings | `Key` (unique) | key-value lookup |

### Cascade delete

- Usunięcie usera → usuwa wszystkie jego `Holdings`, `Transactions`, `ChatMessages`
- Usunięcie coina → usuwa wszystkie powiązane `PricePoints`, `Holdings`, `Transactions`

---

## 3. Najważniejsze funkcjonalności

### 3.1. Handel (Kupno/Sprzedaż)

**Plik:** `Services/TradingService.cs`

Każda transakcja jest atomowa — opakowana w `Database.BeginTransactionAsync()`.

#### Kupno

```csharp
public async Task<TradeResult> BuyAsync(string userId, int coinId, decimal usdAmount)
```

Algorytm:
1. Walidacja: kwota > 0, user istnieje, ma wystarczające saldo
2. Pobranie aktualnej ceny z bazy (najnowszy `PricePoint`)
3. Obliczenie ilości: `quantity = usdAmount / price`
4. Aktualizacja `Holding`:
   - Jeśli pierwszy zakup → utworzenie z `AvgBuyPrice = price`
   - Jeśli kolejny → **średnia ważona**: `(oldQty * oldAvg + newQty * newPrice) / (oldQty + newQty)`
5. Odjęcie USD z `user.VirtualBalance`
6. Zapis `Transaction` (Buy)
7. Commit transakcji

#### Sprzedaż

```csharp
public async Task<TradeResult> SellAsync(string userId, int coinId, decimal quantity)
```

Algorytm:
1. Walidacja: ilość > 0, holding istnieje, ma wystarczającą ilość
2. Aktualna cena z bazy
3. `usdReceived = quantity * price`
4. Zmniejszenie `holding.Quantity`; gdy spadnie do zera — usunięcie rekordu
5. `AvgBuyPrice` zostaje bez zmian (sprzedajemy część po pierwotnej średniej)
6. Dodanie USD do `user.VirtualBalance`
7. Zapis `Transaction` (Sell)
8. Commit transakcji

### 3.2. Liczenie P&L i ranking

**Plik:** `Services/PortfolioService.cs`

#### Snapshot portfela usera

```csharp
PortfolioSnapshot GetSnapshotAsync(string userId)
```

Zwraca:
- `CashBalance` — gotówka
- `HoldingsValue` — Σ (qty × current_price) dla wszystkich coinów
- `TotalValue` — cash + holdings
- `TotalPnlUsd` — `TotalValue - StartingBalance`
- `TotalPnlPercent`
- Lista `HoldingValue` per coin z indywidualnym P&L

P&L per coin: `(current_price - avg_buy_price) × quantity`

#### Ranking

```csharp
List<RankingRow> GetRankingAsync(int top)
```

Sortowanie wszystkich userów po `cash + Σ(qty × current_price)`, ograniczenie do top N. Liczone w locie — przy małej liczbie userów jest to akceptowalne.

### 3.3. Automatyczne odświeżanie cen

**Plik:** `Services/PriceUpdateService.cs`

`BackgroundService` z `PeriodicTimer`:
1. Po starcie aplikacji odczekuje 5 sekund
2. W pętli co `Game:PriceRefreshMinutes` minut (domyślnie 10):
   - Tworzy DI scope dla `DbContext`
   - Wywołuje `CoinGeckoService.RefreshPricesAsync()`

`CoinGeckoService` robi **jedno zapytanie** GET do `/simple/price?ids=...&vs_currencies=usd&include_24hr_change=true` dla wszystkich coinów naraz (lista po przecinku) i zapisuje N nowych `PricePoint`.

### 3.4. Cache ustawień strony

**Plik:** `Services/SiteSettingsService.cs`

Layout odpytuje o ustawienia przy każdym renderowaniu. Żeby nie hammerować bazy, używamy `IMemoryCache` z TTL 5 min. Po zapisie ustawień admin invaliduje cache (`_cache.Remove(CacheKey)`), więc zmiany są widoczne natychmiast.

### 3.5. Upload obrazków

**Plik:** `Controllers/AdminController.cs` → `TrySaveUploadedImageAsync`

Walidacja:
- Rozmiar ≤ 2 MB
- Rozszerzenie z listy: `.png`, `.jpg`, `.jpeg`, `.gif`, `.webp`
- Nowa nazwa pliku: `coin_<GUID>.<ext>` (zapobiega kolizjom i traversal)

Plik trafia do `wwwroot/uploads/`, a w bazie zapisujemy URL `/uploads/<filename>` (serwowane statycznie przez `app.UseStaticFiles()`).

### 3.6. Autoryzacja

| Zasób | Wymagania |
|---|---|
| `/`, `/Coin/Details`, `/Ranking`, `/Chat` (odczyt) | publiczny |
| Wysyłanie wiadomości na czat | zalogowany |
| `/Coin/Buy`, `/Coin/Sell` | zalogowany (`[Authorize]`) |
| `/Portfolio` | zalogowany (`[Authorize]`) |
| `/Admin/*` | rola `Admin` (`[Authorize(Roles = "Admin")]`) |

---

## 4. Frontend

### 4.1. CSS

- **Bootstrap 5.3** z CDN — siatka, komponenty, utility classes
- Własny `wwwroot/css/site.css` z `@media` queries dla 5 breakpointów:
  - `< 576px` — smartphone
  - `< 768px` — tablet portrait
  - `768–991px` — tablet landscape
  - `≥ 1400px` — duże ekrany
  - `print` — wyłączenie nawigacji i przycisków przy drukowaniu

### 4.2. JavaScript

- **Chart.js 4** + `chartjs-adapter-date-fns` — wykres historii cen
- AJAX (fetch API) — pobieranie danych do wykresu z endpoint'u `/Coin/ChartData/{id}` zwracającego JSON
- Auto-reload czatu co 15 sek (`setTimeout(() => location.reload(), 15000)`)
- Bootstrap collapse/dropdown bundle — komponenty interaktywne

### 4.3. Walidacja W3C

Wszystkie widoki używają poprawnej semantyki HTML5:
- `<header>`, `<main>`, `<footer>`, `<nav>`, `<section>`, `<article>`
- Hierarchia nagłówków `<h1>` → `<h2>` zachowana
- Atrybuty `alt` przy `<img>`
- `aria-*` w komponentach nawigacji
- Tytuły stron przez `<title>` w `_Layout`

---

## 5. Bezpieczeństwo

| Mechanizm | Implementacja |
|---|---|
| Hashowanie haseł | ASP.NET Core Identity (PBKDF2) |
| CSRF | `@Html.AntiForgeryToken()` + `[ValidateAntiForgeryToken]` na każdym POST |
| Autoryzacja ról | `[Authorize(Roles = "Admin")]` na `AdminController` |
| Walidacja uploadu | rozszerzenie, rozmiar, randomowa nazwa pliku |
| Walidacja modelu | `[Required]`, `[StringLength]`, `[Range]` na encjach |
| SQL Injection | parametrized queries przez EF Core (LINQ) |
| XSS | Razor automatycznie escape'uje `@variable` (poza `@Html.Raw`) |

---




