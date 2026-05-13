# Find That Book

A library discovery app: paste a messy, partial, or noisy description of a book and get a ranked list of matches.

---

## Quick Start

### Prerequisites

| Tool | Version |
|------|---------|
| .NET | 8.0 |
| Node.js | 18+ |
| npm | 9+ |

### 1. Clone and set your Gemini API key

Get a free key at <https://ai.google.dev/gemini-api/docs/api-key>.

**Option A — environment variable (recommended):**
```bash
# Windows PowerShell
$env:Gemini__ApiKey = "YOUR_KEY_HERE"

# macOS/Linux
export Gemini__ApiKey="YOUR_KEY_HERE"
```

**Option B — `appsettings.Development.json`:**
```json
{
  "Gemini": {
    "ApiKey": "YOUR_KEY_HERE"
  }
}
```

> Never commit a real key. The `appsettings.Development.json` file is in `.gitignore`.

### 2. Run the API

```bash
cd FindThatBook.API
dotnet run
```

The API starts on `https://localhost:7001` (or the port shown in the console).

### 3. Run the frontend (development)

In a second terminal:

```bash
cd client
npm install
npm run dev
```

Open <http://localhost:5173> in your browser. The Vite dev server proxies `/api` requests to the .NET backend automatically.

### Build for production

```bash
cd client && npm run build   # outputs to FindThatBook.API/wwwroot
cd ..
dotnet run --project FindThatBook.API --configuration Release
```

The .NET host then serves both the API and the React SPA.

### Run the tests

```bash
dotnet test FindThatBook.Tests
```

---

## Architecture overview

```
┌──────────────────────────────────────┐
│  React + TypeScript (Vite)           │
│  SearchBox → BookCard                │
│  POST /api/books/search              │
└──────────────────────┬───────────────┘
                       │ HTTP
┌──────────────────────▼───────────────┐
│  .NET 8 Web API                      │
│                                      │
│  BooksController                     │
│       │                              │
│  BookSearchService  ←─── orchestrates│
│    ├── GeminiService                 │
│    │     • ParseQueryAsync           │
│    └── OpenLibraryService            │
│          • SearchAsync               │
│          • GetWorkAsync              │
│          • GetAuthorAsync            │
│                                      │
│  TextNormalizer (static helpers)     │
└──────────────────────────────────────┘
```

### Request flow

1. **User submits a messy query** (e.g. `"tolkien hobbit illustrated deluxe 1937"`)
2. **`GeminiService.ParseQueryAsync`** calls Gemini 2.5 Flash to extract a structured hypothesis: `{ title: "The Hobbit", author: "J.R.R. Tolkien", keywords: ["illustrated", "1937"] }`
3. **`BookSearchService`** chooses a search path:
   - *Title known* → searches Open Library by title + author, fetches `/works/{id}.json` for each result to resolve canonical (primary) authors
   - *Author only* → uses Open Library's author-scoped search, returns top works by edition count
   - *Keywords only* → falls back to raw keyword search
4. **Matching hierarchy** scores each candidate. The spec defines four tiers (4a–4d); the implementation extends spec 4c into two sub-tiers to maintain consistency with how 4a/4b distinguish primary vs contributor authors:
   | Score | Condition | Spec |
   |-------|-----------|------|
   | 100 | Exact/normalised title + primary author | 4a |
   | 80 | Exact/normalised title + contributor-only author | 4b |
   | 70 | Near-match title + primary author | 4c (primary variant) |
   | 55 | Near-match title + contributor author | 4c (contributor variant) |
   | 60 | Exact title match, no author in query — spec doesn't cover this case; ranks above keyword fallback but below any author-confirmed result | extension |
   | 50 | Near title match, no author in query — same reasoning | extension |
   | 40 | Author-only or keyword fallback | 4d |

   **Title matching** uses token-level Jaccard similarity via `TextNormalizer`: diacritics stripped, punctuation removed, stop words excluded. `TitlesOverlap` handles subtitle variants (e.g. "The Hobbit" matches "The Hobbit, or There and Back Again"). Similarity ≥ 0.5 qualifies as a near-match.

   **Author matching** distinguishes primary from contributor by comparing the query author against `/works/{id}.json` canonical authors (primary) vs the full `author_name[]` list from `/search.json` (which includes illustrators, editors, adaptors).

   Candidates are sorted descending by score; top 5 are returned (spec 4e).
5. The final `SearchResponse` is returned to the client.

The Gemini call degrades gracefully: if the API is unavailable, query parsing falls back to simple year-stripping heuristics and the rule-based scores from step 4 determine the ranking.

---

## Design decisions

### AI model choice: Gemini 2.5 Flash
Free tier, fast, supports `responseMimeType: "application/json"` which guarantees JSON output and eliminates a whole class of parsing errors.

### Single AI call per search
Gemini is used once per query — to parse and spell-correct the raw input into a structured hypothesis. Ranking is handled entirely by the deterministic rule-based hierarchy, which is easy to test without mocking the LLM and produces auditable, consistent results.

### Primary author resolution
Open Library's `/search.json` `author_name` field includes everyone listed on editions: illustrators, adaptors, editors. The canonical work record at `/works/{id}.json` lists only the primary author(s). The service fetches both and distinguishes between them explicitly, matching the spec's data quality requirement.

### `TextNormalizer` — Jaccard similarity
Title comparison uses token-level Jaccard similarity over "significant" tokens (stop words removed). This handles:
- Subtitle variants (`"The Hobbit"` vs `"The Hobbit, or There and Back Again"`) via `TitlesOverlap`
- Diacritics (`"Héros"` → `"heros"`)
- Punctuation noise

### Concurrent Open Library fetches
Work details for all candidates are fetched concurrently with `Task.WhenAll`, keeping total latency proportional to the slowest single request rather than the sum.

---

## API

### `POST /api/books/search`

**Request:**
```json
{ "query": "tolkien hobbit illustrated deluxe 1937" }
```

**Response:**
```json
{
  "originalQuery": "tolkien hobbit illustrated deluxe 1937",
  "parsedQuery": {
    "title": "The Hobbit",
    "author": "J.R.R. Tolkien",
    "keywords": ["illustrated", "1937"]
  },
  "candidates": [
    {
      "title": "The Hobbit",
      "author": "J.R.R. Tolkien",
      "allAuthors": ["J.R.R. Tolkien", "Michael Dixon"],
      "firstPublishYear": 1937,
      "workKey": "/works/OL27448W",
      "workUrl": "https://openlibrary.org/works/OL27448W",
      "coverImageUrl": "https://covers.openlibrary.org/b/id/8406786-M.jpg",
      "explanation": "Exact title match; J.R.R. Tolkien is the primary author; Michael Dixon is listed as a contributor."
    }
  ]
}
```

Swagger UI is available at `/swagger` in development.

---

## Testing strategy

Tests live in `FindThatBook.Tests` (xUnit + Moq + FluentAssertions).

The `IGeminiService` and `IOpenLibraryService` interfaces are mocked, isolating the core matching and scoring logic in `BookSearchService` from network dependencies. This lets the tests run fast, deterministically, and without API keys.

**Test coverage:**
| Scenario | Test |
|----------|------|
| Exact title + primary author → top score | `SearchAsync_ExactTitleAndPrimaryAuthor_ReturnsSingleTopCandidate` |
| Author-only query → author fallback path | `SearchAsync_AuthorOnlyQuery_UsesAuthorFallback` |
| Duplicate work keys from OL → collapsed | `SearchAsync_DeduplicatesWorksByKey` |
| No OL results → empty candidate list | `SearchAsync_EmptyResults_ReturnsEmptyCandidates` |
| Contributor author → lower score than primary | `SearchAsync_ContributorAuthorMatch_ScoresLowerThanPrimary` |
| `TextNormalizer` — normalize, similarity, overlap, author match | `TextNormalizerTests` (9 inline-data cases) |

---

## Assumptions & trade-offs

- **Gemini key required** — there is a heuristic fallback for query parsing but explanations will be weaker without it.
- **Open Library rate limits** — the service makes up to 15 search + 15 work detail + N author detail requests per query. Open Library is generally permissive for low-volume use; a production version would add caching (e.g. IMemoryCache with a short TTL).
- **Cover images** — derived from `cover_i` in the search result. A `null` cover shows a placeholder icon.
- **No numeric confidence scores** — scores are used internally for ranking but not exposed in the API response, matching the spec's requirement.

---

## Future improvements

- **Response caching** — cache Open Library results in `IMemoryCache` with a short TTL to reduce latency on repeated queries and ease pressure on the Open Library API.
- **Smarter fallback parsing** — the current heuristic fallback (when Gemini is unavailable) only strips years. A lightweight local NLP approach (e.g. capitalisation patterns, known stop-word filtering) could improve author vs. title detection without an API call.
- **Phonetic / fuzzy matching** — handle misspellings like `"hemmingway"` that Gemini corrects but the fallback path misses, using Soundex or Metaphone.
- **Integration tests** — add a test layer tagged `[Trait("Category", "Integration")]` that hits the real Open Library API in a nightly CI run, catching API contract changes before they reach production.
- **Author disambiguation** — when multiple authors share a surname, use edition count and work popularity signals from Open Library to rank candidates more accurately.
- **Streaming explanations** — stream the Gemini response as it generates for a better perceived latency on slow connections.
- **Rate limiting** — add an ASP.NET Core rate limiter to prevent a single client from exhausting the Gemini free-tier quota or hammering Open Library.

